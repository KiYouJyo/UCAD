using System.Globalization;
using System.Text;
using UCAD.Core.Entities;
using UCAD.Core.Geometry;
using UCAD.Core.Hatching;
using UCAD.Core.Layout;
using UCAD.Core.Styles;

namespace UCAD.Core.Plot;

/// <summary>
/// Dependency-free one-page vector PDF writer used by the v0.12 plot foundation.
/// Geometry remains vector paths. A platform text-outline provider can convert installed
/// fonts, including CJK glyphs, into vector fills without embedding or distributing font
/// files. Built-in Helvetica/ASCII remains an explicit fallback if outline generation fails.
/// Multiple paper-space viewports are emitted into the same physical page with an
/// independent clip rectangle and model transform for each viewport.
/// </summary>
public static class CadPdfExporter
{
    private const double PointsPerMillimetre = 72.0 / 25.4;
    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

    public static CadPdfExportResult Export(CadDocument document, CadPlotPlan plan, string title = "UCAD Drawing")
    {
        ArgumentNullException.ThrowIfNull(plan);
        return Export(document, [plan], title, textOutlineProvider: null);
    }

    public static CadPdfExportResult Export(
        CadDocument document,
        CadPlotPlan plan,
        string title,
        ICadPdfTextOutlineProvider? textOutlineProvider)
    {
        ArgumentNullException.ThrowIfNull(plan);
        return Export(document, [plan], title, textOutlineProvider);
    }

    public static CadPdfExportResult Export(
        CadDocument document,
        IReadOnlyList<CadPlotPlan> plans,
        string title = "UCAD Drawing") =>
        Export(document, plans, title, textOutlineProvider: null);

    public static CadPdfExportResult Export(
        CadDocument document,
        IReadOnlyList<CadPlotPlan> plans,
        string title,
        ICadPdfTextOutlineProvider? textOutlineProvider)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(plans);
        if (plans.Count == 0) throw new ArgumentException("At least one plot plan is required.", nameof(plans));
        if (plans.Any(plan => plan is null)) throw new ArgumentException("Plot plan collection cannot contain null values.", nameof(plans));

        var pageSetup = plans[0].PageSetup;
        foreach (var plan in plans.Skip(1))
        {
            if (Math.Abs(plan.PageSetup.PaperWidthMm - pageSetup.PaperWidthMm) > 1e-6 ||
                Math.Abs(plan.PageSetup.PaperHeightMm - pageSetup.PaperHeightMm) > 1e-6)
            {
                throw new ArgumentException("All plot plans on one PDF page must use the same physical paper size and orientation.", nameof(plans));
            }
        }

        var warnings = new List<string>();
        var content = BuildContent(document, plans, warnings, textOutlineProvider);
        var pdf = BuildPdf(pageSetup, title, content);
        return new CadPdfExportResult(pdf, warnings.Distinct(StringComparer.Ordinal).ToArray());
    }

    private static string BuildContent(
        CadDocument document,
        IReadOnlyList<CadPlotPlan> plans,
        List<string> warnings,
        ICadPdfTextOutlineProvider? textOutlineProvider)
    {
        var sb = new StringBuilder(Math.Max(8192, plans.Count * 8192));
        foreach (var plan in plans)
        {
            sb.AppendLine("q");
            var clip = plan.PaperRectMm;
            var clipLeft = MmToPt(clip.Left);
            var clipBottom = MmToPt(clip.Bottom);
            var clipWidth = MmToPt(clip.Width);
            var clipHeight = MmToPt(clip.Height);
            sb.AppendFormat(Invariant, "{0} {1} {2} {3} re W n\n", F(clipLeft), F(clipBottom), F(clipWidth), F(clipHeight));

            foreach (var entity in document.VisibleEntities)
            {
                var properties = document.GetEntityProperties(entity.Id);
                var layer = document.GetLayer(properties.LayerName);
                var lineWeightMm = properties.LineWeight ?? layer.LineWeight;
                sb.AppendFormat(Invariant, "{0} w\n", F(Math.Max(0.1, MmToPt(lineWeightMm))));
                WritePlotColor(sb, properties.ColorHex ?? layer.ColorHex, plan.PageSetup.PlotStyle);
                WriteEntity(sb, document, entity, plan, warnings, textOutlineProvider);
            }
            sb.AppendLine("Q");
        }
        return sb.ToString();
    }

    private static void WriteEntity(
        StringBuilder sb,
        CadDocument document,
        ICadEntity entity,
        CadPlotPlan plan,
        List<string> warnings,
        ICadPdfTextOutlineProvider? textOutlineProvider)
    {
        switch (entity)
        {
            case LineEntity line:
                StrokeChain(sb, [line.Start, line.End], closed: false, plan);
                break;
            case PolylineEntity polyline:
                StrokeChain(sb, polyline.Points, polyline.Closed, plan);
                break;
            case CircleEntity circle:
                WriteCircle(sb, circle.Center, circle.Radius, plan);
                break;
            case ArcEntity arc:
                StrokeChain(sb, arc.SamplePoints(), closed: false, plan);
                break;
            case PointEntity point:
                WritePoint(sb, point.Position, plan);
                break;
            case EllipseEntity ellipse:
                StrokeChain(sb, ellipse.SamplePoints(), ellipse.IsFullEllipse, plan);
                break;
            case SplineEntity spline:
                StrokeChain(sb, spline.SamplePoints(), spline.Closed, plan);
                break;
            case RayEntity ray:
                WriteInfiniteLine(sb, ray.Origin, ray.Direction, rayOnly: true, plan);
                break;
            case XLineEntity xline:
                WriteInfiniteLine(sb, xline.Point, xline.Direction, rayOnly: false, plan);
                break;
            case TextEntity text:
                WriteText(
                    sb,
                    text.Text,
                    text.Position,
                    text.Height,
                    text.RotationRadians,
                    ResolveTextStyle(document, text.StyleName),
                    plan,
                    warnings,
                    text.Id,
                    textOutlineProvider);
                break;
            case MTextEntity text:
                WriteMText(sb, document, text, plan, warnings, textOutlineProvider);
                break;
            case LinearDimensionEntity dimension:
                WriteLinearDimension(sb, document, dimension, plan, warnings, textOutlineProvider);
                break;
            case AngularDimensionEntity dimension:
                WriteAngularDimension(sb, document, dimension, plan, warnings, textOutlineProvider);
                break;
            case RadialDimensionEntity dimension:
                WriteRadialDimension(sb, document, dimension, plan, warnings, textOutlineProvider);
                break;
            case LeaderEntity leader:
                WriteLeader(sb, document, leader, plan, warnings, textOutlineProvider);
                break;
            case HatchEntity hatch:
                WriteHatch(sb, hatch, plan, warnings);
                break;
            case BlockReferenceEntity reference:
                foreach (var child in reference.Contents)
                    WriteEntity(sb, document, child, plan, warnings, textOutlineProvider);
                break;
            default:
                warnings.Add($"PDF export skipped unsupported entity {entity.GetType().Name} ({entity.Id}).");
                break;
        }
    }

    private static void WriteLinearDimension(
        StringBuilder sb,
        CadDocument document,
        LinearDimensionEntity dimension,
        CadPlotPlan plan,
        List<string> warnings,
        ICadPdfTextOutlineProvider? textOutlineProvider)
    {
        var endpoints = dimension.GetDimensionLineEndpoints();
        StrokeChain(sb, [dimension.FirstExtensionPoint, endpoints.First], false, plan);
        StrokeChain(sb, [dimension.SecondExtensionPoint, endpoints.Second], false, plan);
        StrokeChain(sb, [endpoints.First, endpoints.Second], false, plan);
        var style = ResolveDimensionStyle(document, dimension.StyleName);
        var midpoint = Midpoint(endpoints.First, endpoints.Second);
        var label = dimension.TextOverride ?? style.Format(dimension.Measurement);
        WriteText(
            sb,
            label,
            midpoint,
            style.TextHeight,
            0,
            CadTextStyle.CreateDefault(),
            plan,
            warnings,
            dimension.Id,
            textOutlineProvider);
    }

    private static void WriteAngularDimension(
        StringBuilder sb,
        CadDocument document,
        AngularDimensionEntity dimension,
        CadPlotPlan plan,
        List<string> warnings,
        ICadPdfTextOutlineProvider? textOutlineProvider)
    {
        var radius = dimension.Radius;
        var firstRay = Unit(dimension.FirstRayPoint - dimension.Vertex);
        var secondRay = Unit(dimension.SecondRayPoint - dimension.Vertex);
        StrokeChain(sb, [dimension.Vertex, Add(dimension.Vertex, firstRay, radius)], false, plan);
        StrokeChain(sb, [dimension.Vertex, Add(dimension.Vertex, secondRay, radius)], false, plan);
        StrokeChain(sb, dimension.GetArcSamplePoints(), false, plan);
        var style = ResolveDimensionStyle(document, dimension.StyleName);
        var degrees = dimension.MeasurementRadians * 180.0 / Math.PI;
        var label = dimension.TextOverride ?? style.Format(degrees) + " deg";
        WriteText(
            sb,
            label,
            dimension.GetArcMidpoint(),
            style.TextHeight,
            0,
            CadTextStyle.CreateDefault(),
            plan,
            warnings,
            dimension.Id,
            textOutlineProvider);
    }

    private static void WriteRadialDimension(
        StringBuilder sb,
        CadDocument document,
        RadialDimensionEntity dimension,
        CadPlotPlan plan,
        List<string> warnings,
        ICadPdfTextOutlineProvider? textOutlineProvider)
    {
        StrokeChain(sb, [dimension.Center, dimension.PointOnCircle, dimension.TextPoint], false, plan);
        var style = ResolveDimensionStyle(document, dimension.StyleName);
        var prefix = dimension.Diameter ? "D" : "R";
        var label = dimension.TextOverride ?? prefix + style.Format(dimension.Measurement);
        WriteText(
            sb,
            label,
            dimension.TextPoint,
            style.TextHeight,
            0,
            CadTextStyle.CreateDefault(),
            plan,
            warnings,
            dimension.Id,
            textOutlineProvider);
    }

    private static void WriteLeader(
        StringBuilder sb,
        CadDocument document,
        LeaderEntity leader,
        CadPlotPlan plan,
        List<string> warnings,
        ICadPdfTextOutlineProvider? textOutlineProvider)
    {
        StrokeChain(sb, leader.Points, false, plan);
        WriteText(
            sb,
            leader.Text,
            leader.Points[^1],
            leader.TextHeight,
            0,
            ResolveTextStyle(document, leader.StyleName),
            plan,
            warnings,
            leader.Id,
            textOutlineProvider);
    }

    private static void WriteMText(
        StringBuilder sb,
        CadDocument document,
        MTextEntity text,
        CadPlotPlan plan,
        List<string> warnings,
        ICadPdfTextOutlineProvider? textOutlineProvider)
    {
        var lines = text.ApproximateLines();
        var lineHeight = text.TextHeight * 1.2;
        var normal = new CadVector(-Math.Sin(text.RotationRadians), Math.Cos(text.RotationRadians));
        var style = ResolveTextStyle(document, text.StyleName);
        for (var index = 0; index < lines.Count; index++)
        {
            var position = Add(text.Position, normal, -lineHeight * index);
            WriteText(
                sb,
                lines[index],
                position,
                text.TextHeight,
                text.RotationRadians,
                style,
                plan,
                warnings,
                text.Id,
                textOutlineProvider);
        }
    }

    private static void WriteHatch(StringBuilder sb, HatchEntity hatch, CadPlotPlan plan, List<string> warnings)
    {
        if (string.Equals(hatch.Pattern, "Solid", StringComparison.OrdinalIgnoreCase))
        {
            AppendPath(sb, hatch.Boundary, closed: true, plan);
            foreach (var island in hatch.EffectiveIslandLoops) AppendPath(sb, island, closed: true, plan);
            sb.AppendLine("f*");
            return;
        }

        if (string.Equals(hatch.Pattern, "ANSI31", StringComparison.OrdinalIgnoreCase))
        {
            var pattern = CadHatchPatternGenerator.Generate(hatch);
            foreach (var segment in pattern.Segments)
                StrokeChain(sb, [segment.Start, segment.End], false, plan);
            if (pattern.DensityReduced)
            {
                warnings.Add(
                    $"PDF export reduced ANSI31 render density for hatch {hatch.Id}; " +
                    $"requested spacing {F(pattern.RequestedSpacing)}, effective spacing {F(pattern.EffectiveSpacing)}.");
            }
            return;
        }

        StrokeChain(sb, hatch.Boundary, true, plan);
        foreach (var island in hatch.EffectiveIslandLoops) StrokeChain(sb, island, true, plan);
        warnings.Add($"PDF export plotted hatch '{hatch.Pattern}' boundary only because that pattern is not implemented ({hatch.Id}).");
    }

    private static void WriteCircle(StringBuilder sb, CadPoint center, double radius, CadPlotPlan plan)
    {
        var paperCenter = plan.ModelToPaper(center);
        var radiusPaper = radius / plan.ScaleDenominator;
        var cx = MmToPt(paperCenter.X);
        var cy = MmToPt(paperCenter.Y);
        var r = MmToPt(radiusPaper);
        const double k = 0.5522847498307936;
        var c = r * k;
        sb.AppendFormat(Invariant, "{0} {1} m\n", F(cx + r), F(cy));
        sb.AppendFormat(Invariant, "{0} {1} {2} {3} {4} {5} c\n", F(cx + r), F(cy + c), F(cx + c), F(cy + r), F(cx), F(cy + r));
        sb.AppendFormat(Invariant, "{0} {1} {2} {3} {4} {5} c\n", F(cx - c), F(cy + r), F(cx - r), F(cy + c), F(cx - r), F(cy));
        sb.AppendFormat(Invariant, "{0} {1} {2} {3} {4} {5} c\n", F(cx - r), F(cy - c), F(cx - c), F(cy - r), F(cx), F(cy - r));
        sb.AppendFormat(Invariant, "{0} {1} {2} {3} {4} {5} c S\n", F(cx + c), F(cy - r), F(cx + r), F(cy - c), F(cx + r), F(cy));
    }

    private static void WritePoint(StringBuilder sb, CadPoint point, CadPlotPlan plan)
    {
        var paper = plan.ModelToPaper(point);
        var x = MmToPt(paper.X);
        var y = MmToPt(paper.Y);
        const double size = 2.5;
        sb.AppendFormat(Invariant, "{0} {1} m {2} {3} l S\n", F(x - size), F(y), F(x + size), F(y));
        sb.AppendFormat(Invariant, "{0} {1} m {2} {3} l S\n", F(x), F(y - size), F(x), F(y + size));
    }

    private static void WriteInfiniteLine(StringBuilder sb, CadPoint anchor, CadVector direction, bool rayOnly, CadPlotPlan plan)
    {
        var paperAnchor = plan.ModelToPaper(anchor);
        var paperDirection = plan.ModelVectorToPaper(direction);
        if (paperDirection.Length <= 1e-12) return;
        var unit = new CadVector(paperDirection.X / paperDirection.Length, paperDirection.Y / paperDirection.Length);
        var length = Math.Sqrt((plan.PageSetup.PaperWidthMm * plan.PageSetup.PaperWidthMm) + (plan.PageSetup.PaperHeightMm * plan.PageSetup.PaperHeightMm)) * 4;
        var start = rayOnly
            ? paperAnchor
            : new CadPoint(paperAnchor.X - (unit.X * length), paperAnchor.Y - (unit.Y * length));
        var end = new CadPoint(paperAnchor.X + (unit.X * length), paperAnchor.Y + (unit.Y * length));
        StrokePaperChain(sb, [start, end], false);
    }

    private static void WriteText(
        StringBuilder sb,
        string value,
        CadPoint modelPosition,
        double modelHeight,
        double rotationRadians,
        CadTextStyle textStyle,
        CadPlotPlan plan,
        List<string> warnings,
        Guid sourceId,
        ICadPdfTextOutlineProvider? textOutlineProvider)
    {
        string? outlineWarning = null;
        if (textOutlineProvider is not null &&
            textOutlineProvider.TryCreateOutline(value, textStyle, out var outline, out outlineWarning) &&
            outline is not null)
        {
            WriteTextOutline(sb, outline, modelPosition, modelHeight, rotationRadians, plan);
            return;
        }

        if (textOutlineProvider is not null && !string.IsNullOrWhiteSpace(outlineWarning))
            warnings.Add($"Annotation {sourceId}: {outlineWarning}");

        var ascii = ToPdfAscii(value, out var replaced);
        if (replaced)
            warnings.Add($"PDF text outline was unavailable and built-in Helvetica replaced non-ASCII characters in annotation {sourceId}.");
        var paper = plan.ModelToPaper(modelPosition);
        var x = MmToPt(paper.X);
        var y = MmToPt(paper.Y);
        var fontSize = Math.Max(5, MmToPt(modelHeight / plan.ScaleDenominator));
        var paperRotation = plan.ModelAngleToPaper(rotationRadians);
        var cosine = Math.Cos(paperRotation);
        var sine = Math.Sin(paperRotation);
        sb.AppendLine("BT");
        sb.AppendFormat(Invariant, "/F1 {0} Tf\n", F(fontSize));
        sb.AppendFormat(Invariant, "{0} {1} {2} {3} {4} {5} Tm\n", F(cosine), F(sine), F(-sine), F(cosine), F(x), F(y));
        sb.Append('(').Append(EscapePdfString(ascii)).AppendLine(") Tj");
        sb.AppendLine("ET");
    }

    private static void WriteTextOutline(
        StringBuilder sb,
        CadPdfTextOutline outline,
        CadPoint modelPosition,
        double modelHeight,
        double rotationRadians,
        CadPlotPlan plan)
    {
        var origin = plan.ModelToPaper(modelPosition);
        var scaleMm = modelHeight / plan.ScaleDenominator;
        var paperRotation = plan.ModelAngleToPaper(rotationRadians);
        var cosine = Math.Cos(paperRotation);
        var sine = Math.Sin(paperRotation);

        foreach (var figure in outline.Figures)
        {
            if (figure.Points.Count < 2) continue;
            for (var index = 0; index < figure.Points.Count; index++)
            {
                var local = figure.Points[index];
                var localX = local.X * scaleMm;
                var localY = -local.Y * scaleMm;
                var paperX = origin.X + (localX * cosine) - (localY * sine);
                var paperY = origin.Y + (localX * sine) + (localY * cosine);
                if (index == 0)
                    sb.AppendFormat(Invariant, "{0} {1} m\n", F(MmToPt(paperX)), F(MmToPt(paperY)));
                else
                    sb.AppendFormat(Invariant, "{0} {1} l\n", F(MmToPt(paperX)), F(MmToPt(paperY)));
            }
            if (figure.Closed) sb.AppendLine("h");
        }
        sb.AppendLine("f");
    }

    private static void StrokeChain(StringBuilder sb, IReadOnlyList<CadPoint> points, bool closed, CadPlotPlan plan)
    {
        AppendPath(sb, points, closed, plan);
        sb.AppendLine("S");
    }

    private static void AppendPath(StringBuilder sb, IReadOnlyList<CadPoint> points, bool closed, CadPlotPlan plan)
    {
        if (points.Count == 0) return;
        var first = plan.ModelToPaper(points[0]);
        sb.AppendFormat(Invariant, "{0} {1} m\n", F(MmToPt(first.X)), F(MmToPt(first.Y)));
        for (var i = 1; i < points.Count; i++)
        {
            var paper = plan.ModelToPaper(points[i]);
            sb.AppendFormat(Invariant, "{0} {1} l\n", F(MmToPt(paper.X)), F(MmToPt(paper.Y)));
        }
        if (closed) sb.AppendLine("h");
    }

    private static void StrokePaperChain(StringBuilder sb, IReadOnlyList<CadPoint> paperPoints, bool closed)
    {
        if (paperPoints.Count == 0) return;
        sb.AppendFormat(Invariant, "{0} {1} m\n", F(MmToPt(paperPoints[0].X)), F(MmToPt(paperPoints[0].Y)));
        for (var i = 1; i < paperPoints.Count; i++)
            sb.AppendFormat(Invariant, "{0} {1} l\n", F(MmToPt(paperPoints[i].X)), F(MmToPt(paperPoints[i].Y)));
        if (closed) sb.AppendLine("h");
        sb.AppendLine("S");
    }

    private static void WritePlotColor(StringBuilder sb, string hex, CadPlotStyleMode mode)
    {
        if (mode == CadPlotStyleMode.Monochrome)
        {
            sb.AppendLine("0 G 0 g");
            return;
        }
        if (!TryParseRgb(hex, out var r, out var g, out var b))
        {
            sb.AppendLine("0 G 0 g");
            return;
        }
        if (mode == CadPlotStyleMode.Grayscale)
        {
            var gray = ((0.2126 * r) + (0.7152 * g) + (0.0722 * b)) / 255.0;
            sb.AppendFormat(Invariant, "{0} G {0} g\n", F(gray));
            return;
        }
        sb.AppendFormat(Invariant, "{0} {1} {2} RG {0} {1} {2} rg\n", F(r / 255.0), F(g / 255.0), F(b / 255.0));
    }

    private static byte[] BuildPdf(CadPageSetup pageSetup, string title, string content)
    {
        var widthPt = MmToPt(pageSetup.PaperWidthMm);
        var heightPt = MmToPt(pageSetup.PaperHeightMm);
        var safeTitle = EscapePdfString(ToPdfAscii(title, out _));
        var contentBytes = Encoding.ASCII.GetBytes(content);
        var objects = new List<byte[]>
        {
            Encoding.ASCII.GetBytes("<< /Type /Catalog /Pages 2 0 R >>"),
            Encoding.ASCII.GetBytes("<< /Type /Pages /Kids [3 0 R] /Count 1 >>"),
            Encoding.ASCII.GetBytes($"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 {F(widthPt)} {F(heightPt)}] /Resources << /Font << /F1 5 0 R >> >> /Contents 4 0 R >>"),
            BuildStreamObject(contentBytes),
            Encoding.ASCII.GetBytes("<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>"),
            Encoding.ASCII.GetBytes($"<< /Title ({safeTitle}) /Producer (UCAD) >>")
        };

        using var stream = new MemoryStream();
        WriteAscii(stream, "%PDF-1.4\n%UCAD\n");
        var offsets = new long[objects.Count + 1];
        for (var i = 0; i < objects.Count; i++)
        {
            offsets[i + 1] = stream.Position;
            WriteAscii(stream, $"{i + 1} 0 obj\n");
            stream.Write(objects[i]);
            WriteAscii(stream, "\nendobj\n");
        }
        var xref = stream.Position;
        WriteAscii(stream, $"xref\n0 {objects.Count + 1}\n");
        WriteAscii(stream, "0000000000 65535 f \n");
        for (var i = 1; i < offsets.Length; i++) WriteAscii(stream, $"{offsets[i]:D10} 00000 n \n");
        WriteAscii(stream, $"trailer\n<< /Size {objects.Count + 1} /Root 1 0 R /Info 6 0 R >>\nstartxref\n{xref}\n%%EOF\n");
        return stream.ToArray();
    }

    private static byte[] BuildStreamObject(byte[] content)
    {
        using var stream = new MemoryStream();
        WriteAscii(stream, $"<< /Length {content.Length} >>\nstream\n");
        stream.Write(content);
        WriteAscii(stream, "\nendstream");
        return stream.ToArray();
    }

    private static CadDimensionStyle ResolveDimensionStyle(CadDocument document, string name) =>
        document.TryGetDimensionStyle(name, out var style) && style is not null ? style : CadDimensionStyle.CreateDefault();

    private static CadTextStyle ResolveTextStyle(CadDocument document, string name) =>
        document.TryGetTextStyle(name, out var style) && style is not null ? style : CadTextStyle.CreateDefault();

    private static CadPoint Midpoint(CadPoint first, CadPoint second) => new((first.X + second.X) / 2, (first.Y + second.Y) / 2);

    private static CadVector Unit(CadVector vector)
    {
        var length = vector.Length;
        return length <= 1e-9 ? new CadVector(1, 0) : new CadVector(vector.X / length, vector.Y / length);
    }

    private static CadPoint Add(CadPoint point, CadVector direction, double distance) =>
        new(point.X + (direction.X * distance), point.Y + (direction.Y * distance));

    private static bool TryParseRgb(string value, out int r, out int g, out int b)
    {
        r = g = b = 0;
        var text = value.Trim().TrimStart('#');
        if (text.Length != 6 || !int.TryParse(text, NumberStyles.HexNumber, Invariant, out var rgb)) return false;
        r = (rgb >> 16) & 0xFF;
        g = (rgb >> 8) & 0xFF;
        b = rgb & 0xFF;
        return true;
    }

    private static string ToPdfAscii(string value, out bool replaced)
    {
        replaced = false;
        var builder = new StringBuilder(value.Length);
        foreach (var character in value)
        {
            if (character is >= ' ' and <= '~') builder.Append(character);
            else if (character is '\r' or '\n' or '\t') builder.Append(' ');
            else
            {
                builder.Append('?');
                replaced = true;
            }
        }
        return builder.ToString();
    }

    private static string EscapePdfString(string value) =>
        value.Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("(", "\\(", StringComparison.Ordinal)
            .Replace(")", "\\)", StringComparison.Ordinal);

    private static double MmToPt(double millimetres) => millimetres * PointsPerMillimetre;
    private static string F(double value) => value.ToString("0.###", Invariant);
    private static void WriteAscii(Stream stream, string value) => stream.Write(Encoding.ASCII.GetBytes(value));
}

public sealed record CadPdfExportResult(byte[] Content, IReadOnlyList<string> Warnings)
{
    public bool HasWarnings => Warnings.Count > 0;
}

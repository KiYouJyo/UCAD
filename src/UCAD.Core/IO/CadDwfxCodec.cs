using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using UCAD.Core.Entities;
using UCAD.Core.Geometry;
using UCAD.Core.Layers;

namespace UCAD.Core.IO;

/// <summary>
/// Bounded DWFx/XPS fixed-page adapter. DWFx is an XPS package; UCAD maps the
/// fixed-page vector Path subset to editable 2D geometry without claiming full
/// Autodesk DWF object-property/markup fidelity.
/// </summary>
public static class CadDwfxCodec
{
    private static readonly XNamespace Xps = "http://schemas.microsoft.com/xps/2005/06";
    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;
    private static readonly Regex PathToken = new(@"[MLZmlz]|-?\d+(?:\.\d+)?(?:[eE][+-]?\d+)?", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static CadDwfxImportResult Import(ReadOnlyMemory<byte> content)
    {
        if (content.IsEmpty) throw new ArgumentException("DWFx content cannot be empty.", nameof(content));
        var document = new CadDocument();
        var warnings = new List<string>();

        using var stream = new MemoryStream(content.ToArray(), writable: false);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: false);
        var page = archive.Entries.FirstOrDefault(entry => entry.FullName.EndsWith(".fpage", StringComparison.OrdinalIgnoreCase));
        if (page is null) throw new InvalidDataException("DWFx package does not contain an XPS FixedPage.");

        using var pageStream = page.Open();
        var xml = XDocument.Load(pageStream, LoadOptions.None);
        foreach (var path in xml.Descendants(Xps + "Path"))
        {
            var data = path.Attribute("Data")?.Value;
            if (string.IsNullOrWhiteSpace(data)) continue;
            try
            {
                foreach (var entity in ParsePathData(data)) document.Add(entity, new CadEntityProperties(CadLayer.DefaultLayerName));
            }
            catch (FormatException ex)
            {
                warnings.Add($"DWFx Path was skipped: {ex.Message}");
            }
        }

        var glyphCount = xml.Descendants(Xps + "Glyphs").Count();
        if (glyphCount > 0) warnings.Add($"DWFx contains {glyphCount} Glyphs text run(s); text/font resources are retained only in the source package and are not editable in this vector subset yet.");
        var unsupported = xml.Descendants().Count(element => element.Name == Xps + "ImageBrush" || element.Name == Xps + "VisualBrush");
        if (unsupported > 0) warnings.Add($"DWFx contains {unsupported} raster/visual brush resource(s) outside UCAD's current editable vector subset.");
        document.ResetHistory();
        return new CadDwfxImportResult(document, warnings);
    }

    public static CadDwfxExportResult Export(CadDocument document, double pageWidth = 1056, double pageHeight = 816)
    {
        ArgumentNullException.ThrowIfNull(document);
        if (!double.IsFinite(pageWidth) || pageWidth <= 0) throw new ArgumentOutOfRangeException(nameof(pageWidth));
        if (!double.IsFinite(pageHeight) || pageHeight <= 0) throw new ArgumentOutOfRangeException(nameof(pageHeight));

        var warnings = new List<string>();
        var supported = new List<(ICadEntity Entity, CadEntityProperties Properties)>();
        foreach (var entity in document.VisibleEntities)
        {
            if (entity is LineEntity or PolylineEntity or CircleEntity or ArcEntity)
                supported.Add((entity, document.GetEntityProperties(entity.Id)));
            else
                warnings.Add($"DWFx export: {entity.GetType().Name} is outside the current fixed-page vector subset and was omitted from the published view.");
        }

        var bounds = GetBounds(supported.Select(item => item.Entity));
        var transform = BuildTransform(bounds, pageWidth, pageHeight);
        var fixedPage = new XElement(Xps + "FixedPage",
            new XAttribute("Width", F(pageWidth)),
            new XAttribute("Height", F(pageHeight)),
            new XAttribute(XNamespace.Xml + "lang", "en-US"));

        foreach (var (entity, properties) in supported)
        {
            var data = ToPathData(entity, transform);
            if (data is null) continue;
            fixedPage.Add(new XElement(Xps + "Path",
                new XAttribute("Data", data),
                new XAttribute("Stroke", properties.ColorHex ?? "#FF000000"),
                new XAttribute("StrokeThickness", F(Math.Max(0.5, properties.LineWeight ?? 1.0)))));
        }

        using var output = new MemoryStream();
        using (var archive = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true))
        {
            WriteEntry(archive, "[Content_Types].xml", ContentTypesXml());
            WriteEntry(archive, "_rels/.rels", PackageRelationshipsXml());
            WriteEntry(archive, "FixedDocSeq.fdseq", $"<?xml version=\"1.0\" encoding=\"utf-8\"?><FixedDocumentSequence xmlns=\"{Xps}\"><DocumentReference Source=\"/Documents/1/FixedDocument.fdoc\" /></FixedDocumentSequence>");
            WriteEntry(archive, "Documents/1/FixedDocument.fdoc", $"<?xml version=\"1.0\" encoding=\"utf-8\"?><FixedDocument xmlns=\"{Xps}\"><PageContent Source=\"/Documents/1/Pages/1.fpage\" /></FixedDocument>");
            WriteEntry(archive, "Documents/1/Pages/1.fpage", new XDocument(new XDeclaration("1.0", "utf-8", null), fixedPage).ToString(SaveOptions.DisableFormatting));
        }

        return new CadDwfxExportResult(output.ToArray(), warnings);
    }

    private static IEnumerable<ICadEntity> ParsePathData(string data)
    {
        var tokens = PathToken.Matches(data).Select(match => match.Value).ToArray();
        var result = new List<ICadEntity>();
        var points = new List<CadPoint>();
        var closed = false;
        var i = 0;
        while (i < tokens.Length)
        {
            var token = tokens[i++];
            if (token.Equals("M", StringComparison.OrdinalIgnoreCase))
            {
                if (points.Count >= 2) result.Add(ToPolylineOrLine(points, closed));
                points.Clear();
                closed = false;
                points.Add(ReadPoint(tokens, ref i));
            }
            else if (token.Equals("L", StringComparison.OrdinalIgnoreCase))
            {
                if (points.Count == 0) throw new FormatException("Path L command appeared before M.");
                points.Add(ReadPoint(tokens, ref i));
            }
            else if (token.Equals("Z", StringComparison.OrdinalIgnoreCase))
            {
                closed = true;
            }
            else
            {
                throw new FormatException($"Unsupported DWFx/XPS Path command or token '{token}'. Only M/L/Z are currently editable.");
            }
        }
        if (points.Count >= 2) result.Add(ToPolylineOrLine(points, closed));
        return result;
    }

    private static ICadEntity ToPolylineOrLine(IReadOnlyList<CadPoint> points, bool closed) =>
        points.Count == 2 && !closed ? new LineEntity(points[0], points[1]) : new PolylineEntity(points, closed);

    private static CadPoint ReadPoint(IReadOnlyList<string> tokens, ref int index)
    {
        if (index + 1 >= tokens.Count) throw new FormatException("Path point is incomplete.");
        var x = double.Parse(tokens[index++], NumberStyles.Float, Invariant);
        var y = double.Parse(tokens[index++], NumberStyles.Float, Invariant);
        return new CadPoint(x, y);
    }

    private static string? ToPathData(ICadEntity entity, PageTransform transform)
    {
        IReadOnlyList<CadPoint> points;
        bool closed;
        switch (entity)
        {
            case LineEntity line:
                points = [line.Start, line.End]; closed = false; break;
            case PolylineEntity polyline:
                points = polyline.Points; closed = polyline.Closed; break;
            case CircleEntity circle:
                points = SampleCircle(circle.Center, circle.Radius, 64); closed = true; break;
            case ArcEntity arc:
                points = SampleArc(arc, 32); closed = false; break;
            default:
                return null;
        }
        if (points.Count < 2) return null;
        var builder = new StringBuilder();
        var first = transform.Apply(points[0]);
        builder.Append("M ").Append(F(first.X)).Append(' ').Append(F(first.Y));
        for (var i = 1; i < points.Count; i++)
        {
            var point = transform.Apply(points[i]);
            builder.Append(" L ").Append(F(point.X)).Append(' ').Append(F(point.Y));
        }
        if (closed) builder.Append(" Z");
        return builder.ToString();
    }

    private static IReadOnlyList<CadPoint> SampleCircle(CadPoint center, double radius, int segments) =>
        Enumerable.Range(0, segments).Select(i =>
        {
            var angle = Math.PI * 2 * i / segments;
            return new CadPoint(center.X + Math.Cos(angle) * radius, center.Y + Math.Sin(angle) * radius);
        }).ToArray();

    private static IReadOnlyList<CadPoint> SampleArc(ArcEntity arc, int segments)
    {
        var start = arc.StartAngleRadians;
        var sweep = arc.SweepAngleRadians;
        return Enumerable.Range(0, segments + 1).Select(i =>
        {
            var angle = start + sweep * i / segments;
            return new CadPoint(arc.Center.X + Math.Cos(angle) * arc.Radius, arc.Center.Y + Math.Sin(angle) * arc.Radius);
        }).ToArray();
    }

    private static CadBounds GetBounds(IEnumerable<ICadEntity> entities)
    {
        var points = new List<CadPoint>();
        foreach (var entity in entities)
        {
            switch (entity)
            {
                case LineEntity line: points.Add(line.Start); points.Add(line.End); break;
                case PolylineEntity polyline: points.AddRange(polyline.Points); break;
                case CircleEntity circle:
                    points.Add(new CadPoint(circle.Center.X - circle.Radius, circle.Center.Y - circle.Radius));
                    points.Add(new CadPoint(circle.Center.X + circle.Radius, circle.Center.Y + circle.Radius)); break;
                case ArcEntity arc:
                    points.AddRange(SampleArc(arc, 32)); break;
            }
        }
        if (points.Count == 0) return new CadBounds(0, 0, 1, 1);
        return new CadBounds(points.Min(p => p.X), points.Min(p => p.Y), points.Max(p => p.X), points.Max(p => p.Y));
    }

    private static PageTransform BuildTransform(CadBounds bounds, double width, double height)
    {
        const double margin = 24;
        var sourceWidth = Math.Max(1e-9, bounds.MaxX - bounds.MinX);
        var sourceHeight = Math.Max(1e-9, bounds.MaxY - bounds.MinY);
        var scale = Math.Min((width - margin * 2) / sourceWidth, (height - margin * 2) / sourceHeight);
        if (!double.IsFinite(scale) || scale <= 0) scale = 1;
        return new PageTransform(bounds.MinX, bounds.MaxY, scale, margin);
    }

    private static void WriteEntry(ZipArchive archive, string path, string text)
    {
        var entry = archive.CreateEntry(path, CompressionLevel.Optimal);
        using var writer = new StreamWriter(entry.Open(), new UTF8Encoding(false));
        writer.Write(text);
    }

    private static string ContentTypesXml() =>
        "<?xml version=\"1.0\" encoding=\"utf-8\"?><Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\"><Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/><Default Extension=\"fdseq\" ContentType=\"application/vnd.ms-package.xps-fixeddocumentsequence+xml\"/><Default Extension=\"fdoc\" ContentType=\"application/vnd.ms-package.xps-fixeddocument+xml\"/><Default Extension=\"fpage\" ContentType=\"application/vnd.ms-package.xps-fixedpage+xml\"/></Types>";

    private static string PackageRelationshipsXml() =>
        "<?xml version=\"1.0\" encoding=\"utf-8\"?><Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\"><Relationship Id=\"R1\" Type=\"http://schemas.microsoft.com/xps/2005/06/fixedrepresentation\" Target=\"/FixedDocSeq.fdseq\"/></Relationships>";

    private static string F(double value) => value.ToString("0.###", Invariant);

    private readonly record struct CadBounds(double MinX, double MinY, double MaxX, double MaxY);
    private readonly record struct PageTransform(double MinX, double MaxY, double Scale, double Margin)
    {
        public CadPoint Apply(CadPoint point) => new(Margin + (point.X - MinX) * Scale, Margin + (MaxY - point.Y) * Scale);
    }
}

public sealed record CadDwfxImportResult(CadDocument Document, IReadOnlyList<string> Warnings)
{
    public bool HasWarnings => Warnings.Count > 0;
}
public sealed record CadDwfxExportResult(byte[] Content, IReadOnlyList<string> Warnings)
{
    public bool HasWarnings => Warnings.Count > 0;
}

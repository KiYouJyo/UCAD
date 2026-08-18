using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Geometry;
using Microsoft.Graphics.Canvas.Text;
using Microsoft.Graphics.Canvas.UI.Xaml;
using Microsoft.UI;
using Microsoft.UI.Xaml.Controls;
using System.Globalization;
using System.Numerics;
using UCAD.Core.Entities;
using UCAD.Core.Geometry;
using UCAD.Core.Hatching;
using UCAD.Core.Layout;
using UCAD.Core.Plot;
using UCAD.Core.Styles;
using Windows.Foundation;
using Windows.UI;

namespace UCAD.Views;

public sealed class CadPlotPreviewControl : UserControl
{
    private readonly CanvasControl _canvas = new();
    private CadDocument? _document;
    private IReadOnlyList<CadPlotPlan> _plans = [];

    public CadPlotPreviewControl()
    {
        MinWidth = 720;
        MinHeight = 520;
        _canvas.Draw += Canvas_Draw;
        Content = _canvas;
    }

    public void SetPlot(CadDocument document, CadPlotPlan fallbackPlan)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(fallbackPlan);
        var layout = document.ActiveLayout;
        var plans = layout.Viewports.Count > 0
            ? layout.Viewports.Select(viewport => CadPlotPlan.FromViewport(layout.PageSetup, viewport)).ToArray()
            : [fallbackPlan];
        SetPlots(document, plans);
    }

    public void SetPlots(CadDocument document, IReadOnlyList<CadPlotPlan> plans)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(plans);
        if (plans.Count == 0) throw new ArgumentException("At least one plot plan is required.", nameof(plans));
        if (plans.Any(plan => plan is null)) throw new ArgumentException("Plot plan collection cannot contain null values.", nameof(plans));
        if (_document is not null) _document.Changed -= Document_Changed;
        _document = document;
        _plans = plans.ToArray();
        _document.Changed += Document_Changed;
        _canvas.Invalidate();
    }

    private void Document_Changed(object? sender, CadDocumentChangedEventArgs e) => _canvas.Invalidate();

    private void Canvas_Draw(CanvasControl sender, CanvasDrawEventArgs args)
    {
        var ds = args.DrawingSession;
        ds.Clear(ColorHelper.FromArgb(255, 36, 36, 39));
        if (_document is null || _plans.Count == 0) return;

        var pagePlan = _plans[0];
        var page = GetPageTransform(sender.ActualWidth, sender.ActualHeight, pagePlan);
        ds.FillRectangle(page.Left, page.Top, page.Width, page.Height, Colors.White);
        ds.DrawRectangle(page.Left, page.Top, page.Width, page.Height, ColorHelper.FromArgb(255, 150, 150, 155), 1);

        var printable = pagePlan.PageSetup.PrintablePaperRectMm;
        var printableTopLeft = PaperToScreen(new CadPoint(printable.Left, printable.Top), page, pagePlan);
        var printableBottomRight = PaperToScreen(new CadPoint(printable.Right, printable.Bottom), page, pagePlan);
        ds.DrawRectangle(
            printableTopLeft.X,
            printableTopLeft.Y,
            printableBottomRight.X - printableTopLeft.X,
            printableBottomRight.Y - printableTopLeft.Y,
            ColorHelper.FromArgb(150, 120, 120, 125),
            1);

        foreach (var plan in _plans)
        {
            var clipTopLeft = PaperToScreen(new CadPoint(plan.PaperRectMm.Left, plan.PaperRectMm.Top), page, plan);
            var clipBottomRight = PaperToScreen(new CadPoint(plan.PaperRectMm.Right, plan.PaperRectMm.Bottom), page, plan);
            var clip = new Rect(
                clipTopLeft.X,
                clipTopLeft.Y,
                Math.Max(0, clipBottomRight.X - clipTopLeft.X),
                Math.Max(0, clipBottomRight.Y - clipTopLeft.Y));
            if (clip.Width <= 0 || clip.Height <= 0) continue;

            using (ds.CreateLayer(1f, clip))
            {
                foreach (var entity in _document.VisibleEntities)
                {
                    var properties = _document.GetEntityProperties(entity.Id);
                    var layer = _document.GetLayer(properties.LayerName);
                    var color = ResolvePlotColor(properties.ColorHex ?? layer.ColorHex, plan.PageSetup.PlotStyle);
                    var lineWeightMm = properties.LineWeight ?? layer.LineWeight;
                    var strokeWidth = (float)Math.Clamp(lineWeightMm * page.Scale, 0.5, 6.0);
                    DrawEntity(ds, entity, page, plan, color, strokeWidth);
                }
            }

            if (_plans.Count > 1)
                ds.DrawRectangle(clip, ColorHelper.FromArgb(130, 90, 90, 96), 1);
        }
    }

    private void DrawEntity(
        CanvasDrawingSession ds,
        ICadEntity entity,
        PageTransform page,
        CadPlotPlan plan,
        Color color,
        float strokeWidth)
    {
        switch (entity)
        {
            case LineEntity line:
                DrawChain(ds, [line.Start, line.End], false, page, plan, color, strokeWidth);
                break;
            case PolylineEntity polyline:
                DrawChain(ds, polyline.Points, polyline.Closed, page, plan, color, strokeWidth);
                break;
            case CircleEntity circle:
            {
                var center = ModelToScreen(circle.Center, page, plan);
                var edge = ModelToScreen(new CadPoint(circle.Center.X + circle.Radius, circle.Center.Y), page, plan);
                ds.DrawCircle(center, Vector2.Distance(center, edge), color, strokeWidth);
                break;
            }
            case ArcEntity arc:
                DrawChain(ds, arc.SamplePoints(), false, page, plan, color, strokeWidth);
                break;
            case PointEntity point:
            {
                var p = ModelToScreen(point.Position, page, plan);
                ds.DrawLine(p.X - 3, p.Y, p.X + 3, p.Y, color, strokeWidth);
                ds.DrawLine(p.X, p.Y - 3, p.X, p.Y + 3, color, strokeWidth);
                break;
            }
            case EllipseEntity ellipse:
                DrawChain(ds, ellipse.SamplePoints(), ellipse.IsFullEllipse, page, plan, color, strokeWidth);
                break;
            case SplineEntity spline:
                DrawChain(ds, spline.SamplePoints(), spline.Closed, page, plan, color, strokeWidth);
                break;
            case RayEntity ray:
                DrawInfinite(ds, ray.Origin, ray.Direction, true, page, plan, color, strokeWidth);
                break;
            case XLineEntity xline:
                DrawInfinite(ds, xline.Point, xline.Direction, false, page, plan, color, strokeWidth);
                break;
            case TextEntity text:
                DrawPreviewText(ds, text.Text, text.Position, text.Height, text.RotationRadians, page, plan, color);
                break;
            case MTextEntity text:
                DrawPreviewText(ds, string.Join("\n", text.ApproximateLines()), text.Position, text.TextHeight, text.RotationRadians, page, plan, color);
                break;
            case LinearDimensionEntity dimension:
                DrawLinearDimension(ds, dimension, page, plan, color, strokeWidth);
                break;
            case AngularDimensionEntity dimension:
                DrawAngularDimension(ds, dimension, page, plan, color, strokeWidth);
                break;
            case RadialDimensionEntity dimension:
                DrawRadialDimension(ds, dimension, page, plan, color, strokeWidth);
                break;
            case LeaderEntity leader:
                DrawChain(ds, leader.Points, false, page, plan, color, strokeWidth);
                DrawPreviewText(ds, leader.Text, leader.Points[^1], leader.TextHeight, 0, page, plan, color);
                break;
            case HatchEntity hatch:
                DrawHatch(ds, hatch, page, plan, color, strokeWidth);
                break;
            case BlockReferenceEntity block:
                foreach (var child in block.Contents) DrawEntity(ds, child, page, plan, color, strokeWidth);
                break;
        }
    }

    private void DrawLinearDimension(
        CanvasDrawingSession ds,
        LinearDimensionEntity dimension,
        PageTransform page,
        CadPlotPlan plan,
        Color color,
        float strokeWidth)
    {
        var ends = dimension.GetDimensionLineEndpoints();
        DrawChain(ds, [dimension.FirstExtensionPoint, ends.First], false, page, plan, color, strokeWidth);
        DrawChain(ds, [dimension.SecondExtensionPoint, ends.Second], false, page, plan, color, strokeWidth);
        DrawChain(ds, [ends.First, ends.Second], false, page, plan, color, strokeWidth);
        var style = ResolveDimensionStyle(dimension.StyleName);
        var label = dimension.TextOverride ?? style.Format(dimension.Measurement);
        DrawPreviewText(ds, label, Midpoint(ends.First, ends.Second), style.TextHeight, 0, page, plan, color);
    }

    private void DrawAngularDimension(
        CanvasDrawingSession ds,
        AngularDimensionEntity dimension,
        PageTransform page,
        CadPlotPlan plan,
        Color color,
        float strokeWidth)
    {
        var radius = dimension.Radius;
        var firstRay = Unit(dimension.FirstRayPoint - dimension.Vertex);
        var secondRay = Unit(dimension.SecondRayPoint - dimension.Vertex);
        DrawChain(ds, [dimension.Vertex, Add(dimension.Vertex, firstRay, radius)], false, page, plan, color, strokeWidth);
        DrawChain(ds, [dimension.Vertex, Add(dimension.Vertex, secondRay, radius)], false, page, plan, color, strokeWidth);
        DrawChain(ds, dimension.GetArcSamplePoints(), false, page, plan, color, strokeWidth);
        var style = ResolveDimensionStyle(dimension.StyleName);
        var degrees = dimension.MeasurementRadians * 180.0 / Math.PI;
        var label = dimension.TextOverride ?? style.Format(degrees) + " deg";
        DrawPreviewText(ds, label, dimension.GetArcMidpoint(), style.TextHeight, 0, page, plan, color);
    }

    private void DrawRadialDimension(
        CanvasDrawingSession ds,
        RadialDimensionEntity dimension,
        PageTransform page,
        CadPlotPlan plan,
        Color color,
        float strokeWidth)
    {
        DrawChain(ds, [dimension.Center, dimension.PointOnCircle, dimension.TextPoint], false, page, plan, color, strokeWidth);
        var style = ResolveDimensionStyle(dimension.StyleName);
        var prefix = dimension.Diameter ? "D" : "R";
        var label = dimension.TextOverride ?? prefix + style.Format(dimension.Measurement);
        DrawPreviewText(ds, label, dimension.TextPoint, style.TextHeight, 0, page, plan, color);
    }

    private CadDimensionStyle ResolveDimensionStyle(string name) =>
        _document is not null && _document.TryGetDimensionStyle(name, out var style) && style is not null
            ? style
            : CadDimensionStyle.CreateDefault();

    private static void DrawPreviewText(
        CanvasDrawingSession ds,
        string text,
        CadPoint position,
        double modelHeight,
        double modelRotationRadians,
        PageTransform page,
        CadPlotPlan plan,
        Color color)
    {
        var screen = ModelToScreen(position, page, plan);
        var size = (float)Math.Max(6, (modelHeight / plan.ScaleDenominator) * page.Scale);
        using var format = new CanvasTextFormat
        {
            FontSize = size,
            WordWrapping = CanvasWordWrapping.NoWrap
        };
        var previous = ds.Transform;
        ds.Transform = Matrix3x2.CreateRotation((float)-plan.ModelAngleToPaper(modelRotationRadians), screen);
        ds.DrawText(text, screen, color, format);
        ds.Transform = previous;
    }

    private static void DrawHatch(
        CanvasDrawingSession ds,
        HatchEntity hatch,
        PageTransform page,
        CadPlotPlan plan,
        Color color,
        float strokeWidth)
    {
        if (string.Equals(hatch.Pattern, "ANSI31", StringComparison.OrdinalIgnoreCase))
        {
            var pattern = CadHatchPatternGenerator.Generate(hatch);
            foreach (var segment in pattern.Segments)
                DrawChain(ds, [segment.Start, segment.End], false, page, plan, color, strokeWidth);
            return;
        }

        if (!string.Equals(hatch.Pattern, "Solid", StringComparison.OrdinalIgnoreCase))
        {
            DrawChain(ds, hatch.Boundary, true, page, plan, color, strokeWidth);
            foreach (var island in hatch.EffectiveIslandLoops)
                DrawChain(ds, island, true, page, plan, color, strokeWidth);
            return;
        }

        using var builder = new CanvasPathBuilder(ds.Device);
        builder.SetFilledRegionDetermination(CanvasFilledRegionDetermination.Alternate);
        AddHatchLoop(builder, hatch.Boundary, page, plan);
        foreach (var island in hatch.EffectiveIslandLoops) AddHatchLoop(builder, island, page, plan);
        using var geometry = CanvasGeometry.CreatePath(builder);
        var fill = Color.FromArgb(45, color.R, color.G, color.B);
        ds.FillGeometry(geometry, fill);
        ds.DrawGeometry(geometry, color, strokeWidth);
    }

    private static void AddHatchLoop(
        CanvasPathBuilder builder,
        IReadOnlyList<CadPoint> loop,
        PageTransform page,
        CadPlotPlan plan)
    {
        if (loop.Count < 3) return;
        builder.BeginFigure(ModelToScreen(loop[0], page, plan));
        for (var index = 1; index < loop.Count; index++)
            builder.AddLine(ModelToScreen(loop[index], page, plan));
        builder.EndFigure(CanvasFigureLoop.Closed);
    }

    private static void DrawChain(
        CanvasDrawingSession ds,
        IReadOnlyList<CadPoint> points,
        bool closed,
        PageTransform page,
        CadPlotPlan plan,
        Color color,
        float strokeWidth)
    {
        if (points.Count < 2) return;
        var previous = ModelToScreen(points[0], page, plan);
        for (var i = 1; i < points.Count; i++)
        {
            var current = ModelToScreen(points[i], page, plan);
            ds.DrawLine(previous, current, color, strokeWidth);
            previous = current;
        }
        if (closed)
            ds.DrawLine(previous, ModelToScreen(points[0], page, plan), color, strokeWidth);
    }

    private static void DrawInfinite(
        CanvasDrawingSession ds,
        CadPoint anchor,
        CadVector direction,
        bool rayOnly,
        PageTransform page,
        CadPlotPlan plan,
        Color color,
        float strokeWidth)
    {
        if (direction.Length <= 1e-9) return;
        var unit = new CadVector(direction.X / direction.Length, direction.Y / direction.Length);
        var modelSpan = Math.Max(plan.PageSetup.PaperWidthMm, plan.PageSetup.PaperHeightMm) * plan.ScaleDenominator * 4;
        var start = rayOnly
            ? anchor
            : new CadPoint(anchor.X - (unit.X * modelSpan), anchor.Y - (unit.Y * modelSpan));
        var end = new CadPoint(anchor.X + (unit.X * modelSpan), anchor.Y + (unit.Y * modelSpan));
        ds.DrawLine(ModelToScreen(start, page, plan), ModelToScreen(end, page, plan), color, strokeWidth);
    }

    private static Color ResolvePlotColor(string value, CadPlotStyleMode mode)
    {
        if (mode == CadPlotStyleMode.Monochrome) return Colors.Black;
        if (!TryParseRgb(value, out var red, out var green, out var blue)) return Colors.Black;
        if (mode == CadPlotStyleMode.Grayscale)
        {
            var gray = (byte)Math.Clamp((int)Math.Round((0.2126 * red) + (0.7152 * green) + (0.0722 * blue)), 0, 255);
            return Color.FromArgb(255, gray, gray, gray);
        }
        return Color.FromArgb(255, red, green, blue);
    }

    private static bool TryParseRgb(string value, out byte red, out byte green, out byte blue)
    {
        red = green = blue = 0;
        var text = value.Trim().TrimStart('#');
        if (text.Length != 6 || !int.TryParse(text, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var rgb)) return false;
        red = (byte)((rgb >> 16) & 0xFF);
        green = (byte)((rgb >> 8) & 0xFF);
        blue = (byte)(rgb & 0xFF);
        return true;
    }

    private static CadPoint Midpoint(CadPoint first, CadPoint second) =>
        new((first.X + second.X) / 2, (first.Y + second.Y) / 2);

    private static CadVector Unit(CadVector vector)
    {
        var length = vector.Length;
        return length <= 1e-9 ? new CadVector(1, 0) : new CadVector(vector.X / length, vector.Y / length);
    }

    private static CadPoint Add(CadPoint point, CadVector direction, double distance) =>
        new(point.X + (direction.X * distance), point.Y + (direction.Y * distance));

    private static PageTransform GetPageTransform(double width, double height, CadPlotPlan plan)
    {
        const double padding = 28;
        var availableWidth = Math.Max(1, width - (padding * 2));
        var availableHeight = Math.Max(1, height - (padding * 2));
        var scale = Math.Min(availableWidth / plan.PageSetup.PaperWidthMm, availableHeight / plan.PageSetup.PaperHeightMm);
        var pageWidth = (float)(plan.PageSetup.PaperWidthMm * scale);
        var pageHeight = (float)(plan.PageSetup.PaperHeightMm * scale);
        return new PageTransform(
            (float)((width - pageWidth) / 2),
            (float)((height - pageHeight) / 2),
            pageWidth,
            pageHeight,
            (float)scale);
    }

    private static Vector2 ModelToScreen(CadPoint model, PageTransform page, CadPlotPlan plan) =>
        PaperToScreen(plan.ModelToPaper(model), page, plan);

    private static Vector2 PaperToScreen(CadPoint paper, PageTransform page, CadPlotPlan plan) => new(
        page.Left + ((float)paper.X * page.Scale),
        page.Top + page.Height - ((float)paper.Y * page.Scale));

    private readonly record struct PageTransform(float Left, float Top, float Width, float Height, float Scale);
}

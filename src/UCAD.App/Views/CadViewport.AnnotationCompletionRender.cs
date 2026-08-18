using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Text;
using Microsoft.Graphics.Canvas.UI.Xaml;
using System.Numerics;
using UCAD.Core.Entities;
using UCAD.Core.Geometry;
using UCAD.Core.Interaction;
using UCAD.Core.Styles;
using Windows.UI;

namespace UCAD.Views;

public sealed partial class CadViewport
{
    private bool _annotationCompletionRenderHooksInstalled;

    internal void EnsureAnnotationCompletionRenderHooks()
    {
        if (_annotationCompletionRenderHooksInstalled) return;
        _annotationCompletionRenderHooksInstalled = true;
        Canvas.Draw += Canvas_AnnotationCompletionRender;
        Canvas.Invalidate();
    }

    private void Canvas_AnnotationCompletionRender(CanvasControl sender, CanvasDrawEventArgs args)
    {
        var ds = args.DrawingSession;
        foreach (var entity in _document.VisibleEntities.Where(CadAnnotationEntityGeometry.Supports))
        {
            Color color;
            float strokeWidth;
            if (_interaction.Selection.Contains(entity.Id))
            {
                color = _selectedColor;
                strokeWidth = 2.0f;
            }
            else if (_hoverEntityId == entity.Id)
            {
                color = _hoverColor;
                strokeWidth = 1.6f;
            }
            else
            {
                var properties = _document.GetEntityProperties(entity.Id);
                var layer = _document.GetLayer(properties.LayerName);
                color = ResolveEntityColor(properties.ColorHex, layer.ColorHex);
                strokeWidth = (float)Math.Clamp((properties.LineWeight ?? layer.LineWeight) * 3.0, 0.8, 4.0);
            }
            DrawCompletedAnnotation(ds, entity, color, strokeWidth);
        }

        DrawCompletedAnnotationGrips(ds);
        DrawCompletedAnnotationPreview(ds);
        DrawCadCursor(ds, sender.ActualWidth, sender.ActualHeight);
    }

    private void DrawCompletedAnnotation(CanvasDrawingSession ds, ICadEntity entity, Color color, float strokeWidth)
    {
        switch (entity)
        {
            case MTextEntity text:
                DrawMTextEntity(ds, text, color);
                break;
            case AngularDimensionEntity dimension:
                DrawAngularDimension(ds, dimension, color, strokeWidth);
                break;
            case RadialDimensionEntity dimension:
                DrawRadialDimension(ds, dimension, color, strokeWidth);
                break;
            case LeaderEntity leader:
                DrawLeader(ds, leader, color, strokeWidth);
                break;
        }
    }

    private void DrawMTextEntity(CanvasDrawingSession ds, MTextEntity text, Color color)
    {
        var style = ResolveTextStyle(text.StyleName);
        var lineHeightWorld = text.TextHeight * 1.2;
        var fontSize = (float)Math.Max(6, text.TextHeight * _zoom);
        using var format = new CanvasTextFormat
        {
            FontFamily = style.FontFamily,
            FontSize = fontSize,
            WordWrapping = CanvasWordWrapping.NoWrap
        };
        var origin = WorldToScreen(text.Position);
        var previous = ds.Transform;
        ds.Transform = Matrix3x2.CreateRotation((float)-text.RotationRadians, origin);
        var lines = text.ApproximateLines();
        for (var i = 0; i < lines.Count; i++)
        {
            var lineOrigin = WorldToScreen(new CadPoint(text.Position.X, text.Position.Y - (lineHeightWorld * i)));
            ds.DrawText(lines[i], lineOrigin, color, format);
        }
        ds.Transform = previous;
    }

    private void DrawAngularDimension(CanvasDrawingSession ds, AngularDimensionEntity dimension, Color color, float strokeWidth)
    {
        var radius = dimension.Radius;
        var firstDirection = NormalizeVector(dimension.FirstRayPoint - dimension.Vertex);
        var secondDirection = NormalizeVector(dimension.SecondRayPoint - dimension.Vertex);
        var firstArc = AddWorld(dimension.Vertex, firstDirection, radius);
        var secondArc = AddWorld(dimension.Vertex, secondDirection, radius);
        ds.DrawLine(WorldToScreen(dimension.Vertex), WorldToScreen(firstArc), color, strokeWidth);
        ds.DrawLine(WorldToScreen(dimension.Vertex), WorldToScreen(secondArc), color, strokeWidth);
        DrawWorldChain(ds, dimension.GetArcSamplePoints(), color, strokeWidth);
        DrawDimensionTick(ds, WorldToScreen(firstArc), color, strokeWidth);
        DrawDimensionTick(ds, WorldToScreen(secondArc), color, strokeWidth);

        var style = ResolveDimensionStyle(dimension.StyleName);
        var degrees = dimension.MeasurementRadians * 180.0 / Math.PI;
        var label = dimension.TextOverride ?? $"{style.Format(degrees)}°";
        DrawDimensionLabel(ds, label, dimension.GetArcMidpoint(), style, color);
    }

    private void DrawRadialDimension(CanvasDrawingSession ds, RadialDimensionEntity dimension, Color color, float strokeWidth)
    {
        ds.DrawLine(WorldToScreen(dimension.Center), WorldToScreen(dimension.PointOnCircle), color, strokeWidth);
        ds.DrawLine(WorldToScreen(dimension.PointOnCircle), WorldToScreen(dimension.TextPoint), color, strokeWidth);
        DrawArrowHead(ds, WorldToScreen(dimension.PointOnCircle), WorldToScreen(dimension.Center), color, strokeWidth);
        var style = ResolveDimensionStyle(dimension.StyleName);
        var value = dimension.Measurement;
        var prefix = dimension.Diameter ? "⌀" : "R";
        var label = dimension.TextOverride ?? prefix + style.Format(value);
        DrawDimensionLabel(ds, label, dimension.TextPoint, style, color);
    }

    private void DrawLeader(CanvasDrawingSession ds, LeaderEntity leader, Color color, float strokeWidth)
    {
        DrawWorldChain(ds, leader.Points, color, strokeWidth);
        if (leader.Points.Count >= 2)
            DrawArrowHead(ds, WorldToScreen(leader.Points[0]), WorldToScreen(leader.Points[1]), color, strokeWidth);

        var style = ResolveTextStyle(leader.StyleName);
        var anchor = WorldToScreen(leader.Points[^1]);
        using var format = new CanvasTextFormat
        {
            FontFamily = style.FontFamily,
            FontSize = (float)Math.Max(6, leader.TextHeight * _zoom),
            WordWrapping = CanvasWordWrapping.NoWrap
        };
        ds.DrawText(leader.Text, anchor + new Vector2(4, -12), color, format);
    }

    private void DrawCompletedAnnotationGrips(CanvasDrawingSession ds)
    {
        foreach (var entity in _interaction.Selection.SelectedEntities.Where(CadAnnotationEntityGeometry.Supports))
        {
            foreach (var grip in CadAnnotationEntityGeometry.GetGripPoints(entity))
            {
                var screen = WorldToScreen(grip);
                ds.FillRectangle(screen.X - 2.5f, screen.Y - 2.5f, 5, 5, _selectedColor);
            }
        }
    }

    private void DrawCompletedAnnotationPreview(CanvasDrawingSession ds)
    {
        if (!_modifyPointInputActive || _modifyPreviewFactory is null) return;
        try
        {
            foreach (var entity in _modifyPreviewFactory(CurrentModifyPointerWorldPosition).Where(CadAnnotationEntityGeometry.Supports))
                DrawCompletedAnnotation(ds, entity, _transientColor, 1.2f);
        }
        catch
        {
            // Preview creation owns validity; transient invalid states remain invisible.
        }
    }

    private CadTextStyle ResolveTextStyle(string name) =>
        _document.TryGetTextStyle(name, out var style) && style is not null ? style : CadTextStyle.CreateDefault();

    private CadDimensionStyle ResolveDimensionStyle(string name) =>
        _document.TryGetDimensionStyle(name, out var style) && style is not null ? style : CadDimensionStyle.CreateDefault();

    private void DrawDimensionLabel(CanvasDrawingSession ds, string label, CadPoint worldPoint, CadDimensionStyle style, Color color)
    {
        using var format = new CanvasTextFormat
        {
            FontSize = (float)Math.Max(7, style.TextHeight * _zoom),
            WordWrapping = CanvasWordWrapping.NoWrap
        };
        ds.DrawText(label, WorldToScreen(worldPoint) + new Vector2(4, -12), color, format);
    }

    private void DrawWorldChain(CanvasDrawingSession ds, IReadOnlyList<CadPoint> points, Color color, float strokeWidth)
    {
        for (var i = 1; i < points.Count; i++)
            ds.DrawLine(WorldToScreen(points[i - 1]), WorldToScreen(points[i]), color, strokeWidth);
    }

    private static void DrawDimensionTick(CanvasDrawingSession ds, Vector2 point, Color color, float strokeWidth)
    {
        const float size = 4;
        ds.DrawLine(point.X - size, point.Y - size, point.X + size, point.Y + size, color, strokeWidth);
    }

    private static void DrawArrowHead(CanvasDrawingSession ds, Vector2 tip, Vector2 toward, Color color, float strokeWidth)
    {
        var vector = toward - tip;
        if (vector.LengthSquared() <= 1e-8f) return;
        vector = Vector2.Normalize(vector);
        var perpendicular = new Vector2(-vector.Y, vector.X);
        const float length = 8;
        const float halfWidth = 3.5f;
        var basePoint = tip + (vector * length);
        ds.DrawLine(tip, basePoint + (perpendicular * halfWidth), color, strokeWidth);
        ds.DrawLine(tip, basePoint - (perpendicular * halfWidth), color, strokeWidth);
    }

    private static CadVector NormalizeVector(CadVector vector)
    {
        var length = vector.Length;
        return length <= 1e-9 ? new CadVector(1, 0) : new CadVector(vector.X / length, vector.Y / length);
    }

    private static CadPoint AddWorld(CadPoint point, CadVector direction, double distance) =>
        new(point.X + (direction.X * distance), point.Y + (direction.Y * distance));
}
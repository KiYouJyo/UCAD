using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.UI.Xaml;
using System.Numerics;
using UCAD.Core.Entities;
using UCAD.Core.Interaction;
using Windows.UI;

namespace UCAD.Views;

public sealed partial class CadViewport
{
    private bool _extendedDrawingRenderHooksInstalled;

    public void EnsureExtendedDrawingRenderHooks()
    {
        if (_extendedDrawingRenderHooksInstalled) return;
        _extendedDrawingRenderHooksInstalled = true;
        Canvas.Draw += Canvas_ExtendedDrawingRender;
        Canvas.Invalidate();
    }

    private void Canvas_ExtendedDrawingRender(CanvasControl sender, CanvasDrawEventArgs args)
    {
        var ds = args.DrawingSession;
        foreach (var entity in _document.VisibleEntities.Where(CadExtendedEntityGeometry.Supports))
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
            DrawExtendedEntity(ds, entity, color, strokeWidth, sender.ActualWidth, sender.ActualHeight);
        }

        DrawExtendedSelectionGrips(ds);
        DrawExtendedModifyPreview(ds, sender.ActualWidth, sender.ActualHeight);
        DrawCadCursor(ds, sender.ActualWidth, sender.ActualHeight);
    }

    private void DrawExtendedEntity(
        CanvasDrawingSession ds,
        ICadEntity entity,
        Color color,
        float strokeWidth,
        double viewportWidth,
        double viewportHeight)
    {
        switch (entity)
        {
            case PointEntity point:
                DrawPointMarker(ds, point, color, strokeWidth);
                break;
            case EllipseEntity ellipse:
                DrawExtendedChain(ds, ellipse.SamplePoints(), ellipse.IsFullEllipse, color, strokeWidth);
                break;
            case SplineEntity spline:
                DrawExtendedChain(ds, spline.SamplePoints(), spline.Closed, color, strokeWidth);
                break;
            case RayEntity ray:
                DrawInfiniteEntity(ds, ray.Origin, ray.Direction, rayOnly: true, color, strokeWidth, viewportWidth, viewportHeight);
                break;
            case XLineEntity xline:
                DrawInfiniteEntity(ds, xline.Point, xline.Direction, rayOnly: false, color, strokeWidth, viewportWidth, viewportHeight);
                break;
        }
    }

    private void DrawPointMarker(CanvasDrawingSession ds, PointEntity point, Color color, float strokeWidth)
    {
        var screen = WorldToScreen(point.Position);
        const float size = 4f;
        ds.DrawLine(screen.X - size, screen.Y, screen.X + size, screen.Y, color, strokeWidth);
        ds.DrawLine(screen.X, screen.Y - size, screen.X, screen.Y + size, color, strokeWidth);
        ds.DrawCircle(screen, 2.2f, color, Math.Max(1, strokeWidth));
    }

    private void DrawExtendedChain(CanvasDrawingSession ds, IReadOnlyList<Core.Geometry.CadPoint> points, bool closed, Color color, float strokeWidth)
    {
        if (points.Count < 2) return;
        for (var i = 1; i < points.Count; i++)
            ds.DrawLine(WorldToScreen(points[i - 1]), WorldToScreen(points[i]), color, strokeWidth);
        if (closed && (points[0] - points[^1]).Length > 1e-9)
            ds.DrawLine(WorldToScreen(points[^1]), WorldToScreen(points[0]), color, strokeWidth);
    }

    private void DrawInfiniteEntity(
        CanvasDrawingSession ds,
        Core.Geometry.CadPoint anchor,
        Core.Geometry.CadVector direction,
        bool rayOnly,
        Color color,
        float strokeWidth,
        double viewportWidth,
        double viewportHeight)
    {
        var origin = WorldToScreen(anchor);
        var screenDirection = new Vector2((float)direction.X, (float)-direction.Y);
        if (screenDirection.LengthSquared() < 1e-8f) return;
        screenDirection = Vector2.Normalize(screenDirection);
        var span = (float)(Math.Sqrt((viewportWidth * viewportWidth) + (viewportHeight * viewportHeight)) * 2.5 + 64);
        var start = rayOnly ? origin : origin - (screenDirection * span);
        var end = origin + (screenDirection * span);
        ds.DrawLine(start, end, color, strokeWidth);
    }

    private void DrawExtendedSelectionGrips(CanvasDrawingSession ds)
    {
        foreach (var entity in _interaction.Selection.SelectedEntities.Where(CadExtendedEntityGeometry.Supports))
        {
            foreach (var grip in CadExtendedEntityGeometry.GetGripPoints(entity))
            {
                var screen = WorldToScreen(grip);
                ds.FillRectangle(screen.X - 2.5f, screen.Y - 2.5f, 5, 5, _selectedColor);
            }
        }
    }

    private void DrawExtendedModifyPreview(CanvasDrawingSession ds, double width, double height)
    {
        if (!_modifyPointInputActive || _modifyPreviewFactory is null) return;
        try
        {
            foreach (var entity in _modifyPreviewFactory(CurrentModifyPointerWorldPosition).Where(CadExtendedEntityGeometry.Supports))
                DrawExtendedEntity(ds, entity, _transientColor, 1.2f, width, height);
        }
        catch
        {
            // Invalid transient geometry stays invisible until the next valid pointer position.
        }
    }
}
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Geometry;
using Microsoft.Graphics.Canvas.Text;
using Microsoft.Graphics.Canvas.UI.Xaml;
using System.Numerics;
using UCAD.Core.Entities;
using UCAD.Core.Geometry;
using UCAD.Core.Hatching;
using UCAD.Core.Interaction;
using Windows.UI;

namespace UCAD.Views;

public sealed partial class CadViewport
{
    private bool _authoringRenderHooksInstalled;

    /// <summary>
    /// Final ordered document render pass. All supported 2D entity families are routed
    /// through this single document-order traversal so block children render exactly like
    /// top-level entities and later helper passes cannot reorder annotation/extended geometry.
    /// </summary>
    public void EnsureAuthoringRenderHooks()
    {
        if (_authoringRenderHooksInstalled) return;
        _authoringRenderHooksInstalled = true;
        Canvas.Draw += Canvas_AuthoringRender;
        Canvas.PointerMoved += Canvas_LayerAwarePointerMoved;
        Canvas.Invalidate();
    }

    private void Canvas_AuthoringRender(CanvasControl sender, CanvasDrawEventArgs args)
    {
        var ds = args.DrawingSession;
        ds.Clear(_canvasBackground);
        if (_showGrid) DrawGrid(ds, sender.ActualWidth, sender.ActualHeight);

        foreach (var entity in _document.VisibleEntities)
        {
            if (_interaction.Selection.Contains(entity.Id))
            {
                DrawAuthoringEntity(ds, entity, _selectedColor, 2.0f);
            }
            else if (_hoverEntityId == entity.Id)
            {
                DrawAuthoringEntity(ds, entity, _hoverColor, 1.6f);
            }
            else
            {
                var properties = _document.GetEntityProperties(entity.Id);
                var layer = _document.GetLayer(properties.LayerName);
                var color = ResolveEntityColor(properties.ColorHex, layer.ColorHex);
                var lineWeight = properties.LineWeight ?? layer.LineWeight;
                var strokeWidth = (float)Math.Clamp(lineWeight * 3.0, 0.8, 4.0);
                DrawAuthoringEntity(ds, entity, color, strokeWidth);
            }
        }

        DrawSelectionGrips(ds);
        DrawExtendedSelectionGrips(ds);
        DrawCompletedAnnotationGrips(ds);
        DrawTransientGeometry(ds);
        DrawSelectionWindow(ds);
        DrawSnapMarker(ds);
        DrawAuthoringModifyPreview(ds);
        DrawModifySnapMarker(ds);
        DrawCadCursor(ds, sender.ActualWidth, sender.ActualHeight);
    }

    private Color ResolveEntityColor(string? entityColor, string layerColor)
    {
        if (entityColor is not null && TryParseColor(entityColor, out var explicitColor)) return explicitColor;
        return TryParseColor(layerColor, out var inheritedColor) ? inheritedColor : _geometryColor;
    }

    private void DrawAuthoringEntity(CanvasDrawingSession ds, ICadEntity entity, Color color, float strokeWidth)
    {
        switch (entity)
        {
            case WipeoutEntity wipeout:
                DrawWipeoutEntity(ds, wipeout);
                break;
            case TextEntity text:
                DrawTextEntity(ds, text, color);
                break;
            case MTextEntity or AngularDimensionEntity or RadialDimensionEntity or LeaderEntity:
                DrawCompletedAnnotation(ds, entity, color, strokeWidth);
                break;
            case LinearDimensionEntity dimension:
                DrawDimensionEntity(ds, dimension, color, strokeWidth);
                break;
            case HatchEntity hatch:
                DrawHatchEntity(ds, hatch, color, strokeWidth);
                break;
            case PointEntity or EllipseEntity or SplineEntity or RayEntity or XLineEntity:
                DrawExtendedEntity(ds, entity, color, strokeWidth, Canvas.ActualWidth, Canvas.ActualHeight);
                break;
            case BlockReferenceEntity block:
                foreach (var child in block.Contents) DrawAuthoringEntity(ds, child, color, strokeWidth);
                break;
            default:
                DrawEntity(ds, entity, color, strokeWidth);
                break;
        }
    }

    private void DrawWipeoutEntity(CanvasDrawingSession ds, WipeoutEntity wipeout)
    {
        if (wipeout.Boundary.Count < 3) return;
        using var builder = new CanvasPathBuilder(ds.Device);
        builder.BeginFigure(WorldToScreen(wipeout.Boundary[0]));
        for (var index = 1; index < wipeout.Boundary.Count; index++) builder.AddLine(WorldToScreen(wipeout.Boundary[index]));
        builder.EndFigure(CanvasFigureLoop.Closed);
        using var geometry = CanvasGeometry.CreatePath(builder);
        ds.FillGeometry(geometry, _canvasBackground);
    }

    private void DrawTextEntity(CanvasDrawingSession ds, TextEntity text, Color color)
    {
        var screen = WorldToScreen(text.Position);
        var style = ResolveTextStyle(text.StyleName);
        var size = (float)Math.Max(6, text.Height * _zoom);
        using var format = new CanvasTextFormat
        {
            FontFamily = style.FontFamily,
            FontSize = size,
            WordWrapping = CanvasWordWrapping.NoWrap
        };
        var previous = ds.Transform;
        ds.Transform = Matrix3x2.CreateRotation((float)-text.RotationRadians, screen);
        ds.DrawText(text.Text, screen, color, format);
        ds.Transform = previous;
    }

    private void DrawDimensionEntity(CanvasDrawingSession ds, LinearDimensionEntity dimension, Color color, float strokeWidth)
    {
        var endpoints = dimension.GetDimensionLineEndpoints();
        var a = WorldToScreen(dimension.FirstExtensionPoint);
        var b = WorldToScreen(dimension.SecondExtensionPoint);
        var da = WorldToScreen(endpoints.First);
        var db = WorldToScreen(endpoints.Second);
        ds.DrawLine(a, da, color, strokeWidth);
        ds.DrawLine(b, db, color, strokeWidth);
        ds.DrawLine(da, db, color, strokeWidth);

        const float tick = 4f;
        ds.DrawLine(da.X - tick, da.Y - tick, da.X + tick, da.Y + tick, color, strokeWidth);
        ds.DrawLine(db.X - tick, db.Y - tick, db.X + tick, db.Y + tick, color, strokeWidth);
        var midpoint = (da + db) / 2f;
        var style = ResolveDimensionStyle(dimension.StyleName);
        var label = dimension.TextOverride ?? style.Format(dimension.Measurement);
        using var format = new CanvasTextFormat { FontSize = (float)Math.Max(7, style.TextHeight * _zoom), WordWrapping = CanvasWordWrapping.NoWrap };
        ds.DrawText(label, midpoint + new Vector2(4, -14), color, format);
    }

    private void DrawHatchEntity(CanvasDrawingSession ds, HatchEntity hatch, Color color, float strokeWidth)
    {
        if (hatch.Boundary.Count < 3) return;

        if (string.Equals(hatch.Pattern, "ANSI31", StringComparison.OrdinalIgnoreCase))
        {
            var pattern = CadHatchPatternGenerator.Generate(hatch);
            foreach (var segment in pattern.Segments)
                ds.DrawLine(WorldToScreen(segment.Start), WorldToScreen(segment.End), color, strokeWidth);
            return;
        }

        if (!string.Equals(hatch.Pattern, "Solid", StringComparison.OrdinalIgnoreCase))
        {
            DrawHatchLoop(ds, hatch.Boundary, color, strokeWidth);
            foreach (var island in hatch.EffectiveIslandLoops) DrawHatchLoop(ds, island, color, strokeWidth);
            return;
        }

        using var builder = new CanvasPathBuilder(ds.Device);
        builder.SetFilledRegionDetermination(CanvasFilledRegionDetermination.Alternate);
        AddHatchLoop(builder, hatch.Boundary);
        foreach (var island in hatch.EffectiveIslandLoops) AddHatchLoop(builder, island);
        using var geometry = CanvasGeometry.CreatePath(builder);
        var fill = Color.FromArgb((byte)Math.Min(110, (int)color.A), color.R, color.G, color.B);
        ds.FillGeometry(geometry, fill);
        ds.DrawGeometry(geometry, color, strokeWidth);
    }

    private void AddHatchLoop(CanvasPathBuilder builder, IReadOnlyList<CadPoint> loop)
    {
        if (loop.Count < 3) return;
        builder.BeginFigure(WorldToScreen(loop[0]));
        for (var index = 1; index < loop.Count; index++) builder.AddLine(WorldToScreen(loop[index]));
        builder.EndFigure(CanvasFigureLoop.Closed);
    }

    private void DrawHatchLoop(
        CanvasDrawingSession ds,
        IReadOnlyList<CadPoint> loop,
        Color color,
        float strokeWidth)
    {
        if (loop.Count < 2) return;
        var previous = WorldToScreen(loop[0]);
        for (var index = 1; index < loop.Count; index++)
        {
            var current = WorldToScreen(loop[index]);
            ds.DrawLine(previous, current, color, strokeWidth);
            previous = current;
        }
        ds.DrawLine(previous, WorldToScreen(loop[0]), color, strokeWidth);
    }

    private void DrawAuthoringModifyPreview(CanvasDrawingSession ds)
    {
        if (!_modifyPointInputActive || _modifyPreviewFactory is null) return;
        try
        {
            foreach (var entity in _modifyPreviewFactory(CurrentModifyPointerWorldPosition))
                DrawAuthoringEntity(ds, entity, _transientColor, 1.2f);
        }
        catch
        {
            // Invalid transient geometry is simply not rendered; commit validation still owns correctness.
        }
    }

    private void Canvas_LayerAwarePointerMoved(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        if (_drawingCommand is not null && _interaction.ObjectSnapEnabled)
        {
            _activeSnap = ObjectSnapResolver.Resolve(
                _document.VisibleEntities,
                RawPointerWorldPosition,
                _objectSnapAperturePixels / _zoom,
                _interaction.ObjectSnapModes);
        }
        else if (_modifyPointInputActive && _interaction.ObjectSnapEnabled)
        {
            _activeSnap = ObjectSnapResolver.Resolve(
                _document.VisibleEntities,
                RawPointerWorldPosition,
                _objectSnapAperturePixels / _zoom,
                _interaction.ObjectSnapModes);
        }

        if (_modifyEntityPickActive)
        {
            _hoverEntityId = CadSelectionQuery.HitTestNearest(
                _document.SelectableEntities,
                RawPointerWorldPosition,
                _pickboxSizePixels / _zoom)?.Id;
        }
        else if (_drawingCommand is null && !_modifyPointInputActive && !_selectionPointerDown && !_selectionWindowArmed && _selectionPreview)
        {
            _hoverEntityId = CadSelectionQuery.HitTestNearest(
                _document.SelectableEntities,
                RawPointerWorldPosition,
                _pickboxSizePixels / _zoom)?.Id;
        }
        Canvas.Invalidate();
    }
}

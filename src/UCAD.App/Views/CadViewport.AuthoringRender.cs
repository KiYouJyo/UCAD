using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Geometry;
using Microsoft.Graphics.Canvas.Text;
using Microsoft.Graphics.Canvas.UI.Xaml;
using System.Numerics;
using UCAD.Core.Entities;
using UCAD.Core.Interaction;
using Windows.UI;

namespace UCAD.Views;

public sealed partial class CadViewport
{
    private bool _authoringRenderHooksInstalled;

    /// <summary>
    /// v0.6/v0.7 final render pass. It deliberately runs after the original v0.4/v0.5
    /// renderer, clears that pass, and redraws from document layer state. This lets the
    /// milestone add real layer visibility/properties and new entity types without
    /// creating a second viewport or disturbing the accepted input pipeline.
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
            case TextEntity text:
                DrawTextEntity(ds, text, color);
                break;
            case LinearDimensionEntity dimension:
                DrawDimensionEntity(ds, dimension, color, strokeWidth);
                break;
            case HatchEntity hatch:
                DrawHatchEntity(ds, hatch, color, strokeWidth);
                break;
            case BlockReferenceEntity block:
                foreach (var child in block.Contents) DrawAuthoringEntity(ds, child, color, strokeWidth);
                break;
            default:
                DrawEntity(ds, entity, color, strokeWidth);
                break;
        }
    }

    private void DrawTextEntity(CanvasDrawingSession ds, TextEntity text, Color color)
    {
        var screen = WorldToScreen(text.Position);
        var size = (float)Math.Max(6, text.Height * _zoom);
        using var format = new CanvasTextFormat
        {
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
        var label = dimension.TextOverride ?? dimension.Measurement.ToString("0.##");
        using var format = new CanvasTextFormat { FontSize = 10, WordWrapping = CanvasWordWrapping.NoWrap };
        ds.DrawText(label, midpoint + new Vector2(4, -14), color, format);
    }

    private void DrawHatchEntity(CanvasDrawingSession ds, HatchEntity hatch, Color color, float strokeWidth)
    {
        if (hatch.Boundary.Count < 3) return;
        using var builder = new CanvasPathBuilder(ds.Device);
        builder.BeginFigure(WorldToScreen(hatch.Boundary[0]));
        for (var i = 1; i < hatch.Boundary.Count; i++) builder.AddLine(WorldToScreen(hatch.Boundary[i]));
        builder.EndFigure(CanvasFigureLoop.Closed);
        using var geometry = CanvasGeometry.CreatePath(builder);
        var fill = Color.FromArgb((byte)Math.Min(110, color.A), color.R, color.G, color.B);
        ds.FillGeometry(geometry, fill);
        ds.DrawGeometry(geometry, color, strokeWidth);
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

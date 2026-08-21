using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using System.Numerics;
using UCAD.Core.Geometry;
using UCAD.Core.Interaction;

namespace UCAD.Views;

public sealed partial class CadViewport
{
    private bool _draftingAidHooksInstalled;
    private CadPoint? _objectTrackingAnchor;
    private ObjectTrackingAxis _objectTrackingAxis;

    internal void EnsureDraftingAidHooks()
    {
        EnsureModifyInputHooks();
        if (_draftingAidHooksInstalled) return;
        _draftingAidHooksInstalled = true;

        // ModifyInput already owns the gated pointer pipeline. Wrap that exact pipeline
        // so every legacy draw command and every v0.9 point-input command sees the same
        // constrained preview/commit position.
        Canvas.PointerMoved -= Canvas_ModifyAwarePointerMoved;
        Canvas.PointerPressed -= Canvas_ModifyAwarePointerPressed;
        Canvas.PointerMoved += Canvas_DraftingAwarePointerMoved;
        Canvas.PointerPressed += Canvas_DraftingAwarePointerPressed;
        Canvas.Draw += Canvas_DraftingAidOverlayDraw;
        _interaction.Changed += (_, _) => ApplyDraftingAidVisualState();
        ApplyDraftingAidVisualState();
    }

    internal void ApplyDraftingAidVisualState()
    {
        _showGrid = _interaction.GridDisplayEnabled;
        if (!_interaction.ObjectSnapTrackingEnabled)
        {
            _objectTrackingAnchor = null;
            _objectTrackingAxis = ObjectTrackingAxis.None;
        }
        Canvas.Invalidate();
    }

    private void Canvas_DraftingAwarePointerMoved(object sender, PointerRoutedEventArgs e)
    {
        Canvas_ModifyAwarePointerMoved(sender, e);
        if (_isPanning || !IsDraftingPointInputActive()) return;

        UpdateDraftingPointerInteraction();
        PointerWorldPositionChanged?.Invoke(CurrentDraftingWorldPosition());
        Canvas.Invalidate();
    }

    private void Canvas_DraftingAwarePointerPressed(object sender, PointerRoutedEventArgs e)
    {
        var current = e.GetCurrentPoint(Canvas);
        if (current.Properties.IsMiddleButtonPressed || !current.Properties.IsLeftButtonPressed)
        {
            Canvas_ModifyAwarePointerPressed(sender, e);
            return;
        }

        _pointerScreen = new Vector2((float)current.Position.X, (float)current.Position.Y);

        if (_drawingCommand is not null)
        {
            UpdateDraftingPointerInteraction();
            SubmitDrawingPoint(CurrentPointerWorldPosition);
            e.Handled = true;
            return;
        }

        if (_modifyPointInputActive)
        {
            UpdateDraftingPointerInteraction();
            TryAcceptModifyPoint();
            e.Handled = true;
            return;
        }

        Canvas_ModifyAwarePointerPressed(sender, e);
    }

    private bool IsDraftingPointInputActive() => _drawingCommand is not null || _modifyPointInputActive;

    private CadPoint CurrentDraftingWorldPosition() =>
        _modifyPointInputActive ? CurrentModifyPointerWorldPosition : CurrentPointerWorldPosition;

    private CadPoint? CurrentDraftingBasePoint()
    {
        if (_modifyPointInputActive) return _modifyPointBase;
        if (_drawingCommand is not null && _inputPoints.Count > 0) return _inputPoints[^1];
        return null;
    }

    private void UpdateDraftingPointerInteraction()
    {
        if (!IsDraftingPointInputActive()) return;

        var raw = RawPointerWorldPosition;
        var aperture = _objectSnapAperturePixels / _zoom;

        // 1. Real object snaps always win. They also seed OTRACK acquisition.
        ObjectSnapResult? realSnap = null;
        if (_interaction.ObjectSnapEnabled)
        {
            realSnap = ObjectSnapResolver.Resolve(
                _document.VisibleEntities,
                raw,
                aperture,
                _interaction.ObjectSnapModes);
        }
        if (realSnap is not null)
        {
            _activeSnap = realSnap;
            _objectTrackingAxis = ObjectTrackingAxis.None;
            if (_interaction.ObjectSnapTrackingEnabled) _objectTrackingAnchor = realSnap.Point;
            return;
        }

        _activeSnap = null;
        _objectTrackingAxis = ObjectTrackingAxis.None;

        // 2. Acquired object-snap tracking is stronger than angular/grid constraints.
        if (_interaction.ObjectSnapTrackingEnabled &&
            _objectTrackingAnchor is CadPoint anchor &&
            DraftingConstraints.TryApplyObjectTracking(
                anchor,
                raw,
                aperture,
                out var tracked,
                out var trackingAxis))
        {
            _objectTrackingAxis = trackingAxis;
            _activeSnap = SyntheticSnap(tracked, ObjectSnapKind.Tracking, raw);
            return;
        }

        // 3. Existing Ortho semantics retain priority. Leaving _activeSnap null lets the
        // accepted legacy/Modify resolvers apply their established Ortho behavior.
        if (_interaction.OrthoEnabled) return;

        // 4. Polar tracking constrains angle while preserving cursor radius.
        if (_interaction.PolarTrackingEnabled && CurrentDraftingBasePoint() is CadPoint basePoint)
        {
            var polar = DraftingConstraints.ApplyPolar(basePoint, raw, _interaction.PolarIncrementDegrees);
            _activeSnap = SyntheticSnap(polar, ObjectSnapKind.Polar, raw);
            return;
        }

        // 5. Absolute grid snap is the final constraint.
        if (_interaction.GridSnapEnabled)
        {
            var grid = DraftingConstraints.SnapToGrid(raw, _interaction.GridSnapSpacing);
            _activeSnap = SyntheticSnap(grid, ObjectSnapKind.Grid, raw);
        }
    }

    private static ObjectSnapResult SyntheticSnap(CadPoint point, ObjectSnapKind kind, CadPoint raw) =>
        new(point, kind, Guid.Empty, null, (point - raw).Length);

    private void Canvas_DraftingAidOverlayDraw(CanvasControl sender, CanvasDrawEventArgs args)
    {
        if (!IsDraftingPointInputActive() || _activeSnap is null) return;
        if (_activeSnap.Kind is not (ObjectSnapKind.Grid or ObjectSnapKind.Polar or ObjectSnapKind.Tracking)) return;

        var ds = args.DrawingSession;
        var point = WorldToScreen(_activeSnap.Point);
        const float marker = 5f;

        switch (_activeSnap.Kind)
        {
            case ObjectSnapKind.Grid:
                ds.DrawRectangle(point.X - marker, point.Y - marker, marker * 2, marker * 2, _snapColor, 1.2f);
                ds.DrawLine(point.X - marker - 3, point.Y, point.X + marker + 3, point.Y, _snapColor, 1.0f);
                ds.DrawLine(point.X, point.Y - marker - 3, point.X, point.Y + marker + 3, _snapColor, 1.0f);
                break;

            case ObjectSnapKind.Polar:
                if (CurrentDraftingBasePoint() is CadPoint basePoint)
                    ds.DrawLine(WorldToScreen(basePoint), point, _snapColor, 0.9f);
                ds.DrawCircle(point, marker, _snapColor, 1.2f);
                break;

            case ObjectSnapKind.Tracking:
                if (_objectTrackingAnchor is CadPoint anchor)
                    ds.DrawLine(WorldToScreen(anchor), point, _snapColor, 0.9f);
                ds.DrawLine(point.X - marker, point.Y, point.X + marker, point.Y, _snapColor, 1.2f);
                ds.DrawLine(point.X, point.Y - marker, point.X, point.Y + marker, _snapColor, 1.2f);
                break;
        }

        DrawCadCursor(ds, sender.ActualWidth, sender.ActualHeight);
    }
}
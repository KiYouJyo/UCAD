using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using System.Numerics;
using UCAD.Core.Entities;
using UCAD.Core.Geometry;
using UCAD.Core.Interaction;

namespace UCAD.Views;

public sealed partial class CadViewport
{
    private bool _modifyInputHooksInstalled;
    private bool _modifyPointInputActive;
    private bool _modifyEntityPickActive;
    private CadPoint? _modifyPointBase;
    private bool _modifyPointUseOrtho;
    private Func<CadPoint, IReadOnlyList<ICadEntity>>? _modifyPreviewFactory;

    public event Action<CadPoint>? ModifyPointAccepted;
    public event Action<Guid, CadPoint>? ModifyEntityPicked;

    public bool IsModifyPointInputActive => _modifyPointInputActive;
    public bool IsModifyEntityPickActive => _modifyEntityPickActive;
    public CadPoint CurrentModifyPointerWorldPosition => ResolveModifyPoint(RawPointerWorldPosition);

    internal void EnsureModifyInputHooks()
    {
        if (_modifyInputHooksInstalled)
        {
            return;
        }

        _modifyInputHooksInstalled = true;

        // XAML initially wires the regular drawing/selection handlers. Replace only
        // the two handlers that need an input-mode gate; all existing pointer release,
        // capture, wheel and panning behavior remains unchanged.
        Canvas.PointerMoved -= Canvas_PointerMoved;
        Canvas.PointerPressed -= Canvas_PointerPressed;
        Canvas.PointerMoved += Canvas_ModifyAwarePointerMoved;
        Canvas.PointerPressed += Canvas_ModifyAwarePointerPressed;
        Canvas.Draw += Canvas_ModifyOverlayDraw;
        _interaction.Changed += (_, _) =>
        {
            if (_modifyPointInputActive)
            {
                UpdateModifyPointerInteraction();
                Canvas.Invalidate();
            }
        };
    }

    public void BeginModifyPointInput(
        CadPoint? basePoint = null,
        bool useOrtho = false,
        Func<CadPoint, IReadOnlyList<ICadEntity>>? previewFactory = null)
    {
        CancelSelectionGesture();
        _drawingCommand = null;
        _inputPoints.Clear();
        _modifyEntityPickActive = false;
        _modifyPointInputActive = true;
        _modifyPointBase = basePoint;
        _modifyPointUseOrtho = useOrtho;
        _modifyPreviewFactory = previewFactory;
        _hoverEntityId = null;
        UpdateModifyPointerInteraction();
        Canvas.Invalidate();
    }

    public void BeginModifyEntityPickInput()
    {
        CancelSelectionGesture();
        _drawingCommand = null;
        _inputPoints.Clear();
        _modifyPointInputActive = false;
        _modifyPointBase = null;
        _modifyPointUseOrtho = false;
        _modifyPreviewFactory = null;
        _modifyEntityPickActive = true;
        _activeSnap = null;
        UpdateModifyEntityHover();
        Canvas.Invalidate();
    }

    public bool CancelModifyInput()
    {
        var changed = _modifyPointInputActive || _modifyEntityPickActive || _modifyPreviewFactory is not null;
        _modifyPointInputActive = false;
        _modifyEntityPickActive = false;
        _modifyPointBase = null;
        _modifyPointUseOrtho = false;
        _modifyPreviewFactory = null;
        _activeSnap = null;
        _hoverEntityId = null;
        if (changed)
        {
            Canvas.Invalidate();
        }
        return changed;
    }

    internal CadPoint ResolveModifyPoint(CadPoint raw)
    {
        if (!_modifyPointInputActive)
        {
            return raw;
        }
        if (_activeSnap is not null)
        {
            return _activeSnap.Point;
        }
        if (_modifyPointUseOrtho && _interaction.OrthoEnabled && _modifyPointBase is CadPoint basePoint)
        {
            return OrthoConstraint.Apply(basePoint, raw);
        }
        return raw;
    }

    internal void UpdateModifyPointerInteraction()
    {
        if (_modifyPointInputActive && _interaction.ObjectSnapEnabled)
        {
            _activeSnap = ObjectSnapResolver.Resolve(
                _document.Entities,
                RawPointerWorldPosition,
                _objectSnapAperturePixels / _zoom,
                _interaction.ObjectSnapModes);
        }
        else if (_modifyPointInputActive)
        {
            _activeSnap = null;
        }
    }

    internal bool TryAcceptModifyPoint()
    {
        if (!_modifyPointInputActive)
        {
            return false;
        }
        var point = CurrentModifyPointerWorldPosition;
        ModifyPointAccepted?.Invoke(point);
        Canvas.Invalidate();
        return true;
    }

    internal bool TryAcceptModifyEntityPick(PointerRoutedEventArgs e)
    {
        if (!_modifyEntityPickActive)
        {
            return false;
        }

        var hit = CadSelectionQuery.HitTestNearest(
            _document.Entities,
            RawPointerWorldPosition,
            _pickboxSizePixels / _zoom);
        if (hit is not null)
        {
            ModifyEntityPicked?.Invoke(hit.Id, RawPointerWorldPosition);
        }
        e.Handled = true;
        Canvas.Invalidate();
        return true;
    }

    internal void UpdateModifyEntityHover()
    {
        if (!_modifyEntityPickActive)
        {
            return;
        }
        _activeSnap = null;
        _hoverEntityId = CadSelectionQuery.HitTestNearest(
            _document.Entities,
            RawPointerWorldPosition,
            _pickboxSizePixels / _zoom)?.Id;
    }

    private void Canvas_ModifyAwarePointerMoved(object sender, PointerRoutedEventArgs e)
    {
        Canvas_PointerMoved(sender, e);
        if (_isPanning)
        {
            return;
        }

        if (_modifyPointInputActive)
        {
            _hoverEntityId = null;
            UpdateModifyPointerInteraction();
            PointerWorldPositionChanged?.Invoke(CurrentModifyPointerWorldPosition);
            Canvas.Invalidate();
            return;
        }

        if (_modifyEntityPickActive)
        {
            UpdateModifyEntityHover();
            Canvas.Invalidate();
        }
    }

    private void Canvas_ModifyAwarePointerPressed(object sender, PointerRoutedEventArgs e)
    {
        var current = e.GetCurrentPoint(Canvas);
        if (current.Properties.IsMiddleButtonPressed)
        {
            Canvas_PointerPressed(sender, e);
            return;
        }
        if (!current.Properties.IsLeftButtonPressed)
        {
            Canvas_PointerPressed(sender, e);
            return;
        }

        _pointerScreen = new Vector2((float)current.Position.X, (float)current.Position.Y);
        if (_modifyPointInputActive)
        {
            UpdateModifyPointerInteraction();
            TryAcceptModifyPoint();
            e.Handled = true;
            return;
        }
        if (_modifyEntityPickActive)
        {
            TryAcceptModifyEntityPick(e);
            return;
        }

        Canvas_PointerPressed(sender, e);
    }

    private void Canvas_ModifyOverlayDraw(CanvasControl sender, CanvasDrawEventArgs args)
    {
        if (!_modifyPointInputActive && !_modifyEntityPickActive)
        {
            return;
        }

        DrawModifyPreview(args.DrawingSession);
        DrawModifySnapMarker(args.DrawingSession);
        // The overlay is invoked after the main Canvas_Draw handler. Repaint the CAD
        // cursor last so transient modify geometry never obscures the pickbox/crosshair.
        DrawCadCursor(args.DrawingSession, sender.ActualWidth, sender.ActualHeight);
    }

    internal void DrawModifyPreview(CanvasDrawingSession drawingSession)
    {
        if (!_modifyPointInputActive || _modifyPreviewFactory is null)
        {
            return;
        }

        IReadOnlyList<ICadEntity> preview;
        try
        {
            preview = _modifyPreviewFactory(CurrentModifyPointerWorldPosition);
        }
        catch
        {
            return;
        }

        foreach (var entity in preview)
        {
            DrawEntity(drawingSession, entity, _transientColor, 1.2f);
        }
    }

    private void DrawModifySnapMarker(CanvasDrawingSession drawingSession)
    {
        if (!_modifyPointInputActive || _activeSnap is null)
        {
            return;
        }

        var screen = WorldToScreen(_activeSnap.Point);
        const float size = 5f;
        switch (_activeSnap.Kind)
        {
            case ObjectSnapKind.Endpoint:
                drawingSession.DrawRectangle(screen.X - size, screen.Y - size, size * 2, size * 2, _snapColor, 1.4f);
                break;
            case ObjectSnapKind.Midpoint:
                drawingSession.DrawLine(screen.X, screen.Y - size, screen.X + size, screen.Y + size, _snapColor, 1.4f);
                drawingSession.DrawLine(screen.X + size, screen.Y + size, screen.X - size, screen.Y + size, _snapColor, 1.4f);
                drawingSession.DrawLine(screen.X - size, screen.Y + size, screen.X, screen.Y - size, _snapColor, 1.4f);
                break;
            case ObjectSnapKind.Intersection:
                drawingSession.DrawLine(screen.X - size, screen.Y - size, screen.X + size, screen.Y + size, _snapColor, 1.4f);
                drawingSession.DrawLine(screen.X + size, screen.Y - size, screen.X - size, screen.Y + size, _snapColor, 1.4f);
                break;
            case ObjectSnapKind.Center:
                drawingSession.DrawCircle(screen, size, _snapColor, 1.4f);
                break;
        }
    }
}

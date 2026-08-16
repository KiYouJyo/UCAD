using Microsoft.Graphics.Canvas;
using Microsoft.UI.Xaml.Input;
using UCAD.Core.Entities;
using UCAD.Core.Geometry;
using UCAD.Core.Interaction;

namespace UCAD.Views;

public sealed partial class CadViewport
{
    private bool _modifyPointInputActive;
    private bool _modifyEntityPickActive;
    private CadPoint? _modifyPointBase;
    private bool _modifyPointUseOrtho;
    private Func<CadPoint, IReadOnlyList<ICadEntity>>? _modifyPreviewFactory;

    public event Action<CadPoint>? ModifyPointAccepted;
    public event Action<Guid, CadPoint>? ModifyEntityPicked;

    public bool IsModifyPointInputActive => _modifyPointInputActive;
    public bool IsModifyEntityPickActive => _modifyEntityPickActive;

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
        UpdatePointerInteraction();
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
        if (changed) Canvas.Invalidate();
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
        if (_modifyPointUseOrtho && _modifyPointBase is CadPoint basePoint)
        {
            return OrthoConstraint.Apply(basePoint, raw);
        }
        return raw;
    }

    internal bool TryAcceptModifyPoint()
    {
        if (!_modifyPointInputActive)
        {
            return false;
        }
        var point = CurrentPointerWorldPosition;
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

    internal void DrawModifyPreview(CanvasDrawingSession drawingSession)
    {
        if (!_modifyPointInputActive || _modifyPreviewFactory is null)
        {
            return;
        }

        IReadOnlyList<ICadEntity> preview;
        try
        {
            preview = _modifyPreviewFactory(CurrentPointerWorldPosition);
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
}

using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.UI.Xaml;
using Microsoft.UI;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using System.Globalization;
using System.Numerics;
using UCAD.Core;
using UCAD.Core.Commands;
using UCAD.Core.Entities;
using UCAD.Core.Geometry;
using UCAD.Core.Interaction;
using UCAD.Services;
using Windows.UI;

namespace UCAD.Views;

public sealed partial class CadViewport : UserControl
{
    private const double GeometryEpsilon = 1e-9;
    private const float ClickSelectionAperturePixels = 6f;
    private const float ObjectSnapAperturePixels = 10f;
    private const float SelectionDragThresholdPixels = 4f;

    private readonly CadDocument _document;
    private readonly CadInteractionState _interaction;
    private readonly List<CadPoint> _inputPoints = [];
    private double _zoom = 1.0;
    private Vector2 _pan = new(120, 120);
    private Vector2 _pointerScreen;
    private bool _isPanning;
    private Vector2 _lastPanPointer;
    private DrawingCommandKind? _drawingCommand;

    private bool _selectionPointerDown;
    private bool _selectionDragging;
    private Vector2 _selectionStartScreen;
    private CadPoint _selectionStartWorld;
    private Guid? _hoverEntityId;
    private ObjectSnapResult? _activeSnap;

    private Color _canvasBackground = ColorHelper.FromArgb(255, 14, 16, 18);
    private Color _geometryColor = ColorHelper.FromArgb(255, 237, 237, 242);
    private Color _transientColor = ColorHelper.FromArgb(255, 77, 173, 245);
    private Color _selectedColor = ColorHelper.FromArgb(255, 66, 165, 245);
    private Color _hoverColor = ColorHelper.FromArgb(255, 125, 196, 250);
    private Color _snapColor = ColorHelper.FromArgb(255, 80, 220, 150);
    private Color _gridBaseColor = ColorHelper.FromArgb(255, 90, 94, 101);
    private Color _originColor = ColorHelper.FromArgb(180, 70, 70, 70);
    private Color _crosshairColor = ColorHelper.FromArgb(110, 180, 180, 180);
    private bool _showGrid = true;
    private byte _gridAlpha = 56;
    private bool _zoomAroundCursor = true;
    private bool _middleMousePan = true;
    private bool _reverseWheelZoom;
    private bool _selectionPreview = true;

    public event Action<CadPoint>? PointerWorldPositionChanged;
    public event Action<DrawingCommandKind, int, CadPoint>? DrawingPointAccepted;
    public event Action<DrawingCommandKind>? DrawingCommandCompleted;
    public event Action<double>? ZoomChanged;

    public CadDocument Document => _document;
    public CadInteractionState Interaction => _interaction;
    public CadPoint RawPointerWorldPosition => ScreenToWorld(_pointerScreen);
    public CadPoint CurrentPointerWorldPosition => ResolveCurrentDrawingPoint();
    public ObjectSnapResult? ActiveSnap => _activeSnap;
    public double Zoom => _zoom;

    public CadViewport() : this(new CadDocument()) { }

    public CadViewport(CadDocument document) : this(document, new CadInteractionState(document)) { }

    public CadViewport(CadDocument document, CadInteractionState interaction)
    {
        _document = document ?? throw new ArgumentNullException(nameof(document));
        _interaction = interaction ?? throw new ArgumentNullException(nameof(interaction));
        InitializeComponent();
        _document.Changed += (_, _) => Canvas.Invalidate();
        _interaction.Selection.Changed += (_, _) => Canvas.Invalidate();
        _interaction.Changed += (_, _) =>
        {
            UpdatePointerInteraction();
            Canvas.Invalidate();
        };
    }

    public void ApplySettings(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var lightCanvas = string.Equals(settings.CanvasTheme, "Light", StringComparison.OrdinalIgnoreCase);
        var fallbackBackground = lightCanvas
            ? ColorHelper.FromArgb(255, 255, 255, 255)
            : ColorHelper.FromArgb(255, 14, 16, 18);
        _canvasBackground = TryParseColor(settings.CanvasBackground, out var color)
            ? color
            : fallbackBackground;

        if (lightCanvas)
        {
            _geometryColor = ColorHelper.FromArgb(255, 31, 34, 38);
            _transientColor = ColorHelper.FromArgb(255, 0, 95, 184);
            _selectedColor = ColorHelper.FromArgb(255, 0, 95, 184);
            _hoverColor = ColorHelper.FromArgb(255, 60, 135, 200);
            _snapColor = ColorHelper.FromArgb(255, 0, 145, 90);
            _gridBaseColor = ColorHelper.FromArgb(255, 112, 116, 122);
            _originColor = ColorHelper.FromArgb(180, 105, 105, 110);
            _crosshairColor = ColorHelper.FromArgb(120, 75, 78, 82);
        }
        else
        {
            _geometryColor = ColorHelper.FromArgb(255, 237, 237, 242);
            _transientColor = ColorHelper.FromArgb(255, 77, 173, 245);
            _selectedColor = ColorHelper.FromArgb(255, 66, 165, 245);
            _hoverColor = ColorHelper.FromArgb(255, 125, 196, 250);
            _snapColor = ColorHelper.FromArgb(255, 80, 220, 150);
            _gridBaseColor = ColorHelper.FromArgb(255, 90, 94, 101);
            _originColor = ColorHelper.FromArgb(180, 70, 70, 70);
            _crosshairColor = ColorHelper.FromArgb(110, 180, 180, 180);
        }

        _showGrid = settings.ShowGrid;
        _gridAlpha = (byte)Math.Clamp((int)Math.Round(settings.GridOpacity / 100.0 * 255), 0, 255);
        _zoomAroundCursor = settings.ZoomAroundCursor;
        _middleMousePan = settings.MiddleMousePan;
        _reverseWheelZoom = settings.ReverseWheelZoom;
        _selectionPreview = settings.SelectionPreview;
        if (!_selectionPreview)
        {
            _hoverEntityId = null;
        }
        UpdatePointerInteraction();
        Canvas.Invalidate();
    }

    private static bool TryParseColor(string value, out Color color)
    {
        color = default;
        if (string.IsNullOrWhiteSpace(value)) return false;
        var text = value.Trim().TrimStart('#');
        if (text.Length != 6 || !uint.TryParse(text, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var rgb)) return false;
        color = ColorHelper.FromArgb(255, (byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb);
        return true;
    }

    public void BeginDrawingCommand(DrawingCommandKind kind)
    {
        _drawingCommand = kind;
        _inputPoints.Clear();
        _hoverEntityId = null;
        UpdatePointerInteraction();
        Canvas.Invalidate();
    }

    public bool SubmitDrawingPoint(CadPoint world)
    {
        if (_drawingCommand is not DrawingCommandKind kind) return false;
        switch (kind)
        {
            case DrawingCommandKind.Line: return SubmitLinePoint(world);
            case DrawingCommandKind.Polyline:
                _inputPoints.Add(world);
                DrawingPointAccepted?.Invoke(kind, _inputPoints.Count, world);
                UpdatePointerInteraction();
                Canvas.Invalidate();
                return true;
            case DrawingCommandKind.Rectangle: return SubmitRectanglePoint(world);
            case DrawingCommandKind.Circle: return SubmitCirclePoint(world);
            case DrawingCommandKind.Arc: return SubmitArcPoint(world);
            default: return false;
        }
    }

    public void CompleteDrawingCommand()
    {
        if (_drawingCommand is not DrawingCommandKind kind) return;
        if (kind == DrawingCommandKind.Polyline && _inputPoints.Count >= 2) _document.Add(new PolylineEntity(_inputPoints));
        CompleteDrawingCommandCore(kind);
    }

    public void CancelDrawingCommand()
    {
        _drawingCommand = null;
        _inputPoints.Clear();
        _activeSnap = null;
        Canvas.Invalidate();
    }

    public bool Undo()
    {
        CancelDrawingCommand();
        var changed = _document.Undo();
        Canvas.Invalidate();
        return changed;
    }

    public bool Redo()
    {
        CancelDrawingCommand();
        var changed = _document.Redo();
        Canvas.Invalidate();
        return changed;
    }

    public void ClearDocument()
    {
        CancelDrawingCommand();
        _interaction.Selection.Clear();
        _document.Clear();
        Canvas.Invalidate();
    }

    public void ResetView()
    {
        _zoom = 1.0;
        _pan = new Vector2(120, 120);
        UpdatePointerInteraction();
        ZoomChanged?.Invoke(_zoom);
        Canvas.Invalidate();
    }

    public void InvalidateInteraction()
    {
        UpdatePointerInteraction();
        Canvas.Invalidate();
    }

    private bool SubmitLinePoint(CadPoint world)
    {
        if (_inputPoints.Count > 0)
        {
            var start = _inputPoints[^1];
            if ((world - start).Length < GeometryEpsilon) return false;
            _document.Add(new LineEntity(start, world));
        }
        _inputPoints.Add(world);
        DrawingPointAccepted?.Invoke(DrawingCommandKind.Line, _inputPoints.Count, world);
        UpdatePointerInteraction();
        Canvas.Invalidate();
        return true;
    }

    private bool SubmitRectanglePoint(CadPoint world)
    {
        if (_inputPoints.Count == 0)
        {
            _inputPoints.Add(world);
            DrawingPointAccepted?.Invoke(DrawingCommandKind.Rectangle, 1, world);
            UpdatePointerInteraction();
            Canvas.Invalidate();
            return true;
        }
        var first = _inputPoints[0];
        if (Math.Abs(first.X - world.X) < GeometryEpsilon || Math.Abs(first.Y - world.Y) < GeometryEpsilon) return false;
        var corners = new[] { first, new CadPoint(world.X, first.Y), world, new CadPoint(first.X, world.Y) };
        _inputPoints.Add(world);
        DrawingPointAccepted?.Invoke(DrawingCommandKind.Rectangle, 2, world);
        _document.Add(new PolylineEntity(corners, closed: true));
        CompleteDrawingCommandCore(DrawingCommandKind.Rectangle);
        return true;
    }

    private bool SubmitCirclePoint(CadPoint world)
    {
        if (_inputPoints.Count == 0)
        {
            _inputPoints.Add(world);
            DrawingPointAccepted?.Invoke(DrawingCommandKind.Circle, 1, world);
            UpdatePointerInteraction();
            Canvas.Invalidate();
            return true;
        }
        var center = _inputPoints[0];
        var radius = (world - center).Length;
        if (radius < GeometryEpsilon) return false;
        _inputPoints.Add(world);
        DrawingPointAccepted?.Invoke(DrawingCommandKind.Circle, 2, world);
        _document.Add(new CircleEntity(center, radius));
        CompleteDrawingCommandCore(DrawingCommandKind.Circle);
        return true;
    }

    private bool SubmitArcPoint(CadPoint world)
    {
        if (_inputPoints.Count < 2)
        {
            if (_inputPoints.Count == 1 && (world - _inputPoints[0]).Length < GeometryEpsilon) return false;
            _inputPoints.Add(world);
            DrawingPointAccepted?.Invoke(DrawingCommandKind.Arc, _inputPoints.Count, world);
            UpdatePointerInteraction();
            Canvas.Invalidate();
            return true;
        }
        if (!ArcEntity.TryCreateFromThreePoints(_inputPoints[0], _inputPoints[1], world, out var arc) || arc is null) return false;
        _inputPoints.Add(world);
        DrawingPointAccepted?.Invoke(DrawingCommandKind.Arc, 3, world);
        _document.Add(arc);
        CompleteDrawingCommandCore(DrawingCommandKind.Arc);
        return true;
    }

    private void CompleteDrawingCommandCore(DrawingCommandKind kind)
    {
        _drawingCommand = null;
        _inputPoints.Clear();
        _activeSnap = null;
        Canvas.Invalidate();
        DrawingCommandCompleted?.Invoke(kind);
    }

    private CadPoint ResolveCurrentDrawingPoint()
    {
        var raw = RawPointerWorldPosition;
        if (_drawingCommand is null)
        {
            return raw;
        }
        if (_activeSnap is not null)
        {
            return _activeSnap.Point;
        }
        if (_interaction.OrthoEnabled &&
            _drawingCommand is DrawingCommandKind.Line or DrawingCommandKind.Polyline &&
            _inputPoints.Count > 0)
        {
            return OrthoConstraint.Apply(_inputPoints[^1], raw);
        }
        return raw;
    }

    private void UpdatePointerInteraction()
    {
        if (_drawingCommand is not null && _interaction.ObjectSnapEnabled)
        {
            _activeSnap = ObjectSnapResolver.Resolve(
                _document.Entities,
                RawPointerWorldPosition,
                ObjectSnapAperturePixels / _zoom,
                _interaction.ObjectSnapModes);
        }
        else
        {
            _activeSnap = null;
        }
    }

    private void Canvas_Draw(CanvasControl sender, CanvasDrawEventArgs args)
    {
        var ds = args.DrawingSession;
        ds.Clear(_canvasBackground);
        if (_showGrid) DrawGrid(ds, sender.ActualWidth, sender.ActualHeight);

        foreach (var entity in _document.Entities)
        {
            if (_interaction.Selection.Contains(entity.Id))
            {
                DrawEntity(ds, entity, _selectedColor, 2.0f);
            }
            else if (_hoverEntityId == entity.Id)
            {
                DrawEntity(ds, entity, _hoverColor, 1.6f);
            }
            else
            {
                DrawEntity(ds, entity, _geometryColor, 1.2f);
            }
        }

        DrawSelectionGrips(ds);
        DrawTransientGeometry(ds);
        DrawSelectionWindow(ds);
        DrawSnapMarker(ds);
        DrawCrosshair(ds, sender.ActualWidth, sender.ActualHeight);
    }

    private void DrawEntity(CanvasDrawingSession ds, ICadEntity entity, Color color, float strokeWidth)
    {
        switch (entity)
        {
            case LineEntity line: ds.DrawLine(WorldToScreen(line.Start), WorldToScreen(line.End), color, strokeWidth); break;
            case PolylineEntity polyline: DrawPointChain(ds, polyline.Points, polyline.Closed, color, strokeWidth); break;
            case CircleEntity circle: ds.DrawCircle(WorldToScreen(circle.Center), (float)(circle.Radius * _zoom), color, strokeWidth); break;
            case ArcEntity arc: DrawPointChain(ds, arc.SamplePoints(), false, color, strokeWidth); break;
        }
    }

    private void DrawSelectionGrips(CanvasDrawingSession ds)
    {
        foreach (var entity in _interaction.Selection.SelectedEntities)
        {
            foreach (var grip in CadEntityGeometry.GetGripPoints(entity))
            {
                var screen = WorldToScreen(grip);
                ds.FillRectangle(screen.X - 2.5f, screen.Y - 2.5f, 5, 5, _selectedColor);
            }
        }
    }

    private void DrawTransientGeometry(CanvasDrawingSession ds)
    {
        if (_drawingCommand is not DrawingCommandKind kind) return;
        var pointer = CurrentPointerWorldPosition;
        var pointerScreen = WorldToScreen(pointer);
        switch (kind)
        {
            case DrawingCommandKind.Line when _inputPoints.Count > 0:
                ds.DrawLine(WorldToScreen(_inputPoints[^1]), pointerScreen, _transientColor, 1.0f);
                break;
            case DrawingCommandKind.Polyline when _inputPoints.Count > 0:
                DrawPointChain(ds, _inputPoints, false, _transientColor, 1.0f);
                ds.DrawLine(WorldToScreen(_inputPoints[^1]), pointerScreen, _transientColor, 1.0f);
                break;
            case DrawingCommandKind.Rectangle when _inputPoints.Count == 1:
                var first = _inputPoints[0];
                var corners = new[] { first, new CadPoint(pointer.X, first.Y), pointer, new CadPoint(first.X, pointer.Y) };
                DrawPointChain(ds, corners, true, _transientColor, 1.0f);
                break;
            case DrawingCommandKind.Circle when _inputPoints.Count == 1:
                var radius = (pointer - _inputPoints[0]).Length;
                if (radius > GeometryEpsilon) ds.DrawCircle(WorldToScreen(_inputPoints[0]), (float)(radius * _zoom), _transientColor, 1.0f);
                break;
            case DrawingCommandKind.Arc when _inputPoints.Count == 1:
                ds.DrawLine(WorldToScreen(_inputPoints[0]), pointerScreen, _transientColor, 1.0f);
                break;
            case DrawingCommandKind.Arc when _inputPoints.Count == 2:
                if (ArcEntity.TryCreateFromThreePoints(_inputPoints[0], _inputPoints[1], pointer, out var previewArc) && previewArc is not null)
                    DrawPointChain(ds, previewArc.SamplePoints(), false, _transientColor, 1.0f);
                else
                {
                    DrawPointChain(ds, _inputPoints, false, _transientColor, 1.0f);
                    ds.DrawLine(WorldToScreen(_inputPoints[^1]), pointerScreen, _transientColor, 1.0f);
                }
                break;
        }
    }

    private void DrawSelectionWindow(CanvasDrawingSession ds)
    {
        if (!_selectionDragging) return;
        var left = Math.Min(_selectionStartScreen.X, _pointerScreen.X);
        var top = Math.Min(_selectionStartScreen.Y, _pointerScreen.Y);
        var width = Math.Abs(_pointerScreen.X - _selectionStartScreen.X);
        var height = Math.Abs(_pointerScreen.Y - _selectionStartScreen.Y);
        var crossing = _pointerScreen.X < _selectionStartScreen.X;
        var outline = crossing
            ? ColorHelper.FromArgb(230, 70, 190, 110)
            : ColorHelper.FromArgb(230, 70, 145, 230);
        var fill = crossing
            ? ColorHelper.FromArgb(38, 70, 190, 110)
            : ColorHelper.FromArgb(38, 70, 145, 230);
        ds.FillRectangle(left, top, width, height, fill);
        ds.DrawRectangle(left, top, width, height, outline, 1.0f);
    }

    private void DrawSnapMarker(CanvasDrawingSession ds)
    {
        if (_activeSnap is null || _drawingCommand is null) return;
        var screen = WorldToScreen(_activeSnap.Point);
        const float size = 5f;
        switch (_activeSnap.Kind)
        {
            case ObjectSnapKind.Endpoint:
                ds.DrawRectangle(screen.X - size, screen.Y - size, size * 2, size * 2, _snapColor, 1.4f);
                break;
            case ObjectSnapKind.Midpoint:
                ds.DrawLine(screen.X, screen.Y - size, screen.X + size, screen.Y + size, _snapColor, 1.4f);
                ds.DrawLine(screen.X + size, screen.Y + size, screen.X - size, screen.Y + size, _snapColor, 1.4f);
                ds.DrawLine(screen.X - size, screen.Y + size, screen.X, screen.Y - size, _snapColor, 1.4f);
                break;
            case ObjectSnapKind.Intersection:
                ds.DrawLine(screen.X - size, screen.Y - size, screen.X + size, screen.Y + size, _snapColor, 1.4f);
                ds.DrawLine(screen.X + size, screen.Y - size, screen.X - size, screen.Y + size, _snapColor, 1.4f);
                break;
            case ObjectSnapKind.Center:
                ds.DrawCircle(screen, size, _snapColor, 1.4f);
                break;
        }
    }

    private void DrawPointChain(CanvasDrawingSession ds, IReadOnlyList<CadPoint> points, bool closed, Color color, float strokeWidth)
    {
        for (var i = 1; i < points.Count; i++) ds.DrawLine(WorldToScreen(points[i - 1]), WorldToScreen(points[i]), color, strokeWidth);
        if (closed && points.Count > 2) ds.DrawLine(WorldToScreen(points[^1]), WorldToScreen(points[0]), color, strokeWidth);
    }

    private void DrawGrid(CanvasDrawingSession ds, double width, double height)
    {
        var worldSpacing = 50.0;
        while ((worldSpacing * _zoom) < 24) worldSpacing *= 2;
        while ((worldSpacing * _zoom) > 120) worldSpacing /= 2;
        var topLeft = ScreenToWorld(Vector2.Zero);
        var bottomRight = ScreenToWorld(new Vector2((float)width, (float)height));
        var startX = Math.Floor(topLeft.X / worldSpacing) * worldSpacing;
        var endX = Math.Ceiling(bottomRight.X / worldSpacing) * worldSpacing;
        var startY = Math.Floor(bottomRight.Y / worldSpacing) * worldSpacing;
        var endY = Math.Ceiling(topLeft.Y / worldSpacing) * worldSpacing;
        var gridColor = ColorHelper.FromArgb(_gridAlpha, _gridBaseColor.R, _gridBaseColor.G, _gridBaseColor.B);
        for (var x = startX; x <= endX; x += worldSpacing)
        {
            var sx = WorldToScreen(new CadPoint(x, 0)).X;
            ds.DrawLine(sx, 0, sx, (float)height, gridColor, 1);
        }
        for (var y = startY; y <= endY; y += worldSpacing)
        {
            var sy = WorldToScreen(new CadPoint(0, y)).Y;
            ds.DrawLine(0, sy, (float)width, sy, gridColor, 1);
        }
        var origin = WorldToScreen(new CadPoint(0, 0));
        ds.DrawLine(0, origin.Y, (float)width, origin.Y, _originColor, 1);
        ds.DrawLine(origin.X, 0, origin.X, (float)height, _originColor, 1);
    }

    private void DrawCrosshair(CanvasDrawingSession ds, double width, double height)
    {
        ds.DrawLine(0, _pointerScreen.Y, (float)width, _pointerScreen.Y, _crosshairColor, 0.7f);
        ds.DrawLine(_pointerScreen.X, 0, _pointerScreen.X, (float)height, _crosshairColor, 0.7f);
    }

    private Vector2 WorldToScreen(CadPoint point) => new((float)(point.X * _zoom) + _pan.X, (float)(-point.Y * _zoom) + _pan.Y);
    private CadPoint ScreenToWorld(Vector2 point) => new((point.X - _pan.X) / _zoom, -((point.Y - _pan.Y) / _zoom));

    private void Canvas_PointerMoved(object sender, PointerRoutedEventArgs e)
    {
        var point = e.GetCurrentPoint(Canvas);
        _pointerScreen = new Vector2((float)point.Position.X, (float)point.Position.Y);
        if (_isPanning)
        {
            var delta = _pointerScreen - _lastPanPointer;
            _pan += delta;
            _lastPanPointer = _pointerScreen;
            UpdatePointerInteraction();
        }
        else if (_selectionPointerDown && !_selectionDragging &&
                 Vector2.Distance(_selectionStartScreen, _pointerScreen) >= SelectionDragThresholdPixels)
        {
            _selectionDragging = true;
        }

        if (_drawingCommand is not null)
        {
            _hoverEntityId = null;
            UpdatePointerInteraction();
        }
        else if (!_selectionPointerDown && _selectionPreview)
        {
            _hoverEntityId = CadSelectionQuery.HitTestNearest(
                _document.Entities,
                RawPointerWorldPosition,
                ClickSelectionAperturePixels / _zoom)?.Id;
        }

        PointerWorldPositionChanged?.Invoke(CurrentPointerWorldPosition);
        Canvas.Invalidate();
    }

    private void Canvas_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        var point = e.GetCurrentPoint(Canvas);
        _pointerScreen = new Vector2((float)point.Position.X, (float)point.Position.Y);
        if (_middleMousePan && point.Properties.IsMiddleButtonPressed)
        {
            _isPanning = true;
            _lastPanPointer = _pointerScreen;
            Canvas.CapturePointer(e.Pointer);
            return;
        }

        if (!point.Properties.IsLeftButtonPressed)
        {
            return;
        }

        if (_drawingCommand is not null)
        {
            UpdatePointerInteraction();
            SubmitDrawingPoint(CurrentPointerWorldPosition);
            e.Handled = true;
            return;
        }

        _selectionPointerDown = true;
        _selectionDragging = false;
        _selectionStartScreen = _pointerScreen;
        _selectionStartWorld = RawPointerWorldPosition;
        _hoverEntityId = null;
        Canvas.CapturePointer(e.Pointer);
        e.Handled = true;
    }

    private void Canvas_PointerReleased(object sender, PointerRoutedEventArgs e)
    {
        if (_isPanning)
        {
            _isPanning = false;
            Canvas.ReleasePointerCapture(e.Pointer);
            return;
        }

        if (!_selectionPointerDown)
        {
            return;
        }

        var point = e.GetCurrentPoint(Canvas);
        _pointerScreen = new Vector2((float)point.Position.X, (float)point.Position.Y);
        if (_selectionDragging)
        {
            var rectangle = CadRect.FromPoints(_selectionStartWorld, RawPointerWorldPosition);
            var crossing = _pointerScreen.X < _selectionStartScreen.X;
            _interaction.Selection.Replace(CadSelectionQuery.QueryWindow(_document.Entities, rectangle, crossing));
        }
        else
        {
            var hit = CadSelectionQuery.HitTestNearest(
                _document.Entities,
                RawPointerWorldPosition,
                ClickSelectionAperturePixels / _zoom);
            if (hit is null)
            {
                _interaction.Selection.Clear();
            }
            else
            {
                _interaction.Selection.Replace(hit.Id);
            }
        }

        _selectionPointerDown = false;
        _selectionDragging = false;
        Canvas.ReleasePointerCapture(e.Pointer);
        Canvas.Invalidate();
        e.Handled = true;
    }

    private void Canvas_PointerCaptureLost(object sender, PointerRoutedEventArgs e)
    {
        _isPanning = false;
        _selectionPointerDown = false;
        _selectionDragging = false;
        Canvas.Invalidate();
    }

    private void Canvas_PointerExited(object sender, PointerRoutedEventArgs e)
    {
        if (!_selectionPointerDown && !_isPanning)
        {
            _hoverEntityId = null;
            _activeSnap = null;
            Canvas.Invalidate();
        }
    }

    private void Canvas_PointerWheelChanged(object sender, PointerRoutedEventArgs e)
    {
        var point = e.GetCurrentPoint(Canvas);
        var screen = new Vector2((float)point.Position.X, (float)point.Position.Y);
        var direction = Math.Sign(point.Properties.MouseWheelDelta);
        if (_reverseWheelZoom) direction *= -1;
        var factor = direction > 0 ? 1.12 : 1.0 / 1.12;
        if (_zoomAroundCursor)
        {
            var worldBefore = ScreenToWorld(screen);
            _zoom = Math.Clamp(_zoom * factor, 0.01, 1000.0);
            var screenAfter = WorldToScreen(worldBefore);
            _pan += screen - screenAfter;
        }
        else
        {
            _zoom = Math.Clamp(_zoom * factor, 0.01, 1000.0);
        }
        UpdatePointerInteraction();
        ZoomChanged?.Invoke(_zoom);
        Canvas.Invalidate();
        e.Handled = true;
    }
}

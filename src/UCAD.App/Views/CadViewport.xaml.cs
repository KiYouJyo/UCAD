using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.UI.Xaml;
using Microsoft.UI;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using System.Numerics;
using UCAD.Core;
using UCAD.Core.Commands;
using UCAD.Core.Entities;
using UCAD.Core.Geometry;
using Windows.UI;

namespace UCAD.Views;

public sealed partial class CadViewport : UserControl
{
    private const double GeometryEpsilon = 1e-9;
    private readonly CadDocument _document;
    private readonly List<CadPoint> _inputPoints = [];
    private double _zoom = 1.0;
    private Vector2 _pan = new(120, 120);
    private Vector2 _pointerScreen;
    private bool _isPanning;
    private Vector2 _lastPanPointer;
    private DrawingCommandKind? _drawingCommand;

    public event Action<CadPoint>? PointerWorldPositionChanged;
    public event Action<DrawingCommandKind, int, CadPoint>? DrawingPointAccepted;
    public event Action<DrawingCommandKind>? DrawingCommandCompleted;
    public event Action<double>? ZoomChanged;

    public CadDocument Document => _document;

    public CadPoint CurrentPointerWorldPosition => ScreenToWorld(_pointerScreen);

    public double Zoom => _zoom;

    public CadViewport()
        : this(new CadDocument())
    {
    }

    public CadViewport(CadDocument document)
    {
        _document = document ?? throw new ArgumentNullException(nameof(document));
        InitializeComponent();
    }

    public void BeginDrawingCommand(DrawingCommandKind kind)
    {
        _drawingCommand = kind;
        _inputPoints.Clear();
        Canvas.Invalidate();
    }

    public bool SubmitDrawingPoint(CadPoint world)
    {
        if (_drawingCommand is not DrawingCommandKind kind)
        {
            return false;
        }

        switch (kind)
        {
            case DrawingCommandKind.Line:
                return SubmitLinePoint(world);
            case DrawingCommandKind.Polyline:
                _inputPoints.Add(world);
                DrawingPointAccepted?.Invoke(kind, _inputPoints.Count, world);
                Canvas.Invalidate();
                return true;
            case DrawingCommandKind.Rectangle:
                return SubmitRectanglePoint(world);
            case DrawingCommandKind.Circle:
                return SubmitCirclePoint(world);
            case DrawingCommandKind.Arc:
                return SubmitArcPoint(world);
            default:
                return false;
        }
    }

    public void CompleteDrawingCommand()
    {
        if (_drawingCommand is not DrawingCommandKind kind)
        {
            return;
        }

        if (kind == DrawingCommandKind.Polyline && _inputPoints.Count >= 2)
        {
            _document.Add(new PolylineEntity(_inputPoints));
        }

        CompleteDrawingCommandCore(kind);
    }

    public void CancelDrawingCommand()
    {
        _drawingCommand = null;
        _inputPoints.Clear();
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
        _document.Clear();
        Canvas.Invalidate();
    }

    public void ResetView()
    {
        _zoom = 1.0;
        _pan = new Vector2(120, 120);
        ZoomChanged?.Invoke(_zoom);
        Canvas.Invalidate();
    }

    private bool SubmitLinePoint(CadPoint world)
    {
        if (_inputPoints.Count > 0)
        {
            var start = _inputPoints[^1];
            if ((world - start).Length < GeometryEpsilon)
            {
                return false;
            }

            _document.Add(new LineEntity(start, world));
        }

        _inputPoints.Add(world);
        DrawingPointAccepted?.Invoke(DrawingCommandKind.Line, _inputPoints.Count, world);
        Canvas.Invalidate();
        return true;
    }

    private bool SubmitRectanglePoint(CadPoint world)
    {
        if (_inputPoints.Count == 0)
        {
            _inputPoints.Add(world);
            DrawingPointAccepted?.Invoke(DrawingCommandKind.Rectangle, 1, world);
            Canvas.Invalidate();
            return true;
        }

        var first = _inputPoints[0];
        if (Math.Abs(first.X - world.X) < GeometryEpsilon || Math.Abs(first.Y - world.Y) < GeometryEpsilon)
        {
            return false;
        }

        var corners = new[]
        {
            first,
            new CadPoint(world.X, first.Y),
            world,
            new CadPoint(first.X, world.Y)
        };
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
            Canvas.Invalidate();
            return true;
        }

        var center = _inputPoints[0];
        var radius = (world - center).Length;
        if (radius < GeometryEpsilon)
        {
            return false;
        }

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
            if (_inputPoints.Count == 1 && (world - _inputPoints[0]).Length < GeometryEpsilon)
            {
                return false;
            }

            _inputPoints.Add(world);
            DrawingPointAccepted?.Invoke(DrawingCommandKind.Arc, _inputPoints.Count, world);
            Canvas.Invalidate();
            return true;
        }

        if (!ArcEntity.TryCreateFromThreePoints(_inputPoints[0], _inputPoints[1], world, out var arc) || arc is null)
        {
            return false;
        }

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
        Canvas.Invalidate();
        DrawingCommandCompleted?.Invoke(kind);
    }

    private void Canvas_Draw(CanvasControl sender, CanvasDrawEventArgs args)
    {
        var ds = args.DrawingSession;
        ds.Clear(Colors.Black);
        DrawGrid(ds, sender.ActualWidth, sender.ActualHeight);

        foreach (var entity in _document.Entities)
        {
            DrawEntity(ds, entity, Colors.White, 1.2f);
        }

        DrawTransientGeometry(ds);
        DrawCrosshair(ds, sender.ActualWidth, sender.ActualHeight);
    }

    private void DrawEntity(CanvasDrawingSession ds, ICadEntity entity, Color color, float strokeWidth)
    {
        switch (entity)
        {
            case LineEntity line:
                ds.DrawLine(WorldToScreen(line.Start), WorldToScreen(line.End), color, strokeWidth);
                break;
            case PolylineEntity polyline:
                DrawPointChain(ds, polyline.Points, polyline.Closed, color, strokeWidth);
                break;
            case CircleEntity circle:
                ds.DrawCircle(WorldToScreen(circle.Center), (float)(circle.Radius * _zoom), color, strokeWidth);
                break;
            case ArcEntity arc:
                DrawPointChain(ds, arc.SamplePoints(), false, color, strokeWidth);
                break;
        }
    }

    private void DrawTransientGeometry(CanvasDrawingSession ds)
    {
        if (_drawingCommand is not DrawingCommandKind kind)
        {
            return;
        }

        var pointer = CurrentPointerWorldPosition;
        switch (kind)
        {
            case DrawingCommandKind.Line when _inputPoints.Count > 0:
                ds.DrawLine(WorldToScreen(_inputPoints[^1]), _pointerScreen, Colors.DeepSkyBlue, 1.0f);
                break;
            case DrawingCommandKind.Polyline when _inputPoints.Count > 0:
                DrawPointChain(ds, _inputPoints, false, Colors.DeepSkyBlue, 1.0f);
                ds.DrawLine(WorldToScreen(_inputPoints[^1]), _pointerScreen, Colors.DeepSkyBlue, 1.0f);
                break;
            case DrawingCommandKind.Rectangle when _inputPoints.Count == 1:
                var first = _inputPoints[0];
                var corners = new[]
                {
                    first,
                    new CadPoint(pointer.X, first.Y),
                    pointer,
                    new CadPoint(first.X, pointer.Y)
                };
                DrawPointChain(ds, corners, true, Colors.DeepSkyBlue, 1.0f);
                break;
            case DrawingCommandKind.Circle when _inputPoints.Count == 1:
                var radius = (pointer - _inputPoints[0]).Length;
                if (radius > GeometryEpsilon)
                {
                    ds.DrawCircle(WorldToScreen(_inputPoints[0]), (float)(radius * _zoom), Colors.DeepSkyBlue, 1.0f);
                }
                break;
            case DrawingCommandKind.Arc when _inputPoints.Count == 1:
                ds.DrawLine(WorldToScreen(_inputPoints[0]), _pointerScreen, Colors.DeepSkyBlue, 1.0f);
                break;
            case DrawingCommandKind.Arc when _inputPoints.Count == 2:
                if (ArcEntity.TryCreateFromThreePoints(_inputPoints[0], _inputPoints[1], pointer, out var previewArc) && previewArc is not null)
                {
                    DrawPointChain(ds, previewArc.SamplePoints(), false, Colors.DeepSkyBlue, 1.0f);
                }
                else
                {
                    DrawPointChain(ds, _inputPoints, false, Colors.DeepSkyBlue, 1.0f);
                    ds.DrawLine(WorldToScreen(_inputPoints[^1]), _pointerScreen, Colors.DeepSkyBlue, 1.0f);
                }
                break;
        }
    }

    private void DrawPointChain(CanvasDrawingSession ds, IReadOnlyList<CadPoint> points, bool closed, Color color, float strokeWidth)
    {
        for (var i = 1; i < points.Count; i++)
        {
            ds.DrawLine(WorldToScreen(points[i - 1]), WorldToScreen(points[i]), color, strokeWidth);
        }

        if (closed && points.Count > 2)
        {
            ds.DrawLine(WorldToScreen(points[^1]), WorldToScreen(points[0]), color, strokeWidth);
        }
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

        for (var x = startX; x <= endX; x += worldSpacing)
        {
            var sx = WorldToScreen(new CadPoint(x, 0)).X;
            ds.DrawLine(sx, 0, sx, (float)height, ColorHelper.FromArgb(255, 38, 38, 38), 1);
        }

        for (var y = startY; y <= endY; y += worldSpacing)
        {
            var sy = WorldToScreen(new CadPoint(0, y)).Y;
            ds.DrawLine(0, sy, (float)width, sy, ColorHelper.FromArgb(255, 38, 38, 38), 1);
        }

        var origin = WorldToScreen(new CadPoint(0, 0));
        ds.DrawLine(0, origin.Y, (float)width, origin.Y, ColorHelper.FromArgb(255, 70, 70, 70), 1);
        ds.DrawLine(origin.X, 0, origin.X, (float)height, ColorHelper.FromArgb(255, 70, 70, 70), 1);
    }

    private void DrawCrosshair(CanvasDrawingSession ds, double width, double height)
    {
        ds.DrawLine(0, _pointerScreen.Y, (float)width, _pointerScreen.Y, ColorHelper.FromArgb(110, 180, 180, 180), 0.7f);
        ds.DrawLine(_pointerScreen.X, 0, _pointerScreen.X, (float)height, ColorHelper.FromArgb(110, 180, 180, 180), 0.7f);
    }

    private Vector2 WorldToScreen(CadPoint point) =>
        new((float)(point.X * _zoom) + _pan.X, (float)(-point.Y * _zoom) + _pan.Y);

    private CadPoint ScreenToWorld(Vector2 point) =>
        new((point.X - _pan.X) / _zoom, -((point.Y - _pan.Y) / _zoom));

    private void Canvas_PointerMoved(object sender, PointerRoutedEventArgs e)
    {
        var point = e.GetCurrentPoint(Canvas);
        _pointerScreen = new Vector2((float)point.Position.X, (float)point.Position.Y);

        if (_isPanning)
        {
            var delta = _pointerScreen - _lastPanPointer;
            _pan += delta;
            _lastPanPointer = _pointerScreen;
        }

        PointerWorldPositionChanged?.Invoke(CurrentPointerWorldPosition);
        Canvas.Invalidate();
    }

    private void Canvas_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        var point = e.GetCurrentPoint(Canvas);
        _pointerScreen = new Vector2((float)point.Position.X, (float)point.Position.Y);

        if (point.Properties.IsMiddleButtonPressed)
        {
            _isPanning = true;
            _lastPanPointer = _pointerScreen;
            Canvas.CapturePointer(e.Pointer);
            return;
        }

        if (point.Properties.IsLeftButtonPressed && _drawingCommand is not null)
        {
            SubmitDrawingPoint(CurrentPointerWorldPosition);
        }
    }

    private void Canvas_PointerReleased(object sender, PointerRoutedEventArgs e)
    {
        if (_isPanning)
        {
            _isPanning = false;
            Canvas.ReleasePointerCapture(e.Pointer);
        }
    }

    private void Canvas_PointerWheelChanged(object sender, PointerRoutedEventArgs e)
    {
        var point = e.GetCurrentPoint(Canvas);
        var screen = new Vector2((float)point.Position.X, (float)point.Position.Y);
        var worldBefore = ScreenToWorld(screen);
        var factor = point.Properties.MouseWheelDelta > 0 ? 1.12 : 1.0 / 1.12;
        _zoom = Math.Clamp(_zoom * factor, 0.01, 1000.0);
        var screenAfter = WorldToScreen(worldBefore);
        _pan += screen - screenAfter;
        ZoomChanged?.Invoke(_zoom);
        Canvas.Invalidate();
        e.Handled = true;
    }
}

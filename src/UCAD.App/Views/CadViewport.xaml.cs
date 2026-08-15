using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.UI.Xaml;
using Microsoft.UI;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using System.Numerics;
using UCAD.Core;
using UCAD.Core.Entities;
using UCAD.Core.Geometry;

namespace UCAD.Views;

public sealed partial class CadViewport : UserControl
{
    private readonly CadDocument _document = new();
    private double _zoom = 1.0;
    private Vector2 _pan = new(120, 120);
    private Vector2 _pointerScreen;
    private bool _isPanning;
    private Vector2 _lastPanPointer;
    private bool _lineMode;
    private CadPoint? _lineStart;

    public event Action<CadPoint>? PointerWorldPositionChanged;
    public event Action<CadPoint>? LinePointAccepted;
    public event Action<bool>? LineModeChanged;

    public CadPoint CurrentPointerWorldPosition => ScreenToWorld(_pointerScreen);

    public CadViewport()
    {
        InitializeComponent();
        _document.Add(new LineEntity(new CadPoint(0, 0), new CadPoint(500, 0)));
        _document.Add(new LineEntity(new CadPoint(0, 0), new CadPoint(0, 300)));
    }

    public void BeginLineCommand()
    {
        _lineMode = true;
        _lineStart = null;
        LineModeChanged?.Invoke(true);
        Canvas.Invalidate();
    }

    public void ToggleLineMode()
    {
        if (_lineMode)
        {
            CompleteLineCommand();
        }
        else
        {
            BeginLineCommand();
        }
    }

    public void CompleteLineCommand()
    {
        _lineMode = false;
        _lineStart = null;
        LineModeChanged?.Invoke(false);
        Canvas.Invalidate();
    }

    public void CancelLineCommand()
    {
        _lineMode = false;
        _lineStart = null;
        LineModeChanged?.Invoke(false);
        Canvas.Invalidate();
    }

    public void SubmitLinePoint(CadPoint world)
    {
        if (!_lineMode)
        {
            return;
        }

        if (_lineStart is null)
        {
            _lineStart = world;
        }
        else
        {
            _document.Add(new LineEntity(_lineStart.Value, world));
            _lineStart = world;
        }

        LinePointAccepted?.Invoke(world);
        Canvas.Invalidate();
    }

    public void ClearDocument()
    {
        _document.Clear();
        _lineStart = null;
        Canvas.Invalidate();
    }

    public void ResetView()
    {
        _zoom = 1.0;
        _pan = new Vector2(120, 120);
        Canvas.Invalidate();
    }

    private void Canvas_Draw(CanvasControl sender, CanvasDrawEventArgs args)
    {
        var ds = args.DrawingSession;
        ds.Clear(Colors.Black);
        DrawGrid(ds, sender.ActualWidth, sender.ActualHeight);

        foreach (var entity in _document.Entities)
        {
            if (entity is LineEntity line)
            {
                ds.DrawLine(WorldToScreen(line.Start), WorldToScreen(line.End), Colors.White, 1.2f);
            }
        }

        if (_lineMode && _lineStart is CadPoint start)
        {
            ds.DrawLine(WorldToScreen(start), _pointerScreen, Colors.DeepSkyBlue, 1.0f);
        }

        DrawCrosshair(ds, sender.ActualWidth, sender.ActualHeight);
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

        if (point.Properties.IsLeftButtonPressed && _lineMode)
        {
            SubmitLinePoint(CurrentPointerWorldPosition);
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
        Canvas.Invalidate();
        e.Handled = true;
    }
}

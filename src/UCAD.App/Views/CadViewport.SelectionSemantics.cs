using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using System.Numerics;
using System.Runtime.InteropServices;
using UCAD.Core.Geometry;
using UCAD.Core.Interaction;
using Windows.System;

namespace UCAD.Views;

public sealed partial class CadViewport
{
    private static readonly IntPtr ArrowCursorId = new(32512);

    [DllImport("user32.dll")]
    private static extern IntPtr SetCursor(IntPtr hCursor);

    [DllImport("user32.dll")]
    private static extern IntPtr LoadCursor(IntPtr hInstance, IntPtr lpCursorName);

    private void CadViewport_Loaded(object sender, RoutedEventArgs e)
    {
        Loaded -= CadViewport_Loaded;

        // The CAD cursor is rendered entirely by Win2D. Do not layer a Windows
        // arrow/cross on top of the pickbox: that produces the double-cursor
        // appearance seen in v0.4.1 acceptance builds. These handlers run after
        // the normal Canvas pointer handlers and suppress the native cursor while
        // the pointer is inside the drawing surface.
        Canvas.PointerEntered += Canvas_HideNativeCursor;
        Canvas.PointerMoved += Canvas_HideNativeCursor;
        Canvas.PointerPressed += Canvas_HideNativeCursor;
        Canvas.PointerReleased += Canvas_HideNativeCursor;
        Canvas.PointerWheelChanged += Canvas_HideNativeCursor;
        Canvas.PointerExited += Canvas_RestoreNativeCursor;
    }

    private static void Canvas_HideNativeCursor(object sender, PointerRoutedEventArgs e) =>
        _ = SetCursor(IntPtr.Zero);

    private static void Canvas_RestoreNativeCursor(object sender, PointerRoutedEventArgs e)
    {
        var arrow = LoadCursor(IntPtr.Zero, ArrowCursorId);
        if (arrow != IntPtr.Zero)
        {
            _ = SetCursor(arrow);
        }
    }

    private static bool ShiftSelection(PointerRoutedEventArgs e) =>
        (e.KeyModifiers & VirtualKeyModifiers.Shift) != 0;

    private void ArmTwoClickSelectionWindow(Vector2 screen, CadPoint world)
    {
        _selectionStartScreen = screen;
        _selectionStartWorld = world;
        _selectionWindowArmed = true;
        _selectionPointerDown = false;
        _selectionDragging = false;
        _hoverEntityId = null;
        Canvas.Invalidate();
    }

    private void CommitSelectionWindow(CadPoint endWorld, bool remove)
    {
        var startScreen = WorldToScreen(_selectionStartWorld);
        var rectangle = CadRect.FromPoints(_selectionStartWorld, endWorld);
        var crossing = _pointerScreen.X < startScreen.X;
        var ids = CadSelectionQuery.QueryWindow(_document.Entities, rectangle, crossing);

        if (remove)
        {
            _interaction.Selection.Remove(ids);
        }
        else if (ids.Count == 0)
        {
            // An empty completed window is the CAD-style quick way to clear a
            // selection set. The first blank click still only starts the window.
            _interaction.Selection.Clear();
        }
        else
        {
            _interaction.Selection.Add(ids);
        }

        _selectionWindowArmed = false;
        _selectionPointerDown = false;
        _selectionDragging = false;
        Canvas.Invalidate();
    }

    private void ApplyPointSelection(Guid entityId, bool remove)
    {
        if (remove)
        {
            _interaction.Selection.Remove(entityId);
        }
        else
        {
            // AutoCAD-style PICKADD behavior: each new pick joins the current set.
            _interaction.Selection.Add(entityId);
        }
    }

    public bool CancelSelectionGesture()
    {
        if (!_selectionWindowArmed && !_selectionPointerDown && !_selectionDragging)
        {
            return false;
        }

        _selectionWindowArmed = false;
        _selectionPointerDown = false;
        _selectionDragging = false;
        _hoverEntityId = null;
        Canvas.Invalidate();
        return true;
    }
}

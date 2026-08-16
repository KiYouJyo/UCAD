using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using System.Numerics;
using UCAD.Core.Geometry;
using UCAD.Core.Interaction;
using UCAD.Interop;
using Windows.System;

namespace UCAD.Views;

public sealed partial class CadViewport
{
    private void CadViewport_Loaded(object sender, RoutedEventArgs e)
    {
        // ProtectedCursor is the Windows App SDK cursor path for XAML elements.
        // Give WinUI a real but fully transparent InputCursor, then let Win2D draw
        // the visible CAD crosshair/pickbox. This avoids the race where SetCursor(NULL)
        // is immediately overwritten again by WinUI/DefWindowProc on mouse movement.
        try
        {
            ProtectedCursor = TransparentInputCursor.GetOrCreate();
        }
        catch (Exception ex)
        {
            App.WriteStartupFailure("CadViewport.TransparentCursor", ex);
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

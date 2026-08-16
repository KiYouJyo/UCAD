using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using System.Numerics;
using UCAD.Core.Interaction;

namespace UCAD.Views;

public sealed partial class CadViewport
{
    private Guid[] _selectionPriorIds = [];
    private Vector2 _selectionSemanticStartScreen;
    private bool _selectionSemanticArmed;

    private void CadViewport_Loaded(object sender, RoutedEventArgs e)
    {
        Loaded -= CadViewport_Loaded;

        // The primary pointer handlers own hit testing, capture and window rendering.
        // These handled-events-too hooks add AutoCAD-style PICKADD semantics without
        // duplicating selection ownership outside the document-scoped SelectionSet.
        Canvas.AddHandler(UIElement.PointerPressedEvent, new PointerEventHandler(SelectionSemantic_PointerPressed), true);
        Canvas.AddHandler(UIElement.PointerReleasedEvent, new PointerEventHandler(SelectionSemantic_PointerReleased), true);
    }

    private void SelectionSemantic_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        var point = e.GetCurrentPoint(Canvas);
        if (_drawingCommand is not null || !point.Properties.IsLeftButtonPressed || point.Properties.IsMiddleButtonPressed)
        {
            _selectionSemanticArmed = false;
            return;
        }

        _selectionPriorIds = _interaction.Selection.SelectedIds.ToArray();
        _selectionSemanticStartScreen = new Vector2((float)point.Position.X, (float)point.Position.Y);
        _selectionSemanticArmed = true;
    }

    private void SelectionSemantic_PointerReleased(object sender, PointerRoutedEventArgs e)
    {
        if (!_selectionSemanticArmed)
        {
            return;
        }
        _selectionSemanticArmed = false;

        var point = e.GetCurrentPoint(Canvas);
        var releaseScreen = new Vector2((float)point.Position.X, (float)point.Position.Y);
        var wasWindow = Vector2.Distance(_selectionSemanticStartScreen, releaseScreen) >= SelectionDragThresholdPixels;
        var clickedEntity = wasWindow
            ? null
            : CadSelectionQuery.HitTestNearest(
                _document.Entities,
                ScreenToWorld(releaseScreen),
                ClickSelectionAperturePixels / _zoom);

        // Run after the primary release handler has committed the click/window result.
        // Blank clicks deliberately keep the primary handler's Clear() behavior.
        if (wasWindow || clickedEntity is not null)
        {
            var previous = _selectionPriorIds;
            DispatcherQueue.TryEnqueue(() =>
            {
                _interaction.Selection.Add(previous);
                Canvas.Invalidate();
            });
        }
    }
}

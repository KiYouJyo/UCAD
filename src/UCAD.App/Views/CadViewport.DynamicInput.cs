using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;

namespace UCAD.Views;

public sealed partial class CadViewport
{
    private bool _dynamicInputInitialized;
    private bool _dynamicPointerInside;
    private string _dynamicPrompt = string.Empty;
    private string _dynamicInput = string.Empty;
    private bool _dynamicCommandActive;

    /// <summary>
    /// Mirrors the command-line state beside the CAD pickbox without creating a second
    /// text editor. Keeping one real TextBox preserves IME, clipboard, history and
    /// accessibility semantics while still providing AutoCAD-style cursor-local feedback.
    /// </summary>
    public void SetDynamicCommandDisplay(string? prompt, string? input, bool commandActive)
    {
        EnsureDynamicInputInitialized();
        _dynamicPrompt = prompt?.Trim() ?? string.Empty;
        _dynamicInput = input ?? string.Empty;
        _dynamicCommandActive = commandActive;
        UpdateDynamicInputOverlay();
    }

    private void EnsureDynamicInputInitialized()
    {
        if (_dynamicInputInitialized) return;
        _dynamicInputInitialized = true;
        Canvas.PointerEntered += DynamicInput_CanvasPointerEntered;
        Canvas.PointerExited += DynamicInput_CanvasPointerExited;
        Canvas.PointerMoved += DynamicInput_CanvasPointerMoved;
        SizeChanged += DynamicInput_SizeChanged;
    }

    private void DynamicInput_CanvasPointerEntered(object sender, PointerRoutedEventArgs e)
    {
        _dynamicPointerInside = true;
        UpdateDynamicInputOverlay();
    }

    private void DynamicInput_CanvasPointerExited(object sender, PointerRoutedEventArgs e)
    {
        _dynamicPointerInside = false;
        DynamicInputBorder.Visibility = Visibility.Collapsed;
    }

    private void DynamicInput_CanvasPointerMoved(object sender, PointerRoutedEventArgs e)
    {
        _dynamicPointerInside = true;
        UpdateDynamicInputOverlay();
    }

    private void DynamicInput_SizeChanged(object sender, SizeChangedEventArgs e) => UpdateDynamicInputOverlay();

    private void UpdateDynamicInputOverlay()
    {
        if (!_dynamicInputInitialized ||
            !_dynamicPointerInside ||
            _isPanning ||
            (!_dynamicCommandActive && string.IsNullOrWhiteSpace(_dynamicInput)))
        {
            DynamicInputBorder.Visibility = Visibility.Collapsed;
            return;
        }

        DynamicPromptText.Text = _dynamicPrompt;
        DynamicInputText.Text = _dynamicInput;
        DynamicInputText.Visibility = string.IsNullOrEmpty(_dynamicInput)
            ? Visibility.Collapsed
            : Visibility.Visible;
        DynamicInputBorder.Visibility = Visibility.Visible;

        const double gap = 12;
        const double estimatedWidth = 300;
        const double estimatedHeight = 36;
        var pickboxOffset = (_pickboxSizePixels / 2.0) + gap;
        var left = _pointerScreen.X + pickboxOffset;
        var top = _pointerScreen.Y + pickboxOffset;

        if (left + estimatedWidth > ActualWidth - 8)
            left = Math.Max(8, _pointerScreen.X - estimatedWidth - pickboxOffset);
        if (top + estimatedHeight > ActualHeight - 8)
            top = Math.Max(8, _pointerScreen.Y - estimatedHeight - pickboxOffset);

        Microsoft.UI.Xaml.Controls.Canvas.SetLeft(DynamicInputBorder, left);
        Microsoft.UI.Xaml.Controls.Canvas.SetTop(DynamicInputBorder, top);
    }
}

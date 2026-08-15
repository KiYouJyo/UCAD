using Microsoft.UI.Xaml;
using Microsoft.Windows.ApplicationModel.Resources;

namespace UCAD;

public sealed partial class MainWindow : Window
{
    private readonly ResourceLoader _resources;

    public MainWindow()
    {
        InitializeComponent();
        _resources = new ResourceLoader();
        Title = GetString("AppWindowTitle");
        ModeText.Text = GetString("Status_Ready");

        Viewport.PointerWorldPositionChanged += point =>
            CoordinateText.Text = $"X {point.X:0.00}  Y {point.Y:0.00}";

        Viewport.LineModeChanged += enabled =>
            ModeText.Text = enabled ? GetString("Status_LineFirst") : GetString("Status_Ready");
    }

    private string GetString(string key)
    {
        var value = _resources.GetString(key);
        return string.IsNullOrWhiteSpace(value) ? key : value;
    }

    private void Line_Click(object sender, RoutedEventArgs e) => Viewport.ToggleLineMode();

    private void Clear_Click(object sender, RoutedEventArgs e)
    {
        Viewport.ClearDocument();
        ModeText.Text = GetString("Status_Cleared");
    }

    private void ResetView_Click(object sender, RoutedEventArgs e)
    {
        Viewport.ResetView();
        ModeText.Text = GetString("Status_ViewReset");
    }
}

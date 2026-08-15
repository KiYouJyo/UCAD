using Microsoft.UI.Xaml;

namespace UCAD;

public sealed partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        Viewport.PointerWorldPositionChanged += point =>
            CoordinateText.Text = $"X {point.X:0.00}  Y {point.Y:0.00}";

        Viewport.LineModeChanged += enabled =>
            ModeText.Text = enabled ? "LINE · Specify first point" : "READY";
    }

    private void Line_Click(object sender, RoutedEventArgs e) => Viewport.ToggleLineMode();

    private void Clear_Click(object sender, RoutedEventArgs e) => Viewport.ClearDocument();

    private void ResetView_Click(object sender, RoutedEventArgs e) => Viewport.ResetView();
}

using Microsoft.UI.Xaml;
using Microsoft.Windows.ApplicationModel.Resources;
using System.Globalization;
using UCAD.Core.Geometry;

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
        Viewport.CursorWorldChanged += OnCursorWorldChanged;
        Viewport.LineDrawingStateChanged += OnLineDrawingStateChanged;
    }

    private string GetString(string key)
    {
        var value = _resources.GetString(key);
        return string.IsNullOrWhiteSpace(value) ? key : value;
    }

    private void Line_Click(object sender, RoutedEventArgs e)
    {
        Viewport.BeginLineCommand();
    }

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

    private void OnCursorWorldChanged(CadPoint point)
    {
        CoordinateText.Text = string.Create(
            CultureInfo.InvariantCulture,
            $"X: {point.X:0.000}   Y: {point.Y:0.000}");
    }

    private void OnLineDrawingStateChanged(LineDrawingState state)
    {
        ModeText.Text = state switch
        {
            LineDrawingState.WaitingForFirstPoint => GetString("Status_LineFirst"),
            LineDrawingState.WaitingForSecondPoint => GetString("Status_LineSecond"),
            _ => GetString("Status_Ready")
        };
    }
}

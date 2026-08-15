using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace UCAD.Views;

public sealed partial class StartPage : UserControl
{
    public StartPage()
    {
        InitializeComponent();
    }

    public event EventHandler? NewDrawingRequested;

    public event EventHandler? OpenDrawingRequested;

    public event EventHandler<string>? LearnRequested;

    private void NewDrawingButton_Click(object sender, RoutedEventArgs e) =>
        NewDrawingRequested?.Invoke(this, EventArgs.Empty);

    private void OpenDrawingButton_Click(object sender, RoutedEventArgs e) =>
        OpenDrawingRequested?.Invoke(this, EventArgs.Empty);

    private void LearnButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: string target })
        {
            LearnRequested?.Invoke(this, target);
        }
    }
}

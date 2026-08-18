using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using UCAD.Services;

namespace UCAD.Views;

public sealed partial class StartPage : UserControl
{
    private readonly LocalizationService _localization = LocalizationService.Current;
    private Border? _recentHost;
    private UIElement? _recentEmptyContent;

    public StartPage()
    {
        InitializeComponent();
        CaptureRecentHost();
        RefreshLocalization();
    }

    public event EventHandler? NewDrawingRequested;
    public event EventHandler? OpenDrawingRequested;
    public event EventHandler<string>? RecentFileRequested;
    public event EventHandler<string>? LearnRequested;

    /// <summary>
    /// Optional v0.8 file action supplied by the shell. When present the Start page
    /// executes the real file picker instead of raising the legacy unavailable event.
    /// </summary>
    public Func<Task>? OpenDrawingAction { get; set; }

    public void RefreshLocalization()
    {
        StartTitleText.Text = Get("Start_Title.Text");
        StartSubtitleText.Text = Get("Start_Subtitle.Text");
        NewDrawingTitleText.Text = Get("Start_NewDrawing_Title.Text");
        NewDrawingDescriptionText.Text = Get("Start_NewDrawing_Description.Text");
        OpenDrawingTitleText.Text = Get("Start_OpenDrawing_Title.Text");
        OpenDrawingDescriptionText.Text = Get("Start_OpenDrawing_Description.Text");
        RecentTitleText.Text = Get("Start_Recent_Title.Text");
        RecentShowAllButton.Content = Get("Start_Recent_ShowAll.Content");
        RecentEmptyTitleText.Text = Get("Start_Recent_EmptyTitle.Text");
        RecentEmptyDescriptionText.Text = Get("Start_Recent_EmptyDescription.Text");
        TemplatesTitleText.Text = Get("Start_Templates_Title.Text");
        BlankTitleText.Text = Get("Start_Template_Blank_Title.Text");
        BlankDescriptionText.Text = Get("Start_Template_Blank_Description.Text");
        ArchitectureTitleText.Text = Get("Start_Template_Architecture_Title.Text");
        ArchitectureDescriptionText.Text = Get("Start_Template_Architecture_Description.Text");
        ArchitectureComingSoonText.Text = Get("Common_ComingSoon.Text");
        UrbanTitleText.Text = Get("Start_Template_Urban_Title.Text");
        UrbanDescriptionText.Text = Get("Start_Template_Urban_Description.Text");
        UrbanComingSoonText.Text = Get("Common_ComingSoon.Text");
        LearnTitleText.Text = Get("Start_Learn_Title.Text");
        ShortcutsText.Text = Get("Start_Learn_Shortcuts.Text");
        BasicDrawingText.Text = Get("Start_Learn_BasicDrawing.Text");
        CommandsText.Text = Get("Start_Learn_Commands.Text");
        DocsText.Text = Get("Start_Learn_Docs.Text");

        var language = _localization.CurrentLanguageTag;
        if (!string.IsNullOrWhiteSpace(language)) Language = language;
    }

    public void SetRecentFiles(IReadOnlyList<RecentFileEntry> files)
    {
        if (_recentHost is null) return;
        if (files.Count == 0)
        {
            if (_recentEmptyContent is not null) _recentHost.Child = _recentEmptyContent;
            _recentHost.Height = 58;
            return;
        }

        var list = new StackPanel { Spacing = 0, Padding = new Thickness(6) };
        foreach (var entry in files.Take(6))
        {
            var button = new Button
            {
                Tag = entry.Path,
                Height = 48,
                Padding = new Thickness(10, 0, 10, 0),
                HorizontalContentAlignment = HorizontalAlignment.Stretch,
                Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent),
                BorderThickness = new Thickness(0)
            };
            var grid = new Grid { ColumnSpacing = 10 };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(24) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.Children.Add(new FontIcon
            {
                FontFamily = new FontFamily("Segoe Fluent Icons"),
                Glyph = "\uE8A5",
                FontSize = 15,
                Foreground = (Brush)Application.Current.Resources["UcadTextSecondaryBrush"]
            });
            var text = new StackPanel { Spacing = 1, VerticalAlignment = VerticalAlignment.Center };
            text.Children.Add(new TextBlock
            {
                Text = entry.DisplayName,
                FontSize = 10,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                Foreground = (Brush)Application.Current.Resources["UcadTextPrimaryBrush"]
            });
            text.Children.Add(new TextBlock
            {
                Text = entry.Path,
                FontSize = 8,
                TextTrimming = TextTrimming.CharacterEllipsis,
                Foreground = (Brush)Application.Current.Resources["UcadTextSecondaryBrush"]
            });
            Grid.SetColumn(text, 1);
            grid.Children.Add(text);
            button.Content = grid;
            button.Click += RecentFileButton_Click;
            list.Children.Add(button);
        }

        _recentHost.Height = double.NaN;
        _recentHost.MinHeight = 58;
        _recentHost.Child = list;
    }

    private void CaptureRecentHost()
    {
        DependencyObject? current = RecentEmptyTitleText;
        while (current is not null && current is not Border)
            current = VisualTreeHelper.GetParent(current);
        _recentHost = current as Border;
        _recentEmptyContent = _recentHost?.Child;
    }

    private string Get(string key)
    {
        var value = _localization.GetV039String(key);
        return string.IsNullOrWhiteSpace(value) ? key : value;
    }

    private void NewDrawingButton_Click(object sender, RoutedEventArgs e) =>
        NewDrawingRequested?.Invoke(this, EventArgs.Empty);

    private async void OpenDrawingButton_Click(object sender, RoutedEventArgs e)
    {
        if (OpenDrawingAction is not null)
        {
            await OpenDrawingAction();
            return;
        }
        OpenDrawingRequested?.Invoke(this, EventArgs.Empty);
    }

    private void RecentFileButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: string path })
            RecentFileRequested?.Invoke(this, path);
    }

    private void LearnButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: string target }) LearnRequested?.Invoke(this, target);
    }
}

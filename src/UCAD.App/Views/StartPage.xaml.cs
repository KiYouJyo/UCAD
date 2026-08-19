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
        // StartLive uses plain resource IDs deliberately. Property-style *.Text keys
        // belong to XAML x:Uid loading and proved unreliable as imperative runtime
        // lookups when switching language in an already-created StartPage.
        StartTitleText.Text = GetStart("Title");
        StartSubtitleText.Text = GetStart("Subtitle");
        NewDrawingTitleText.Text = GetStart("NewDrawingTitle");
        NewDrawingDescriptionText.Text = GetStart("NewDrawingDescription");
        OpenDrawingTitleText.Text = GetStart("OpenDrawingTitle");
        OpenDrawingDescriptionText.Text = GetStart("OpenDrawingDescription");
        RecentTitleText.Text = GetStart("RecentTitle");
        RecentShowAllButton.Content = GetStart("RecentShowAll");
        RecentEmptyTitleText.Text = GetStart("RecentEmptyTitle");
        RecentEmptyDescriptionText.Text = GetStart("RecentEmptyDescription");
        TemplatesTitleText.Text = GetStart("TemplatesTitle");
        BlankTitleText.Text = GetStart("BlankTitle");
        BlankDescriptionText.Text = GetStart("BlankDescription");
        ArchitectureTitleText.Text = GetStart("ArchitectureTitle");
        ArchitectureDescriptionText.Text = GetStart("ArchitectureDescription");
        ArchitectureComingSoonText.Text = GetStart("ComingSoon");
        UrbanTitleText.Text = GetStart("UrbanTitle");
        UrbanDescriptionText.Text = GetStart("UrbanDescription");
        UrbanComingSoonText.Text = GetStart("ComingSoon");
        LearnTitleText.Text = GetStart("LearnTitle");
        ShortcutsText.Text = GetStart("Shortcuts");
        BasicDrawingText.Text = GetStart("BasicDrawing");
        CommandsText.Text = GetStart("Commands");
        DocsText.Text = GetStart("Docs");

        var language = _localization.CurrentLanguageTag;
        if (!string.IsNullOrWhiteSpace(language)) Language = language;
    }

    internal bool ValidateLocalization(string language)
    {
        var expected = language switch
        {
            "zh-CN" => ("开始使用 UCAD", "新建图纸", "打开图纸", "模板", "学习 UCAD"),
            "ja-JP" => ("UCAD を開始", "新しい図面", "図面を開く", "テンプレート", "UCAD を学ぶ"),
            "en-US" => ("Start with UCAD", "New drawing", "Open drawing", "Templates", "Learn UCAD"),
            _ => (string.Empty, string.Empty, string.Empty, string.Empty, string.Empty)
        };
        if (string.IsNullOrEmpty(expected.Item1)) return true;
        return StartTitleText.Text == expected.Item1 &&
               NewDrawingTitleText.Text == expected.Item2 &&
               OpenDrawingTitleText.Text == expected.Item3 &&
               TemplatesTitleText.Text == expected.Item4 &&
               LearnTitleText.Text == expected.Item5;
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

    private string GetStart(string key)
    {
        var value = _localization.GetStartString(key);
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

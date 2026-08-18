using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using UCAD.Services;

namespace UCAD.Views;

public sealed partial class StartPage : UserControl
{
    private readonly LocalizationService _localization = LocalizationService.Current;

    public StartPage()
    {
        InitializeComponent();
        RefreshLocalization();
    }

    public event EventHandler? NewDrawingRequested;
    public event EventHandler? OpenDrawingRequested;
    public event EventHandler<string>? LearnRequested;

    /// <summary>
    /// Optional v0.8 file action supplied by the shell. When present the Start page
    /// executes the real DXF picker instead of raising the legacy unavailable event.
    /// Keeping the event fallback preserves the v0.3.9 shell contract for tests and
    /// older call sites while allowing file I/O to live in its own MainWindow partial.
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
        if (!string.IsNullOrWhiteSpace(language))
        {
            Language = language;
        }
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

    private void LearnButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: string target })
        {
            LearnRequested?.Invoke(this, target);
        }
    }
}

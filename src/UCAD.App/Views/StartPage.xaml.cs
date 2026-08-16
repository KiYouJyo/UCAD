using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Windows.ApplicationModel.Resources;

namespace UCAD.Views;

public sealed partial class StartPage : UserControl
{
    private readonly ResourceLoader _resources = new("UcadV039");

    public StartPage()
    {
        InitializeComponent();
        RefreshLocalization();
    }

    public event EventHandler? NewDrawingRequested;
    public event EventHandler? OpenDrawingRequested;
    public event EventHandler<string>? LearnRequested;

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
    }

    private string Get(string key)
    {
        var value = _resources.GetString(key);
        return string.IsNullOrWhiteSpace(value) ? key : value;
    }

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

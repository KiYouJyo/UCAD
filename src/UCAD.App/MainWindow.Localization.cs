using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using UCAD.Services;

namespace UCAD;

public sealed partial class MainWindow
{
    /// <summary>
    /// Re-resolves every visible localized surface against the current MRT context.
    /// This intentionally keeps the Window, document tabs, CadDocument instances,
    /// undo stacks and viewport state alive while the display language changes.
    /// </summary>
    internal void RefreshLocalization()
    {
        var language = LocalizationService.Current.CurrentLanguageTag;
        if (!string.IsNullOrWhiteSpace(language))
        {
            RootLayout.Language = language;
        }

        Title = GetString("AppWindowTitle");

        FileMenuButton.Content = GetString("FileMenuButton.Content");
        EditMenuButton.Content = GetString("EditMenuButton.Content");
        ViewMenuButton.Content = GetString("ViewMenuButton.Content");
        NewDrawingMenuItem.Text = GetString("NewDrawingMenuItem.Text");
        CloseDrawingMenuItem.Text = GetString("CloseDrawingMenuItem.Text");
        UndoMenuItem.Text = GetString("UndoMenuItem.Text");
        RedoMenuItem.Text = GetString("RedoMenuItem.Text");
        ClearMenuItem.Text = GetString("ClearMenuItem.Text");
        ResetViewMenuItem.Text = GetString("ResetViewMenuItem.Text");

        DrawCategoryButton.Content = GetString("DrawCategoryButton.Content");
        ModifyCategoryButton.Content = GetString("ModifyCategoryButton.Content");
        AnnotateCategoryButton.Content = GetString("AnnotateCategoryButton.Content");
        LayersCategoryButton.Content = GetString("LayersCategoryButton.Content");
        BlocksCategoryButton.Content = GetString("BlocksCategoryButton.Content");
        MeasureCategoryButton.Content = GetString("MeasureCategoryButton.Content");
        ViewCategoryButton.Content = GetString("ViewCategoryButton.Content");
        CommandSearch.PlaceholderText = GetString("CommandSearch.PlaceholderText");
        ToolTipService.SetToolTip(SettingsButton, GetString("Settings_TabTitle"));

        LineToolLabel.Text = GetString("LineToolLabel.Text");
        PolylineToolLabel.Text = GetString("PolylineToolLabel.Text");
        RectangleToolLabel.Text = GetString("RectangleToolLabel.Text");
        CircleToolLabel.Text = GetString("CircleToolLabel.Text");
        ArcToolLabel.Text = GetString("ArcToolLabel.Text");
        HatchToolLabel.Text = GetString("HatchToolLabel.Text");
        BoundaryToolLabel.Text = GetString("BoundaryToolLabel.Text");
        RayToolLabel.Text = GetString("RayToolLabel.Text");
        ConstructionToolLabel.Text = GetString("ConstructionToolLabel.Text");
        ResetViewToolLabel.Text = GetString("ResetViewToolLabel.Text");
        UpdateToolShelfHint();
        if (UnavailableToolShelf.Visibility == Visibility.Visible)
        {
            UnavailableToolShelfText.Text = GetString("ToolShelfUnavailable");
        }

        RectangleMoreItem.Text = GetString("RectangleMoreItem.Text");
        CircleMoreItem.Text = GetString("CircleMoreItem.Text");
        ArcMoreItem.Text = GetString("ArcMoreItem.Text");
        UndoMoreItem.Text = GetString("UndoMoreItem.Text");
        RedoMoreItem.Text = GetString("RedoMoreItem.Text");
        ClearMoreItem.Text = GetString("ClearMoreItem.Text");
        ResetViewMoreItem.Text = GetString("ResetViewMoreItem.Text");

        PropertiesTabButton.Content = GetString("PropertiesTabButton.Content");
        LayersTabButton.Content = GetString("LayersTabButton.Content");
        NoSelectionText.Text = GetString("NoSelectionText.Text");
        SelectionUnavailableText.Text = GetString("SelectionUnavailableText.Text");
        DocumentSectionText.Text = GetString("DocumentSectionText.Text");
        EntityCountLabel.Text = GetString("EntityCountLabel.Text");
        ActiveCommandLabel.Text = GetString("ActiveCommandLabel.Text");
        UndoAvailableLabel.Text = GetString("UndoAvailableLabel.Text");
        RedoAvailableLabel.Text = GetString("RedoAvailableLabel.Text");
        V04FoundationHint.Text = GetString("V04FoundationHint.Text");

        CommandPrompt.Text = GetString("CommandPrompt.Text");
        CommandInput.PlaceholderText = GetString("CommandInput.PlaceholderText");
        SnapStatusButton.Content = GetString("SnapStatusButton.Content");
        GridStatusButton.Content = GetString("GridStatusButton.Content");
        OrthoStatusButton.Content = GetString("OrthoStatusButton.Content");
        PolarStatusButton.Content = GetString("PolarStatusButton.Content");
        OsnapStatusButton.Content = GetString("OsnapStatusButton.Content");
        OtrackStatusButton.Content = GetString("OtrackStatusButton.Content");

        if (_startTab is not null)
        {
            _startTab.Header = GetString("Start_TabTitle");
        }
        if (_settingsTab is not null)
        {
            _settingsTab.Header = GetString("Settings_TabTitle");
        }

        _startPage?.RefreshLocalization();
        _settingsPage?.RefreshLocalization();

        foreach (var pair in _sessions)
        {
            var session = pair.Value;
            session.UpdateDisplayName(string.Format(GetString("Document_UntitledFormat"), session.Ordinal));
            UpdateTabHeader(pair.Key, session);

            // A completed/idle session can be translated immediately. If a drawing
            // command is in progress, preserve the exact prompt/state until the next
            // command interaction rather than guessing its accepted-point count.
            if (!session.CommandSession.IsActive)
            {
                session.StatusText = GetString("Status_Ready");
            }
        }

        if (_activeSession is not null)
        {
            UpdateCoordinateText(_activeSession);
            UpdateSessionUi(_activeSession);
        }

        UpdateCategoryVisuals();
        App.WriteStartupEvent($"Live localization refresh completed: {language}");
    }

    internal bool ValidateCurrentLocalization(string expectedLanguage)
    {
        var title = GetString("Settings_Nav_Title");
        var start = GetString("Start_TabTitle");
        var file = GetString("FileMenuButton.Content");

        if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(start) || string.IsNullOrWhiteSpace(file))
        {
            return false;
        }
        if (title.StartsWith("Settings_", StringComparison.Ordinal) || start == "Start_TabTitle" || file == "FileMenuButton.Content")
        {
            return false;
        }

        return expectedLanguage switch
        {
            "zh-CN" => title == "设置" && start == "开始" && file == "文件",
            "ja-JP" => title == "設定" && start == "スタート" && file == "ファイル",
            "en-US" => title == "Settings" && start == "Start" && file == "File",
            _ => true
        };
    }
}

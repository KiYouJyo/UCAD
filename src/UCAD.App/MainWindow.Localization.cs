using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using UCAD.Services;

namespace UCAD;

public sealed partial class MainWindow
{
    internal void ApplyLiveLocalizationFromSettings()
    {
        RootLayout.DispatcherQueue.TryEnqueue(() =>
        {
            var localization = LocalizationService.Current;
            if (localization.IsSettingsLanguageApplied)
            {
                return;
            }

            if (localization.ApplyFromSettings())
            {
                RefreshLocalization();
            }
        });
    }

    internal void ScheduleLocalizationSmoke()
    {
        if (!string.Equals(Environment.GetEnvironmentVariable("UCAD_LOCALIZATION_SMOKE"), "1", StringComparison.Ordinal))
        {
            return;
        }

        RootLayout.Loaded += RootLayout_LocalizationSmokeLoaded;
    }

    private void RootLayout_LocalizationSmokeLoaded(object sender, RoutedEventArgs e)
    {
        RootLayout.Loaded -= RootLayout_LocalizationSmokeLoaded;
        RootLayout.DispatcherQueue.TryEnqueue(() =>
        {
            var localization = LocalizationService.Current;
            var originalLanguage = SettingsService.Current.Settings.DisplayLanguage;
            var originalFollowSystem = SettingsService.Current.Settings.FollowSystemLanguage;

            try
            {
                foreach (var language in new[] { "zh-CN", "ja-JP", "en-US" })
                {
                    if (!localization.ApplyLanguagePreference(language, followSystemLanguage: false, writeLog: false))
                    {
                        throw new InvalidOperationException($"Could not apply {language} during localization smoke.");
                    }

                    RefreshLocalization();
                    if (!ValidateCurrentLocalization(language))
                    {
                        throw new InvalidOperationException($"Live localization validation failed for {language}.");
                    }
                }

                App.WriteStartupEvent("Localization smoke: zh-CN -> ja-JP -> en-US refreshed without restart");
            }
            catch (Exception ex)
            {
                App.WriteStartupFailure("LocalizationSmoke", ex);
                throw;
            }
            finally
            {
                if (localization.ApplyLanguagePreference(originalLanguage, originalFollowSystem, writeLog: false))
                {
                    RefreshLocalization();
                }
            }
        });
    }

    private string ShellString(string key)
    {
        var value = LocalizationService.Current.GetShellString(key);
        return string.IsNullOrWhiteSpace(value) ? key : value;
    }

    /// <summary>
    /// Re-resolves every visible localized surface against the current MRT context.
    /// The legacy x:Uid property resources remain available for initial XAML loading,
    /// while hot refresh uses plain IDs from ShellLive.resw so imperative lookup does
    /// not depend on XAML property-resource semantics.
    /// </summary>
    internal void RefreshLocalization()
    {
        var language = LocalizationService.Current.CurrentLanguageTag;
        if (!string.IsNullOrWhiteSpace(language))
        {
            RootLayout.Language = language;
        }

        Title = GetString("AppWindowTitle");

        FileMenuButton.Content = ShellString("File");
        EditMenuButton.Content = ShellString("Edit");
        ViewMenuButton.Content = ShellString("View");
        NewDrawingMenuItem.Text = ShellString("NewDrawing");
        CloseDrawingMenuItem.Text = ShellString("CloseDrawing");
        UndoMenuItem.Text = ShellString("Undo");
        RedoMenuItem.Text = ShellString("Redo");
        ClearMenuItem.Text = ShellString("Clear");
        ResetViewMenuItem.Text = ShellString("ResetView");

        DrawCategoryButton.Content = ShellString("CategoryDraw");
        ModifyCategoryButton.Content = ShellString("CategoryModify");
        AnnotateCategoryButton.Content = ShellString("CategoryAnnotate");
        LayersCategoryButton.Content = ShellString("CategoryLayers");
        BlocksCategoryButton.Content = ShellString("CategoryBlocks");
        MeasureCategoryButton.Content = ShellString("CategoryMeasure");
        ViewCategoryButton.Content = ShellString("CategoryView");
        CommandSearch.PlaceholderText = ShellString("CommandSearchPlaceholder");
        ToolTipService.SetToolTip(SettingsButton, GetString("Settings_TabTitle"));

        LineToolLabel.Text = ShellString("ToolLine");
        PolylineToolLabel.Text = ShellString("ToolPolyline");
        RectangleToolLabel.Text = ShellString("ToolRectangle");
        CircleToolLabel.Text = ShellString("ToolCircle");
        ArcToolLabel.Text = ShellString("ToolArc");
        HatchToolLabel.Text = ShellString("ToolHatch");
        BoundaryToolLabel.Text = ShellString("ToolBoundary");
        RayToolLabel.Text = ShellString("ToolRay");
        ConstructionToolLabel.Text = ShellString("ToolConstruction");
        ResetViewToolLabel.Text = ShellString("ResetView");
        UpdateToolShelfHint();
        if (UnavailableToolShelf.Visibility == Visibility.Visible)
        {
            UnavailableToolShelfText.Text = GetString("ToolShelfUnavailable");
        }

        RectangleMoreItem.Text = ShellString("ToolRectangle");
        CircleMoreItem.Text = ShellString("ToolCircle");
        ArcMoreItem.Text = ShellString("ToolArc");
        UndoMoreItem.Text = ShellString("Undo");
        RedoMoreItem.Text = ShellString("Redo");
        ClearMoreItem.Text = ShellString("Clear");
        ResetViewMoreItem.Text = ShellString("ResetView");

        PropertiesTabButton.Content = ShellString("InspectorProperties");
        LayersTabButton.Content = ShellString("InspectorLayers");
        NoSelectionText.Text = ShellString("InspectorNoSelection");
        SelectionUnavailableText.Text = ShellString("InspectorSelectionUnavailable");
        DocumentSectionText.Text = ShellString("InspectorDocument");
        EntityCountLabel.Text = ShellString("InspectorEntityCount");
        ActiveCommandLabel.Text = ShellString("InspectorActiveCommand");
        UndoAvailableLabel.Text = ShellString("InspectorUndoAvailable");
        RedoAvailableLabel.Text = ShellString("InspectorRedoAvailable");
        V04FoundationHint.Text = ShellString("InspectorFoundationHint");

        CommandPrompt.Text = ShellString("CommandPrompt");
        CommandInput.PlaceholderText = ShellString("CommandInputPlaceholder");
        SnapStatusButton.Content = ShellString("StatusSnap");
        GridStatusButton.Content = ShellString("StatusGrid");
        OrthoStatusButton.Content = ShellString("StatusOrtho");
        PolarStatusButton.Content = ShellString("StatusPolar");
        OsnapStatusButton.Content = ShellString("StatusOsnap");
        OtrackStatusButton.Content = ShellString("StatusOtrack");

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
        var file = LocalizationService.Current.GetShellString("File");
        App.WriteStartupEvent($"Localization probe [{expectedLanguage}]: Settings_Nav_Title='{title}' | Start_TabTitle='{start}' | ShellLive/File='{file}'");

        if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(start) || string.IsNullOrWhiteSpace(file))
        {
            return false;
        }
        if (title.StartsWith("Settings_", StringComparison.Ordinal) || start == "Start_TabTitle")
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

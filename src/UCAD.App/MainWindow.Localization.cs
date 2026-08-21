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
                    App.WriteStartupEvent($"Localization surface audit passed: {language} / Start + shell + generated tools");
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

    private string CommandDisplayName(string command)
    {
        var normalized = command.Trim().ToUpperInvariant();
        var value = LocalizationService.Current.GetShellString($"CommandLabel_{normalized}");
        return string.IsNullOrWhiteSpace(value) || value.StartsWith("CommandLabel_", StringComparison.Ordinal)
            ? normalized
            : value;
    }

    /// <summary>
    /// Re-resolves every visible localized surface against the current MRT context.
    /// The legacy x:Uid property resources remain available for initial XAML loading,
    /// while hot refresh uses plain IDs so imperative lookup does not depend on XAML
    /// property-resource semantics.
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

        RefreshCommandSurfaceLabels();

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
            SyncDynamicCommandDisplay();
        }

        UpdateCategoryVisuals();
        App.WriteStartupEvent($"Live localization refresh completed: {language}");
    }

    private IEnumerable<Button> CommandSurfaceButtons()
    {
        var seen = new HashSet<Button>();
        foreach (var panel in new Panel[] { DrawToolShelf, ModifyToolShelf, UnavailableToolShelf, ViewToolShelf })
        {
            foreach (var button in panel.Children.OfType<Button>())
                if (seen.Add(button)) yield return button;
            foreach (var button in Descendants<Button>(panel))
                if (seen.Add(button)) yield return button;
        }

        foreach (var button in _extendedShelfButtons)
            if (seen.Add(button)) yield return button;

        foreach (var button in Descendants<Button>(RootLayout))
            if (seen.Add(button)) yield return button;
    }

    private void RefreshCommandSurfaceLabels()
    {
        foreach (var button in CommandSurfaceButtons())
        {
            if (!TryGetCommandFromToolTag(button.Tag, out var command) ||
                !_commandRegistry.TryResolve(command, out _))
            {
                continue;
            }
            ApplyLocalizedCommandLabel(button, command);
        }
    }

    private static bool TryGetCommandFromToolTag(object? tag, out string command)
    {
        command = string.Empty;
        if (tag is not string raw || string.IsNullOrWhiteSpace(raw)) return false;
        var separator = raw.LastIndexOf('|');
        command = (separator >= 0 ? raw[(separator + 1)..] : raw).Trim().ToUpperInvariant();
        return command.Length > 0;
    }

    private static TextBlock? CommandLabelTextBlock(Button button)
    {
        // The generated shelves own their StackPanel Content even while the button has
        // never entered the visual tree. Query that logical content first; otherwise a
        // Collapsed tool category reports no visual TextBlock and misses localization.
        if (button.Content is Panel contentPanel)
        {
            var direct = contentPanel.Children
                .OfType<TextBlock>()
                .FirstOrDefault(text => text.FontSize >= 9 && text.FontSize <= 12);
            if (direct is not null) return direct;

            return Descendants<TextBlock>(contentPanel)
                .FirstOrDefault(text => text.FontSize >= 9 && text.FontSize <= 12);
        }

        return Descendants<TextBlock>(button)
            .FirstOrDefault(text => text.FontSize >= 9 && text.FontSize <= 12);
    }

    private void ApplyLocalizedCommandLabel(Button button, string command)
    {
        var label = CommandLabelTextBlock(button);
        if (label is not null) label.Text = CommandDisplayName(command);
    }

    private string? RenderedCommandLabel(string command)
    {
        foreach (var button in CommandSurfaceButtons())
        {
            if (!TryGetCommandFromToolTag(button.Tag, out var candidate) ||
                !string.Equals(candidate, command, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }
            var label = CommandLabelTextBlock(button);
            if (label is not null) return label.Text;
        }
        return null;
    }

    internal bool ValidateCurrentLocalization(string expectedLanguage)
    {
        var title = GetString("Settings_Nav_Title");
        var start = GetString("Start_TabTitle");
        var file = LocalizationService.Current.GetShellString("File");
        var startSurfaceOkay = _startPage?.ValidateLocalization(expectedLanguage) ?? false;
        var moveLabel = RenderedCommandLabel("MOVE");
        var eraseLabel = RenderedCommandLabel("ERASE");
        App.WriteStartupEvent(
            $"Localization probe [{expectedLanguage}]: Settings='{title}' | StartTab='{start}' | File='{file}' | StartSurface={startSurfaceOkay} | MOVE='{moveLabel}' | ERASE='{eraseLabel}'");

        if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(start) || string.IsNullOrWhiteSpace(file) ||
            string.IsNullOrWhiteSpace(moveLabel) || string.IsNullOrWhiteSpace(eraseLabel) || !startSurfaceOkay)
        {
            return false;
        }
        if (title.StartsWith("Settings_", StringComparison.Ordinal) || start == "Start_TabTitle")
        {
            return false;
        }

        return expectedLanguage switch
        {
            "zh-CN" => title == "设置" && start == "开始" && file == "文件" && moveLabel == "移动" && eraseLabel == "删除",
            "ja-JP" => title == "設定" && start == "スタート" && file == "ファイル" && moveLabel == "移動" && eraseLabel == "削除",
            "en-US" => title == "Settings" && start == "Start" && file == "File" && moveLabel == "Move" && eraseLabel == "Erase",
            _ => true
        };
    }
}

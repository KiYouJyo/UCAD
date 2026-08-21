using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.Windows.ApplicationModel.Resources;
using System.Globalization;
using UCAD.Core;
using UCAD.Core.Commands;
using UCAD.Core.Geometry;
using UCAD.Models;
using UCAD.Services;
using UCAD.Views;
using UCAD.Workspace;
using Windows.System;

namespace UCAD;

public sealed partial class MainWindow : Window
{
    private readonly ResourceLoader _resources;
    private readonly CommandRegistry _commandRegistry;
    private readonly SettingsService _settingsService = SettingsService.Current;
    private readonly Dictionary<TabViewItem, CadWorkspaceSession> _sessions = [];
    private readonly Dictionary<TabViewItem, WorkspacePageKind> _tabKinds = [];
    private CadWorkspaceSession? _activeSession;
    private TabViewItem? _startTab;
    private TabViewItem? _settingsTab;
    private StartPage? _startPage;
    private UcadSettingsPage? _settingsPage;
    private int _nextDocumentOrdinal = 1;
    private string? _activeShelfCategory = "DRAW";
    private bool _initialWorkspaceCreated;

    public MainWindow()
    {
        InitializeComponent();
        _resources = new ResourceLoader();
        _commandRegistry = CommandRegistry.CreateDefault();

        ExtendsContentIntoTitleBar = true;
        SetTitleBar(TitleDragRegion);
        Title = GetString("AppWindowTitle");
        VersionText.Text = AppVersionInfo.ProductDisplayVersion;

        CommandSearch.ItemsSource = _commandRegistry.Commands
            .SelectMany(command => command.Tokens)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(token => token, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        RootLayout.Loaded += RootLayout_Loaded;
        RootLayout.ActualThemeChanged += RootLayout_ActualThemeChanged;
        _settingsService.SettingsChanged += SettingsService_SettingsChanged;
        ApplyAppTheme();
        UpdateCategoryVisuals();
        UpdateToolShelfHint();
    }

    private CadWorkspaceSession? ActiveSession => _activeSession;

    private double DocumentTabWidth => (double)Application.Current.Resources["UcadDocumentTabWidth"];

    private double DocumentTabHeight => (double)Application.Current.Resources["UcadDocumentTabHeight"];

    private ToggleButton[] CategoryButtons =>
    [
        DrawCategoryButton,
        ModifyCategoryButton,
        AnnotateCategoryButton,
        LayersCategoryButton,
        BlocksCategoryButton,
        MeasureCategoryButton,
        ViewCategoryButton
    ];

    private void RootLayout_Loaded(object sender, RoutedEventArgs e)
    {
        RootLayout.Loaded -= RootLayout_Loaded;
        if (_initialWorkspaceCreated)
        {
            return;
        }

        _initialWorkspaceCreated = true;
        App.WriteStartupEvent("RootLayout loaded; creating initial page");
        try
        {
            switch (_settingsService.Settings.StartupBehavior)
            {
                case "BlankDrawing":
                    CreateNewWorkspace();
                    break;
                case "RestoreSession":
                    // Session serialization does not exist yet. Never fabricate a restored
                    // document; fall back to the real Start page and leave a diagnostic trail.
                    App.WriteStartupEvent("RestoreSession requested but session persistence is not available; opening Start instead");
                    CreateStartTab();
                    break;
                default:
                    CreateStartTab();
                    break;
            }

            if (string.Equals(Environment.GetEnvironmentVariable("UCAD_STARTUP_SMOKE"), "1", StringComparison.Ordinal))
            {
                CreateSettingsTab();
                CreateStartTab();
                App.WriteStartupEvent("Startup smoke: Start and Settings initialized");
            }

            App.WriteStartupEvent("Initial page created");
            if (_settingsService.Settings.AutoCheckUpdates &&
                !string.Equals(Environment.GetEnvironmentVariable("UCAD_STARTUP_SMOKE"), "1", StringComparison.Ordinal))
            {
                _ = CheckForUpdatesAsync(showUpToDate: false);
            }
        }
        catch (Exception ex)
        {
            App.WriteStartupFailure("CreateInitialPage", ex);
            throw;
        }
    }

    private string GetString(string key)
    {
        try
        {
            var value = _resources.GetString(key);
            return string.IsNullOrWhiteSpace(value) ? key : value;
        }
        catch (System.Runtime.InteropServices.COMException ex) when (ex.HResult == unchecked((int)0x80073B17))
        {
            App.WriteStartupFailure($"MissingResource:{key}", ex);
            return key;
        }
    }

    private void ApplyAppTheme()
    {
        RootLayout.RequestedTheme = _settingsService.Settings.AppTheme switch
        {
            "Light" => ElementTheme.Light,
            "Dark" => ElementTheme.Dark,
            _ => ElementTheme.Default
        };
        ApplyShellPalette(IsLightShellTheme());
    }

    private bool IsLightShellTheme() => _settingsService.Settings.AppTheme switch
    {
        "Light" => true,
        "Dark" => false,
        _ => RootLayout.ActualTheme == ElementTheme.Light
    };

    private void RootLayout_ActualThemeChanged(FrameworkElement sender, object args)
    {
        if (string.Equals(_settingsService.Settings.AppTheme, "System", StringComparison.Ordinal))
        {
            ApplyShellPalette(sender.ActualTheme == ElementTheme.Light);
            UpdateCategoryVisuals();
        }
    }

    private static void ApplyShellPalette(bool light)
    {
        if (light)
        {
            SetBrushColor("UcadAppBackgroundBrush", 255, 243, 243, 243);
            SetBrushColor("UcadTitleBarBrush", 255, 249, 249, 249);
            SetBrushColor("UcadCategoryBarBrush", 255, 245, 245, 245);
            SetBrushColor("UcadToolShelfBrush", 255, 250, 250, 250);
            SetBrushColor("UcadNavigationBrush", 255, 248, 248, 248);
            SetBrushColor("UcadCardBrush", 255, 255, 255, 255);
            SetBrushColor("UcadAboutCardBrush", 255, 255, 255, 255);
            SetBrushColor("UcadPanelBrush", 255, 250, 250, 250);
            SetBrushColor("UcadOverlayBrush", 255, 243, 243, 243);
            SetBrushColor("UcadStatusBarBrush", 255, 245, 245, 245);
            SetBrushColor("UcadControlFillBrush", 255, 255, 255, 255);
            SetBrushColor("UcadControlFillSubtleBrush", 255, 247, 247, 247);
            SetBrushColor("UcadControlFillStrongBrush", 255, 238, 238, 238);
            SetBrushColor("UcadTextPrimaryBrush", 255, 28, 28, 30);
            SetBrushColor("UcadTextSecondaryBrush", 255, 88, 88, 94);
            SetBrushColor("UcadTextTertiaryBrush", 255, 112, 112, 118);
            SetBrushColor("UcadTextDisabledBrush", 255, 158, 158, 164);
            SetBrushColor("UcadDividerBrush", 255, 218, 218, 222);
            SetBrushColor("UcadCardBorderBrush", 153, 198, 198, 204);
            SetBrushColor("UcadStartActionBorderBrush", 191, 190, 190, 198);
            SetBrushColor("UcadStartTemplateBorderBrush", 128, 190, 190, 198);
            SetBrushColor("UcadAboutCardBorderBrush", 255, 204, 204, 210);
            SetBrushColor("UcadControlBorderBrush", 255, 196, 196, 202);
            SetBrushColor("UcadDividerSoftBrush", 255, 226, 226, 230);
        }
        else
        {
            SetBrushColor("UcadAppBackgroundBrush", 255, 24, 24, 26);
            SetBrushColor("UcadTitleBarBrush", 255, 32, 32, 34);
            SetBrushColor("UcadCategoryBarBrush", 255, 37, 37, 40);
            SetBrushColor("UcadToolShelfBrush", 255, 29, 29, 32);
            SetBrushColor("UcadNavigationBrush", 255, 29, 29, 32);
            SetBrushColor("UcadCardBrush", 255, 34, 34, 37);
            SetBrushColor("UcadAboutCardBrush", 255, 32, 32, 35);
            SetBrushColor("UcadPanelBrush", 255, 31, 31, 34);
            SetBrushColor("UcadOverlayBrush", 255, 24, 24, 26);
            SetBrushColor("UcadStatusBarBrush", 255, 34, 34, 37);
            SetBrushColor("UcadControlFillBrush", 255, 46, 46, 51);
            SetBrushColor("UcadControlFillSubtleBrush", 255, 40, 40, 44);
            SetBrushColor("UcadControlFillStrongBrush", 255, 37, 37, 40);
            SetBrushColor("UcadTextPrimaryBrush", 255, 237, 237, 242);
            SetBrushColor("UcadTextSecondaryBrush", 255, 158, 158, 171);
            SetBrushColor("UcadTextTertiaryBrush", 255, 126, 129, 137);
            SetBrushColor("UcadTextDisabledBrush", 255, 104, 104, 114);
            SetBrushColor("UcadDividerBrush", 255, 64, 64, 71);
            SetBrushColor("UcadCardBorderBrush", 153, 64, 64, 71);
            SetBrushColor("UcadStartActionBorderBrush", 191, 64, 64, 71);
            SetBrushColor("UcadStartTemplateBorderBrush", 128, 64, 64, 71);
            SetBrushColor("UcadAboutCardBorderBrush", 255, 64, 64, 74);
            SetBrushColor("UcadControlBorderBrush", 255, 64, 64, 74);
            SetBrushColor("UcadDividerSoftBrush", 255, 46, 50, 54);
        }
    }

    private static void SetBrushColor(string key, byte alpha, byte red, byte green, byte blue)
    {
        if (Application.Current.Resources[key] is SolidColorBrush brush)
        {
            brush.Color = Microsoft.UI.ColorHelper.FromArgb(alpha, red, green, blue);
        }
    }

    private void SettingsService_SettingsChanged(object? sender, EventArgs e)
    {
        ApplyAppTheme();
        foreach (var session in _sessions.Values)
        {
            session.Viewport.ApplySettings(_settingsService.Settings);
        }
        if (_activeSession is not null)
        {
            UpdateCoordinateText(_activeSession);
        }
    }

    private TabViewItem CreateStartTab()
    {
        if (_startTab is not null && DocumentTabs.TabItems.Contains(_startTab))
        {
            DocumentTabs.SelectedItem = _startTab;
            ShowStartPage();
            return _startTab;
        }

        _startPage = new StartPage();
        _startPage.NewDrawingRequested += (_, _) => CreateNewWorkspace();
        _startPage.OpenDrawingRequested += async (_, _) => await ShowFeatureUnavailableAsync("Feature_Files_Title", "Feature_Files_Message");
        _startPage.LearnRequested += StartPage_LearnRequested;

        _startTab = CreatePageTab(GetString("Start_TabTitle"), WorkspacePageKind.Start);
        DocumentTabs.TabItems.Add(_startTab);
        UpdateTabStripWidth();
        DocumentTabs.SelectedItem = _startTab;
        ShowStartPage();
        return _startTab;
    }

    private TabViewItem CreateSettingsTab()
    {
        if (_settingsTab is not null && DocumentTabs.TabItems.Contains(_settingsTab))
        {
            DocumentTabs.SelectedItem = _settingsTab;
            ShowSettingsPage();
            return _settingsTab;
        }

        _settingsPage = new UcadSettingsPage();
        _settingsPage.CheckUpdatesRequested += async (_, _) => await CheckForUpdatesAsync(showUpToDate: true);
        _settingsPage.LanguageChanged += (_, _) => App.WriteStartupEvent("Display language preference changed; applies on next launch");

        _settingsTab = CreatePageTab(GetString("Settings_TabTitle"), WorkspacePageKind.Settings);
        DocumentTabs.TabItems.Add(_settingsTab);
        UpdateTabStripWidth();
        DocumentTabs.SelectedItem = _settingsTab;
        ShowSettingsPage();
        return _settingsTab;
    }

    private TabViewItem CreatePageTab(string title, WorkspacePageKind kind)
    {
        var tab = new TabViewItem
        {
            Header = title,
            IsClosable = true,
            Width = DocumentTabWidth,
            Height = DocumentTabHeight
        };
        _tabKinds[tab] = kind;
        return tab;
    }

    private async void StartPage_LearnRequested(object? sender, string target)
    {
        if (target == "docs")
        {
            await Launcher.LaunchUriAsync(new Uri("https://github.com/KiYouJyo/UCAD#readme"));
            return;
        }

        await ShowFeatureUnavailableAsync("Feature_Learn_Title", "Feature_Learn_Message");
    }

    private async Task ShowFeatureUnavailableAsync(string titleKey, string messageKey)
    {
        var dialog = new ContentDialog
        {
            XamlRoot = RootLayout.XamlRoot,
            Title = GetString(titleKey),
            Content = GetString(messageKey),
            CloseButtonText = GetString("Common_Close")
        };
        await dialog.ShowAsync();
    }

    private CadWorkspaceSession CreateNewWorkspace()
    {
        var ordinal = _nextDocumentOrdinal++;
        var displayName = string.Format(GetString("Document_UntitledFormat"), ordinal);
        var session = new CadWorkspaceSession(ordinal, displayName, _commandRegistry)
        {
            StatusText = GetString("Status_Ready")
        };
        session.Viewport.ApplySettings(_settingsService.Settings);

        var tab = new TabViewItem
        {
            Tag = session,
            Header = displayName,
            IsClosable = true,
            Width = DocumentTabWidth,
            Height = DocumentTabHeight
        };
        _sessions[tab] = session;
        _tabKinds[tab] = WorkspacePageKind.Drawing;

        session.Document.Changed += (_, _) =>
        {
            UpdateTabHeader(tab, session);
            if (ReferenceEquals(_activeSession, session))
            {
                UpdateSessionUi(session);
            }
        };
        session.Viewport.PointerWorldPositionChanged += point =>
        {
            session.PointerWorldPosition = point;
            if (ReferenceEquals(_activeSession, session)) UpdateCoordinateText(session);
        };
        session.Viewport.DrawingPointAccepted += (kind, count, point) =>
        {
            session.CommandBasePoint = point;
            SetDrawingPrompt(session, kind, count);
        };
        session.Viewport.DrawingCommandCompleted += kind =>
        {
            if (session.CommandSession.ActiveCommand?.DrawingKind == kind) session.CommandSession.Complete();
            session.CommandBasePoint = null;
            SetSessionStatus(session, GetString("Status_Ready"));
            if (ReferenceEquals(_activeSession, session)) UpdateSessionUi(session);
        };
        session.Viewport.ZoomChanged += _ =>
        {
            if (ReferenceEquals(_activeSession, session)) UpdateZoomText(session);
        };

        DocumentTabs.TabItems.Add(tab);
        UpdateTabStripWidth();
        DocumentTabs.SelectedItem = tab;
        ActivateSession(session);
        return session;
    }

    private void UpdateTabStripWidth()
    {
        var desired = 36 + (DocumentTabs.TabItems.Count * (DocumentTabWidth + 4));
        DocumentTabs.Width = Math.Clamp(desired, 230, 920);
    }

    private void ShowStartPage()
    {
        if (_startPage is null) return;
        _activeSession = null;
        PageContentHost.Content = _startPage;
        PageOverlay.Visibility = Visibility.Visible;
        UpdatePageModeVisuals(WorkspacePageKind.Start);
    }

    private void ShowSettingsPage()
    {
        if (_settingsPage is null) return;
        _activeSession = null;
        PageContentHost.Content = _settingsPage;
        PageOverlay.Visibility = Visibility.Visible;
        UpdatePageModeVisuals(WorkspacePageKind.Settings);
    }

    private void ActivateSession(CadWorkspaceSession session)
    {
        _activeSession = session;
        PageContentHost.Content = null;
        PageOverlay.Visibility = Visibility.Collapsed;
        ViewportHost.Content = session.Viewport;
        CommandInput.Text = string.Empty;
        ModeText.Text = session.StatusText;
        UpdateCoordinateText(session);
        UpdateZoomText(session);
        UpdateSessionUi(session);
        UpdatePageModeVisuals(WorkspacePageKind.Drawing);
    }

    private void UpdatePageModeVisuals(WorkspacePageKind kind)
    {
        SettingsButton.Background = kind == WorkspacePageKind.Settings
            ? (Brush)Application.Current.Resources["UcadAccentSelectedBrush"]
            : (Brush)Application.Current.Resources["UcadAccentBrush"];
        UpdateCategoryVisuals();
    }

    private void UpdateSessionUi(CadWorkspaceSession session)
    {
        if (!ReferenceEquals(_activeSession, session)) return;
        var canUndo = session.Document.CanUndo;
        var canRedo = session.Document.CanRedo;
        UndoMenuItem.IsEnabled = canUndo;
        RedoMenuItem.IsEnabled = canRedo;
        UndoMoreItem.IsEnabled = canUndo;
        RedoMoreItem.IsEnabled = canRedo;
        EntityCountValue.Text = session.Document.Entities.Count.ToString();
        ActiveCommandValue.Text = session.CommandSession.ActiveCommand?.Name ?? GetString("Inspector_None");
        UndoAvailableValue.Text = canUndo ? GetString("Inspector_Yes") : GetString("Inspector_No");
        RedoAvailableValue.Text = canRedo ? GetString("Inspector_Yes") : GetString("Inspector_No");
        ModeText.Text = session.StatusText;
    }

    private void UpdateCoordinateText(CadWorkspaceSession session)
    {
        var format = _settingsService.Settings.Precision switch
        {
            "0" => "0",
            "0.0" => "0.0",
            "0.000" => "0.000",
            _ => "0.00"
        };
        var provider = NumericFormatProvider();
        CoordinateText.Text = $"X {session.PointerWorldPosition.X.ToString(format, provider)}   Y {session.PointerWorldPosition.Y.ToString(format, provider)}";
    }

    private IFormatProvider NumericFormatProvider()
    {
        var source = string.Equals(_settingsService.Settings.NumberFormat, "Invariant", StringComparison.Ordinal)
            ? CultureInfo.InvariantCulture.NumberFormat
            : CultureInfo.CurrentCulture.NumberFormat;
        var format = (NumberFormatInfo)source.Clone();
        format.NumberDecimalSeparator = _settingsService.Settings.AngleDecimalFormat switch
        {
            "Dot" => ".",
            "Comma" => ",",
            _ => format.NumberDecimalSeparator
        };
        return format;
    }

    private void UpdateZoomText(CadWorkspaceSession session) =>
        ZoomText.Text = $"{session.Viewport.Zoom * 100:0}%";

    private static void UpdateTabHeader(TabViewItem tab, CadWorkspaceSession session) =>
        tab.Header = session.IsDirty ? $"{session.DisplayName} •" : session.DisplayName;

    private void SetSessionStatus(CadWorkspaceSession session, string text)
    {
        session.StatusText = text;
        if (ReferenceEquals(_activeSession, session)) ModeText.Text = text;
    }

    private void NewDrawingMenuItem_Click(object sender, RoutedEventArgs e) => CreateNewWorkspace();

    private async void CloseDrawingMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (DocumentTabs.SelectedItem is TabViewItem tab) await TryCloseTabAsync(tab);
    }

    private void SettingsButton_Click(object sender, RoutedEventArgs e) => CreateSettingsTab();

    private void DocumentTabs_AddTabButtonClick(TabView sender, object args)
    {
        if (_settingsService.Settings.ShowStartOnNewTab)
        {
            CreateStartTab();
        }
        else
        {
            CreateNewWorkspace();
        }
    }

    private void DocumentTabs_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (DocumentTabs.SelectedItem is not TabViewItem tab || !_tabKinds.TryGetValue(tab, out var kind)) return;
        switch (kind)
        {
            case WorkspacePageKind.Drawing when _sessions.TryGetValue(tab, out var session):
                ActivateSession(session);
                break;
            case WorkspacePageKind.Start:
                ShowStartPage();
                break;
            case WorkspacePageKind.Settings:
                ShowSettingsPage();
                break;
        }
    }

    private async void DocumentTabs_TabCloseRequested(TabView sender, TabViewTabCloseRequestedEventArgs args) =>
        await TryCloseTabAsync(args.Tab);

    private async Task<bool> TryCloseTabAsync(TabViewItem tab)
    {
        _tabKinds.TryGetValue(tab, out var kind);
        if (kind == WorkspacePageKind.Drawing && _sessions.TryGetValue(tab, out var session))
        {
            if (session.IsDirty && _settingsService.Settings.ConfirmUnsaved)
            {
                var dialog = new ContentDialog
                {
                    XamlRoot = RootLayout.XamlRoot,
                    Title = GetString("CloseDirtyDialog_Title"),
                    Content = string.Format(GetString("CloseDirtyDialog_Content"), session.DisplayName),
                    PrimaryButtonText = GetString("CloseDirtyDialog_Primary"),
                    CloseButtonText = GetString("CloseDirtyDialog_Cancel"),
                    DefaultButton = ContentDialogButton.Close
                };
                if (await dialog.ShowAsync() != ContentDialogResult.Primary) return false;
            }
            _sessions.Remove(tab);
            if (ReferenceEquals(_activeSession, session)) _activeSession = null;
        }
        else if (kind == WorkspacePageKind.Start && ReferenceEquals(tab, _startTab))
        {
            _startTab = null;
            _startPage = null;
        }
        else if (kind == WorkspacePageKind.Settings && ReferenceEquals(tab, _settingsTab))
        {
            _settingsTab = null;
            _settingsPage = null;
        }

        _tabKinds.Remove(tab);
        DocumentTabs.TabItems.Remove(tab);
        UpdateTabStripWidth();
        if (DocumentTabs.TabItems.Count == 0)
        {
            CreateStartTab();
        }
        else if (DocumentTabs.SelectedItem is TabViewItem selected)
        {
            DocumentTabs_SelectionChanged(DocumentTabs, new SelectionChangedEventArgs([], []));
        }
        return true;
    }

    private void RunCommand_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: string command }) StartToolbarCommand(command);
    }

    private void CommandSearch_QuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
    {
        var token = args.ChosenSuggestion?.ToString();
        if (string.IsNullOrWhiteSpace(token)) token = args.QueryText;
        if (!string.IsNullOrWhiteSpace(token))
        {
            StartToolbarCommand(token);
            sender.Text = string.Empty;
        }
    }

    private void CategoryButton_Click(object sender, RoutedEventArgs e)
    {
        if (ActiveSession is null || PageOverlay.Visibility == Visibility.Visible) return;
        if (sender is ToggleButton { Tag: string category }) ToggleToolShelf(category);
    }

    private void ToggleToolShelf(string category)
    {
        if (_activeShelfCategory == category && ToolShelfHost.Visibility == Visibility.Visible)
        {
            ToolShelfHost.Visibility = Visibility.Collapsed;
            _activeShelfCategory = null;
            DrawToolShelf.Visibility = Visibility.Collapsed;
            ModifyToolShelf.Visibility = Visibility.Collapsed;
            ViewToolShelf.Visibility = Visibility.Collapsed;
            UnavailableToolShelf.Visibility = Visibility.Collapsed;
            UpdateCategoryVisuals();
            return;
        }
        _activeShelfCategory = category;
        ToolShelfHost.Visibility = Visibility.Visible;
        DrawToolShelf.Visibility = category == "DRAW" ? Visibility.Visible : Visibility.Collapsed;
        ModifyToolShelf.Visibility = category == "MODIFY" ? Visibility.Visible : Visibility.Collapsed;
        ViewToolShelf.Visibility = category == "VIEW" ? Visibility.Visible : Visibility.Collapsed;
        var unavailable = category is not ("DRAW" or "MODIFY" or "VIEW");
        UnavailableToolShelf.Visibility = unavailable ? Visibility.Visible : Visibility.Collapsed;
        if (unavailable) UnavailableToolShelfText.Text = GetString("ToolShelfUnavailable");
        UpdateCategoryVisuals();
        UpdateToolShelfHint();
    }

    private void UpdateCategoryVisuals()
    {
        var resources = Application.Current.Resources;
        var selectedBrush = (Brush)resources["UcadCategorySelectedBrush"];
        var primaryBrush = (Brush)resources["UcadTextPrimaryBrush"];
        var secondaryBrush = (Brush)resources["UcadTextSecondaryBrush"];
        var transparentBrush = new SolidColorBrush(Microsoft.UI.Colors.Transparent);
        foreach (var button in CategoryButtons)
        {
            var selected = PageOverlay.Visibility != Visibility.Visible &&
                           ToolShelfHost.Visibility == Visibility.Visible &&
                           button.Tag is string category && category == _activeShelfCategory;
            button.IsChecked = selected;
            button.Background = selected ? selectedBrush : transparentBrush;
            button.Foreground = selected ? primaryBrush : secondaryBrush;
            button.FontWeight = selected ? FontWeights.SemiBold : FontWeights.Normal;
        }
    }

    private void UpdateToolShelfHint() => ToolShelfHintText.Text = GetString("ToolShelfHint");

    private void CommandInput_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key is VirtualKey.Enter or VirtualKey.Space)
        {
            SubmitCommandLine();
            e.Handled = true;
        }
        else if (e.Key == VirtualKey.Escape)
        {
            CancelActiveCommand();
            e.Handled = true;
        }
    }

    private void RootLayout_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Escape && ActiveSession is not null)
        {
            CancelActiveCommand();
            e.Handled = true;
        }
    }

    private void SubmitCommandLine()
    {
        var session = ActiveSession;
        if (session is null) return;
        var input = CommandInput.Text.Trim();
        CommandInput.Text = string.Empty;
        if (session.CommandSession.ActiveCommand?.DrawingKind is DrawingCommandKind drawingKind)
        {
            if (string.IsNullOrWhiteSpace(input))
            {
                if (drawingKind is DrawingCommandKind.Line or DrawingCommandKind.Polyline) session.Viewport.CompleteDrawingCommand();
                else SetSessionStatus(session, GetString("Status_PointRequired"));
                return;
            }
            if (!TryResolvePointInput(session, input, out var point))
            {
                SetSessionStatus(session, GetString("Status_InvalidPoint"));
                return;
            }
            if (!session.Viewport.SubmitDrawingPoint(point)) SetSessionStatus(session, GetString("Status_InvalidGeometry"));
            return;
        }
        StartCommand(session, input);
    }

    private bool TryResolvePointInput(CadWorkspaceSession session, string input, out CadPoint point)
    {
        if (CommandInputParser.TryParsePoint(input, session.CommandBasePoint, out point)) return true;
        if (session.CommandBasePoint is not CadPoint basePoint || !CommandInputParser.TryParseNumber(input, out var distance))
        {
            point = default;
            return false;
        }
        var cursor = session.Viewport.CurrentPointerWorldPosition;
        var dx = cursor.X - basePoint.X;
        var dy = cursor.Y - basePoint.Y;
        var length = Math.Sqrt((dx * dx) + (dy * dy));
        if (length < 1e-9)
        {
            point = default;
            return false;
        }
        point = new CadPoint(basePoint.X + (dx / length * distance), basePoint.Y + (dy / length * distance));
        return true;
    }

    private void StartToolbarCommand(string command)
    {
        var session = ActiveSession;
        if (session is null) return;
        if (session.CommandSession.IsActive)
        {
            session.Viewport.CancelDrawingCommand();
            session.CommandSession.Cancel();
            session.CommandBasePoint = null;
        }
        StartCommand(session, command);
        CommandInput.Focus(FocusState.Programmatic);
    }

    private void StartCommand(CadWorkspaceSession session, string? token)
    {
        var result = session.CommandSession.Start(token);
        switch (result.Status)
        {
            case CommandStartStatus.NoPreviousCommand:
                SetSessionStatus(session, GetString("Status_NoPreviousCommand"));
                break;
            case CommandStartStatus.Unknown:
                SetSessionStatus(session, string.Format(GetString("Status_UnknownCommand"), result.Token));
                break;
            case CommandStartStatus.Started:
                DispatchStartedCommand(session, result.Command!);
                break;
        }
        UpdateSessionUi(session);
    }

    private void DispatchStartedCommand(CadWorkspaceSession session, CadCommandDefinition command)
    {
        if (command.DrawingKind is DrawingCommandKind drawingKind)
        {
            session.CommandBasePoint = null;
            session.Viewport.BeginDrawingCommand(drawingKind);
            SetDrawingPrompt(session, drawingKind, 0);
            CommandInput.Focus(FocusState.Programmatic);
            return;
        }
        switch (command.Name)
        {
            case "UNDO":
                SetSessionStatus(session, session.Viewport.Undo() ? GetString("Status_Undo") : GetString("Status_NothingToUndo"));
                session.CommandSession.Complete();
                break;
            case "REDO":
                SetSessionStatus(session, session.Viewport.Redo() ? GetString("Status_Redo") : GetString("Status_NothingToRedo"));
                session.CommandSession.Complete();
                break;
            case "CLEAR":
                session.Viewport.ClearDocument();
                session.CommandSession.Complete();
                SetSessionStatus(session, GetString("Status_Cleared"));
                break;
            case "RESETVIEW":
                session.Viewport.ResetView();
                session.CommandSession.Complete();
                SetSessionStatus(session, GetString("Status_ViewReset"));
                break;
        }
    }

    private void SetDrawingPrompt(CadWorkspaceSession session, DrawingCommandKind kind, int acceptedPointCount)
    {
        var key = kind switch
        {
            DrawingCommandKind.Line => acceptedPointCount == 0 ? "Status_LineFirst" : "Status_LineNext",
            DrawingCommandKind.Polyline => acceptedPointCount == 0 ? "Status_PlineFirst" : "Status_PlineNext",
            DrawingCommandKind.Rectangle => acceptedPointCount == 0 ? "Status_RectFirst" : "Status_RectOpposite",
            DrawingCommandKind.Circle => acceptedPointCount == 0 ? "Status_CircleCenter" : "Status_CircleRadius",
            DrawingCommandKind.Arc => acceptedPointCount switch { 0 => "Status_ArcStart", 1 => "Status_ArcSecond", _ => "Status_ArcEnd" },
            _ => "Status_Ready"
        };
        SetSessionStatus(session, GetString(key));
    }

    private void CancelActiveCommand()
    {
        var session = ActiveSession;
        if (session is null) return;
        CommandInput.Text = string.Empty;
        if (session.CommandSession.ActiveCommand?.DrawingKind is not null) session.Viewport.CancelDrawingCommand();
        if (session.CommandSession.Cancel())
        {
            session.CommandBasePoint = null;
            SetSessionStatus(session, GetString("Status_CommandCancelled"));
        }
        else SetSessionStatus(session, GetString("Status_Ready"));
        UpdateSessionUi(session);
        CommandInput.Focus(FocusState.Programmatic);
    }
}

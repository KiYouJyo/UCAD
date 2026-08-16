using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.Windows.ApplicationModel.Resources;
using UCAD.Controls;
using UCAD.Services;
using Windows.System;

namespace UCAD.Views;

public sealed partial class UcadSettingsPage : UserControl
{
    private enum SettingsSection
    {
        General,
        Appearance,
        Drafting,
        Input,
        Files,
        Language,
        About
    }

    private readonly ResourceLoader _resources = new();
    private readonly SettingsService _service = SettingsService.Current;
    private SettingsSection _section = SettingsSection.General;
    private bool _hasSection;

    public UcadSettingsPage()
    {
        InitializeComponent();
        RefreshLocalization();
        Navigate(SettingsSection.General);
    }

    public event EventHandler? SettingsChanged;
    public event EventHandler? CheckUpdatesRequested;
    public event EventHandler? LanguageChanged;

    public void RefreshLocalization()
    {
        SettingsNavTitle.Text = GetString("Settings_Nav_Title");
        GeneralNavButton.Content = GetString("Settings_Nav_General");
        AppearanceNavButton.Content = GetString("Settings_Nav_Appearance");
        DraftingNavButton.Content = GetString("Settings_Nav_Drafting");
        InputNavButton.Content = GetString("Settings_Nav_Input");
        FilesNavButton.Content = GetString("Settings_Nav_Files");
        LanguageNavButton.Content = GetString("Settings_Nav_Language");
        AboutNavButton.Content = GetString("Settings_Nav_About");
        BuildCurrentSection();
    }

    private string GetString(string key)
    {
        try
        {
            var value = _resources.GetString(key);
            return string.IsNullOrWhiteSpace(value) ? key : value;
        }
        catch
        {
            return key;
        }
    }

    private void SettingsNavButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: string tag } && Enum.TryParse<SettingsSection>(tag, out var section))
        {
            Navigate(section);
        }
    }

    private void Navigate(SettingsSection section)
    {
        _section = section;
        var selected = (Brush)Application.Current.Resources["UcadAccentSelectedBrush"];
        var primary = (Brush)Application.Current.Resources["UcadTextPrimaryBrush"];
        var secondary = (Brush)Application.Current.Resources["UcadTextSecondaryBrush"];
        var transparent = new SolidColorBrush(Microsoft.UI.Colors.Transparent);

        foreach (var button in NavButtons())
        {
            var active = string.Equals(button.Tag?.ToString(), section.ToString(), StringComparison.Ordinal);
            button.Background = active ? selected : transparent;
            button.Foreground = active ? primary : secondary;
            button.FontWeight = active ? FontWeights.SemiBold : FontWeights.Normal;
        }

        BuildCurrentSection();
        SettingsScrollViewer.ChangeView(null, 0, null, true);
    }

    private IEnumerable<Button> NavButtons()
    {
        yield return GeneralNavButton;
        yield return AppearanceNavButton;
        yield return DraftingNavButton;
        yield return InputNavButton;
        yield return FilesNavButton;
        yield return LanguageNavButton;
        yield return AboutNavButton;
    }

    private void BuildCurrentSection()
    {
        if (SettingsContent is null)
        {
            return;
        }

        SettingsContent.Children.Clear();
        _hasSection = false;

        switch (_section)
        {
            case SettingsSection.General:
                BuildGeneral();
                break;
            case SettingsSection.Appearance:
                BuildAppearance();
                break;
            case SettingsSection.Drafting:
                BuildDrafting();
                break;
            case SettingsSection.Input:
                BuildInput();
                break;
            case SettingsSection.Files:
                BuildFiles();
                break;
            case SettingsSection.Language:
                BuildLanguage();
                break;
            case SettingsSection.About:
                BuildAbout();
                break;
        }
    }

    private void AddPageHeader(string titleKey, string subtitleKey)
    {
        SettingsContent.Children.Add(new TextBlock
        {
            Text = GetString(titleKey),
            FontSize = 26,
            FontWeight = FontWeights.Bold,
            Foreground = Brush("UcadTextPrimaryBrush")
        });
        SettingsContent.Children.Add(new TextBlock
        {
            Text = GetString(subtitleKey),
            FontSize = 10,
            Foreground = Brush("UcadTextSecondaryBrush"),
            Margin = new Thickness(0, 2, 0, 0)
        });
        SettingsContent.Children.Add(Spacer(TokenDouble("UcadSettingsTitleToSectionSpacing")));
    }

    private void AddSection(string titleKey, params SettingCard[] cards)
    {
        if (_hasSection)
        {
            SettingsContent.Children.Add(Spacer(TokenDouble("UcadSettingsSectionSpacing")));
        }

        SettingsContent.Children.Add(new TextBlock
        {
            Text = GetString(titleKey),
            FontSize = 13,
            FontWeight = FontWeights.SemiBold,
            Foreground = Brush("UcadTextPrimaryBrush")
        });
        SettingsContent.Children.Add(Spacer(TokenDouble("UcadSettingsSectionToCardSpacing")));
        for (var index = 0; index < cards.Length; index++)
        {
            if (index > 0)
            {
                SettingsContent.Children.Add(Spacer(TokenDouble("UcadSettingsCardSpacing")));
            }
            SettingsContent.Children.Add(cards[index]);
        }

        _hasSection = true;
    }

    private SettingCard Card(string titleKey, string descriptionKey, string glyph, UIElement action) => new()
    {
        Title = GetString(titleKey),
        Description = GetString(descriptionKey),
        IconGlyph = glyph,
        ActionContent = action
    };

    private ToggleSwitch Toggle(bool current, Action<bool> changed, bool enabled = true)
    {
        var control = new ToggleSwitch
        {
            IsOn = current,
            OnContent = string.Empty,
            OffContent = string.Empty,
            MinWidth = 0,
            Width = 42,
            IsEnabled = enabled
        };
        control.Toggled += (_, _) =>
        {
            if (!control.IsEnabled)
            {
                return;
            }
            changed(control.IsOn);
            Persist();
        };
        return control;
    }

    private ComboBox Combo(
        string current,
        IEnumerable<(string Value, string LabelKey)> options,
        Action<string> changed,
        double width = 120,
        bool enabled = true,
        ISet<string>? disabledValues = null)
    {
        var control = new ComboBox
        {
            Width = width,
            Height = 30,
            FontSize = 10,
            Background = Brush("UcadControlFillSubtleBrush"),
            BorderBrush = Brush("UcadControlBorderBrush"),
            IsEnabled = enabled
        };

        ComboBoxItem? selected = null;
        foreach (var option in options)
        {
            var item = new ComboBoxItem
            {
                Tag = option.Value,
                Content = GetString(option.LabelKey),
                IsEnabled = disabledValues is null || !disabledValues.Contains(option.Value)
            };
            control.Items.Add(item);
            if (option.Value == current)
            {
                selected = item;
            }
        }
        control.SelectedItem = selected ?? control.Items.FirstOrDefault();
        control.SelectionChanged += (_, _) =>
        {
            if (!control.IsEnabled)
            {
                return;
            }
            if (control.SelectedItem is ComboBoxItem { Tag: string value, IsEnabled: true })
            {
                changed(value);
                Persist();
            }
        };
        return control;
    }

    private Button ActionButton(string labelKey, Action action, double width = 76, bool enabled = true)
    {
        var button = new Button
        {
            Content = GetString(labelKey),
            Width = width,
            Height = 30,
            Padding = new Thickness(10, 0, 10, 0),
            FontSize = 10,
            HorizontalContentAlignment = HorizontalAlignment.Center,
            IsEnabled = enabled
        };
        button.Click += (_, _) =>
        {
            if (button.IsEnabled)
            {
                action();
            }
        };
        return button;
    }

    private void Persist()
    {
        _service.Save();
        SettingsChanged?.Invoke(this, EventArgs.Empty);
    }

    private void BuildGeneral()
    {
        var s = _service.Settings;
        AddPageHeader("Settings_General_Title", "Settings_General_Subtitle");
        AddSection("Settings_General_StartupSection",
            Card("Settings_General_StartupBehavior_Title", "Settings_General_StartupBehavior_Description", "\uE7E8",
                Combo(s.StartupBehavior,
                    [("StartPage", "Settings_Option_StartPage"), ("BlankDrawing", "Settings_Option_BlankDrawing"), ("RestoreSession", "Settings_Option_RestoreSession")],
                    value => s.StartupBehavior = value,
                    142,
                    disabledValues: new HashSet<string>(StringComparer.Ordinal) { "RestoreSession" })),
            Card("Settings_General_NewTab_Title", "Settings_General_NewTab_Description", "\uE710",
                Toggle(s.ShowStartOnNewTab, value => s.ShowStartOnNewTab = value)),
            Card("Settings_General_CloseUnsaved_Title", "Settings_General_CloseUnsaved_Description", "\uE8BB",
                Toggle(s.ConfirmUnsaved, value => s.ConfirmUnsaved = value)));
        AddSection("Settings_General_UpdatesSection",
            Card("Settings_General_AutoUpdate_Title", "Settings_General_AutoUpdate_Description", "\uE895",
                Toggle(s.AutoCheckUpdates, value => s.AutoCheckUpdates = value, enabled: false)),
            Card("Settings_General_CheckUpdate_Title", "Settings_General_CheckUpdate_Description", "\uE72C",
                ActionButton("Settings_Action_CheckNow", () => CheckUpdatesRequested?.Invoke(this, EventArgs.Empty), 92)));
        SettingsContent.Children.Add(Spacer(20));
        SettingsContent.Children.Add(new TextBlock
        {
            Text = AppVersionInfo.ProductDisplayVersion,
            FontSize = 9,
            Foreground = Brush("UcadTextTertiaryBrush")
        });
    }

    private void BuildAppearance()
    {
        var s = _service.Settings;
        AddPageHeader("Settings_Appearance_Title", "Settings_Appearance_Subtitle");
        AddSection("Settings_Appearance_ThemeSection",
            Card("Settings_Appearance_AppTheme_Title", "Settings_Appearance_AppTheme_Description", "\uE790",
                Combo(s.AppTheme, [("System", "Settings_Option_System"), ("Dark", "Settings_Option_Dark"), ("Light", "Settings_Option_Light")], value => s.AppTheme = value, 132)),
            Card("Settings_Appearance_CanvasTheme_Title", "Settings_Appearance_CanvasTheme_Description", "\uE771",
                Combo(s.CanvasTheme, [("Dark", "Settings_Option_Dark"), ("Light", "Settings_Option_Light")], value => s.CanvasTheme = value, 110)),
            Card("Settings_Appearance_CanvasBackground_Title", "Settings_Appearance_CanvasBackground_Description", "\uE2B1",
                Combo(s.CanvasBackground, [("#0E1012", "Settings_Option_CanvasBlack"), ("#18181A", "Settings_Option_CanvasGraphite"), ("#FFFFFF", "Settings_Option_CanvasWhite")], value => s.CanvasBackground = value, 108)));
        AddSection("Settings_Appearance_CanvasSection",
            Card("Settings_Appearance_ShowGrid_Title", "Settings_Appearance_ShowGrid_Description", "\uE80A",
                Toggle(s.ShowGrid, value => s.ShowGrid = value)),
            Card("Settings_Appearance_GridOpacity_Title", "Settings_Appearance_GridOpacity_Description", "\uE91F",
                Combo(s.GridOpacity.ToString(), [("10", "Settings_Option_Opacity10"), ("22", "Settings_Option_Opacity22"), ("50", "Settings_Option_Opacity50"), ("75", "Settings_Option_Opacity75")], value => s.GridOpacity = int.Parse(value), 86)),
            Card("Settings_Appearance_UiScale_Title", "Settings_Appearance_UiScale_Description", "\uE740",
                Combo(s.UiScale, [("System", "Settings_Option_System"), ("100", "Settings_Option_Scale100"), ("125", "Settings_Option_Scale125"), ("150", "Settings_Option_Scale150")], value => s.UiScale = value, 132, enabled: false)));
    }

    private void BuildDrafting()
    {
        var s = _service.Settings;
        AddPageHeader("Settings_Drafting_Title", "Settings_Drafting_Subtitle");
        AddSection("Settings_Drafting_DefaultsSection",
            Card("Settings_Drafting_LengthUnit_Title", "Settings_Drafting_LengthUnit_Description", "\uE8F4",
                Combo(s.LengthUnit, [("Millimeters", "Settings_Option_Millimeters"), ("Meters", "Settings_Option_Meters"), ("Inches", "Settings_Option_Inches")], value => s.LengthUnit = value, 132)),
            Card("Settings_Drafting_Precision_Title", "Settings_Drafting_Precision_Description", "\uE9D2",
                Combo(s.Precision, [("0", "Settings_Option_Precision0"), ("0.0", "Settings_Option_Precision1"), ("0.00", "Settings_Option_Precision2"), ("0.000", "Settings_Option_Precision3")], value => s.Precision = value, 90)),
            Card("Settings_Drafting_AngleUnit_Title", "Settings_Drafting_AngleUnit_Description", "\uE7F8",
                Combo(s.AngleUnit, [("DecimalDegrees", "Settings_Option_DecimalDegrees"), ("Radians", "Settings_Option_Radians")], value => s.AngleUnit = value, 142)));
        AddSection("Settings_Drafting_AidsSection",
            Card("Settings_Drafting_ObjectSnap_Title", "Settings_Drafting_ObjectSnap_Description", "\uE81E",
                Toggle(s.DefaultObjectSnap, value => s.DefaultObjectSnap = value)),
            Card("Settings_Drafting_SnapTypes_Title", "Settings_Drafting_SnapTypes_Description", "\uEA3A",
                Combo(s.DefaultSnapTypes, [("EndpointMidpointIntersection", "Settings_Option_SnapCore"), ("EndpointMidpoint", "Settings_Option_SnapBasic")], value => s.DefaultSnapTypes = value, 188)),
            Card("Settings_Drafting_Ortho_Title", "Settings_Drafting_Ortho_Description", "\uE809",
                Toggle(s.DefaultOrtho, value => s.DefaultOrtho = value)));
        SettingsContent.Children.Add(Spacer(TokenDouble("UcadSettingsSectionToCardSpacing")));
        SettingsContent.Children.Add(InfoNote("Settings_Drafting_PendingNote"));
    }

    private void BuildInput()
    {
        var s = _service.Settings;
        AddPageHeader("Settings_Input_Title", "Settings_Input_Subtitle");
        AddSection("Settings_Input_MouseSection",
            Card("Settings_Input_ZoomCursor_Title", "Settings_Input_ZoomCursor_Description", "\uE71E",
                Toggle(s.ZoomAroundCursor, value => s.ZoomAroundCursor = value)),
            Card("Settings_Input_MiddlePan_Title", "Settings_Input_MiddlePan_Description", "\uE7C2",
                Toggle(s.MiddleMousePan, value => s.MiddleMousePan = value)),
            Card("Settings_Input_ReverseWheel_Title", "Settings_Input_ReverseWheel_Description", "\uE72A",
                Toggle(s.ReverseWheelZoom, value => s.ReverseWheelZoom = value)));
        AddSection("Settings_Input_SelectionSection",
            Card("Settings_Input_WindowCrossing_Title", "Settings_Input_WindowCrossing_Description", "\uE7C7",
                Combo(s.WindowCrossingSelection, [("CadStandard", "Settings_Option_CadStandard")], value => s.WindowCrossingSelection = value, 112)),
            Card("Settings_Input_SelectionPreview_Title", "Settings_Input_SelectionPreview_Description", "\uE890",
                Toggle(s.SelectionPreview, value => s.SelectionPreview = value)));
        AddSection("Settings_Input_CommandSection",
            Card("Settings_Input_CommandSuggestions_Title", "Settings_Input_CommandSuggestions_Description", "\uE721",
                Toggle(s.CommandSuggestions, value => s.CommandSuggestions = value)));
    }

    private void BuildFiles()
    {
        var s = _service.Settings;
        AddPageHeader("Settings_Files_Title", "Settings_Files_Subtitle");
        AddSection("Settings_Files_AutoSaveSection",
            Card("Settings_Files_AutoSave_Title", "Settings_Files_AutoSave_Description", "\uE74E",
                Toggle(s.AutoSave, value => s.AutoSave = value)),
            Card("Settings_Files_AutoSaveInterval_Title", "Settings_Files_AutoSaveInterval_Description", "\uE823",
                Combo(s.AutoSaveIntervalMinutes.ToString(), [("5", "Settings_Option_Minutes5"), ("10", "Settings_Option_Minutes10"), ("15", "Settings_Option_Minutes15"), ("30", "Settings_Option_Minutes30")], value => s.AutoSaveIntervalMinutes = int.Parse(value), 104)),
            Card("Settings_Files_Backup_Title", "Settings_Files_Backup_Description", "\uE8B7",
                Toggle(s.BackupOnSave, value => s.BackupOnSave = value)));
        AddSection("Settings_Files_RecentSection",
            Card("Settings_Files_ShowRecent_Title", "Settings_Files_ShowRecent_Description", "\uE8EF",
                Toggle(s.ShowRecentFiles, value => s.ShowRecentFiles = value)),
            Card("Settings_Files_RecentCount_Title", "Settings_Files_RecentCount_Description", "\uE9D2",
                Combo(s.RecentFileCount.ToString(), [("10", "Settings_Option_Count10"), ("20", "Settings_Option_Count20"), ("30", "Settings_Option_Count30")], value => s.RecentFileCount = int.Parse(value), 82)),
            Card("Settings_Files_ClearRecent_Title", "Settings_Files_ClearRecent_Description", "\uE74D",
                ActionButton("Settings_Action_Clear", _service.ResetRecentHistory, 72, enabled: false)));
    }

    private void BuildLanguage()
    {
        var s = _service.Settings;
        AddPageHeader("Settings_Language_Title", "Settings_Language_Subtitle");

        ComboBox? displayLanguage = null;
        displayLanguage = Combo(
            s.DisplayLanguage,
            [("System", "Settings_Option_SystemLanguage"), ("zh-CN", "Settings_Option_Chinese"), ("ja-JP", "Settings_Option_Japanese"), ("en-US", "Settings_Option_English")],
            value =>
            {
                s.DisplayLanguage = value;
                LanguageChanged?.Invoke(this, EventArgs.Empty);
            },
            132,
            enabled: !s.FollowSystemLanguage);

        var followSystem = Toggle(s.FollowSystemLanguage, value =>
        {
            s.FollowSystemLanguage = value;
            if (displayLanguage is not null)
            {
                displayLanguage.IsEnabled = !value;
            }
            LanguageChanged?.Invoke(this, EventArgs.Empty);
        });

        AddSection("Settings_Language_LanguageSection",
            Card("Settings_Language_Display_Title", "Settings_Language_Display_Description", "\uF2B7", displayLanguage),
            Card("Settings_Language_FollowSystem_Title", "Settings_Language_FollowSystem_Description", "\uE774", followSystem));
        AddSection("Settings_Language_RegionalSection",
            Card("Settings_Language_NumberFormat_Title", "Settings_Language_NumberFormat_Description", "\uE9D2",
                Combo(s.NumberFormat, [("System", "Settings_Option_SystemRegion"), ("Invariant", "Settings_Option_InvariantRegion")], value => s.NumberFormat = value, 142)),
            Card("Settings_Language_UnitDisplay_Title", "Settings_Language_UnitDisplay_Description", "\uE8F4",
                Combo(s.UnitDisplay, [("Metric", "Settings_Option_Metric"), ("Imperial", "Settings_Option_Imperial")], value => s.UnitDisplay = value, 104)),
            Card("Settings_Language_AngleDecimal_Title", "Settings_Language_AngleDecimal_Description", "\uE8C1",
                Combo(s.AngleDecimalFormat, [("Automatic", "Settings_Option_Automatic"), ("Dot", "Settings_Option_DecimalDot"), ("Comma", "Settings_Option_DecimalComma")], value => s.AngleDecimalFormat = value, 104)));
        SettingsContent.Children.Add(Spacer(TokenDouble("UcadSettingsCardSpacing")));
        SettingsContent.Children.Add(InfoNote("Settings_Language_ReloadNote"));
    }

    private void BuildAbout()
    {
        AddPageHeader("Settings_About_Title", "Settings_About_Subtitle");
        SettingsContent.Children.Add(new TextBlock
        {
            Text = GetString("Settings_About_AppInfoSection"),
            FontSize = 13,
            FontWeight = FontWeights.SemiBold,
            Foreground = Brush("UcadTextPrimaryBrush")
        });
        SettingsContent.Children.Add(Spacer(TokenDouble("UcadSettingsSectionToCardSpacing")));
        SettingsContent.Children.Add(BuildAppInfoCard());
        _hasSection = true;

        AddAboutSection("Settings_About_ProjectSection",
            AboutRow("\uE943", "Settings_About_GitHub_Title", "Settings_About_GitHub_Description", "Settings_Action_Open", "https://github.com/KiYouJyo/UCAD"),
            AboutRow("\uE895", "Settings_About_Releases_Title", "Settings_About_Releases_Description", "Settings_Action_View", "https://github.com/KiYouJyo/UCAD/releases"),
            AboutRow("\uE783", "Settings_About_Issue_Title", "Settings_About_Issue_Description", "Settings_Action_Open", "https://github.com/KiYouJyo/UCAD/issues"));
        AddAboutSection("Settings_About_OpenSourceSection",
            AboutRow("\uE943", "Settings_About_OpenSource_Title", "Settings_About_OpenSource_Description", "Settings_Action_View", "https://github.com/KiYouJyo/UCAD"),
            AboutRow("\uE734", "Settings_About_Credits_Title", "Settings_About_Credits_Description", null, null));
        SettingsContent.Children.Add(Spacer(TokenDouble("UcadSettingsSectionToCardSpacing")));
        SettingsContent.Children.Add(new TextBlock
        {
            Text = "© UCAD Project",
            FontSize = 9,
            Foreground = Brush("UcadTextTertiaryBrush")
        });
    }

    private Border BuildAppInfoCard()
    {
        var grid = new Grid { Padding = new Thickness(20, 0, 20, 0) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(64) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(18) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(110) });

        var mark = new Border
        {
            Width = 64,
            Height = 64,
            VerticalAlignment = VerticalAlignment.Center,
            Background = Brush("UcadAboutMarkBrush"),
            CornerRadius = new CornerRadius(12),
            Child = new TextBlock
            {
                Text = "UA",
                FontSize = 20,
                FontWeight = FontWeights.Bold,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = new SolidColorBrush(Microsoft.UI.Colors.White)
            }
        };
        grid.Children.Add(mark);

        var text = new StackPanel { Spacing = 4, VerticalAlignment = VerticalAlignment.Center };
        Grid.SetColumn(text, 2);
        text.Children.Add(new TextBlock { Text = "UCAD", FontSize = 20, FontWeight = FontWeights.Bold, Foreground = Brush("UcadTextPrimaryBrush") });
        text.Children.Add(new TextBlock { Text = GetString("Settings_About_AppDescription"), FontSize = 10, Foreground = Brush("UcadTextSecondaryBrush") });
        text.Children.Add(new TextBlock { Text = "WinUI 3 · .NET · Windows", FontSize = 9, Foreground = Brush("UcadTextTertiaryBrush") });
        grid.Children.Add(text);

        var version = new StackPanel { Spacing = 6, VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Right };
        Grid.SetColumn(version, 3);
        version.Children.Add(new Border
        {
            Background = Brush("UcadControlFillSubtleBrush"),
            BorderBrush = Brush("UcadControlBorderBrush"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(10, 6, 10, 6),
            Child = new TextBlock { Text = AppVersionInfo.DisplayVersion, FontSize = 10, Foreground = Brush("UcadTextPrimaryBrush") }
        });
        version.Children.Add(new Border
        {
            Background = Brush("UcadAccentBrush"),
            CornerRadius = new CornerRadius(999),
            Padding = new Thickness(10, 5, 10, 5),
            HorizontalAlignment = HorizontalAlignment.Right,
            Child = new TextBlock { Text = GetString("Settings_About_Preview"), FontSize = 9, Foreground = Brush("UcadAccentTextBrush") }
        });
        grid.Children.Add(version);

        return new Border
        {
            Width = TokenDouble("UcadSettingsCardWidth"),
            Height = TokenDouble("UcadSettingsAboutCardHeight"),
            Background = Brush("UcadAboutCardBrush"),
            BorderBrush = Brush("UcadAboutCardBorderBrush"),
            BorderThickness = new Thickness(1),
            CornerRadius = (CornerRadius)Application.Current.Resources["UcadRadiusAboutCard"],
            Child = grid
        };
    }

    private void AddAboutSection(string titleKey, params Border[] rows)
    {
        SettingsContent.Children.Add(Spacer(TokenDouble("UcadSettingsSectionSpacing")));
        SettingsContent.Children.Add(new TextBlock
        {
            Text = GetString(titleKey),
            FontSize = 13,
            FontWeight = FontWeights.SemiBold,
            Foreground = Brush("UcadTextPrimaryBrush")
        });
        SettingsContent.Children.Add(Spacer(TokenDouble("UcadSettingsSectionToCardSpacing")));
        for (var i = 0; i < rows.Length; i++)
        {
            if (i > 0) SettingsContent.Children.Add(Spacer(TokenDouble("UcadSettingsCardSpacing")));
            SettingsContent.Children.Add(rows[i]);
        }
    }

    private Border AboutRow(string glyph, string titleKey, string descriptionKey, string? actionKey, string? uri)
    {
        var grid = new Grid { Padding = new Thickness(16, 0, 12, 0) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(36) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(14) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.Children.Add(new Border
        {
            Width = 36,
            Height = 36,
            VerticalAlignment = VerticalAlignment.Center,
            Background = Brush("UcadControlFillSubtleBrush"),
            CornerRadius = new CornerRadius(5),
            Child = new FontIcon { FontFamily = new FontFamily("Segoe Fluent Icons"), Glyph = glyph, FontSize = 15, Foreground = Brush("UcadTextSecondaryBrush") }
        });
        var text = new StackPanel { Spacing = 2, VerticalAlignment = VerticalAlignment.Center };
        Grid.SetColumn(text, 2);
        text.Children.Add(new TextBlock { Text = GetString(titleKey), FontSize = 12, FontWeight = FontWeights.SemiBold, Foreground = Brush("UcadTextPrimaryBrush") });
        text.Children.Add(new TextBlock { Text = GetString(descriptionKey), FontSize = 9, Foreground = Brush("UcadTextSecondaryBrush") });
        grid.Children.Add(text);
        if (actionKey is not null && uri is not null)
        {
            var button = ActionButton(actionKey, () => _ = Launcher.LaunchUriAsync(new Uri(uri)), 68);
            Grid.SetColumn(button, 3);
            grid.Children.Add(button);
        }
        return new Border
        {
            Width = TokenDouble("UcadSettingsCardWidth"),
            Height = TokenDouble("UcadSettingsCardHeight"),
            Background = Brush("UcadAboutCardBrush"),
            BorderBrush = Brush("UcadAboutCardBorderBrush"),
            BorderThickness = new Thickness(1),
            CornerRadius = (CornerRadius)Application.Current.Resources["UcadRadiusAboutCard"],
            Child = grid
        };
    }

    private Border InfoNote(string textKey)
    {
        var grid = new Grid { Padding = new Thickness(14, 0, 14, 0) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(22) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.Children.Add(new FontIcon
        {
            FontFamily = new FontFamily("Segoe Fluent Icons"),
            Glyph = "\uE946",
            FontSize = 14,
            Foreground = Brush("UcadAccentTextBrush"),
            VerticalAlignment = VerticalAlignment.Center
        });
        var text = new TextBlock
        {
            Text = GetString(textKey),
            FontSize = 10,
            Foreground = Brush("UcadTextSecondaryBrush"),
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(text, 1);
        grid.Children.Add(text);
        return new Border
        {
            Width = TokenDouble("UcadSettingsCardWidth"),
            Height = 54,
            Background = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 23, 31, 36)),
            BorderBrush = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 41, 77, 97)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Child = grid
        };
    }

    private static Border Spacer(double height) => new() { Height = height };

    private static double TokenDouble(string key) => (double)Application.Current.Resources[key];

    private static Brush Brush(string key) => (Brush)Application.Current.Resources[key];
}

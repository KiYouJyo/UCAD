using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using UCAD.Services;

namespace UCAD;

public sealed partial class MainWindow
{
    private bool _pr19AcceptanceUiInitialized;
    private bool _normalizingPr19Buttons;
    private long _settingsButtonBackgroundChangedToken;

    internal void EnsurePr19AcceptanceUiInitialized()
    {
        if (_pr19AcceptanceUiInitialized) return;
        _pr19AcceptanceUiInitialized = true;

        RootLayout.ActualThemeChanged += Pr19_RootLayoutActualThemeChanged;
        SettingsService.Current.SettingsChanged += Pr19_SettingsChanged;
        DocumentTabs.SelectionChanged += Pr19_DocumentTabsSelectionChanged;
        DocumentTabs.AddTabButtonClick += Pr19_DocumentTabsAddTabButtonClick;
        SettingsButton.Click += Pr19_ButtonVisualStateChanged;
        foreach (var button in CategoryButtons)
        {
            button.Click += Pr19_ButtonVisualStateChanged;
        }

        _settingsButtonBackgroundChangedToken = SettingsButton.RegisterPropertyChangedCallback(
            Control.BackgroundProperty,
            Pr19_SettingsButtonBackgroundChanged);

        ApplyPr19ShellPalette(IsLightShellTheme());
        ApplyPr19TabContract();
        NormalizePr19ButtonVisuals();

        RootLayout.Loaded += Pr19_RootLayoutLoaded;
    }

    private void Pr19_RootLayoutLoaded(object sender, RoutedEventArgs e)
    {
        RootLayout.Loaded -= Pr19_RootLayoutLoaded;
        ApplyPr19TabContract();
        NormalizePr19ButtonVisuals();

        if (!string.Equals(Environment.GetEnvironmentVariable("UCAD_STARTUP_SMOKE"), "1", StringComparison.Ordinal))
        {
            return;
        }

        RunPr19VisualThemeSmoke();
    }

    private void Pr19_RootLayoutActualThemeChanged(FrameworkElement sender, object args)
    {
        if (string.Equals(SettingsService.Current.Settings.AppTheme, "System", StringComparison.Ordinal))
        {
            ApplyPr19ShellPalette(sender.ActualTheme == ElementTheme.Light);
            NormalizePr19ButtonVisuals();
        }
    }

    private void Pr19_SettingsChanged(object? sender, EventArgs e)
    {
        ApplyPr19ShellPalette(IsLightShellTheme());
        NormalizePr19ButtonVisuals();
    }

    private void Pr19_DocumentTabsSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        ApplyPr19TabContract();
        NormalizePr19ButtonVisuals();
    }

    private void Pr19_DocumentTabsAddTabButtonClick(TabView sender, object args)
    {
        ApplyPr19TabContract();
        NormalizePr19ButtonVisuals();
    }

    private void Pr19_ButtonVisualStateChanged(object sender, RoutedEventArgs e)
    {
        RootLayout.DispatcherQueue.TryEnqueue(NormalizePr19ButtonVisuals);
    }

    private void Pr19_SettingsButtonBackgroundChanged(DependencyObject sender, DependencyProperty dp)
    {
        if (_normalizingPr19Buttons) return;
        RootLayout.DispatcherQueue.TryEnqueue(NormalizePr19ButtonVisuals);
    }

    private void NormalizePr19ButtonVisuals()
    {
        if (_normalizingPr19Buttons || RootLayout.XamlRoot is null) return;
        _normalizingPr19Buttons = true;
        try
        {
            // Remove local/custom chrome so WinUI's native Button/ToggleButton theme style
            // owns normal, pointer-over, pressed, checked and disabled states. Layout and
            // icon/content properties are intentionally preserved.
            foreach (var button in Descendants<ButtonBase>(RootLayout))
            {
                button.ClearValue(Control.BackgroundProperty);
                button.ClearValue(Control.ForegroundProperty);
                button.ClearValue(Control.BorderBrushProperty);
                button.ClearValue(Control.BorderThicknessProperty);
            }
        }
        finally
        {
            _normalizingPr19Buttons = false;
        }
    }

    private void ApplyPr19TabContract()
    {
        var width = (double)Application.Current.Resources["UcadDocumentTabWidth"];
        var height = (double)Application.Current.Resources["UcadDocumentTabHeight"];

        DocumentTabs.Height = 60;
        DocumentTabs.VerticalAlignment = VerticalAlignment.Stretch;

        if (DocumentTabs.Parent is Grid titleBar)
        {
            titleBar.Height = 60;
            titleBar.Padding = new Thickness(12, 0, 138, 0);

            var brand = titleBar.Children.OfType<StackPanel>().FirstOrDefault();
            if (brand is not null)
            {
                brand.Width = 220;
                brand.Spacing = 12;
                brand.Margin = new Thickness(8, 0, 12, 0);

                if (brand.Children.OfType<Border>().FirstOrDefault() is Border mark)
                {
                    mark.Width = 40;
                    mark.Height = 40;
                    mark.CornerRadius = new CornerRadius(10);
                    if (mark.Child is TextBlock markText) markText.FontSize = 13;
                }

                var wordmark = brand.Children.OfType<TextBlock>().FirstOrDefault(text => string.Equals(text.Text, "UCAD", StringComparison.Ordinal));
                if (wordmark is not null) wordmark.FontSize = 18;
            }
        }

        foreach (var tab in DocumentTabs.TabItems.OfType<TabViewItem>())
        {
            tab.Width = width;
            tab.Height = height;
            tab.MinWidth = width;
            tab.CornerRadius = new CornerRadius(12);
            tab.Margin = new Thickness(5, 6, 5, 6);
            tab.Padding = new Thickness(18, 0, 12, 0);
            tab.VerticalContentAlignment = VerticalAlignment.Center;
        }

        var desired = 52 + (DocumentTabs.TabItems.Count * (width + 10));
        DocumentTabs.Width = Math.Clamp(desired, width + 52, 1120);
    }

    private static void ApplyPr19ShellPalette(bool light)
    {
        if (light)
        {
            SetBrushColor("UcadAppBackgroundBrush", 255, 241, 248, 248);
            SetBrushColor("UcadTitleBarBrush", 255, 247, 251, 251);
            SetBrushColor("UcadCategoryBarBrush", 255, 234, 245, 244);
            SetBrushColor("UcadToolShelfBrush", 255, 243, 249, 249);
            SetBrushColor("UcadNavigationBrush", 255, 238, 247, 246);
            SetBrushColor("UcadCardBrush", 255, 255, 255, 255);
            SetBrushColor("UcadAboutCardBrush", 255, 250, 253, 253);
            SetBrushColor("UcadPanelBrush", 255, 244, 250, 249);
            SetBrushColor("UcadOverlayBrush", 255, 241, 248, 248);
            SetBrushColor("UcadStatusBarBrush", 255, 234, 245, 244);
            SetBrushColor("UcadControlFillBrush", 255, 255, 255, 255);
            SetBrushColor("UcadControlFillSubtleBrush", 255, 240, 247, 246);
            SetBrushColor("UcadControlFillStrongBrush", 255, 228, 240, 239);
            SetBrushColor("UcadTextPrimaryBrush", 255, 22, 48, 50);
            SetBrushColor("UcadTextSecondaryBrush", 255, 71, 96, 98);
            SetBrushColor("UcadTextTertiaryBrush", 255, 99, 124, 126);
            SetBrushColor("UcadTextDisabledBrush", 255, 148, 165, 166);
            SetBrushColor("UcadAccentBrush", 255, 58, 122, 126);
            SetBrushColor("UcadAccentSelectedBrush", 255, 47, 110, 114);
            SetBrushColor("UcadAboutMarkBrush", 255, 47, 110, 114);
            SetBrushColor("UcadAccentHoverBrush", 255, 75, 143, 147);
            SetBrushColor("UcadAccentTextBrush", 255, 35, 105, 109);
            SetBrushColor("UcadAccentStrokeBrush", 89, 47, 110, 114);
            SetBrushColor("UcadCategorySelectedBrush", 255, 218, 237, 236);
            SetBrushColor("UcadAccentBrightBrush", 255, 58, 122, 126);
            SetBrushColor("UcadDividerBrush", 255, 199, 217, 216);
            SetBrushColor("UcadCardBorderBrush", 153, 182, 205, 204);
            SetBrushColor("UcadStartActionBorderBrush", 191, 174, 201, 199);
            SetBrushColor("UcadStartTemplateBorderBrush", 128, 174, 201, 199);
            SetBrushColor("UcadAboutCardBorderBrush", 255, 190, 211, 210);
            SetBrushColor("UcadControlBorderBrush", 255, 182, 205, 204);
            SetBrushColor("UcadDividerSoftBrush", 255, 218, 232, 231);
        }
        else
        {
            SetBrushColor("UcadAppBackgroundBrush", 255, 15, 29, 31);
            SetBrushColor("UcadTitleBarBrush", 255, 16, 36, 38);
            SetBrushColor("UcadCategoryBarBrush", 255, 19, 48, 51);
            SetBrushColor("UcadToolShelfBrush", 255, 16, 40, 42);
            SetBrushColor("UcadNavigationBrush", 255, 16, 37, 39);
            SetBrushColor("UcadCardBrush", 255, 21, 50, 53);
            SetBrushColor("UcadAboutCardBrush", 255, 20, 48, 51);
            SetBrushColor("UcadPanelBrush", 255, 17, 41, 43);
            SetBrushColor("UcadOverlayBrush", 255, 15, 29, 31);
            SetBrushColor("UcadStatusBarBrush", 255, 19, 48, 51);
            SetBrushColor("UcadControlFillBrush", 255, 29, 58, 61);
            SetBrushColor("UcadControlFillSubtleBrush", 255, 23, 50, 53);
            SetBrushColor("UcadControlFillStrongBrush", 255, 20, 48, 51);
            SetBrushColor("UcadTextPrimaryBrush", 255, 240, 247, 247);
            SetBrushColor("UcadTextSecondaryBrush", 255, 169, 187, 188);
            SetBrushColor("UcadTextTertiaryBrush", 255, 130, 153, 155);
            SetBrushColor("UcadTextDisabledBrush", 255, 98, 119, 122);
            SetBrushColor("UcadAccentBrush", 255, 46, 111, 115);
            SetBrushColor("UcadAccentSelectedBrush", 255, 57, 127, 131);
            SetBrushColor("UcadAboutMarkBrush", 255, 57, 127, 131);
            SetBrushColor("UcadAccentHoverBrush", 255, 73, 143, 147);
            SetBrushColor("UcadAccentTextBrush", 255, 112, 193, 196);
            SetBrushColor("UcadAccentStrokeBrush", 89, 112, 193, 196);
            SetBrushColor("UcadCategorySelectedBrush", 255, 36, 72, 75);
            SetBrushColor("UcadAccentBrightBrush", 255, 112, 193, 196);
            SetBrushColor("UcadDividerBrush", 255, 53, 80, 82);
            SetBrushColor("UcadCardBorderBrush", 153, 80, 106, 108);
            SetBrushColor("UcadStartActionBorderBrush", 191, 80, 106, 108);
            SetBrushColor("UcadStartTemplateBorderBrush", 128, 80, 106, 108);
            SetBrushColor("UcadAboutCardBorderBrush", 255, 80, 106, 108);
            SetBrushColor("UcadControlBorderBrush", 255, 80, 106, 108);
            SetBrushColor("UcadDividerSoftBrush", 255, 38, 63, 65);
        }
    }

    private void RunPr19VisualThemeSmoke()
    {
        var canvas = ((SolidColorBrush)Application.Current.Resources["UcadCanvasBrush"]).Color;

        ApplyPr19ShellPalette(light: true);
        AssertPr19Brush("UcadTitleBarBrush", 255, 247, 251, 251, "light title bar");
        AssertPr19Brush("UcadCategoryBarBrush", 255, 234, 245, 244, "light category bar");
        AssertCanvasUnchanged(canvas, "light theme");

        ApplyPr19ShellPalette(light: false);
        AssertPr19Brush("UcadTitleBarBrush", 255, 16, 36, 38, "dark title bar");
        AssertPr19Brush("UcadCategoryBarBrush", 255, 19, 48, 51, "dark category bar");
        AssertCanvasUnchanged(canvas, "dark theme");

        ApplyPr19ShellPalette(IsLightShellTheme());
        ApplyPr19TabContract();
        NormalizePr19ButtonVisuals();

        var tabWidth = (double)Application.Current.Resources["UcadDocumentTabWidth"];
        var tabHeight = (double)Application.Current.Resources["UcadDocumentTabHeight"];
        if (tabWidth < 260 || tabHeight < 46 || DocumentTabs.Height < 58)
        {
            throw new InvalidOperationException($"PR19 Figma tab metrics are too compact: {tabWidth}x{tabHeight}, strip={DocumentTabs.Height}.");
        }

        if (DocumentTabs.TabItems.OfType<TabViewItem>().Any(tab => tab.CornerRadius.TopLeft < 10))
        {
            throw new InvalidOperationException("PR19 document tabs are missing the rounded Figma contract.");
        }

        App.WriteStartupEvent("PR19 UI smoke: Figma tabs + native Fluent button chrome + light/dark teal shell + canvas isolation passed");
    }

    private static void AssertPr19Brush(string key, byte a, byte r, byte g, byte b, string scope)
    {
        if (Application.Current.Resources[key] is not SolidColorBrush brush ||
            brush.Color.A != a || brush.Color.R != r || brush.Color.G != g || brush.Color.B != b)
        {
            throw new InvalidOperationException($"PR19 {scope} palette mismatch for {key}.");
        }
    }

    private static void AssertCanvasUnchanged(Windows.UI.Color expected, string scope)
    {
        if (Application.Current.Resources["UcadCanvasBrush"] is not SolidColorBrush brush || brush.Color != expected)
        {
            throw new InvalidOperationException($"PR19 {scope} changed the CAD canvas palette.");
        }
    }
}

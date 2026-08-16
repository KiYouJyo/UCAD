using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using UCAD.Controls;
using UCAD.Services;

namespace UCAD.Views;

public sealed partial class UcadSettingsPage
{
    private const string CadPointerSettingsTag = "UCAD.CadPointerSettings";
    private bool _cadPointerSettingsExtensionInitialized;

    protected override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        if (_cadPointerSettingsExtensionInitialized)
        {
            return;
        }

        _cadPointerSettingsExtensionInitialized = true;
        SettingsContent.LayoutUpdated += SettingsContent_CadPointerLayoutUpdated;
    }

    private void SettingsContent_CadPointerLayoutUpdated(object? sender, object e)
    {
        if (_section != SettingsSection.Input || HasCadPointerSettings())
        {
            return;
        }

        AppendCadPointerSettings();
    }

    private bool HasCadPointerSettings() =>
        SettingsContent.Children
            .OfType<FrameworkElement>()
            .Any(element => string.Equals(element.Tag?.ToString(), CadPointerSettingsTag, StringComparison.Ordinal));

    private void AppendCadPointerSettings()
    {
        var settings = _service.Settings;
        var crosshair = CadPointerCard(
            CadPointerString("CrosshairTitle"),
            CadPointerString("CrosshairDescription"),
            "\uE7F8",
            NumericSlider(
                settings.CrosshairSizePercent,
                5,
                100,
                5,
                "%",
                value => settings.CrosshairSizePercent = value));
        crosshair.Tag = CadPointerSettingsTag;

        AddCadPointerSection(
            CadPointerString("Section"),
            crosshair,
            CadPointerCard(
                CadPointerString("PickboxTitle"),
                CadPointerString("PickboxDescription"),
                "\uE8A7",
                NumericSlider(
                    settings.PickboxSize,
                    3,
                    20,
                    1,
                    " px",
                    value => settings.PickboxSize = value)),
            CadPointerCard(
                CadPointerString("ApertureTitle"),
                CadPointerString("ApertureDescription"),
                "\uE81E",
                NumericSlider(
                    settings.ObjectSnapAperture,
                    3,
                    50,
                    1,
                    " px",
                    value => settings.ObjectSnapAperture = value)));
    }

    private SettingCard CadPointerCard(string title, string description, string glyph, UIElement action) => new()
    {
        Title = title,
        Description = description,
        IconGlyph = glyph,
        ActionContent = action
    };

    private void AddCadPointerSection(string title, params SettingCard[] cards)
    {
        if (_hasSection)
        {
            SettingsContent.Children.Add(Spacer(TokenDouble("UcadSettingsSectionSpacing")));
        }

        SettingsContent.Children.Add(new TextBlock
        {
            Text = title,
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

    private UIElement NumericSlider(
        int current,
        int minimum,
        int maximum,
        int step,
        string suffix,
        Action<int> changed)
    {
        var grid = new Grid
        {
            Width = 210,
            ColumnSpacing = 8
        };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(150) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(52) });

        var valueText = new TextBlock
        {
            Text = $"{current}{suffix}",
            FontSize = 10,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Right,
            Foreground = Brush("UcadTextSecondaryBrush")
        };
        Grid.SetColumn(valueText, 1);

        var slider = new Slider
        {
            Minimum = minimum,
            Maximum = maximum,
            Value = Math.Clamp(current, minimum, maximum),
            StepFrequency = step,
            SnapsTo = SliderSnapsTo.StepValues,
            MinWidth = 0,
            Width = 150,
            VerticalAlignment = VerticalAlignment.Center
        };
        slider.ValueChanged += (_, args) =>
        {
            var value = Math.Clamp((int)Math.Round(args.NewValue), minimum, maximum);
            valueText.Text = $"{value}{suffix}";
            changed(value);
            Persist();
        };

        grid.Children.Add(slider);
        grid.Children.Add(valueText);
        return grid;
    }

    private static string CadPointerString(string key)
    {
        var language = LocalizationService.Current.CurrentLanguageTag;
        return (language, key) switch
        {
            ("ja-JP", "Section") => "CAD カーソル",
            ("ja-JP", "CrosshairTitle") => "クロスヘアのサイズ",
            ("ja-JP", "CrosshairDescription") => "作図領域に対するクロスヘアの長さを調整します（5–100%）。",
            ("ja-JP", "PickboxTitle") => "ピックボックスのサイズ",
            ("ja-JP", "PickboxDescription") => "中央の選択用四角を 3–20 px で調整します（既定値 10 px）。",
            ("ja-JP", "ApertureTitle") => "OSNAP アパーチャのサイズ",
            ("ja-JP", "ApertureDescription") => "オブジェクトスナップが候補を取得する画面上の範囲を調整します。",

            ("en-US", "Section") => "CAD cursor",
            ("en-US", "CrosshairTitle") => "Crosshair size",
            ("en-US", "CrosshairDescription") => "Adjust crosshair length as a percentage of the drawing area (5–100%).",
            ("en-US", "PickboxTitle") => "Pickbox size",
            ("en-US", "PickboxDescription") => "Adjust the central selection square from 3–20 px (10 px default).",
            ("en-US", "ApertureTitle") => "OSNAP aperture size",
            ("en-US", "ApertureDescription") => "Adjust the screen-space range used to acquire object-snap candidates.",

            (_, "Section") => "CAD 光标",
            (_, "CrosshairTitle") => "十字光标大小",
            (_, "CrosshairDescription") => "按绘图区百分比调整十字线长度（5–100%）。",
            (_, "PickboxTitle") => "中心拾取框大小",
            (_, "PickboxDescription") => "调整对象选择用的中央方框，范围 3–20 px，默认 10 px。",
            (_, "ApertureTitle") => "对象捕捉孔径",
            (_, "ApertureDescription") => "调整 OSNAP 搜索捕捉候选点的屏幕范围。",
            _ => key
        };
    }
}

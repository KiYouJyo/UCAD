using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using UCAD.Controls;

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
        var crosshair = Card(
            "Settings_Input_CrosshairSize_Title",
            "Settings_Input_CrosshairSize_Description",
            "\uE7F8",
            NumericSlider(
                settings.CrosshairSizePercent,
                5,
                100,
                5,
                "%",
                value => settings.CrosshairSizePercent = value));
        crosshair.Tag = CadPointerSettingsTag;

        AddSection(
            "Settings_Input_CadPointerSection",
            crosshair,
            Card(
                "Settings_Input_PickboxSize_Title",
                "Settings_Input_PickboxSize_Description",
                "\uE8A7",
                NumericSlider(
                    settings.PickboxSize,
                    3,
                    20,
                    1,
                    " px",
                    value => settings.PickboxSize = value)),
            Card(
                "Settings_Input_OsnapAperture_Title",
                "Settings_Input_OsnapAperture_Description",
                "\uE81E",
                NumericSlider(
                    settings.ObjectSnapAperture,
                    3,
                    50,
                    1,
                    " px",
                    value => settings.ObjectSnapAperture = value)));
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
}

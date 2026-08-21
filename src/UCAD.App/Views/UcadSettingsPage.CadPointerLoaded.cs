using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using UCAD.Controls;

namespace UCAD.Views;

public sealed partial class UcadSettingsPage
{
    private bool _cadPointerLoadedHookInitialized;

    private void UcadSettingsPage_Loaded(object sender, RoutedEventArgs e)
    {
        if (_cadPointerLoadedHookInitialized)
        {
            EnsureCadPointerSettingsVisible();
            EnsureUpdateControlEnabled();
            return;
        }

        _cadPointerLoadedHookInitialized = true;

        // UserControl.OnApplyTemplate is not a reliable lifecycle point for this
        // dynamically rebuilt settings page. Attach from Loaded instead, and keep a
        // LayoutUpdated fallback because language switching rebuilds the active section
        // without another navigation click.
        SettingsContent.LayoutUpdated += (_, _) =>
        {
            EnsureCadPointerSettingsVisible();
            EnsureUpdateControlEnabled();
        };
        InputNavButton.Click += (_, _) => DispatcherQueue.TryEnqueue(EnsureCadPointerSettingsVisible);
        GeneralNavButton.Click += (_, _) => DispatcherQueue.TryEnqueue(EnsureUpdateControlEnabled);

        EnsureCadPointerSettingsVisible();
        EnsureUpdateControlEnabled();
    }

    private void EnsureCadPointerSettingsVisible()
    {
        if (_section == SettingsSection.Input && !HasCadPointerSettings())
        {
            AppendCadPointerSettings();
        }
    }

    private void EnsureUpdateControlEnabled()
    {
        if (_section != SettingsSection.General)
        {
            return;
        }

        var autoUpdateTitle = GetString("Settings_General_AutoUpdate_Title");
        foreach (var card in SettingsContent.Children.OfType<SettingCard>())
        {
            if (string.Equals(card.Title, autoUpdateTitle, StringComparison.Ordinal) &&
                card.ActionContent is ToggleSwitch toggle)
            {
                toggle.IsEnabled = true;
                return;
            }
        }
    }
}

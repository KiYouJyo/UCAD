using Microsoft.UI.Xaml;

namespace UCAD.Views;

public sealed partial class UcadSettingsPage
{
    private bool _cadPointerLoadedHookInitialized;

    private void UcadSettingsPage_Loaded(object sender, RoutedEventArgs e)
    {
        if (_cadPointerLoadedHookInitialized)
        {
            EnsureCadPointerSettingsVisible();
            return;
        }

        _cadPointerLoadedHookInitialized = true;

        // UserControl.OnApplyTemplate is not a reliable lifecycle point for this
        // dynamically rebuilt settings page. Attach from Loaded instead, and keep a
        // LayoutUpdated fallback because language switching rebuilds the active section
        // without another navigation click.
        SettingsContent.LayoutUpdated += (_, _) => EnsureCadPointerSettingsVisible();
        InputNavButton.Click += (_, _) => DispatcherQueue.TryEnqueue(EnsureCadPointerSettingsVisible);

        EnsureCadPointerSettingsVisible();
    }

    private void EnsureCadPointerSettingsVisible()
    {
        if (_section == SettingsSection.Input && !HasCadPointerSettings())
        {
            AppendCadPointerSettings();
        }
    }
}

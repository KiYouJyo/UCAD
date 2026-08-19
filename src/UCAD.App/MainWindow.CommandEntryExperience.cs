using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;

namespace UCAD;

public sealed partial class MainWindow
{
    private bool _commandEntryExperienceInitialized;
    private long _modeTextChangedToken;

    /// <summary>
    /// Owns the AutoCAD-style command-entry ergonomics that must work independently
    /// from whichever child control happened to receive focus during shell creation.
    /// </summary>
    internal void EnsureCommandEntryExperienceInitialized()
    {
        if (_commandEntryExperienceInitialized) return;
        _commandEntryExperienceInitialized = true;

        CommandInput.TextChanged += CommandEntry_CommandInputTextChanged;
        DocumentTabs.SelectionChanged += CommandEntry_DocumentTabsSelectionChanged;
        RootLayout.Loaded += CommandEntry_RootLayoutLoaded;
        _modeTextChangedToken = ModeText.RegisterPropertyChangedCallback(
            TextBlock.TextProperty,
            (_, _) => SyncDynamicCommandDisplay());
    }

    private void CommandEntry_RootLayoutLoaded(object sender, RoutedEventArgs e)
    {
        RootLayout.Loaded -= CommandEntry_RootLayoutLoaded;
        ScheduleActiveCommandInputFocus();
        SyncDynamicCommandDisplay();
    }

    private void CommandEntry_DocumentTabsSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ActiveSession is null || PageOverlay.Visibility == Visibility.Visible) return;
        ScheduleActiveCommandInputFocus();
        RootLayout.DispatcherQueue.TryEnqueue(SyncDynamicCommandDisplay);
    }

    private void CommandEntry_CommandInputTextChanged(object sender, TextChangedEventArgs e) => SyncDynamicCommandDisplay();

    private void ScheduleActiveCommandInputFocus()
    {
        RootLayout.DispatcherQueue.TryEnqueue(() =>
        {
            if (ActiveSession is null || PageOverlay.Visibility == Visibility.Visible) return;

            var acquired = CommandInput.Focus(FocusState.Programmatic);
            CommandInput.SelectionStart = CommandInput.Text.Length;
            CommandInput.SelectionLength = 0;

            if (string.Equals(Environment.GetEnvironmentVariable("UCAD_INTERACTION_SMOKE"), "1", StringComparison.Ordinal))
            {
                var focused = RootLayout.XamlRoot is not null
                    ? FocusManager.GetFocusedElement(RootLayout.XamlRoot)
                    : null;
                if (!acquired || !ReferenceEquals(focused, CommandInput))
                    throw new InvalidOperationException("Initial drawing command input did not acquire keyboard focus.");
                App.WriteStartupEvent("Interaction smoke: initial drawing command input focus acquired");
            }
        });
    }

    private void SyncDynamicCommandDisplay()
    {
        var session = ActiveSession;
        if (session is null) return;
        session.Viewport.SetDynamicCommandDisplay(
            session.StatusText,
            CommandInput.Text,
            session.CommandSession.IsActive);
    }
}

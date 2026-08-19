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
        Activated += CommandEntry_WindowActivated;
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

    private void CommandEntry_WindowActivated(object sender, WindowActivatedEventArgs args)
    {
        if (args.WindowActivationState == WindowActivationState.Deactivated ||
            ActiveSession is null ||
            PageOverlay.Visibility == Visibility.Visible)
        {
            return;
        }

        // Taskbar restore / Alt+Tab / foreground activation must always hand keyboard
        // input back to the CAD command line. Multiple dispatcher turns cover the case
        // where Win32 activation settles one frame after the WinUI Activated event.
        ScheduleActiveCommandInputFocus(remainingAttempts: 4, foregroundRestore: true);
    }

    private void CommandEntry_CommandInputTextChanged(object sender, TextChangedEventArgs e) => SyncDynamicCommandDisplay();

    private void ScheduleActiveCommandInputFocus(int remainingAttempts = 4, bool foregroundRestore = false)
    {
        RootLayout.DispatcherQueue.TryEnqueue(() =>
        {
            if (ActiveSession is null || PageOverlay.Visibility == Visibility.Visible) return;

            if (TryFocusActiveCommandInput())
            {
                if (foregroundRestore)
                {
                    App.WriteStartupEvent("Command input focus restored after foreground activation");
                }
                else if (string.Equals(Environment.GetEnvironmentVariable("UCAD_INTERACTION_SMOKE"), "1", StringComparison.Ordinal))
                {
                    App.WriteStartupEvent("Interaction smoke: initial drawing command input focus acquired");
                }
                return;
            }

            if (remainingAttempts > 1)
            {
                ScheduleActiveCommandInputFocus(remainingAttempts - 1, foregroundRestore);
                return;
            }

            if (string.Equals(Environment.GetEnvironmentVariable("UCAD_INTERACTION_SMOKE"), "1", StringComparison.Ordinal))
            {
                throw new InvalidOperationException(foregroundRestore
                    ? "Foreground drawing command input did not reacquire keyboard focus."
                    : "Initial drawing command input did not acquire keyboard focus.");
            }
        });
    }

    private bool TryFocusActiveCommandInput()
    {
        if (ActiveSession is null || PageOverlay.Visibility == Visibility.Visible || RootLayout.XamlRoot is null)
        {
            return false;
        }

        var acquired = CommandInput.Focus(FocusState.Programmatic);
        CommandInput.SelectionStart = CommandInput.Text.Length;
        CommandInput.SelectionLength = 0;
        var focused = FocusManager.GetFocusedElement(RootLayout.XamlRoot);
        return acquired && ReferenceEquals(focused, CommandInput);
    }

    private void SyncDynamicCommandDisplay()
    {
        var session = ActiveSession;
        if (session is null) return;
        var active = session.CommandSession.IsActive;
        var prompt = active ? session.StatusText : ShellString("CommandPrompt");
        session.Viewport.SetDynamicCommandDisplay(prompt, CommandInput.Text, active);
    }
}

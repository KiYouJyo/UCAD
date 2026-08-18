using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using UCAD.Workspace;

namespace UCAD;

public sealed partial class MainWindow
{
    private readonly HashSet<CadWorkspaceSession> _layoutPersistenceSubscribedSessions = [];
    private readonly Dictionary<CadWorkspaceSession, string> _layoutPersistenceActiveCommands = [];
    private bool _layoutPersistenceBridgeInitialized;

    internal void EnsureLayoutPersistenceBridgeInitialized()
    {
        if (_layoutPersistenceBridgeInitialized) return;
        _layoutPersistenceBridgeInitialized = true;
        RootLayout.Loaded += LayoutPersistence_RootLoaded;
        DocumentTabs.SelectionChanged += LayoutPersistence_DocumentTabsSelectionChanged;
    }

    private void LayoutPersistence_RootLoaded(object sender, RoutedEventArgs e)
    {
        RootLayout.Loaded -= LayoutPersistence_RootLoaded;
        if (ActiveSession is CadWorkspaceSession session) EnsureLayoutPersistenceSubscribed(session);
    }

    private void LayoutPersistence_DocumentTabsSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ActiveSession is CadWorkspaceSession session) EnsureLayoutPersistenceSubscribed(session);
    }

    private void EnsureLayoutPersistenceSubscribed(CadWorkspaceSession session)
    {
        if (!_layoutPersistenceSubscribedSessions.Add(session)) return;

        // LayoutPlot may already have created its default transient state. Replace it with
        // the document-owned state so opening a .ucad restores its real paper layouts.
        _layoutStates[session] = new LayoutSessionState(
            session.Document.ActivePageSetup,
            session.Document.Layouts,
            session.Document.ActiveLayoutName);

        session.CommandSession.Changed += (_, _) => LayoutPersistence_CommandSessionChanged(session);
    }

    private void LayoutPersistence_CommandSessionChanged(CadWorkspaceSession session)
    {
        var active = session.CommandSession.ActiveCommand;
        if (active is not null)
        {
            if (IsLayoutPlotCommand(active.Name)) _layoutPersistenceActiveCommands[session] = active.Name;
            return;
        }

        if (!_layoutPersistenceActiveCommands.Remove(session, out var completedCommand)) return;
        if (completedCommand is not ("PAGESETUP" or "LAYOUT" or "VIEWPORT")) return;

        var state = GetLayoutState(session);
        // Activating another layout must also switch the effective page setup used by
        // preview/plot; PAGESETUP and VIEWPORT already keep these values aligned.
        state.PageSetup = state.ActiveLayout.PageSetup;
        session.Document.SetLayoutTable(state.Layouts, state.ActiveLayoutName);
    }
}

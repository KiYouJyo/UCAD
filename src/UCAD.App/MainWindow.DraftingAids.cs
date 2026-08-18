using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using UCAD.Workspace;
using Windows.System;

namespace UCAD;

public sealed partial class MainWindow
{
    private readonly HashSet<CadWorkspaceSession> _draftingAidSubscribedSessions = [];
    private bool _draftingAidUiInitialized;

    internal void EnsureDraftingAidUiInitialized()
    {
        if (_draftingAidUiInitialized) return;
        _draftingAidUiInitialized = true;

        GridStatusButton.IsHitTestVisible = true;
        SnapStatusButton.IsHitTestVisible = true;
        PolarStatusButton.IsHitTestVisible = true;
        OtrackStatusButton.IsHitTestVisible = true;

        GridStatusButton.Click += GridStatusButton_Click;
        SnapStatusButton.Click += SnapStatusButton_Click;
        PolarStatusButton.Click += PolarStatusButton_Click;
        OtrackStatusButton.Click += OtrackStatusButton_Click;

        DocumentTabs.SelectionChanged += DraftingAid_DocumentTabsSelectionChanged;
        RootLayout.AddHandler(UIElement.KeyDownEvent, new KeyEventHandler(DraftingAid_RootKeyDown), true);
        _settingsService.SettingsChanged += DraftingAid_SettingsChanged;
        RootLayout.Loaded += DraftingAid_RootLoaded;

        RefreshActiveDraftingAidUi();
    }

    private void DraftingAid_RootLoaded(object sender, RoutedEventArgs e)
    {
        RootLayout.Loaded -= DraftingAid_RootLoaded;
        if (ActiveSession is CadWorkspaceSession session) EnsureDraftingAidSessionSubscribed(session);
        RefreshActiveDraftingAidUi();
    }

    private void DraftingAid_DocumentTabsSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ActiveSession is CadWorkspaceSession session) EnsureDraftingAidSessionSubscribed(session);
        RefreshActiveDraftingAidUi();
    }

    private void DraftingAid_SettingsChanged(object? sender, EventArgs e)
    {
        foreach (var session in _sessions.Values) session.Viewport.ApplyDraftingAidVisualState();
        RefreshActiveDraftingAidUi();
    }

    private void EnsureDraftingAidSessionSubscribed(CadWorkspaceSession session)
    {
        session.Viewport.EnsureDraftingAidHooks();
        session.Viewport.ApplyDraftingAidVisualState();
        if (!_draftingAidSubscribedSessions.Add(session)) return;

        session.Interaction.Changed += (_, _) =>
        {
            // F8 is still owned by the accepted v0.4 interaction path. Enforce the
            // AutoCAD-style Ortho/Polar mutual exclusion centrally when that path flips.
            if (session.Interaction.OrthoEnabled && session.Interaction.PolarTrackingEnabled)
            {
                session.Interaction.PolarTrackingEnabled = false;
                return;
            }

            session.Viewport.ApplyDraftingAidVisualState();
            if (ReferenceEquals(ActiveSession, session)) RefreshDraftingAidUi(session);
        };
    }

    private void GridStatusButton_Click(object sender, RoutedEventArgs e)
    {
        if (ActiveSession is not CadWorkspaceSession session) return;
        session.Interaction.GridDisplayEnabled = !session.Interaction.GridDisplayEnabled;
        SetSessionStatus(session, session.Interaction.GridDisplayEnabled
            ? DraftingAidText("GridOn")
            : DraftingAidText("GridOff"));
        RefreshDraftingAidUi(session);
    }

    private void SnapStatusButton_Click(object sender, RoutedEventArgs e)
    {
        if (ActiveSession is not CadWorkspaceSession session) return;
        session.Interaction.GridSnapEnabled = !session.Interaction.GridSnapEnabled;
        SetSessionStatus(session, session.Interaction.GridSnapEnabled
            ? DraftingAidText("SnapOn")
            : DraftingAidText("SnapOff"));
        RefreshDraftingAidUi(session);
    }

    private void PolarStatusButton_Click(object sender, RoutedEventArgs e)
    {
        if (ActiveSession is not CadWorkspaceSession session) return;
        TogglePolar(session);
    }

    private void OtrackStatusButton_Click(object sender, RoutedEventArgs e)
    {
        if (ActiveSession is not CadWorkspaceSession session) return;
        session.Interaction.ObjectSnapTrackingEnabled = !session.Interaction.ObjectSnapTrackingEnabled;
        SetSessionStatus(session, session.Interaction.ObjectSnapTrackingEnabled
            ? DraftingAidText("OtrackOn")
            : DraftingAidText("OtrackOff"));
        RefreshDraftingAidUi(session);
    }

    private void DraftingAid_RootKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (ActiveSession is not CadWorkspaceSession session) return;

        switch (e.Key)
        {
            case VirtualKey.F7:
                session.Interaction.GridDisplayEnabled = !session.Interaction.GridDisplayEnabled;
                SetSessionStatus(session, session.Interaction.GridDisplayEnabled
                    ? DraftingAidText("GridOn")
                    : DraftingAidText("GridOff"));
                e.Handled = true;
                break;

            case VirtualKey.F9:
                session.Interaction.GridSnapEnabled = !session.Interaction.GridSnapEnabled;
                SetSessionStatus(session, session.Interaction.GridSnapEnabled
                    ? DraftingAidText("SnapOn")
                    : DraftingAidText("SnapOff"));
                e.Handled = true;
                break;

            case VirtualKey.F10:
                TogglePolar(session);
                e.Handled = true;
                break;

            case VirtualKey.F11:
                session.Interaction.ObjectSnapTrackingEnabled = !session.Interaction.ObjectSnapTrackingEnabled;
                SetSessionStatus(session, session.Interaction.ObjectSnapTrackingEnabled
                    ? DraftingAidText("OtrackOn")
                    : DraftingAidText("OtrackOff"));
                e.Handled = true;
                break;
        }

        if (e.Handled) RefreshDraftingAidUi(session);
    }

    private void TogglePolar(CadWorkspaceSession session)
    {
        var enable = !session.Interaction.PolarTrackingEnabled;
        if (enable && session.Interaction.OrthoEnabled) session.Interaction.OrthoEnabled = false;
        session.Interaction.PolarTrackingEnabled = enable;
        SetSessionStatus(session, enable ? DraftingAidText("PolarOn") : DraftingAidText("PolarOff"));
        RefreshInteractionUi(session);
        RefreshDraftingAidUi(session);
    }

    private void RefreshActiveDraftingAidUi()
    {
        if (ActiveSession is CadWorkspaceSession session)
        {
            EnsureDraftingAidSessionSubscribed(session);
            RefreshDraftingAidUi(session);
            return;
        }

        SetStatusButtonState(GridStatusButton, false, enabled: false);
        SetStatusButtonState(SnapStatusButton, false, enabled: false);
        SetStatusButtonState(PolarStatusButton, false, enabled: false);
        SetStatusButtonState(OtrackStatusButton, false, enabled: false);
    }

    private void RefreshDraftingAidUi(CadWorkspaceSession session)
    {
        if (!ReferenceEquals(ActiveSession, session)) return;
        SetStatusButtonState(GridStatusButton, session.Interaction.GridDisplayEnabled, enabled: true);
        SetStatusButtonState(SnapStatusButton, session.Interaction.GridSnapEnabled, enabled: true);
        SetStatusButtonState(PolarStatusButton, session.Interaction.PolarTrackingEnabled, enabled: true);
        SetStatusButtonState(OtrackStatusButton, session.Interaction.ObjectSnapTrackingEnabled, enabled: true);
    }

    private static string DraftingAidText(string key)
    {
        var language = Services.LocalizationService.Current.CurrentLanguageTag;
        var ja = language.StartsWith("ja", StringComparison.OrdinalIgnoreCase);
        var en = language.StartsWith("en", StringComparison.OrdinalIgnoreCase);
        return key switch
        {
            "GridOn" => ja ? "グリッド表示: オン (F7)" : en ? "Grid display: On (F7)" : "栅格显示：开（F7）",
            "GridOff" => ja ? "グリッド表示: オフ (F7)" : en ? "Grid display: Off (F7)" : "栅格显示：关（F7）",
            "SnapOn" => ja ? "グリッドスナップ: オン (F9)" : en ? "Grid snap: On (F9)" : "栅格捕捉：开（F9）",
            "SnapOff" => ja ? "グリッドスナップ: オフ (F9)" : en ? "Grid snap: Off (F9)" : "栅格捕捉：关（F9）",
            "PolarOn" => ja ? "極トラッキング: オン (F10)" : en ? "Polar tracking: On (F10)" : "极轴追踪：开（F10）",
            "PolarOff" => ja ? "極トラッキング: オフ (F10)" : en ? "Polar tracking: Off (F10)" : "极轴追踪：关（F10）",
            "OtrackOn" => ja ? "オブジェクトスナップトラッキング: オン (F11)" : en ? "Object snap tracking: On (F11)" : "对象捕捉追踪：开（F11）",
            "OtrackOff" => ja ? "オブジェクトスナップトラッキング: オフ (F11)" : en ? "Object snap tracking: Off (F11)" : "对象捕捉追踪：关（F11）",
            _ => key
        };
    }
}
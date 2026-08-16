using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using UCAD.Core.Entities;
using UCAD.Core.Interaction;
using UCAD.Services;
using UCAD.Workspace;
using Windows.System;

namespace UCAD;

public sealed partial class MainWindow
{
    private readonly HashSet<CadWorkspaceSession> _interactionSubscribedSessions = [];
    private bool _interactionUiInitialized;
    private int _interactionLocalizationGeneration = -1;

    internal void EnsureInteractionUiInitialized()
    {
        if (_interactionUiInitialized)
        {
            RefreshActiveInteractionUi();
            return;
        }

        _interactionUiInitialized = true;

        // v0.4.0 activates only the drafting aids that have real interaction logic.
        // SNAP (grid snap), POLAR and OTRACK remain reserved rather than becoming no-op buttons.
        OsnapStatusButton.IsHitTestVisible = true;
        OrthoStatusButton.IsHitTestVisible = true;
        OsnapStatusButton.Click += OsnapStatusButton_Click;
        OrthoStatusButton.Click += OrthoStatusButton_Click;
        DocumentTabs.SelectionChanged += Interaction_DocumentTabsSelectionChanged;
        RootLayout.AddHandler(UIElement.KeyDownEvent, new KeyEventHandler(Interaction_RootKeyDown), true);
        RootLayout.LayoutUpdated += Interaction_LayoutUpdated;

        RefreshActiveInteractionUi();
    }

    private void Interaction_DocumentTabsSelectionChanged(object sender, Microsoft.UI.Xaml.Controls.SelectionChangedEventArgs e)
    {
        if (ActiveSession is CadWorkspaceSession session)
        {
            EnsureSessionInteractionSubscribed(session);
        }
        RefreshActiveInteractionUi();
    }

    private void EnsureSessionInteractionSubscribed(CadWorkspaceSession session)
    {
        if (!_interactionSubscribedSessions.Add(session))
        {
            return;
        }

        session.Interaction.Selection.Changed += (_, _) =>
        {
            if (ReferenceEquals(ActiveSession, session))
            {
                RefreshInteractionUi(session);
            }
        };
        session.Interaction.Changed += (_, _) =>
        {
            session.Viewport.InvalidateInteraction();
            if (ReferenceEquals(ActiveSession, session))
            {
                RefreshInteractionUi(session);
            }
        };
        session.Document.Changed += (_, _) =>
        {
            if (ReferenceEquals(ActiveSession, session))
            {
                RefreshInteractionUi(session);
            }
        };
    }

    private void OsnapStatusButton_Click(object sender, RoutedEventArgs e)
    {
        if (ActiveSession is not CadWorkspaceSession session) return;
        session.Interaction.ObjectSnapEnabled = !session.Interaction.ObjectSnapEnabled;
        SetSessionStatus(session, session.Interaction.ObjectSnapEnabled
            ? ShellString("StatusOsnapOnMessage")
            : ShellString("StatusOsnapOffMessage"));
        RefreshInteractionUi(session);
    }

    private void OrthoStatusButton_Click(object sender, RoutedEventArgs e)
    {
        if (ActiveSession is not CadWorkspaceSession session) return;
        session.Interaction.OrthoEnabled = !session.Interaction.OrthoEnabled;
        SetSessionStatus(session, session.Interaction.OrthoEnabled
            ? ShellString("StatusOrthoOnMessage")
            : ShellString("StatusOrthoOffMessage"));
        RefreshInteractionUi(session);
    }

    private void Interaction_RootKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (ActiveSession is not CadWorkspaceSession session) return;

        if (e.Key == VirtualKey.F3)
        {
            session.Interaction.ObjectSnapEnabled = !session.Interaction.ObjectSnapEnabled;
            SetSessionStatus(session, session.Interaction.ObjectSnapEnabled
                ? ShellString("StatusOsnapOnMessage")
                : ShellString("StatusOsnapOffMessage"));
            RefreshInteractionUi(session);
            e.Handled = true;
            return;
        }

        if (e.Key == VirtualKey.F8)
        {
            session.Interaction.OrthoEnabled = !session.Interaction.OrthoEnabled;
            SetSessionStatus(session, session.Interaction.OrthoEnabled
                ? ShellString("StatusOrthoOnMessage")
                : ShellString("StatusOrthoOffMessage"));
            RefreshInteractionUi(session);
            e.Handled = true;
            return;
        }

        if (e.Key == VirtualKey.Escape && !session.CommandSession.IsActive && !session.Interaction.Selection.IsEmpty)
        {
            session.Interaction.Selection.Clear();
            SetSessionStatus(session, GetString("Status_Ready"));
            RefreshInteractionUi(session);
            e.Handled = true;
        }
    }

    private void Interaction_LayoutUpdated(object? sender, object e)
    {
        var generation = LocalizationService.Current.Generation;
        if (generation == _interactionLocalizationGeneration)
        {
            return;
        }
        _interactionLocalizationGeneration = generation;
        RefreshActiveInteractionUi();
    }

    private void RefreshActiveInteractionUi()
    {
        if (ActiveSession is CadWorkspaceSession session)
        {
            EnsureSessionInteractionSubscribed(session);
            RefreshInteractionUi(session);
            return;
        }

        SetStatusButtonState(OsnapStatusButton, false, enabled: false);
        SetStatusButtonState(OrthoStatusButton, false, enabled: false);
    }

    private void RefreshInteractionUi(CadWorkspaceSession session)
    {
        if (!ReferenceEquals(ActiveSession, session)) return;

        SetStatusButtonState(OsnapStatusButton, session.Interaction.ObjectSnapEnabled, enabled: true);
        SetStatusButtonState(OrthoStatusButton, session.Interaction.OrthoEnabled, enabled: true);
        RefreshInspectorSelection(session);
    }

    private static void SetStatusButtonState(Microsoft.UI.Xaml.Controls.Button button, bool active, bool enabled)
    {
        button.IsEnabled = enabled;
        button.Opacity = enabled ? 1.0 : 0.45;
        button.Background = active
            ? (Brush)Application.Current.Resources["UcadAccentSelectedBrush"]
            : new SolidColorBrush(Microsoft.UI.Colors.Transparent);
    }

    private void RefreshInspectorSelection(CadWorkspaceSession session)
    {
        var selected = session.Interaction.Selection.SelectedEntities;
        if (selected.Count == 0)
        {
            NoSelectionText.Text = ShellString("InspectorNoSelection");
            SelectionUnavailableText.Text = ShellString("InspectorSelectionHelp");
            DocumentSectionText.Text = ShellString("InspectorDocument");
            EntityCountLabel.Text = ShellString("InspectorEntityCount");
            EntityCountValue.Text = session.Document.Entities.Count.ToString();
            ActiveCommandLabel.Text = ShellString("InspectorActiveCommand");
            ActiveCommandValue.Text = session.CommandSession.ActiveCommand?.Name ?? GetString("Inspector_None");
            UndoAvailableLabel.Text = ShellString("InspectorUndoAvailable");
            UndoAvailableValue.Text = session.Document.CanUndo ? GetString("Inspector_Yes") : GetString("Inspector_No");
            RedoAvailableLabel.Text = ShellString("InspectorRedoAvailable");
            RedoAvailableValue.Text = session.Document.CanRedo ? GetString("Inspector_Yes") : GetString("Inspector_No");
            V04FoundationHint.Text = ShellString("InspectorSelectionWindowHint");
            return;
        }

        var first = selected[0];
        NoSelectionText.Text = selected.Count == 1
            ? EntityTypeName(first)
            : string.Format(ShellString("InspectorMultipleSelectionFormat"), selected.Count);
        SelectionUnavailableText.Text = selected.Count == 1
            ? EntityGeometrySummary(first)
            : ShellString("InspectorMixedSelection");
        DocumentSectionText.Text = ShellString("InspectorSelection");
        EntityCountLabel.Text = ShellString("InspectorSelectedCount");
        EntityCountValue.Text = selected.Count.ToString();
        ActiveCommandLabel.Text = ShellString("InspectorEntityType");
        ActiveCommandValue.Text = selected.Count == 1 ? EntityTypeName(first) : ShellString("InspectorMixed");
        UndoAvailableLabel.Text = ShellString("InspectorGeometry");
        UndoAvailableValue.Text = selected.Count == 1 ? EntityGeometrySummary(first) : "—";
        RedoAvailableLabel.Text = ShellString("InspectorEntityId");
        RedoAvailableValue.Text = selected.Count == 1 ? first.Id.ToString("N")[..8] : "—";
        V04FoundationHint.Text = ShellString("InspectorSelectionWindowHint");
    }

    private string EntityTypeName(ICadEntity entity) => entity switch
    {
        LineEntity => ShellString("EntityLine"),
        PolylineEntity => ShellString("EntityPolyline"),
        CircleEntity => ShellString("EntityCircle"),
        ArcEntity => ShellString("EntityArc"),
        _ => entity.GetType().Name
    };

    private string EntityGeometrySummary(ICadEntity entity)
    {
        var provider = NumericFormatProvider();
        return entity switch
        {
            LineEntity line => string.Format(provider, ShellString("GeometryLineFormat"), line.Length),
            PolylineEntity polyline => string.Format(provider, ShellString("GeometryPolylineFormat"), polyline.Points.Count, polyline.Length),
            CircleEntity circle => string.Format(provider, ShellString("GeometryCircleFormat"), circle.Radius),
            ArcEntity arc => string.Format(provider, ShellString("GeometryArcFormat"), arc.Radius, Math.Abs(arc.SweepAngleRadians) * 180 / Math.PI),
            _ => "—"
        };
    }
}

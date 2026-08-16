using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using UCAD.Core.Commands;
using UCAD.Core.Entities;
using UCAD.Core.Geometry;
using UCAD.Core.Interaction;
using UCAD.Interop;
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
        ApplyRegisteredCapabilityState();
        ActivateModifyToolSurfaces();

        // v0.4.x activates only the drafting aids that have real interaction logic.
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

    internal void ScheduleInteractionSmoke()
    {
        var explicitInteractionSmoke = string.Equals(
            Environment.GetEnvironmentVariable("UCAD_INTERACTION_SMOKE"),
            "1",
            StringComparison.Ordinal);
        var startupSmoke = string.Equals(
            Environment.GetEnvironmentVariable("UCAD_STARTUP_SMOKE"),
            "1",
            StringComparison.Ordinal);
        if (!explicitInteractionSmoke && !startupSmoke)
        {
            return;
        }

        RootLayout.Loaded += RootLayout_InteractionSmokeLoaded;
    }

    private void RootLayout_InteractionSmokeLoaded(object sender, RoutedEventArgs e)
    {
        RootLayout.Loaded -= RootLayout_InteractionSmokeLoaded;
        RootLayout.DispatcherQueue.TryEnqueue(() =>
        {
            try
            {
                CreateNewWorkspace();
                var session = ActiveSession ?? throw new InvalidOperationException("Interaction smoke could not create a Drawing workspace.");
                EnsureSessionInteractionSubscribed(session);

                // Exercise the exact Windows App SDK interop path used by CadViewport.
                // If the transparent HCURSOR cannot be projected into InputCursor,
                // the smoke must fail instead of shipping a visible system arrow.
                _ = TransparentInputCursor.GetOrCreate();

                var line = new LineEntity(new CadPoint(0, 0), new CadPoint(10, 0));
                var circle = new CircleEntity(new CadPoint(20, 0), 5);
                session.Document.Add(line);
                session.Document.Add(circle);

                session.Interaction.Selection.Replace(line.Id);
                session.Interaction.Selection.Add(circle.Id);
                if (session.Interaction.Selection.Count != 2)
                {
                    throw new InvalidOperationException("Interaction smoke additive selection failed.");
                }

                session.Interaction.Selection.Remove(line.Id);
                if (session.Interaction.Selection.Count != 1 || session.Interaction.Selection.Contains(line.Id))
                {
                    throw new InvalidOperationException("Interaction smoke Shift-style removal failed.");
                }
                session.Interaction.Selection.Add(line.Id);

                session.Interaction.ObjectSnapEnabled = true;
                session.Interaction.ObjectSnapModes = ObjectSnapMode.Endpoint | ObjectSnapMode.Midpoint | ObjectSnapMode.Center | ObjectSnapMode.Intersection;
                var snap = ObjectSnapResolver.Resolve(
                    session.Document.Entities,
                    new CadPoint(0.1, 0.1),
                    0.5,
                    session.Interaction.ObjectSnapModes);
                if (snap is null || snap.Kind != ObjectSnapKind.Endpoint || (snap.Point - new CadPoint(0, 0)).Length > 1e-8)
                {
                    throw new InvalidOperationException("Interaction smoke endpoint OSNAP failed.");
                }

                var centerSnap = ObjectSnapResolver.Resolve(
                    session.Document.Entities,
                    new CadPoint(20.1, 0.1),
                    0.5,
                    ObjectSnapMode.Center);
                if (centerSnap is null || centerSnap.Kind != ObjectSnapKind.Center || (centerSnap.Point - circle.Center).Length > 1e-8)
                {
                    throw new InvalidOperationException("Interaction smoke center OSNAP failed.");
                }

                session.Interaction.OrthoEnabled = true;
                var ortho = OrthoConstraint.Apply(new CadPoint(0, 0), new CadPoint(8, 2));
                if ((ortho - new CadPoint(8, 0)).Length > 1e-8)
                {
                    throw new InvalidOperationException("Interaction smoke Ortho constraint failed.");
                }

                if (!DrawCategoryButton.IsEnabled || !ViewCategoryButton.IsEnabled || !ModifyCategoryButton.IsEnabled)
                {
                    throw new InvalidOperationException("Interaction smoke capability-derived category state failed.");
                }

                RefreshInteractionUi(session);
                if (NoSelectionText.Text.StartsWith("Inspector", StringComparison.Ordinal) ||
                    EntityCountValue.Text != "2")
                {
                    throw new InvalidOperationException("Interaction smoke Inspector binding failed.");
                }

                var eraseResult = session.CommandSession.Start("ERASE");
                if (eraseResult.Status != CommandStartStatus.Started ||
                    session.Document.Entities.Count != 0 ||
                    !session.Interaction.Selection.IsEmpty)
                {
                    throw new InvalidOperationException("Interaction smoke ERASE command path failed.");
                }
                if (!session.Document.Undo() || session.Document.Entities.Count != 2)
                {
                    throw new InvalidOperationException("Interaction smoke ERASE one-step Undo failed.");
                }

                App.WriteStartupEvent("Interaction smoke: Selection + ERASE + OSNAP + ORTHO + Inspector + transparent CAD cursor initialized");
            }
            catch (Exception ex)
            {
                App.WriteStartupFailure("InteractionSmoke", ex);
                throw;
            }
        });
    }

    private void ApplyRegisteredCapabilityState()
    {
        DrawCategoryButton.IsEnabled = HasRegisteredCategory(CadCommandCategory.Draw);
        ModifyCategoryButton.IsEnabled = HasRegisteredCategory(CadCommandCategory.Modify);
        AnnotateCategoryButton.IsEnabled = HasRegisteredCategory(CadCommandCategory.Annotate);
        LayersCategoryButton.IsEnabled = HasRegisteredCategory(CadCommandCategory.Layer);
        BlocksCategoryButton.IsEnabled = HasRegisteredCategory(CadCommandCategory.Block);
        MeasureCategoryButton.IsEnabled = HasRegisteredCategory(CadCommandCategory.Measure);
        ViewCategoryButton.IsEnabled = HasRegisteredCategory(CadCommandCategory.View);
    }

    private bool HasRegisteredCategory(CadCommandCategory category) =>
        _commandRegistry.Commands.Any(command => command.Category == category);

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
        session.CommandSession.Changed += (_, _) =>
        {
            // ERASE stays on the shared command boundary so keyboard Delete, typed
            // ERASE/E/DELETE, and command search all converge on one implementation.
            if (session.CommandSession.ActiveCommand?.Name == "ERASE")
            {
                ExecuteEraseSelection(session);
                session.CommandSession.Complete();
                return;
            }

            // v0.5 Modify commands use the same CommandSession boundary. The controller
            // owns phased mouse/keyboard input but never creates a parallel command model.
            if (HandleModifyCommandSessionChanged(session))
            {
                RootLayout.DispatcherQueue.TryEnqueue(() =>
                {
                    if (ReferenceEquals(ActiveSession, session))
                    {
                        ModeText.Text = session.StatusText;
                        RefreshInspectorSelection(session);
                    }
                });
                return;
            }

            // MainWindow's command-dispatch stack can still update legacy Inspector rows
            // after CommandSession changes. Defer the selection Inspector refresh so
            // it wins after that synchronous stack without coupling Core back to WinUI.
            RootLayout.DispatcherQueue.TryEnqueue(() =>
            {
                if (ReferenceEquals(ActiveSession, session))
                {
                    RefreshInspectorSelection(session);
                }
            });
        };
        session.Document.Changed += (_, _) =>
        {
            if (ReferenceEquals(ActiveSession, session))
            {
                RefreshInteractionUi(session);
            }
        };
    }

    private void ExecuteEraseSelection(CadWorkspaceSession session)
    {
        var selectedIds = session.Interaction.Selection.SelectedIds.ToArray();
        if (selectedIds.Length == 0)
        {
            SetSessionStatus(session, ShellString("StatusEraseNothing"));
            return;
        }

        var removed = session.Document.RemoveRange(selectedIds);
        SetSessionStatus(session, removed > 0
            ? string.Format(ShellString("StatusEraseCountFormat"), removed)
            : ShellString("StatusEraseNothing"));
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

        if (e.Key == VirtualKey.Delete && !session.CommandSession.IsActive)
        {
            // Delete inside command/text input edits text; it must not erase drawing
            // selection merely because the root listens with handledEventsToo.
            var focused = FocusManager.GetFocusedElement(RootLayout.XamlRoot);
            if (focused is Microsoft.UI.Xaml.Controls.TextBox)
            {
                return;
            }

            StartToolbarCommand("ERASE");
            e.Handled = true;
            return;
        }

        if (e.Key == VirtualKey.Escape && !session.CommandSession.IsActive)
        {
            // First Esc cancels an in-progress two-click/window gesture. A subsequent Esc
            // clears the completed noun/verb selection set, matching CAD expectations.
            if (session.Viewport.CancelSelectionGesture())
            {
                SetSessionStatus(session, GetString("Status_Ready"));
                e.Handled = true;
                return;
            }

            if (!session.Interaction.Selection.IsEmpty)
            {
                session.Interaction.Selection.Clear();
                SetSessionStatus(session, GetString("Status_Ready"));
                RefreshInteractionUi(session);
                e.Handled = true;
            }
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
        if (ActiveSession is CadWorkspaceSession session)
        {
            RefreshModifyLocalization(session);
        }
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
        ModeText.Text = session.StatusText;
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

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.Windows.ApplicationModel.Resources;
using UCAD.Core;
using UCAD.Core.Commands;
using UCAD.Core.Geometry;
using UCAD.Workspace;
using Windows.System;

namespace UCAD;

public sealed partial class MainWindow : Window
{
    private readonly ResourceLoader _resources;
    private readonly CommandRegistry _commandRegistry;
    private readonly Dictionary<TabViewItem, CadWorkspaceSession> _sessions = [];
    private CadWorkspaceSession? _activeSession;
    private int _nextDocumentOrdinal = 1;
    private string? _activeShelfCategory = "DRAW";

    public MainWindow()
    {
        InitializeComponent();
        _resources = new ResourceLoader();
        _commandRegistry = CommandRegistry.CreateDefault();

        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);
        Title = GetString("AppWindowTitle");
        AppTitleBar.Title = "UCAD";

        CommandSearch.ItemsSource = _commandRegistry.Commands
            .SelectMany(command => command.Tokens)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(token => token, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        CreateNewWorkspace();
        UpdateToolShelfHint();
    }

    private CadWorkspaceSession? ActiveSession => _activeSession;

    private string GetString(string key)
    {
        var value = _resources.GetString(key);
        return string.IsNullOrWhiteSpace(value) ? key : value;
    }

    private CadWorkspaceSession CreateNewWorkspace()
    {
        var ordinal = _nextDocumentOrdinal++;
        var displayName = string.Format(GetString("Document_UntitledFormat"), ordinal);
        var session = new CadWorkspaceSession(ordinal, displayName, _commandRegistry)
        {
            StatusText = GetString("Status_Ready")
        };

        var tab = new TabViewItem
        {
            Tag = session,
            Header = displayName,
            IsClosable = true
        };
        _sessions[tab] = session;

        session.Document.Changed += (_, _) =>
        {
            UpdateTabHeader(tab, session);
            if (ReferenceEquals(_activeSession, session))
            {
                UpdateSessionUi(session);
            }
        };

        session.Viewport.PointerWorldPositionChanged += point =>
        {
            session.PointerWorldPosition = point;
            if (ReferenceEquals(_activeSession, session))
            {
                UpdateCoordinateText(session);
            }
        };

        session.Viewport.DrawingPointAccepted += (kind, count, point) =>
        {
            session.CommandBasePoint = point;
            SetDrawingPrompt(session, kind, count);
        };

        session.Viewport.DrawingCommandCompleted += kind =>
        {
            if (session.CommandSession.ActiveCommand?.DrawingKind == kind)
            {
                session.CommandSession.Complete();
            }

            session.CommandBasePoint = null;
            SetSessionStatus(session, GetString("Status_Ready"));
            if (ReferenceEquals(_activeSession, session))
            {
                UpdateSessionUi(session);
            }
        };

        session.Viewport.ZoomChanged += _ =>
        {
            if (ReferenceEquals(_activeSession, session))
            {
                UpdateZoomText(session);
            }
        };

        DocumentTabs.TabItems.Add(tab);
        DocumentTabs.SelectedItem = tab;
        ActivateSession(session);
        return session;
    }

    private void ActivateSession(CadWorkspaceSession session)
    {
        _activeSession = session;
        ViewportHost.Content = session.Viewport;
        CommandInput.Text = string.Empty;
        ModeText.Text = session.StatusText;
        UpdateCoordinateText(session);
        UpdateZoomText(session);
        UpdateSessionUi(session);
    }

    private void UpdateSessionUi(CadWorkspaceSession session)
    {
        var isActive = ReferenceEquals(_activeSession, session);
        if (!isActive)
        {
            return;
        }

        var canUndo = session.Document.CanUndo;
        var canRedo = session.Document.CanRedo;
        UndoMenuItem.IsEnabled = canUndo;
        RedoMenuItem.IsEnabled = canRedo;
        UndoMoreItem.IsEnabled = canUndo;
        RedoMoreItem.IsEnabled = canRedo;

        EntityCountValue.Text = session.Document.Entities.Count.ToString();
        ActiveCommandValue.Text = session.CommandSession.ActiveCommand?.Name ?? GetString("Inspector_None");
        UndoAvailableValue.Text = canUndo ? GetString("Inspector_Yes") : GetString("Inspector_No");
        RedoAvailableValue.Text = canRedo ? GetString("Inspector_Yes") : GetString("Inspector_No");
        ModeText.Text = session.StatusText;
    }

    private void UpdateCoordinateText(CadWorkspaceSession session) =>
        CoordinateText.Text = $"X {session.PointerWorldPosition.X:0.00}  Y {session.PointerWorldPosition.Y:0.00}";

    private void UpdateZoomText(CadWorkspaceSession session) =>
        ZoomText.Text = $"{session.Viewport.Zoom * 100:0}%";

    private static void UpdateTabHeader(TabViewItem tab, CadWorkspaceSession session) =>
        tab.Header = session.IsDirty ? $"{session.DisplayName} •" : session.DisplayName;

    private void SetSessionStatus(CadWorkspaceSession session, string text)
    {
        session.StatusText = text;
        if (ReferenceEquals(_activeSession, session))
        {
            ModeText.Text = text;
        }
    }

    private void NewDrawingMenuItem_Click(object sender, RoutedEventArgs e) => CreateNewWorkspace();

    private async void CloseDrawingMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (DocumentTabs.SelectedItem is TabViewItem tab)
        {
            await TryCloseTabAsync(tab);
        }
    }

    private void DocumentTabs_AddTabButtonClick(TabView sender, object args) => CreateNewWorkspace();

    private void DocumentTabs_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (DocumentTabs.SelectedItem is TabViewItem tab && _sessions.TryGetValue(tab, out var session))
        {
            ActivateSession(session);
        }
    }

    private async void DocumentTabs_TabCloseRequested(TabView sender, TabViewTabCloseRequestedEventArgs args) =>
        await TryCloseTabAsync(args.Tab);

    private async Task<bool> TryCloseTabAsync(TabViewItem tab)
    {
        if (!_sessions.TryGetValue(tab, out var session))
        {
            return false;
        }

        if (session.IsDirty)
        {
            var dialog = new ContentDialog
            {
                XamlRoot = RootLayout.XamlRoot,
                Title = GetString("CloseDirtyDialog_Title"),
                Content = string.Format(GetString("CloseDirtyDialog_Content"), session.DisplayName),
                PrimaryButtonText = GetString("CloseDirtyDialog_Primary"),
                CloseButtonText = GetString("CloseDirtyDialog_Cancel"),
                DefaultButton = ContentDialogButton.Close
            };

            if (await dialog.ShowAsync() != ContentDialogResult.Primary)
            {
                return false;
            }
        }

        _sessions.Remove(tab);
        DocumentTabs.TabItems.Remove(tab);

        if (DocumentTabs.TabItems.Count == 0)
        {
            CreateNewWorkspace();
        }
        else if (_activeSession is null || ReferenceEquals(_activeSession, session))
        {
            if (DocumentTabs.SelectedItem is TabViewItem selected && _sessions.TryGetValue(selected, out var nextSession))
            {
                ActivateSession(nextSession);
            }
        }

        return true;
    }

    private void RunCommand_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement element && element.Tag is string command)
        {
            StartToolbarCommand(command);
        }
    }

    private void CommandSearch_QuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
    {
        var token = args.ChosenSuggestion?.ToString();
        if (string.IsNullOrWhiteSpace(token))
        {
            token = args.QueryText;
        }

        if (!string.IsNullOrWhiteSpace(token))
        {
            StartToolbarCommand(token);
            sender.Text = string.Empty;
        }
    }

    private void DrawCategoryButton_Click(object sender, RoutedEventArgs e) => ToggleToolShelf("DRAW");

    private void ViewCategoryButton_Click(object sender, RoutedEventArgs e) => ToggleToolShelf("VIEW");

    private void ToggleToolShelf(string category)
    {
        if (_activeShelfCategory == category && ToolShelfHost.Visibility == Visibility.Visible)
        {
            ToolShelfHost.Visibility = Visibility.Collapsed;
            DrawCategoryButton.IsChecked = false;
            ViewCategoryButton.IsChecked = false;
            _activeShelfCategory = null;
            return;
        }

        _activeShelfCategory = category;
        ToolShelfHost.Visibility = Visibility.Visible;
        DrawToolShelf.Visibility = category == "DRAW" ? Visibility.Visible : Visibility.Collapsed;
        ViewToolShelf.Visibility = category == "VIEW" ? Visibility.Visible : Visibility.Collapsed;
        DrawCategoryButton.IsChecked = category == "DRAW";
        ViewCategoryButton.IsChecked = category == "VIEW";
        UpdateToolShelfHint();
    }

    private void UpdateToolShelfHint() => ToolShelfHintText.Text = GetString("ToolShelfHintText.Text");

    private void CommandInput_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key is VirtualKey.Enter or VirtualKey.Space)
        {
            SubmitCommandLine();
            e.Handled = true;
        }
        else if (e.Key == VirtualKey.Escape)
        {
            CancelActiveCommand();
            e.Handled = true;
        }
    }

    private void RootLayout_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Escape)
        {
            CancelActiveCommand();
            e.Handled = true;
        }
    }

    private void SubmitCommandLine()
    {
        var session = ActiveSession;
        if (session is null)
        {
            return;
        }

        var input = CommandInput.Text.Trim();
        CommandInput.Text = string.Empty;

        if (session.CommandSession.ActiveCommand?.DrawingKind is DrawingCommandKind drawingKind)
        {
            if (string.IsNullOrWhiteSpace(input))
            {
                if (drawingKind is DrawingCommandKind.Line or DrawingCommandKind.Polyline)
                {
                    session.Viewport.CompleteDrawingCommand();
                }
                else
                {
                    SetSessionStatus(session, GetString("Status_PointRequired"));
                }

                return;
            }

            if (!TryResolvePointInput(session, input, out var point))
            {
                SetSessionStatus(session, GetString("Status_InvalidPoint"));
                return;
            }

            if (!session.Viewport.SubmitDrawingPoint(point))
            {
                SetSessionStatus(session, GetString("Status_InvalidGeometry"));
            }

            return;
        }

        StartCommand(session, input);
    }

    private bool TryResolvePointInput(CadWorkspaceSession session, string input, out CadPoint point)
    {
        if (CommandInputParser.TryParsePoint(input, session.CommandBasePoint, out point))
        {
            return true;
        }

        if (session.CommandBasePoint is not CadPoint basePoint || !CommandInputParser.TryParseNumber(input, out var distance))
        {
            point = default;
            return false;
        }

        var cursor = session.Viewport.CurrentPointerWorldPosition;
        var dx = cursor.X - basePoint.X;
        var dy = cursor.Y - basePoint.Y;
        var length = Math.Sqrt((dx * dx) + (dy * dy));
        if (length < 1e-9)
        {
            point = default;
            return false;
        }

        point = new CadPoint(basePoint.X + (dx / length * distance), basePoint.Y + (dy / length * distance));
        return true;
    }

    private void StartToolbarCommand(string command)
    {
        var session = ActiveSession;
        if (session is null)
        {
            return;
        }

        if (session.CommandSession.IsActive)
        {
            session.Viewport.CancelDrawingCommand();
            session.CommandSession.Cancel();
            session.CommandBasePoint = null;
        }

        StartCommand(session, command);
        CommandInput.Focus(FocusState.Programmatic);
    }

    private void StartCommand(CadWorkspaceSession session, string? token)
    {
        var result = session.CommandSession.Start(token);
        switch (result.Status)
        {
            case CommandStartStatus.NoPreviousCommand:
                SetSessionStatus(session, GetString("Status_NoPreviousCommand"));
                break;
            case CommandStartStatus.Unknown:
                SetSessionStatus(session, string.Format(GetString("Status_UnknownCommand"), result.Token));
                break;
            case CommandStartStatus.Started:
                DispatchStartedCommand(session, result.Command!);
                break;
        }

        UpdateSessionUi(session);
    }

    private void DispatchStartedCommand(CadWorkspaceSession session, CadCommandDefinition command)
    {
        if (command.DrawingKind is DrawingCommandKind drawingKind)
        {
            session.CommandBasePoint = null;
            session.Viewport.BeginDrawingCommand(drawingKind);
            SetDrawingPrompt(session, drawingKind, 0);
            CommandInput.Focus(FocusState.Programmatic);
            return;
        }

        switch (command.Name)
        {
            case "UNDO":
                SetSessionStatus(session, session.Viewport.Undo() ? GetString("Status_Undo") : GetString("Status_NothingToUndo"));
                session.CommandSession.Complete();
                break;
            case "REDO":
                SetSessionStatus(session, session.Viewport.Redo() ? GetString("Status_Redo") : GetString("Status_NothingToRedo"));
                session.CommandSession.Complete();
                break;
            case "CLEAR":
                session.Viewport.ClearDocument();
                session.CommandSession.Complete();
                SetSessionStatus(session, GetString("Status_Cleared"));
                break;
            case "RESETVIEW":
                session.Viewport.ResetView();
                session.CommandSession.Complete();
                SetSessionStatus(session, GetString("Status_ViewReset"));
                break;
        }
    }

    private void SetDrawingPrompt(CadWorkspaceSession session, DrawingCommandKind kind, int acceptedPointCount)
    {
        var key = kind switch
        {
            DrawingCommandKind.Line => acceptedPointCount == 0 ? "Status_LineFirst" : "Status_LineNext",
            DrawingCommandKind.Polyline => acceptedPointCount == 0 ? "Status_PlineFirst" : "Status_PlineNext",
            DrawingCommandKind.Rectangle => acceptedPointCount == 0 ? "Status_RectFirst" : "Status_RectOpposite",
            DrawingCommandKind.Circle => acceptedPointCount == 0 ? "Status_CircleCenter" : "Status_CircleRadius",
            DrawingCommandKind.Arc => acceptedPointCount switch
            {
                0 => "Status_ArcStart",
                1 => "Status_ArcSecond",
                _ => "Status_ArcEnd"
            },
            _ => "Status_Ready"
        };
        SetSessionStatus(session, GetString(key));
    }

    private void CancelActiveCommand()
    {
        var session = ActiveSession;
        if (session is null)
        {
            return;
        }

        CommandInput.Text = string.Empty;
        if (session.CommandSession.ActiveCommand?.DrawingKind is not null)
        {
            session.Viewport.CancelDrawingCommand();
        }

        if (session.CommandSession.Cancel())
        {
            session.CommandBasePoint = null;
            SetSessionStatus(session, GetString("Status_CommandCancelled"));
        }
        else
        {
            SetSessionStatus(session, GetString("Status_Ready"));
        }

        UpdateSessionUi(session);
        CommandInput.Focus(FocusState.Programmatic);
    }
}

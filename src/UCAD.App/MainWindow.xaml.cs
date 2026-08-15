using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using Microsoft.Windows.ApplicationModel.Resources;
using UCAD.Core.Commands;
using UCAD.Core.Geometry;
using Windows.System;

namespace UCAD;

public sealed partial class MainWindow : Window
{
    private readonly ResourceLoader _resources;
    private readonly CommandSession _commandSession;
    private CadPoint? _commandBasePoint;

    public MainWindow()
    {
        InitializeComponent();
        _resources = new ResourceLoader();
        _commandSession = new CommandSession(CommandRegistry.CreateDefault());
        Title = GetString("AppWindowTitle");
        ModeText.Text = GetString("Status_Ready");

        Viewport.PointerWorldPositionChanged += point =>
            CoordinateText.Text = $"X {point.X:0.00}  Y {point.Y:0.00}";

        Viewport.DrawingPointAccepted += (kind, count, point) =>
        {
            _commandBasePoint = point;
            SetDrawingPrompt(kind, count);
        };

        Viewport.DrawingCommandCompleted += kind =>
        {
            if (_commandSession.ActiveCommand is not null &&
                TryGetDrawingKind(_commandSession.ActiveCommand.Name, out var activeKind) &&
                activeKind == kind)
            {
                _commandSession.Complete();
            }

            _commandBasePoint = null;
            ModeText.Text = GetString("Status_Ready");
        };

        Viewport.HistoryStateChanged += (canUndo, canRedo) =>
        {
            UndoButton.IsEnabled = canUndo;
            RedoButton.IsEnabled = canRedo;
        };
    }

    private string GetString(string key)
    {
        var value = _resources.GetString(key);
        return string.IsNullOrWhiteSpace(value) ? key : value;
    }

    private void Line_Click(object sender, RoutedEventArgs e) => StartToolbarCommand("LINE");

    private void Polyline_Click(object sender, RoutedEventArgs e) => StartToolbarCommand("PLINE");

    private void Rectangle_Click(object sender, RoutedEventArgs e) => StartToolbarCommand("RECTANGLE");

    private void Circle_Click(object sender, RoutedEventArgs e) => StartToolbarCommand("CIRCLE");

    private void Arc_Click(object sender, RoutedEventArgs e) => StartToolbarCommand("ARC");

    private void Undo_Click(object sender, RoutedEventArgs e) => StartToolbarCommand("UNDO");

    private void Redo_Click(object sender, RoutedEventArgs e) => StartToolbarCommand("REDO");

    private void Clear_Click(object sender, RoutedEventArgs e) => StartToolbarCommand("CLEAR");

    private void ResetView_Click(object sender, RoutedEventArgs e) => StartToolbarCommand("RESETVIEW");

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
        var input = CommandInput.Text.Trim();
        CommandInput.Text = string.Empty;

        if (_commandSession.ActiveCommand is not null &&
            TryGetDrawingKind(_commandSession.ActiveCommand.Name, out var drawingKind))
        {
            if (string.IsNullOrWhiteSpace(input))
            {
                if (drawingKind is DrawingCommandKind.Line or DrawingCommandKind.Polyline)
                {
                    Viewport.CompleteDrawingCommand();
                }
                else
                {
                    ModeText.Text = GetString("Status_PointRequired");
                }

                return;
            }

            if (!TryResolvePointInput(input, out var point))
            {
                ModeText.Text = GetString("Status_InvalidPoint");
                return;
            }

            if (!Viewport.SubmitDrawingPoint(point))
            {
                ModeText.Text = GetString("Status_InvalidGeometry");
            }

            return;
        }

        StartCommand(input);
    }

    private bool TryResolvePointInput(string input, out CadPoint point)
    {
        if (CommandInputParser.TryParsePoint(input, _commandBasePoint, out point))
        {
            return true;
        }

        if (_commandBasePoint is not CadPoint basePoint || !CommandInputParser.TryParseNumber(input, out var distance))
        {
            point = default;
            return false;
        }

        var cursor = Viewport.CurrentPointerWorldPosition;
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
        if (_commandSession.IsActive)
        {
            Viewport.CancelDrawingCommand();
            _commandSession.Cancel();
            _commandBasePoint = null;
        }

        StartCommand(command);
        CommandInput.Focus(FocusState.Programmatic);
    }

    private void StartCommand(string? token)
    {
        var result = _commandSession.Start(token);
        switch (result.Status)
        {
            case CommandStartStatus.NoPreviousCommand:
                ModeText.Text = GetString("Status_NoPreviousCommand");
                break;
            case CommandStartStatus.Unknown:
                ModeText.Text = string.Format(GetString("Status_UnknownCommand"), result.Token);
                break;
            case CommandStartStatus.Started:
                DispatchStartedCommand(result.Command!);
                break;
        }
    }

    private void DispatchStartedCommand(CadCommandDefinition command)
    {
        if (TryGetDrawingKind(command.Name, out var drawingKind))
        {
            _commandBasePoint = null;
            Viewport.BeginDrawingCommand(drawingKind);
            SetDrawingPrompt(drawingKind, 0);
            CommandInput.Focus(FocusState.Programmatic);
            return;
        }

        switch (command.Name)
        {
            case "UNDO":
                ModeText.Text = Viewport.Undo() ? GetString("Status_Undo") : GetString("Status_NothingToUndo");
                _commandSession.Complete();
                break;
            case "REDO":
                ModeText.Text = Viewport.Redo() ? GetString("Status_Redo") : GetString("Status_NothingToRedo");
                _commandSession.Complete();
                break;
            case "CLEAR":
                Viewport.ClearDocument();
                _commandSession.Complete();
                ModeText.Text = GetString("Status_Cleared");
                break;
            case "RESETVIEW":
                Viewport.ResetView();
                _commandSession.Complete();
                ModeText.Text = GetString("Status_ViewReset");
                break;
        }
    }

    private void SetDrawingPrompt(DrawingCommandKind kind, int acceptedPointCount)
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
        ModeText.Text = GetString(key);
    }

    private static bool TryGetDrawingKind(string commandName, out DrawingCommandKind kind)
    {
        kind = commandName switch
        {
            "LINE" => DrawingCommandKind.Line,
            "PLINE" => DrawingCommandKind.Polyline,
            "RECTANGLE" => DrawingCommandKind.Rectangle,
            "CIRCLE" => DrawingCommandKind.Circle,
            "ARC" => DrawingCommandKind.Arc,
            _ => default
        };

        return commandName is "LINE" or "PLINE" or "RECTANGLE" or "CIRCLE" or "ARC";
    }

    private void CancelActiveCommand()
    {
        CommandInput.Text = string.Empty;
        if (_commandSession.ActiveCommand is not null && TryGetDrawingKind(_commandSession.ActiveCommand.Name, out _))
        {
            Viewport.CancelDrawingCommand();
        }

        if (_commandSession.Cancel())
        {
            _commandBasePoint = null;
            ModeText.Text = GetString("Status_CommandCancelled");
        }
        else
        {
            ModeText.Text = GetString("Status_Ready");
        }

        CommandInput.Focus(FocusState.Programmatic);
    }
}

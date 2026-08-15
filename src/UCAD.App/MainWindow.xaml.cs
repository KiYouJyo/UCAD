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

        Viewport.LinePointAccepted += point =>
        {
            _commandBasePoint = point;
            ModeText.Text = GetString("Status_LineNext");
        };

        Viewport.LineModeChanged += enabled =>
        {
            if (!enabled && _commandSession.ActiveCommand?.Name == "LINE")
            {
                _commandSession.Complete();
                _commandBasePoint = null;
            }
        };
    }

    private string GetString(string key)
    {
        var value = _resources.GetString(key);
        return string.IsNullOrWhiteSpace(value) ? key : value;
    }

    private void Line_Click(object sender, RoutedEventArgs e) => StartCommand("LINE");

    private void Clear_Click(object sender, RoutedEventArgs e) => ExecuteImmediateCommand("CLEAR");

    private void ResetView_Click(object sender, RoutedEventArgs e) => ExecuteImmediateCommand("RESETVIEW");

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

        if (_commandSession.ActiveCommand?.Name == "LINE")
        {
            if (string.IsNullOrWhiteSpace(input))
            {
                CompleteLineCommand();
                return;
            }

            if (TryResolvePointInput(input, out var point))
            {
                Viewport.SubmitLinePoint(point);
                _commandBasePoint = point;
                ModeText.Text = GetString("Status_LineNext");
            }
            else
            {
                ModeText.Text = GetString("Status_InvalidPoint");
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
        switch (command.Name)
        {
            case "LINE":
                _commandBasePoint = null;
                Viewport.BeginLineCommand();
                ModeText.Text = GetString("Status_LineFirst");
                CommandInput.Focus(FocusState.Programmatic);
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

    private void ExecuteImmediateCommand(string command)
    {
        StartCommand(command);
        CommandInput.Focus(FocusState.Programmatic);
    }

    private void CompleteLineCommand()
    {
        Viewport.CompleteLineCommand();
        _commandSession.Complete();
        _commandBasePoint = null;
        ModeText.Text = GetString("Status_Ready");
    }

    private void CancelActiveCommand()
    {
        CommandInput.Text = string.Empty;
        if (_commandSession.ActiveCommand?.Name == "LINE")
        {
            Viewport.CancelLineCommand();
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

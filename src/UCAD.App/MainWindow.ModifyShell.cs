using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using UCAD.Workspace;

using UCAD.Services;

namespace UCAD;

public sealed partial class MainWindow
{
    private bool _modifyToolSurfacesActivated;
    private bool _modifySmokeScheduled;

    private void ActivateModifyToolSurfaces()
    {
        if (_modifyToolSurfacesActivated)
        {
            return;
        }
        _modifyToolSurfacesActivated = true;

        // The v0.3 shell intentionally shipped the first four Modify buttons as inert
        // placeholders. v0.5 promotes those exact surfaces to real commands and appends
        // the remaining foundational transforms without reworking the frozen shell layout.
        var commands = new[] { "MOVE", "COPY", "OFFSET", "TRIM" };
        var existing = ModifyToolShelf.Children.OfType<Button>().ToArray();
        for (var i = 0; i < Math.Min(commands.Length, existing.Length); i++)
        {
            ConfigureModifyButton(existing[i], commands[i]);
        }

        foreach (var command in new[] { "ROTATE", "SCALE", "MIRROR", "EXTEND" })
        {
            ModifyToolShelf.Children.Add(CreateModifyShelfButton(command, existing.FirstOrDefault()?.Style));
        }

        // Promote the four high-frequency rail placeholders (MOVE/COPY/OFFSET/TRIM)
        // without depending on x:Uid as a code-side identifier.
        foreach (var button in Descendants<Button>(RootLayout))
        {
            if (button.Tag is not null || !TryGetModifyCommandLabel(button, out var command))
            {
                continue;
            }
            if (commands.Contains(command, StringComparer.Ordinal))
            {
                ConfigureModifyButton(button, command);
            }
        }

        ScheduleV05ModifySmokeIfRequested();
    }

    private void ConfigureModifyButton(Button button, string command)
    {
        button.Tag = command;
        button.IsHitTestVisible = true;
        button.IsEnabled = true;
        button.Opacity = 1;
        button.Click += RunCommand_Click;
    }

    private Button CreateModifyShelfButton(string command, Style? inheritedStyle)
    {
        var button = new Button
        {
            Tag = command,
            Style = inheritedStyle,
            IsHitTestVisible = true,
            IsEnabled = true,
            Opacity = 1,
            Content = new StackPanel
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                Spacing = 1,
                Children =
                {
                    CadToolIconService.Create(command),
                    new TextBlock
                    {
                        Text = command,
                        FontSize = 10,
                        HorizontalAlignment = HorizontalAlignment.Center
                    },
                    new TextBlock
                    {
                        Text = command switch
                        {
                            "ROTATE" => "RO",
                            "SCALE" => "SC",
                            "MIRROR" => "MI",
                            "EXTEND" => "EX",
                            _ => command
                        },
                        FontSize = 8,
                        Foreground = (Brush)Application.Current.Resources["UcadTextSecondaryBrush"],
                        HorizontalAlignment = HorizontalAlignment.Center
                    }
                }
            }
        };
        button.Click += RunCommand_Click;
        return button;
    }

    private static bool TryGetModifyCommandLabel(Button button, out string command)
    {
        command = string.Empty;
        if (button.Content is not StackPanel stack)
        {
            return false;
        }

        command = stack.Children
            .OfType<TextBlock>()
            .Select(text => text.Text?.Trim().ToUpperInvariant())
            .FirstOrDefault(text => text is "MOVE" or "COPY" or "OFFSET" or "TRIM") ?? string.Empty;
        return command.Length > 0;
    }

    private static IEnumerable<T> Descendants<T>(DependencyObject root) where T : DependencyObject
    {
        var count = VisualTreeHelper.GetChildrenCount(root);
        for (var i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is T match)
            {
                yield return match;
            }
            foreach (var descendant in Descendants<T>(child))
            {
                yield return descendant;
            }
        }
    }

    private void ScheduleV05ModifySmokeIfRequested()
    {
        if (_modifySmokeScheduled || !string.Equals(
                Environment.GetEnvironmentVariable("UCAD_MODIFY_SMOKE"),
                "1",
                StringComparison.Ordinal))
        {
            return;
        }

        _modifySmokeScheduled = true;
        if (RootLayout.IsLoaded)
        {
            RootLayout.DispatcherQueue.TryEnqueue(RunV05ModifySmokeGuarded);
        }
        else
        {
            RootLayout.Loaded += RootLayout_ModifySmokeLoaded;
        }
    }

    private void RootLayout_ModifySmokeLoaded(object sender, RoutedEventArgs e)
    {
        RootLayout.Loaded -= RootLayout_ModifySmokeLoaded;
        RootLayout.DispatcherQueue.TryEnqueue(RunV05ModifySmokeGuarded);
    }

    private void RunV05ModifySmokeGuarded()
    {
        try
        {
            RunV05ModifyInteractionSmoke();
        }
        catch (Exception ex)
        {
            App.WriteStartupFailure("ModifySmoke", ex);
            throw;
        }
    }

    private void RefreshModifyLocalization(CadWorkspaceSession session)
    {
        if (!_modifyContexts.TryGetValue(session, out var context) ||
            session.CommandSession.ActiveCommand?.Category != UCAD.Core.Commands.CadCommandCategory.Modify)
        {
            return;
        }

        var key = context.Phase switch
        {
            ModifyPhase.SelectObjects => "ModifySelectObjects",
            ModifyPhase.BasePoint => "ModifyBasePoint",
            ModifyPhase.TargetPoint => context.CommandName == "COPY" ? "ModifyCopyTarget" : "ModifyMoveTarget",
            ModifyPhase.RotationAngle => "ModifyRotationAngle",
            ModifyPhase.ScaleFactor => "ModifyScaleFactor",
            ModifyPhase.MirrorFirstPoint => "ModifyMirrorFirstPoint",
            ModifyPhase.MirrorSecondPoint => "ModifyMirrorSecondPoint",
            ModifyPhase.MirrorEraseOption => "ModifyMirrorEraseSource",
            ModifyPhase.OffsetDistance => "ModifyOffsetDistance",
            ModifyPhase.OffsetPickEntity => "ModifyOffsetPickEntity",
            ModifyPhase.OffsetSidePoint => "ModifyOffsetSidePoint",
            ModifyPhase.TrimPick => "ModifyTrimPick",
            ModifyPhase.ExtendPick => "ModifyExtendPick",
            _ => "ModifyComplete"
        };
        SetSessionStatus(session, ShellString(key));
    }
}

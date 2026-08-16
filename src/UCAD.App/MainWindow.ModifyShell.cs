using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using UCAD.Workspace;

namespace UCAD;

public sealed partial class MainWindow
{
    private bool _modifyToolSurfacesActivated;

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
            existing[i].Tag = commands[i];
            existing[i].IsHitTestVisible = true;
            existing[i].Opacity = 1;
            existing[i].Click += RunCommand_Click;
        }

        foreach (var command in new[] { "ROTATE", "SCALE", "MIRROR", "EXTEND" })
        {
            ModifyToolShelf.Children.Add(CreateModifyShelfButton(command));
        }
    }

    private Button CreateModifyShelfButton(string command)
    {
        var button = new Button
        {
            Tag = command,
            Style = (Style)Application.Current.Resources["UcadToolShelfButtonStyle"],
            IsHitTestVisible = true,
            Opacity = 1,
            Content = new StackPanel
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                Spacing = 1,
                Children =
                {
                    new TextBlock
                    {
                        Text = command,
                        FontSize = 10,
                        FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
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
                        Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["UcadTextSecondaryBrush"],
                        HorizontalAlignment = HorizontalAlignment.Center
                    }
                }
            }
        };
        button.Click += RunCommand_Click;
        return button;
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

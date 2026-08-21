using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using UCAD.Workspace;
using UCAD.Services;
using Windows.System;

namespace UCAD;

public sealed partial class MainWindow
{
    private bool _modifyToolSurfacesActivated;
    private bool _modifySmokeScheduled;
    private KeyboardAccelerator? _deleteDrawingAccelerator;

    private void ActivateModifyToolSurfaces()
    {
        if (_modifyToolSurfacesActivated)
        {
            return;
        }
        _modifyToolSurfacesActivated = true;

        // A routed KeyDown is not guaranteed when the CAD surface itself does not hold
        // keyboard focus. Register Delete as a root KeyboardAccelerator so the physical
        // key remains available anywhere in the drawing window while unrelated text
        // editors keep their normal Delete behavior.
        _deleteDrawingAccelerator = new KeyboardAccelerator
        {
            Key = VirtualKey.Delete
        };
        _deleteDrawingAccelerator.Invoked += DeleteDrawingAccelerator_Invoked;
        RootLayout.KeyboardAccelerators.Add(_deleteDrawingAccelerator);

        // The v0.3 shell intentionally shipped the first four Modify buttons as inert
        // placeholders. v0.5 promotes those exact surfaces to real commands. ERASE is inserted
        // as a discoverable high-frequency command; the remaining transforms are appended.
        var commands = new[] { "MOVE", "COPY", "OFFSET", "TRIM" };
        var existing = ModifyToolShelf.Children.OfType<Button>().ToArray();
        for (var i = 0; i < Math.Min(commands.Length, existing.Length); i++)
        {
            ConfigureModifyButton(existing[i], commands[i]);
        }

        var eraseButton = CreateModifyShelfButton("ERASE", existing.FirstOrDefault()?.Style);
        ModifyToolShelf.Children.Insert(Math.Min(2, ModifyToolShelf.Children.Count), eraseButton);

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

        EnsureAutoCadMigrationUi();
        ScheduleV05ModifySmokeIfRequested();
    }

    private void DeleteDrawingAccelerator_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        if (TryExecuteDeleteShortcut())
        {
            args.Handled = true;
        }
    }

    private bool TryExecuteDeleteShortcut()
    {
        if (ActiveSession is not CadWorkspaceSession session || session.CommandSession.IsActive)
        {
            return false;
        }

        var focused = RootLayout.XamlRoot is null
            ? null
            : FocusManager.GetFocusedElement(RootLayout.XamlRoot);

        // PR #19 acceptance rule: CommandInput is the CAD command owner, not a generic
        // text editor for the Delete key. Even when command text is present or selected,
        // physical Delete must execute ERASE and must not mutate that text. Other text
        // editors (settings, search, dialogs, etc.) retain ordinary text-editing Delete.
        if (!ReferenceEquals(focused, CommandInput) && IsTextEditingFocus(focused))
        {
            return false;
        }

        StartToolbarCommand("ERASE");
        return true;
    }

    private static bool IsTextEditingFocus(object? focused) => focused is
        TextBox or
        RichEditBox or
        PasswordBox or
        AutoSuggestBox or
        NumberBox;

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
                    CreateModifyCommandIcon(command),
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
                            "ERASE" => "E",
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

    private static IconElement CreateModifyCommandIcon(string command)
    {
        if (string.Equals(command, "ERASE", StringComparison.Ordinal))
        {
            return new FontIcon
            {
                FontFamily = new FontFamily("Segoe Fluent Icons"),
                Glyph = "\uE74D",
                FontSize = 16
            };
        }

        return CadToolIconService.Create(command);
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
        // VisualTreeHelper reports zero children for some Collapsed panels before they
        // have ever been materialized. Those are exactly the shelves that used to miss
        // the startup localization pass. Panels own a stable logical Children collection,
        // so traverse that collection directly and fall back to the visual tree elsewhere.
        if (root is Panel panel)
        {
            foreach (var child in panel.Children)
            {
                if (child is T match) yield return match;
                foreach (var descendant in Descendants<T>(child)) yield return descendant;
            }
            yield break;
        }

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

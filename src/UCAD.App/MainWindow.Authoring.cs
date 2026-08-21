using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using UCAD.Core.Blocks;
using UCAD.Core.Commands;
using UCAD.Core.Entities;
using UCAD.Core.Geometry;
using UCAD.Core.Layers;
using UCAD.Workspace;

using UCAD.Services;

namespace UCAD;

public sealed partial class MainWindow
{
    private readonly HashSet<CadWorkspaceSession> _authoringSubscribedSessions = [];
    private readonly HashSet<CadWorkspaceSession> _authoringRunningSessions = [];
    private readonly List<Button> _extendedShelfButtons = [];
    private bool _authoringUiInitialized;

    internal void EnsureAuthoringUiInitialized()
    {
        if (_authoringUiInitialized) return;
        _authoringUiInitialized = true;

        RootLayout.Loaded += Authoring_RootLoaded;
        DocumentTabs.SelectionChanged += Authoring_DocumentTabsSelectionChanged;
        foreach (var button in CategoryButtons) button.Click += Authoring_CategoryButtonClick;

        // Turn the reserved v0.3.9 HATCH shelf surface into the real v0.7 command.
        var drawButtons = DrawToolShelf.Children.OfType<Button>().ToArray();
        if (drawButtons.Length >= 6)
        {
            var hatchButton = drawButtons[5];
            hatchButton.Tag = "HATCH";
            hatchButton.IsHitTestVisible = true;
            hatchButton.Opacity = 1;
            hatchButton.Click += RunCommand_Click;
        }

        // EXPLODE belongs to Modify but is delivered together with the block foundation.
        var explodeButton = CreateShelfButton("EXPLODE", "X");
        ModifyToolShelf.Children.Add(explodeButton);

        AddExtendedShelfButton("ANNOTATE", "TEXT", "T");
        AddExtendedShelfButton("ANNOTATE", "DIM", "DLI");
        AddExtendedShelfButton("LAYERS", "LAYER", "LA");
        AddExtendedShelfButton("LAYERS", "CHPROP", "CH");
        AddExtendedShelfButton("BLOCKS", "BLOCK", "B");
        AddExtendedShelfButton("BLOCKS", "INSERT", "I");

        LayersTabButton.IsHitTestVisible = true;
        LayersTabButton.Opacity = 1;
        LayersTabButton.Click += (_, _) => StartToolbarCommand("LAYER");
    }

    private void Authoring_RootLoaded(object sender, RoutedEventArgs e)
    {
        RootLayout.Loaded -= Authoring_RootLoaded;
        if (ActiveSession is CadWorkspaceSession session) EnsureAuthoringSessionSubscribed(session);
    }

    private void Authoring_DocumentTabsSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ActiveSession is CadWorkspaceSession session)
        {
            EnsureAuthoringSessionSubscribed(session);
            ScheduleAuthoringInspectorRefresh(session);
        }
    }

    private void EnsureAuthoringSessionSubscribed(CadWorkspaceSession session)
    {
        session.Viewport.EnsureAuthoringRenderHooks();
        if (!_authoringSubscribedSessions.Add(session)) return;

        session.CommandSession.Changed += (_, _) => Authoring_CommandSessionChanged(session);
        session.Interaction.Selection.Changed += (_, _) => ScheduleAuthoringInspectorRefresh(session);
        session.Document.Changed += (_, _) => ScheduleAuthoringInspectorRefresh(session);
    }

    private void Authoring_CommandSessionChanged(CadWorkspaceSession session)
    {
        var command = session.CommandSession.ActiveCommand;
        if (command is null)
        {
            session.Viewport.CancelModifyInput();
            return;
        }
        if (!IsAuthoringCommand(command) || !_authoringRunningSessions.Add(session)) return;

        RootLayout.DispatcherQueue.TryEnqueue(async () =>
        {
            try
            {
                await RunAuthoringCommandAsync(session, command);
            }
            catch (TaskCanceledException)
            {
                // Esc / command replacement already owns the visible cancellation status.
            }
            catch (Exception ex)
            {
                App.WriteStartupFailure($"AuthoringCommand:{command.Name}", ex);
                SetSessionStatus(session, string.Format(AuthoringText("AuthoringFailedFormat"), command.Name, ex.Message));
                if (session.CommandSession.IsActive) session.CommandSession.Cancel();
            }
            finally
            {
                session.Viewport.CancelModifyInput();
                _authoringRunningSessions.Remove(session);
                ScheduleAuthoringInspectorRefresh(session);
            }
        });
    }

    private static bool IsAuthoringCommand(CadCommandDefinition command) =>
        command.Category is CadCommandCategory.Annotate or CadCommandCategory.Layer or CadCommandCategory.Block ||
        command.Name is "HATCH" or "EXPLODE";

    private async Task RunAuthoringCommandAsync(CadWorkspaceSession session, CadCommandDefinition command)
    {
        switch (command.Name)
        {
            case "LAYER":
                await ShowLayerManagerAsync(session);
                CompleteAuthoringCommand(session, AuthoringText("LayerComplete"));
                return;
            case "CHPROP":
                await ShowEntityPropertiesAsync(session);
                CompleteAuthoringCommand(session, AuthoringText("PropertiesComplete"));
                return;
            case "TEXT":
                await RunTextCommandAsync(session);
                return;
            case "DIM":
                await RunDimensionCommandAsync(session);
                return;
            case "HATCH":
                RunHatchCommand(session);
                return;
            case "BLOCK":
                await RunBlockCommandAsync(session);
                return;
            case "INSERT":
                await RunInsertCommandAsync(session);
                return;
            case "EXPLODE":
                RunExplodeCommand(session);
                return;
        }
    }

    private async Task RunTextCommandAsync(CadWorkspaceSession session)
    {
        var point = await RequestAuthoringPointAsync(session, AuthoringText("TextInsertionPoint"));
        var values = await PromptTextParametersAsync();
        if (values is null)
        {
            session.CommandSession.Cancel();
            return;
        }

        var entity = new TextEntity(point, values.Value.Text, values.Value.Height, values.Value.RotationDegrees * Math.PI / 180.0);
        session.Document.Add(entity);
        session.Interaction.Selection.Replace(entity.Id);
        CompleteAuthoringCommand(session, AuthoringText("TextComplete"));
    }

    private async Task RunDimensionCommandAsync(CadWorkspaceSession session)
    {
        var first = await RequestAuthoringPointAsync(session, AuthoringText("DimFirstPoint"));
        session.CommandBasePoint = first;
        var second = await RequestAuthoringPointAsync(session, AuthoringText("DimSecondPoint"), first, useOrtho: false);
        session.CommandBasePoint = second;
        var linePoint = await RequestAuthoringPointAsync(session, AuthoringText("DimLinePoint"));
        var entity = new LinearDimensionEntity(first, second, linePoint);
        session.Document.Add(entity);
        session.Interaction.Selection.Replace(entity.Id);
        CompleteAuthoringCommand(session, AuthoringText("DimComplete"));
    }

    private void RunHatchCommand(CadWorkspaceSession session)
    {
        var selected = session.Interaction.Selection.SelectedEntities;
        IReadOnlyList<CadPoint>? boundary = selected.Count == 1 ? selected[0] switch
        {
            PolylineEntity { Closed: true } polyline => polyline.Points,
            CircleEntity circle => Enumerable.Range(0, 64)
                .Select(index =>
                {
                    var angle = Math.Tau * index / 64.0;
                    return new CadPoint(circle.Center.X + Math.Cos(angle) * circle.Radius, circle.Center.Y + Math.Sin(angle) * circle.Radius);
                })
                .ToArray(),
            _ => null
        } : null;

        if (boundary is null)
        {
            SetSessionStatus(session, AuthoringText("HatchNeedsBoundary"));
            session.CommandSession.Cancel();
            return;
        }

        var sourceProperties = session.Document.GetEntityProperties(selected[0].Id);
        var hatch = new HatchEntity(boundary, "Solid");
        session.Document.Add(hatch, sourceProperties);
        session.Interaction.Selection.Replace(hatch.Id);
        CompleteAuthoringCommand(session, AuthoringText("HatchComplete"));
    }

    private async Task RunBlockCommandAsync(CadWorkspaceSession session)
    {
        var selected = session.Interaction.Selection.SelectedEntities;
        if (selected.Count == 0)
        {
            SetSessionStatus(session, AuthoringText("BlockNeedsSelection"));
            session.CommandSession.Cancel();
            return;
        }

        var name = await PromptSimpleTextAsync(AuthoringText("BlockNameTitle"), AuthoringText("BlockNamePrompt"), $"Block{session.Document.Blocks.Count + 1}");
        if (string.IsNullOrWhiteSpace(name))
        {
            session.CommandSession.Cancel();
            return;
        }
        var basePoint = await RequestAuthoringPointAsync(session, AuthoringText("BlockBasePoint"));
        session.Document.DefineBlock(new CadBlockDefinition(name, basePoint, selected));
        CompleteAuthoringCommand(session, string.Format(AuthoringText("BlockCompleteFormat"), name));
    }

    private async Task RunInsertCommandAsync(CadWorkspaceSession session)
    {
        if (session.Document.Blocks.Count == 0)
        {
            SetSessionStatus(session, AuthoringText("InsertNoBlocks"));
            session.CommandSession.Cancel();
            return;
        }

        var options = await PromptInsertOptionsAsync(session.Document.Blocks);
        if (options is null)
        {
            session.CommandSession.Cancel();
            return;
        }
        var insertion = await RequestAuthoringPointAsync(session, AuthoringText("InsertPoint"));
        var definition = session.Document.GetBlock(options.Value.BlockName);
        var reference = CadBlockFactory.CreateReference(
            definition,
            insertion,
            options.Value.Scale,
            options.Value.RotationDegrees * Math.PI / 180.0);
        session.Document.Add(reference);
        session.Interaction.Selection.Replace(reference.Id);
        CompleteAuthoringCommand(session, string.Format(AuthoringText("InsertCompleteFormat"), definition.Name));
    }

    private void RunExplodeCommand(CadWorkspaceSession session)
    {
        var references = session.Interaction.Selection.SelectedEntities.OfType<BlockReferenceEntity>().ToArray();
        if (references.Length != 1)
        {
            SetSessionStatus(session, AuthoringText("ExplodeNeedsOneBlock"));
            session.CommandSession.Cancel();
            return;
        }

        var reference = references[0];
        var pieces = CadBlockFactory.Explode(reference);
        session.Document.Replace(reference.Id, pieces);
        session.Interaction.Selection.Replace(pieces.Select(entity => entity.Id));
        CompleteAuthoringCommand(session, string.Format(AuthoringText("ExplodeCompleteFormat"), pieces.Count));
    }

    private async Task<CadPoint> RequestAuthoringPointAsync(
        CadWorkspaceSession session,
        string prompt,
        CadPoint? basePoint = null,
        bool useOrtho = true)
    {
        var tcs = new TaskCompletionSource<CadPoint>();
        Action<CadPoint>? pointHandler = null;
        EventHandler? commandHandler = null;
        pointHandler = point => tcs.TrySetResult(point);
        commandHandler = (_, _) =>
        {
            if (!session.CommandSession.IsActive) tcs.TrySetCanceled();
        };

        session.Viewport.ModifyPointAccepted += pointHandler;
        session.CommandSession.Changed += commandHandler;
        session.Viewport.BeginModifyPointInput(basePoint, useOrtho);
        SetSessionStatus(session, prompt);
        try
        {
            return await tcs.Task;
        }
        finally
        {
            session.Viewport.ModifyPointAccepted -= pointHandler;
            session.CommandSession.Changed -= commandHandler;
            session.Viewport.CancelModifyInput();
        }
    }

    private void CompleteAuthoringCommand(CadWorkspaceSession session, string status)
    {
        session.Viewport.CancelModifyInput();
        session.CommandBasePoint = null;
        if (session.CommandSession.IsActive) session.CommandSession.Complete();
        SetSessionStatus(session, status);
        UpdateSessionUi(session);
    }

    private async Task ShowLayerManagerAsync(CadWorkspaceSession session)
    {
        var content = new StackPanel { Spacing = 10, MinWidth = 560 };
        var currentLabel = new TextBlock { Text = AuthoringText("LayerCurrent") };
        var current = new ComboBox { MinWidth = 220 };
        var createRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        var newName = new TextBox { Width = 220, PlaceholderText = AuthoringText("LayerNewName") };
        var add = new Button { Content = AuthoringText("LayerAdd") };
        createRow.Children.Add(newName);
        createRow.Children.Add(add);
        var rows = new StackPanel { Spacing = 6 };
        content.Children.Add(currentLabel);
        content.Children.Add(current);
        content.Children.Add(createRow);
        content.Children.Add(new ScrollViewer { MaxHeight = 360, Content = rows });

        void Rebuild()
        {
            current.ItemsSource = session.Document.Layers.Select(layer => layer.Name).ToArray();
            current.SelectedItem = session.Document.CurrentLayerName;
            rows.Children.Clear();
            foreach (var layer in session.Document.Layers)
            {
                var row = new Grid { ColumnSpacing = 6 };
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(150) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(90) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(75) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(60) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(60) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                var name = new TextBox { Text = layer.Name, IsEnabled = layer.Name != CadLayer.DefaultLayerName };
                var color = new TextBox { Text = layer.ColorHex };
                var weight = new NumberBox { Value = layer.LineWeight, Minimum = 0.05, Maximum = 5, SmallChange = 0.05, SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Compact };
                var visible = new CheckBox { IsChecked = layer.IsVisible, Content = AuthoringText("LayerVisible") };
                var locked = new CheckBox { IsChecked = layer.IsLocked, Content = AuthoringText("LayerLocked") };
                var apply = new Button { Content = AuthoringText("LayerApply") };
                var delete = new Button { Content = AuthoringText("LayerDelete"), IsEnabled = layer.Name != CadLayer.DefaultLayerName };
                Grid.SetColumn(name, 0); Grid.SetColumn(color, 1); Grid.SetColumn(weight, 2); Grid.SetColumn(visible, 3); Grid.SetColumn(locked, 4); Grid.SetColumn(apply, 5); Grid.SetColumn(delete, 6);
                row.Children.Add(name); row.Children.Add(color); row.Children.Add(weight); row.Children.Add(visible); row.Children.Add(locked); row.Children.Add(apply); row.Children.Add(delete);

                apply.Click += (_, _) =>
                {
                    var activeName = layer.Name;
                    if (!string.Equals(name.Text.Trim(), layer.Name, StringComparison.Ordinal) && !string.IsNullOrWhiteSpace(name.Text))
                    {
                        session.Document.RenameLayer(layer.Name, name.Text.Trim());
                        activeName = name.Text.Trim();
                    }
                    session.Document.UpdateLayer(
                        activeName,
                        colorHex: color.Text.Trim(),
                        lineWeight: double.IsNaN(weight.Value) ? layer.LineWeight : weight.Value,
                        isVisible: visible.IsChecked == true,
                        isLocked: locked.IsChecked == true);
                    Rebuild();
                };
                delete.Click += (_, _) =>
                {
                    session.Document.DeleteLayer(layer.Name);
                    Rebuild();
                };
                rows.Children.Add(row);
            }
        }

        current.SelectionChanged += (_, _) =>
        {
            if (current.SelectedItem is string layerName && !string.Equals(layerName, session.Document.CurrentLayerName, StringComparison.Ordinal))
                session.Document.SetCurrentLayer(layerName);
        };
        add.Click += (_, _) =>
        {
            if (string.IsNullOrWhiteSpace(newName.Text)) return;
            try
            {
                session.Document.CreateLayer(new CadLayer(newName.Text.Trim()));
                session.Document.SetCurrentLayer(newName.Text.Trim());
                newName.Text = string.Empty;
                Rebuild();
            }
            catch (Exception ex)
            {
                SetSessionStatus(session, ex.Message);
            }
        };
        Rebuild();

        var dialog = new ContentDialog
        {
            XamlRoot = RootLayout.XamlRoot,
            Title = AuthoringText("LayerManagerTitle"),
            Content = content,
            CloseButtonText = AuthoringText("Close")
        };
        await dialog.ShowAsync();
    }

    private async Task ShowEntityPropertiesAsync(CadWorkspaceSession session)
    {
        var ids = session.Interaction.Selection.SelectedIds.ToArray();
        if (ids.Length == 0)
        {
            SetSessionStatus(session, AuthoringText("PropertiesNeedsSelection"));
            return;
        }
        var first = session.Document.GetEntityProperties(ids[0]);
        var panel = new StackPanel { Spacing = 8, MinWidth = 360 };
        var layer = new ComboBox { Header = AuthoringText("PropertyLayer"), ItemsSource = session.Document.Layers.Select(item => item.Name).ToArray(), SelectedItem = first.LayerName };
        var color = new TextBox { Header = AuthoringText("PropertyColor"), Text = first.ColorHex ?? string.Empty, PlaceholderText = AuthoringText("ByLayer") };
        var weight = new TextBox { Header = AuthoringText("PropertyLineWeight"), Text = first.LineWeight?.ToString("0.##") ?? string.Empty, PlaceholderText = AuthoringText("ByLayer") };
        var lineType = new ComboBox { Header = AuthoringText("PropertyLineType"), ItemsSource = new[] { "ByLayer", "Continuous", "Dashed", "Center" }, SelectedItem = first.LineType };
        panel.Children.Add(layer); panel.Children.Add(color); panel.Children.Add(weight); panel.Children.Add(lineType);
        var dialog = new ContentDialog
        {
            XamlRoot = RootLayout.XamlRoot,
            Title = string.Format(AuthoringText("PropertiesTitleFormat"), ids.Length),
            Content = panel,
            PrimaryButtonText = AuthoringText("Apply"),
            CloseButtonText = AuthoringText("Cancel")
        };
        if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;

        var layerName = layer.SelectedItem?.ToString() ?? first.LayerName;
        var colorValue = string.IsNullOrWhiteSpace(color.Text) ? null : color.Text.Trim();
        double? weightValue = null;
        if (!string.IsNullOrWhiteSpace(weight.Text) && (!double.TryParse(weight.Text, out var parsedWeight) || parsedWeight <= 0))
            throw new InvalidOperationException(AuthoringText("InvalidLineWeight"));
        if (!string.IsNullOrWhiteSpace(weight.Text)) weightValue = double.Parse(weight.Text);
        var lineTypeValue = lineType.SelectedItem?.ToString() ?? "ByLayer";
        session.Document.SetEntityProperties(ids, _ => new CadEntityProperties(layerName, colorValue, weightValue, lineTypeValue));
    }

    private async Task<(string Text, double Height, double RotationDegrees)?> PromptTextParametersAsync()
    {
        var panel = new StackPanel { Spacing = 8, MinWidth = 340 };
        var text = new TextBox { Header = AuthoringText("TextContent"), AcceptsReturn = true, MinHeight = 60 };
        var height = new NumberBox { Header = AuthoringText("TextHeight"), Value = 10, Minimum = 0.1, Maximum = 100000 };
        var rotation = new NumberBox { Header = AuthoringText("RotationDegrees"), Value = 0, Minimum = -36000, Maximum = 36000 };
        panel.Children.Add(text); panel.Children.Add(height); panel.Children.Add(rotation);
        var dialog = new ContentDialog { XamlRoot = RootLayout.XamlRoot, Title = AuthoringText("TextDialogTitle"), Content = panel, PrimaryButtonText = AuthoringText("Create"), CloseButtonText = AuthoringText("Cancel") };
        if (await dialog.ShowAsync() != ContentDialogResult.Primary || string.IsNullOrEmpty(text.Text)) return null;
        return (text.Text, height.Value, rotation.Value);
    }

    private async Task<(string BlockName, double Scale, double RotationDegrees)?> PromptInsertOptionsAsync(IReadOnlyList<CadBlockDefinition> blocks)
    {
        var panel = new StackPanel { Spacing = 8, MinWidth = 340 };
        var block = new ComboBox { Header = AuthoringText("InsertBlock"), ItemsSource = blocks.Select(item => item.Name).ToArray(), SelectedIndex = 0 };
        var scale = new NumberBox { Header = AuthoringText("InsertScale"), Value = 1, Minimum = 0.0001, Maximum = 100000 };
        var rotation = new NumberBox { Header = AuthoringText("RotationDegrees"), Value = 0, Minimum = -36000, Maximum = 36000 };
        panel.Children.Add(block); panel.Children.Add(scale); panel.Children.Add(rotation);
        var dialog = new ContentDialog { XamlRoot = RootLayout.XamlRoot, Title = AuthoringText("InsertDialogTitle"), Content = panel, PrimaryButtonText = AuthoringText("Continue"), CloseButtonText = AuthoringText("Cancel") };
        if (await dialog.ShowAsync() != ContentDialogResult.Primary || block.SelectedItem is not string name) return null;
        return (name, scale.Value, rotation.Value);
    }

    private async Task<string?> PromptSimpleTextAsync(string title, string prompt, string defaultValue)
    {
        var box = new TextBox { Header = prompt, Text = defaultValue, MinWidth = 320 };
        var dialog = new ContentDialog { XamlRoot = RootLayout.XamlRoot, Title = title, Content = box, PrimaryButtonText = AuthoringText("Continue"), CloseButtonText = AuthoringText("Cancel") };
        return await dialog.ShowAsync() == ContentDialogResult.Primary ? box.Text.Trim() : null;
    }

    private Button CreateShelfButton(string command, string alias)
    {
        var button = new Button
        {
            Tag = command,
            Style = (Style)Application.Current.Resources["UcadToolShelfButtonStyle"]
        };
        var panel = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center, Spacing = 1 };
        panel.Children.Add(CadToolIconService.Create(command));
        panel.Children.Add(new TextBlock { Text = command, FontSize = 10, FontWeight = Microsoft.UI.Text.FontWeights.Medium, HorizontalAlignment = HorizontalAlignment.Center });
        panel.Children.Add(new TextBlock { Text = alias, FontSize = 8, Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["UcadTextSecondaryBrush"], HorizontalAlignment = HorizontalAlignment.Center });
        button.Content = panel;
        button.Click += RunCommand_Click;
        return button;
    }

    private void AddExtendedShelfButton(string category, string command, string alias)
    {
        var button = CreateShelfButton(command, alias);
        button.Tag = $"{category}|{command}";
        button.Click -= RunCommand_Click;
        button.Click += (_, _) => StartToolbarCommand(command);
        button.Visibility = Visibility.Collapsed;
        _extendedShelfButtons.Add(button);
        UnavailableToolShelf.Children.Add(button);
    }

    private void Authoring_CategoryButtonClick(object sender, RoutedEventArgs e)
    {
        if (sender is not ToggleButton { Tag: string category }) return;
        if (ToolShelfHost.Visibility != Visibility.Visible || _activeShelfCategory != category) return;
        var supported = category is "ANNOTATE" or "LAYERS" or "BLOCKS";
        UnavailableToolShelfText.Visibility = supported ? Visibility.Collapsed : Visibility.Visible;
        foreach (var button in _extendedShelfButtons)
        {
            var owner = button.Tag?.ToString()?.Split('|')[0];
            button.Visibility = supported && owner == category ? Visibility.Visible : Visibility.Collapsed;
        }
    }

    private void ScheduleAuthoringInspectorRefresh(CadWorkspaceSession session)
    {
        RootLayout.DispatcherQueue.TryEnqueue(() =>
        {
            if (!ReferenceEquals(ActiveSession, session)) return;
            var selected = session.Interaction.Selection.SelectedEntities;
            if (selected.Count != 1) return;
            var properties = session.Document.GetEntityProperties(selected[0].Id);
            var layer = session.Document.GetLayer(properties.LayerName);
            var color = properties.ColorHex ?? $"{AuthoringText("ByLayer")} {layer.ColorHex}";
            var weight = properties.LineWeight?.ToString("0.##") ?? $"{AuthoringText("ByLayer")} {layer.LineWeight:0.##}";
            V04FoundationHint.Text = string.Format(
                AuthoringText("InspectorPropertySummaryFormat"),
                properties.LayerName,
                color,
                weight,
                properties.LineType);
        });
    }

    private string AuthoringText(string key)
    {
        var value = LocalizationService.Current.GetShellString(key);
        return string.IsNullOrWhiteSpace(value) || value == key ? AuthoringEnglishFallback(key) : value;
    }

    private static string AuthoringEnglishFallback(string key) => key switch
    {
        "AuthoringFailedFormat" => "{0} failed: {1}",
        "LayerComplete" => "Layer manager closed.",
        "PropertiesComplete" => "Properties updated.",
        "TextInsertionPoint" => "TEXT: specify insertion point.",
        "TextComplete" => "Text created.",
        "DimFirstPoint" => "DIM: specify first extension point.",
        "DimSecondPoint" => "DIM: specify second extension point.",
        "DimLinePoint" => "DIM: specify dimension line location.",
        "DimComplete" => "Dimension created.",
        "HatchNeedsBoundary" => "HATCH requires one selected closed polyline or circle.",
        "HatchComplete" => "Solid hatch created.",
        "BlockNeedsSelection" => "BLOCK requires a selection.",
        "BlockNameTitle" => "Create Block",
        "BlockNamePrompt" => "Block name",
        "BlockBasePoint" => "BLOCK: specify base point.",
        "BlockCompleteFormat" => "Block '{0}' defined.",
        "InsertNoBlocks" => "No block definitions are available.",
        "InsertPoint" => "INSERT: specify insertion point.",
        "InsertCompleteFormat" => "Block '{0}' inserted.",
        "ExplodeNeedsOneBlock" => "EXPLODE requires exactly one selected block reference.",
        "ExplodeCompleteFormat" => "Block exploded into {0} entities.",
        "LayerCurrent" => "Current layer",
        "LayerNewName" => "New layer name",
        "LayerAdd" => "Add",
        "LayerVisible" => "On",
        "LayerLocked" => "Lock",
        "LayerApply" => "Apply",
        "LayerDelete" => "Delete",
        "LayerManagerTitle" => "Layer Manager",
        "Close" => "Close",
        "PropertiesNeedsSelection" => "CHPROP requires a selection.",
        "PropertyLayer" => "Layer",
        "PropertyColor" => "Color (#RRGGBB)",
        "PropertyLineWeight" => "Lineweight",
        "PropertyLineType" => "Linetype",
        "ByLayer" => "ByLayer",
        "PropertiesTitleFormat" => "Properties — {0} selected",
        "Apply" => "Apply",
        "Cancel" => "Cancel",
        "InvalidLineWeight" => "Lineweight must be a positive number.",
        "TextContent" => "Text",
        "TextHeight" => "Height",
        "RotationDegrees" => "Rotation (degrees)",
        "TextDialogTitle" => "Text",
        "Create" => "Create",
        "InsertBlock" => "Block",
        "InsertScale" => "Scale",
        "InsertDialogTitle" => "Insert Block",
        "Continue" => "Continue",
        "InspectorPropertySummaryFormat" => "Layer: {0}   Color: {1}   Lineweight: {2}   Linetype: {3}",
        _ => key
    };
}

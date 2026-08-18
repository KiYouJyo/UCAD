using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using UCAD.Core;
using UCAD.Core.Commands;
using UCAD.Core.Entities;
using UCAD.Core.Geometry;
using UCAD.Core.Modify;
using UCAD.Workspace;

namespace UCAD;

public sealed partial class MainWindow
{
    private readonly HashSet<CadWorkspaceSession> _modifyCompletionSubscribedSessions = [];
    private readonly HashSet<CadWorkspaceSession> _modifyCompletionRunningSessions = [];
    private bool _modifyCompletionUiInitialized;

    internal void EnsureModifyCompletionUiInitialized()
    {
        if (_modifyCompletionUiInitialized) return;
        _modifyCompletionUiInitialized = true;

        RegisterModifyCompletionCommands();
        AppendModifyCompletionButtons();
        RefreshCommandSearchSource();
        RootLayout.Loaded += ModifyCompletion_RootLoaded;
        DocumentTabs.SelectionChanged += ModifyCompletion_DocumentTabsSelectionChanged;
    }

    private void RegisterModifyCompletionCommands()
    {
        RegisterCompletionCommand("STRETCH", "S");
        RegisterCompletionCommand("FILLET", "F");
        RegisterCompletionCommand("CHAMFER", "CHA");
        RegisterCompletionCommand("ARRAY", "AR");
        RegisterCompletionCommand("BREAK", "BR");
        RegisterCompletionCommand("JOIN", "J");
        RegisterCompletionCommand("PEDIT", "PE");
    }

    private void RegisterCompletionCommand(string name, params string[] aliases)
    {
        if (_commandRegistry.TryResolve(name, out _)) return;
        // The legacy v0.5 Modify controller owns every CadCommandCategory.Modify command.
        // Completion commands use Edit until that controller is refactored into shared
        // handlers; their visible location and behavior remain the Modify shelf.
        _commandRegistry.Register(new CadCommandDefinition(name, CadCommandCategory.Edit, aliases));
    }

    private void AppendModifyCompletionButtons()
    {
        var inheritedStyle = ModifyToolShelf.Children.OfType<Button>().FirstOrDefault()?.Style;
        foreach (var command in new[] { "STRETCH", "FILLET", "CHAMFER", "ARRAY", "BREAK", "JOIN", "PEDIT" })
        {
            if (ModifyToolShelf.Children.OfType<Button>().Any(button => string.Equals(button.Tag?.ToString(), command, StringComparison.Ordinal)))
                continue;
            ModifyToolShelf.Children.Add(CreateModifyCompletionButton(command, inheritedStyle));
        }
    }

    private Button CreateModifyCompletionButton(string command, Style? style)
    {
        var alias = command switch
        {
            "STRETCH" => "S",
            "FILLET" => "F",
            "CHAMFER" => "CHA",
            "ARRAY" => "AR",
            "BREAK" => "BR",
            "JOIN" => "J",
            "PEDIT" => "PE",
            _ => command
        };
        var button = new Button
        {
            Tag = command,
            Style = style,
            IsHitTestVisible = true,
            IsEnabled = true,
            Opacity = 1,
            Content = new StackPanel
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                Spacing = 1,
                Children =
                {
                    new TextBlock { Text = command, FontSize = 10, HorizontalAlignment = HorizontalAlignment.Center },
                    new TextBlock
                    {
                        Text = alias,
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

    private void ModifyCompletion_RootLoaded(object sender, RoutedEventArgs e)
    {
        RootLayout.Loaded -= ModifyCompletion_RootLoaded;
        if (ActiveSession is CadWorkspaceSession session) EnsureModifyCompletionSessionSubscribed(session);
    }

    private void ModifyCompletion_DocumentTabsSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ActiveSession is CadWorkspaceSession session) EnsureModifyCompletionSessionSubscribed(session);
    }

    private void EnsureModifyCompletionSessionSubscribed(CadWorkspaceSession session)
    {
        session.Viewport.EnsureModifyInputHooks();
        session.Viewport.EnsureDraftingAidHooks();
        if (!_modifyCompletionSubscribedSessions.Add(session)) return;
        session.CommandSession.Changed += (_, _) => ModifyCompletion_CommandSessionChanged(session);
    }

    private void ModifyCompletion_CommandSessionChanged(CadWorkspaceSession session)
    {
        var command = session.CommandSession.ActiveCommand;
        if (command is null || !IsModifyCompletionCommand(command.Name) || !_modifyCompletionRunningSessions.Add(session)) return;

        RootLayout.DispatcherQueue.TryEnqueue(async () =>
        {
            try
            {
                await RunModifyCompletionCommandAsync(session, command.Name);
            }
            catch (TaskCanceledException)
            {
                // Esc/command replacement owns the visible cancellation status.
            }
            catch (Exception ex)
            {
                App.WriteStartupFailure($"ModifyCompletion:{command.Name}", ex);
                SetSessionStatus(session, string.Format(ModifyCompletionText("FailedFormat"), command.Name, ex.Message));
                if (session.CommandSession.IsActive) session.CommandSession.Cancel();
            }
            finally
            {
                session.Viewport.CancelModifyInput();
                session.CommandBasePoint = null;
                _modifyCompletionRunningSessions.Remove(session);
                UpdateSessionUi(session);
            }
        });
    }

    private static bool IsModifyCompletionCommand(string name) => name is
        "STRETCH" or "FILLET" or "CHAMFER" or "ARRAY" or "BREAK" or "JOIN" or "PEDIT";

    private async Task RunModifyCompletionCommandAsync(CadWorkspaceSession session, string command)
    {
        switch (command)
        {
            case "FILLET": await RunFilletAsync(session); break;
            case "CHAMFER": await RunChamferAsync(session); break;
            case "JOIN": await RunJoinAsync(session); break;
            case "BREAK": await RunBreakAsync(session); break;
            case "ARRAY": await RunArrayAsync(session); break;
            case "STRETCH": await RunStretchAsync(session); break;
            case "PEDIT": await RunPeditAsync(session); break;
        }
    }

    private async Task RunFilletAsync(CadWorkspaceSession session)
    {
        var radius = await PromptModifyNumberAsync(ModifyCompletionText("FilletTitle"), ModifyCompletionText("Radius"), 5, 0.0001, 1_000_000);
        if (radius is null) { session.CommandSession.Cancel(); return; }
        var first = await RequestCompletionEntityAsync(session, ModifyCompletionText("PickFirstLine"), entity => entity is LineEntity);
        var second = await RequestCompletionEntityAsync(session, ModifyCompletionText("PickSecondLine"), entity => entity is LineEntity && entity.Id != first.Entity.Id);
        if (!CadFilletChamfer.TryFillet(
                (LineEntity)first.Entity, first.PickPoint,
                (LineEntity)second.Entity, second.PickPoint,
                radius.Value,
                out var firstResult, out var secondResult, out var arc) ||
            firstResult is null || secondResult is null || arc is null)
            throw new InvalidOperationException(ModifyCompletionText("CornerInvalid"));

        var properties = session.Document.GetEntityProperties(first.Entity.Id);
        session.Document.ApplyCompoundEdit(
            replacements: [firstResult, secondResult],
            additions: [(arc, properties)]);
        session.Interaction.Selection.Replace(arc.Id);
        CompleteModifyCompletion(session, ModifyCompletionText("FilletComplete"));
    }

    private async Task RunChamferAsync(CadWorkspaceSession session)
    {
        var distances = await PromptChamferAsync();
        if (distances is null) { session.CommandSession.Cancel(); return; }
        var first = await RequestCompletionEntityAsync(session, ModifyCompletionText("PickFirstLine"), entity => entity is LineEntity);
        var second = await RequestCompletionEntityAsync(session, ModifyCompletionText("PickSecondLine"), entity => entity is LineEntity && entity.Id != first.Entity.Id);
        if (!CadFilletChamfer.TryChamfer(
                (LineEntity)first.Entity, first.PickPoint,
                (LineEntity)second.Entity, second.PickPoint,
                distances.Value.First, distances.Value.Second,
                out var firstResult, out var secondResult, out var chamfer) ||
            firstResult is null || secondResult is null || chamfer is null)
            throw new InvalidOperationException(ModifyCompletionText("CornerInvalid"));

        var properties = session.Document.GetEntityProperties(first.Entity.Id);
        session.Document.ApplyCompoundEdit(
            replacements: [firstResult, secondResult],
            additions: [(chamfer, properties)]);
        session.Interaction.Selection.Replace(chamfer.Id);
        CompleteModifyCompletion(session, ModifyCompletionText("ChamferComplete"));
    }

    private async Task RunJoinAsync(CadWorkspaceSession session)
    {
        var first = await RequestCompletionEntityAsync(session, ModifyCompletionText("JoinFirst"), IsJoinable);
        var second = await RequestCompletionEntityAsync(session, ModifyCompletionText("JoinSecond"), entity => IsJoinable(entity) && entity.Id != first.Entity.Id);
        var tolerance = await PromptModifyNumberAsync(ModifyCompletionText("JoinTitle"), ModifyCompletionText("Tolerance"), 0.01, 0, 1_000_000);
        if (tolerance is null) { session.CommandSession.Cancel(); return; }
        if (!CadJoinBreak.TryJoin(first.Entity, second.Entity, tolerance.Value, out var joined) || joined is null)
            throw new InvalidOperationException(ModifyCompletionText("JoinInvalid"));

        session.Document.ApplyCompoundEdit(replacements: [joined], removals: [second.Entity.Id]);
        session.Interaction.Selection.Replace(joined.Id);
        CompleteModifyCompletion(session, ModifyCompletionText("JoinComplete"));
    }

    private async Task RunBreakAsync(CadWorkspaceSession session)
    {
        var selected = await RequestCompletionEntityAsync(
            session,
            ModifyCompletionText("BreakEntity"),
            entity => entity is LineEntity || entity is PolylineEntity { Closed: false });
        var first = await RequestCompletionPointAsync(session, ModifyCompletionText("BreakFirst"));
        session.CommandBasePoint = first;
        var second = await RequestCompletionPointAsync(session, ModifyCompletionText("BreakSecond"), first);
        if (!CadJoinBreak.TryBreak(selected.Entity, first, second, out var pieces) || pieces.Count == 0)
            throw new InvalidOperationException(ModifyCompletionText("BreakInvalid"));

        var sourceProperties = session.Document.GetEntityProperties(selected.Entity.Id);
        var replacement = pieces.FirstOrDefault(piece => piece.Id == selected.Entity.Id);
        var additions = pieces
            .Where(piece => piece.Id != selected.Entity.Id)
            .Select(piece => (piece, sourceProperties))
            .ToArray();
        session.Document.ApplyCompoundEdit(
            replacements: replacement is null ? [] : [replacement],
            removals: replacement is null ? [selected.Entity.Id] : [],
            additions: additions);
        session.Interaction.Selection.Clear();
        foreach (var piece in pieces) session.Interaction.Selection.Add(piece.Id);
        CompleteModifyCompletion(session, ModifyCompletionText("BreakComplete"));
    }

    private async Task RunArrayAsync(CadWorkspaceSession session)
    {
        var sources = session.Interaction.Selection.SelectedEntities.ToArray();
        if (sources.Length == 0)
        {
            var picked = await RequestCompletionEntityAsync(session, ModifyCompletionText("ArraySelect"), _ => true);
            sources = [picked.Entity];
            session.Interaction.Selection.Replace(picked.Entity.Id);
        }

        var parameters = await PromptArrayAsync();
        if (parameters is null) { session.CommandSession.Cancel(); return; }
        IReadOnlyList<ICadEntity> copies;
        if (parameters.Value.Kind == ArrayKind.Rectangular)
        {
            copies = CadArray.CreateRectangular(
                sources,
                parameters.Value.Rows,
                parameters.Value.Columns,
                parameters.Value.RowSpacing,
                parameters.Value.ColumnSpacing);
        }
        else
        {
            var center = await RequestCompletionPointAsync(session, ModifyCompletionText("ArrayCenter"));
            copies = CadArray.CreatePolar(
                sources,
                center,
                parameters.Value.Items,
                parameters.Value.FillAngleDegrees * Math.PI / 180.0,
                parameters.Value.RotateItems);
        }

        var sourceProperties = sources.Select(entity => session.Document.GetEntityProperties(entity.Id)).ToArray();
        var additions = copies.Select((copy, index) => (copy, sourceProperties[index % sourceProperties.Length])).ToArray();
        if (additions.Length > 0) session.Document.AddRange(additions);
        session.Interaction.Selection.Clear();
        foreach (var copy in copies) session.Interaction.Selection.Add(copy.Id);
        CompleteModifyCompletion(session, string.Format(ModifyCompletionText("ArrayCompleteFormat"), copies.Count));
    }

    private async Task RunStretchAsync(CadWorkspaceSession session)
    {
        var firstCorner = await RequestCompletionPointAsync(session, ModifyCompletionText("StretchWindowFirst"));
        var secondCorner = await RequestCompletionPointAsync(session, ModifyCompletionText("StretchWindowSecond"), firstCorner);
        var window = CadRect.FromPoints(firstCorner, secondCorner);
        var basePoint = await RequestCompletionPointAsync(session, ModifyCompletionText("StretchBase"));
        session.CommandBasePoint = basePoint;
        var target = await RequestCompletionPointAsync(
            session,
            ModifyCompletionText("StretchTarget"),
            basePoint,
            useOrtho: true,
            previewFactory: pointer => BuildStretchPreview(session, window, pointer - basePoint));
        var displacement = target - basePoint;
        var replacements = BuildStretchPreview(session, window, displacement);
        if (replacements.Count == 0) throw new InvalidOperationException(ModifyCompletionText("StretchNothing"));
        session.Document.ReplaceRange(replacements);
        session.Interaction.Selection.Replace(replacements.Select(entity => entity.Id));
        CompleteModifyCompletion(session, string.Format(ModifyCompletionText("StretchCompleteFormat"), replacements.Count));
    }

    private async Task RunPeditAsync(CadWorkspaceSession session)
    {
        var picked = await RequestCompletionEntityAsync(session, ModifyCompletionText("PeditSelect"), entity => entity is PolylineEntity);
        var polyline = (PolylineEntity)picked.Entity;
        var action = await PromptPeditActionAsync(polyline.Closed);
        if (action is null) { session.CommandSession.Cancel(); return; }

        switch (action.Value)
        {
            case PeditAction.Reverse:
                session.Document.ReplaceRange([CadPolylineEdit.Reverse(polyline)]);
                break;
            case PeditAction.Close:
                session.Document.ReplaceRange([CadPolylineEdit.SetClosed(polyline, true)]);
                break;
            case PeditAction.Open:
                session.Document.ReplaceRange([CadPolylineEdit.SetClosed(polyline, false)]);
                break;
            case PeditAction.Join:
                var tolerance = await PromptModifyNumberAsync(ModifyCompletionText("PeditTitle"), ModifyCompletionText("Tolerance"), 0.01, 0, 1_000_000);
                if (tolerance is null) { session.CommandSession.Cancel(); return; }
                if (!CadPolylineEdit.TryJoinMany(
                        polyline,
                        session.Document.SelectableEntities,
                        tolerance.Value,
                        out var joined,
                        out var consumed))
                    throw new InvalidOperationException(ModifyCompletionText("JoinInvalid"));
                session.Document.ApplyCompoundEdit(replacements: [joined], removals: consumed);
                polyline = joined;
                break;
        }

        session.Interaction.Selection.Replace(polyline.Id);
        CompleteModifyCompletion(session, ModifyCompletionText("PeditComplete"));
    }

    private async Task<(ICadEntity Entity, CadPoint PickPoint)> RequestCompletionEntityAsync(
        CadWorkspaceSession session,
        string prompt,
        Func<ICadEntity, bool> predicate)
    {
        var tcs = new TaskCompletionSource<(ICadEntity, CadPoint)>(TaskCreationOptions.RunContinuationsAsynchronously);
        void Picked(Guid id, CadPoint point)
        {
            var entity = session.Document.SelectableEntities.FirstOrDefault(candidate => candidate.Id == id);
            if (entity is null || !predicate(entity))
            {
                SetSessionStatus(session, ModifyCompletionText("InvalidEntity"));
                session.Viewport.BeginModifyEntityPickInput();
                return;
            }
            tcs.TrySetResult((entity, point));
        }
        void CommandChanged(object? sender, EventArgs e)
        {
            var active = session.CommandSession.ActiveCommand;
            if (active is null || !IsModifyCompletionCommand(active.Name)) tcs.TrySetCanceled();
        }

        session.Viewport.ModifyEntityPicked += Picked;
        session.CommandSession.Changed += CommandChanged;
        try
        {
            SetSessionStatus(session, prompt);
            session.Viewport.BeginModifyEntityPickInput();
            return await tcs.Task;
        }
        finally
        {
            session.Viewport.ModifyEntityPicked -= Picked;
            session.CommandSession.Changed -= CommandChanged;
        }
    }

    private async Task<CadPoint> RequestCompletionPointAsync(
        CadWorkspaceSession session,
        string prompt,
        CadPoint? basePoint = null,
        bool useOrtho = false,
        Func<CadPoint, IReadOnlyList<ICadEntity>>? previewFactory = null)
    {
        var tcs = new TaskCompletionSource<CadPoint>(TaskCreationOptions.RunContinuationsAsynchronously);
        void Accepted(CadPoint point) => tcs.TrySetResult(point);
        void CommandChanged(object? sender, EventArgs e)
        {
            var active = session.CommandSession.ActiveCommand;
            if (active is null || !IsModifyCompletionCommand(active.Name)) tcs.TrySetCanceled();
        }

        session.Viewport.ModifyPointAccepted += Accepted;
        session.CommandSession.Changed += CommandChanged;
        try
        {
            SetSessionStatus(session, prompt);
            session.Viewport.BeginModifyPointInput(basePoint, useOrtho, previewFactory);
            return await tcs.Task;
        }
        finally
        {
            session.Viewport.ModifyPointAccepted -= Accepted;
            session.CommandSession.Changed -= CommandChanged;
        }
    }

    private static IReadOnlyList<ICadEntity> BuildStretchPreview(CadWorkspaceSession session, CadRect window, CadVector displacement)
    {
        var replacements = new List<ICadEntity>();
        foreach (var entity in session.Document.SelectableEntities)
            if (CadStretch.TryStretch(entity, window, displacement, out var stretched) && stretched is not null)
                replacements.Add(stretched);
        return replacements;
    }

    private async Task<double?> PromptModifyNumberAsync(string title, string label, double initial, double minimum, double maximum)
    {
        var number = new NumberBox
        {
            Value = initial,
            Minimum = minimum,
            Maximum = maximum,
            SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Inline,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        var dialog = new ContentDialog
        {
            XamlRoot = RootLayout.XamlRoot,
            Title = title,
            Content = new StackPanel
            {
                Width = 300,
                Spacing = 8,
                Children = { new TextBlock { Text = label }, number }
            },
            PrimaryButtonText = ModifyCompletionText("Confirm"),
            CloseButtonText = ModifyCompletionText("Cancel"),
            DefaultButton = ContentDialogButton.Primary
        };
        if (await dialog.ShowAsync() != ContentDialogResult.Primary) return null;
        return double.IsNaN(number.Value) ? initial : Math.Clamp(number.Value, minimum, maximum);
    }

    private async Task<(double First, double Second)?> PromptChamferAsync()
    {
        var first = new NumberBox { Value = 5, Minimum = 0.0001, Maximum = 1_000_000, SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Inline };
        var second = new NumberBox { Value = 5, Minimum = 0.0001, Maximum = 1_000_000, SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Inline };
        var content = new StackPanel
        {
            Width = 300,
            Spacing = 8,
            Children =
            {
                new TextBlock { Text = ModifyCompletionText("FirstDistance") }, first,
                new TextBlock { Text = ModifyCompletionText("SecondDistance") }, second
            }
        };
        var dialog = new ContentDialog
        {
            XamlRoot = RootLayout.XamlRoot,
            Title = ModifyCompletionText("ChamferTitle"),
            Content = content,
            PrimaryButtonText = ModifyCompletionText("Confirm"),
            CloseButtonText = ModifyCompletionText("Cancel"),
            DefaultButton = ContentDialogButton.Primary
        };
        if (await dialog.ShowAsync() != ContentDialogResult.Primary) return null;
        return (double.IsNaN(first.Value) ? 5 : first.Value, double.IsNaN(second.Value) ? 5 : second.Value);
    }

    private async Task<ArrayParameters?> PromptArrayAsync()
    {
        var kind = new ComboBox
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            ItemsSource = new[] { ModifyCompletionText("Rectangular"), ModifyCompletionText("Polar") },
            SelectedIndex = 0
        };
        var rows = Number(2, 1, 1000);
        var columns = Number(2, 1, 1000);
        var rowSpacing = Number(20, -1_000_000, 1_000_000);
        var columnSpacing = Number(20, -1_000_000, 1_000_000);
        var items = Number(6, 2, 1000);
        var fill = Number(360, -3600, 3600);
        var rotate = new ToggleSwitch { IsOn = true };
        var content = new StackPanel
        {
            Width = 330,
            Spacing = 6,
            Children =
            {
                Label(ModifyCompletionText("ArrayType")), kind,
                Label(ModifyCompletionText("Rows")), rows,
                Label(ModifyCompletionText("Columns")), columns,
                Label(ModifyCompletionText("RowSpacing")), rowSpacing,
                Label(ModifyCompletionText("ColumnSpacing")), columnSpacing,
                Label(ModifyCompletionText("Items")), items,
                Label(ModifyCompletionText("FillAngle")), fill,
                Label(ModifyCompletionText("RotateItems")), rotate
            }
        };
        var dialog = new ContentDialog
        {
            XamlRoot = RootLayout.XamlRoot,
            Title = ModifyCompletionText("ArrayTitle"),
            Content = content,
            PrimaryButtonText = ModifyCompletionText("Confirm"),
            CloseButtonText = ModifyCompletionText("Cancel"),
            DefaultButton = ContentDialogButton.Primary
        };
        if (await dialog.ShowAsync() != ContentDialogResult.Primary) return null;
        return new ArrayParameters(
            kind.SelectedIndex == 1 ? ArrayKind.Polar : ArrayKind.Rectangular,
            Math.Max(1, (int)Math.Round(rows.Value)),
            Math.Max(1, (int)Math.Round(columns.Value)),
            rowSpacing.Value,
            columnSpacing.Value,
            Math.Max(2, (int)Math.Round(items.Value)),
            Math.Abs(fill.Value) <= 1e-9 ? 360 : fill.Value,
            rotate.IsOn);
    }

    private async Task<PeditAction?> PromptPeditActionAsync(bool currentlyClosed)
    {
        var actions = new List<(PeditAction Action, string Label)>
        {
            (PeditAction.Reverse, ModifyCompletionText("Reverse")),
            (PeditAction.Join, ModifyCompletionText("Join")),
            (currentlyClosed ? PeditAction.Open : PeditAction.Close, currentlyClosed ? ModifyCompletionText("Open") : ModifyCompletionText("Close"))
        };
        var combo = new ComboBox
        {
            Width = 280,
            ItemsSource = actions.Select(item => item.Label).ToArray(),
            SelectedIndex = 0
        };
        var dialog = new ContentDialog
        {
            XamlRoot = RootLayout.XamlRoot,
            Title = ModifyCompletionText("PeditTitle"),
            Content = combo,
            PrimaryButtonText = ModifyCompletionText("Confirm"),
            CloseButtonText = ModifyCompletionText("Cancel"),
            DefaultButton = ContentDialogButton.Primary
        };
        if (await dialog.ShowAsync() != ContentDialogResult.Primary) return null;
        return actions[Math.Max(0, combo.SelectedIndex)].Action;
    }

    private static NumberBox Number(double value, double min, double max) => new()
    {
        Value = value,
        Minimum = min,
        Maximum = max,
        SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Inline,
        HorizontalAlignment = HorizontalAlignment.Stretch
    };

    private static TextBlock Label(string text) => new() { Text = text, FontSize = 11 };

    private static bool IsJoinable(ICadEntity entity) =>
        entity is LineEntity || entity is PolylineEntity { Closed: false };

    private void CompleteModifyCompletion(CadWorkspaceSession session, string status)
    {
        session.Viewport.CancelModifyInput();
        session.CommandBasePoint = null;
        session.CommandSession.Complete();
        SetSessionStatus(session, status);
        UpdateSessionUi(session);
    }

    private static string ModifyCompletionText(string key)
    {
        var language = Services.LocalizationService.Current.CurrentLanguageTag;
        var ja = language.StartsWith("ja", StringComparison.OrdinalIgnoreCase);
        var en = language.StartsWith("en", StringComparison.OrdinalIgnoreCase);
        return key switch
        {
            "FilletTitle" => ja ? "フィレット" : en ? "Fillet" : "圆角",
            "ChamferTitle" => ja ? "面取り" : en ? "Chamfer" : "倒角",
            "JoinTitle" => ja ? "結合" : en ? "Join" : "合并",
            "ArrayTitle" => ja ? "配列複写" : en ? "Array" : "阵列",
            "PeditTitle" => ja ? "ポリライン編集" : en ? "Polyline edit" : "多段线编辑",
            "Radius" => ja ? "半径" : en ? "Radius" : "半径",
            "FirstDistance" => ja ? "第 1 距離" : en ? "First distance" : "第一距离",
            "SecondDistance" => ja ? "第 2 距離" : en ? "Second distance" : "第二距离",
            "Tolerance" => ja ? "結合許容差" : en ? "Join tolerance" : "合并容差",
            "PickFirstLine" => ja ? "1 本目の線分を選択:" : en ? "Select first line:" : "选择第一条直线：",
            "PickSecondLine" => ja ? "2 本目の線分を選択:" : en ? "Select second line:" : "选择第二条直线：",
            "JoinFirst" => ja ? "最初の線/ポリラインを選択:" : en ? "Select first line/polyline:" : "选择第一条直线/多段线：",
            "JoinSecond" => ja ? "結合する線/ポリラインを選択:" : en ? "Select line/polyline to join:" : "选择要合并的直线/多段线：",
            "BreakEntity" => ja ? "切断する線/ポリラインを選択:" : en ? "Select line/polyline to break:" : "选择要打断的直线/多段线：",
            "BreakFirst" => ja ? "第 1 切断点を指定:" : en ? "Specify first break point:" : "指定第一打断点：",
            "BreakSecond" => ja ? "第 2 切断点を指定:" : en ? "Specify second break point:" : "指定第二打断点：",
            "ArraySelect" => ja ? "配列するオブジェクトを選択:" : en ? "Select object to array:" : "选择要阵列的对象：",
            "ArrayCenter" => ja ? "極配列の中心を指定:" : en ? "Specify polar array center:" : "指定环形阵列中心：",
            "StretchWindowFirst" => ja ? "交差窓の第 1 コーナーを指定:" : en ? "Specify first crossing-window corner:" : "指定交叉窗口第一角点：",
            "StretchWindowSecond" => ja ? "交差窓の反対コーナーを指定:" : en ? "Specify opposite crossing-window corner:" : "指定交叉窗口另一角点：",
            "StretchBase" => ja ? "基点を指定:" : en ? "Specify base point:" : "指定基点：",
            "StretchTarget" => ja ? "移動先を指定:" : en ? "Specify second point:" : "指定第二点：",
            "PeditSelect" => ja ? "ポリラインを選択:" : en ? "Select polyline:" : "选择多段线：",
            "CornerInvalid" => ja ? "選択した線と値から有効なコーナーを作成できません。" : en ? "A valid corner cannot be created from the selected lines and values." : "无法根据所选直线和参数创建有效转角。",
            "JoinInvalid" => ja ? "指定許容差内でオブジェクトを結合できません。" : en ? "Objects cannot be joined within the specified tolerance." : "对象无法在指定容差内合并。",
            "BreakInvalid" => ja ? "指定位置でオブジェクトを切断できません。" : en ? "The object cannot be broken at the specified positions." : "无法在指定位置打断对象。",
            "StretchNothing" => ja ? "交差窓内にストレッチ可能なグリップがありません。" : en ? "No stretchable grips are inside the crossing window." : "交叉窗口内没有可拉伸的夹点。",
            "InvalidEntity" => ja ? "このコマンドではそのオブジェクトを使用できません。別のオブジェクトを選択してください。" : en ? "That entity is not valid for this command. Select another entity." : "该对象不适用于此命令，请选择其他对象。",
            "FilletComplete" => ja ? "フィレットを作成しました。" : en ? "Fillet created." : "圆角已创建。",
            "ChamferComplete" => ja ? "面取りを作成しました。" : en ? "Chamfer created." : "倒角已创建。",
            "JoinComplete" => ja ? "オブジェクトを結合しました。" : en ? "Objects joined." : "对象已合并。",
            "BreakComplete" => ja ? "オブジェクトを切断しました。" : en ? "Object broken." : "对象已打断。",
            "ArrayCompleteFormat" => ja ? "{0} 個の配列コピーを作成しました。" : en ? "Created {0} array copies." : "已创建 {0} 个阵列副本。",
            "StretchCompleteFormat" => ja ? "{0} 個のオブジェクトをストレッチしました。" : en ? "Stretched {0} objects." : "已拉伸 {0} 个对象。",
            "PeditComplete" => ja ? "ポリラインを更新しました。" : en ? "Polyline updated." : "多段线已更新。",
            "ArrayType" => ja ? "配列タイプ" : en ? "Array type" : "阵列类型",
            "Rectangular" => ja ? "矩形状" : en ? "Rectangular" : "矩形阵列",
            "Polar" => ja ? "円形状" : en ? "Polar" : "环形阵列",
            "Rows" => ja ? "行" : en ? "Rows" : "行数",
            "Columns" => ja ? "列" : en ? "Columns" : "列数",
            "RowSpacing" => ja ? "行間隔" : en ? "Row spacing" : "行间距",
            "ColumnSpacing" => ja ? "列間隔" : en ? "Column spacing" : "列间距",
            "Items" => ja ? "項目数" : en ? "Items" : "项目数",
            "FillAngle" => ja ? "全体角度" : en ? "Fill angle" : "填充角度",
            "RotateItems" => ja ? "項目を回転" : en ? "Rotate items" : "旋转项目",
            "Reverse" => ja ? "反転" : en ? "Reverse" : "反转",
            "Join" => ja ? "結合" : en ? "Join" : "合并",
            "Open" => ja ? "開く" : en ? "Open" : "打开",
            "Close" => ja ? "閉じる" : en ? "Close" : "闭合",
            "Confirm" => ja ? "確定" : en ? "OK" : "确定",
            "Cancel" => ja ? "キャンセル" : en ? "Cancel" : "取消",
            "FailedFormat" => ja ? "{0} に失敗しました: {1}" : en ? "{0} failed: {1}" : "{0} 失败：{1}",
            _ => key
        };
    }

    private enum ArrayKind { Rectangular, Polar }
    private enum PeditAction { Reverse, Join, Close, Open }
    private readonly record struct ArrayParameters(
        ArrayKind Kind,
        int Rows,
        int Columns,
        double RowSpacing,
        double ColumnSpacing,
        int Items,
        double FillAngleDegrees,
        bool RotateItems);
}
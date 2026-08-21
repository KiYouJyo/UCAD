using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using UCAD.Core.Blocks;
using UCAD.Core.Commands;
using UCAD.Core.Entities;
using UCAD.Core.Geometry;
using UCAD.Core.IO;
using UCAD.Workspace;
using Windows.Storage.Pickers;

namespace UCAD;

public sealed partial class MainWindow
{
    private readonly HashSet<CadWorkspaceSession> _advancedAuthoringSubscribedSessions = [];
    private readonly HashSet<CadWorkspaceSession> _advancedAuthoringRunningSessions = [];
    private bool _advancedAuthoringUiInitialized;

    internal void EnsureAdvancedAuthoringUiInitialized()
    {
        if (_advancedAuthoringUiInitialized) return;
        _advancedAuthoringUiInitialized = true;

        RegisterAdvancedAuthoringCommand("HATCHADV", "HA");
        RegisterAdvancedAuthoringCommand("HATCHEDIT", "HE");
        RegisterAdvancedAuthoringCommand("BLOCKMANAGER", "BM");
        RegisterAdvancedAuthoringCommand("ATTDEF", "ATT");
        RegisterAdvancedAuthoringCommand("ATTEDIT", "ATE");
        RegisterAdvancedAuthoringCommand("BLOCKREDEFINE", "BRD");
        RegisterAdvancedAuthoringCommand("XREF", "XR");

        AddExtendedShelfButton("DRAW", "HATCHADV", "HA");
        AddExtendedShelfButton("DRAW", "HATCHEDIT", "HE");
        AddExtendedShelfButton("BLOCKS", "BLOCKMANAGER", "BM");
        AddExtendedShelfButton("BLOCKS", "ATTDEF", "ATT");
        AddExtendedShelfButton("BLOCKS", "ATTEDIT", "ATE");
        AddExtendedShelfButton("BLOCKS", "BLOCKREDEFINE", "BRD");
        AddExtendedShelfButton("BLOCKS", "XREF", "XR");
        RefreshCommandSearchSource();

        RootLayout.Loaded += AdvancedAuthoring_RootLoaded;
        DocumentTabs.SelectionChanged += AdvancedAuthoring_DocumentTabsSelectionChanged;
    }

    private void RegisterAdvancedAuthoringCommand(string name, params string[] aliases)
    {
        if (_commandRegistry.TryResolve(name, out _)) return;
        _commandRegistry.Register(new CadCommandDefinition(name, CadCommandCategory.Edit, aliases));
    }

    private void AdvancedAuthoring_RootLoaded(object sender, RoutedEventArgs e)
    {
        RootLayout.Loaded -= AdvancedAuthoring_RootLoaded;
        if (ActiveSession is CadWorkspaceSession session) EnsureAdvancedAuthoringSessionSubscribed(session);
    }

    private void AdvancedAuthoring_DocumentTabsSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ActiveSession is CadWorkspaceSession session) EnsureAdvancedAuthoringSessionSubscribed(session);
    }

    private void EnsureAdvancedAuthoringSessionSubscribed(CadWorkspaceSession session)
    {
        session.Viewport.EnsureModifyInputHooks();
        session.Viewport.EnsureDraftingAidHooks();
        if (!_advancedAuthoringSubscribedSessions.Add(session)) return;
        session.CommandSession.Changed += (_, _) => AdvancedAuthoring_CommandSessionChanged(session);
    }

    private void AdvancedAuthoring_CommandSessionChanged(CadWorkspaceSession session)
    {
        var command = session.CommandSession.ActiveCommand;
        if (command is null || !IsAdvancedAuthoringCommand(command.Name) || !_advancedAuthoringRunningSessions.Add(session)) return;

        RootLayout.DispatcherQueue.TryEnqueue(async () =>
        {
            try
            {
                await RunAdvancedAuthoringCommandAsync(session, command.Name);
            }
            catch (TaskCanceledException)
            {
                // Command replacement / Esc already owns visible cancellation state.
            }
            catch (Exception ex)
            {
                App.WriteStartupFailure($"AdvancedAuthoring:{command.Name}", ex);
                SetSessionStatus(session, string.Format(AdvancedAuthoringText("FailedFormat"), command.Name, ex.Message));
                if (session.CommandSession.IsActive) session.CommandSession.Cancel();
            }
            finally
            {
                session.Viewport.CancelModifyInput();
                session.CommandBasePoint = null;
                _advancedAuthoringRunningSessions.Remove(session);
                UpdateSessionUi(session);
            }
        });
    }

    private static bool IsAdvancedAuthoringCommand(string name) => name is
        "HATCHADV" or "HATCHEDIT" or "BLOCKMANAGER" or "ATTDEF" or "ATTEDIT" or "BLOCKREDEFINE" or "XREF";

    private async Task RunAdvancedAuthoringCommandAsync(CadWorkspaceSession session, string command)
    {
        switch (command)
        {
            case "HATCHADV": await RunAdvancedHatchAsync(session); break;
            case "HATCHEDIT": await RunHatchEditAsync(session); break;
            case "BLOCKMANAGER": await RunBlockManagerAsync(session); break;
            case "ATTDEF": await RunAttributeDefinitionAsync(session); break;
            case "ATTEDIT": await RunAttributeEditAsync(session); break;
            case "BLOCKREDEFINE": await RunBlockRedefineAsync(session); break;
            case "XREF": await RunXrefAsync(session); break;
        }
    }

    private async Task RunAdvancedHatchAsync(CadWorkspaceSession session)
    {
        var outerPick = await RequestAdvancedEntityAsync(
            session,
            AdvancedAuthoringText("HatchBoundary"),
            entity => entity is PolylineEntity { Closed: true });
        var outer = (PolylineEntity)outerPick.Entity;
        var islands = session.Interaction.Selection.SelectedEntities
            .OfType<PolylineEntity>()
            .Where(polyline => polyline.Closed && polyline.Id != outer.Id)
            .Where(polyline => polyline.Points.All(point => PointInPolygon(point, outer.Points)))
            .ToArray();
        var parameters = await PromptAdvancedHatchAsync("Solid", 1, 0, HatchIslandDetection.Normal, associative: false);
        if (parameters is null) { session.CommandSession.Cancel(); return; }

        var hatch = CadHatchFactory.CreateFromClosedPolyline(
            outer,
            parameters.Value.Pattern,
            parameters.Value.Scale,
            parameters.Value.AngleDegrees * Math.PI / 180.0,
            islands,
            parameters.Value.Associative,
            parameters.Value.IslandDetection);
        session.Document.Add(hatch, session.Document.GetEntityProperties(outer.Id));
        session.Interaction.Selection.Replace(hatch.Id);
        CompleteAdvancedAuthoring(session, string.Format(AdvancedAuthoringText("HatchCreatedFormat"), islands.Length));
    }

    private async Task RunHatchEditAsync(CadWorkspaceSession session)
    {
        var pick = await RequestAdvancedEntityAsync(session, AdvancedAuthoringText("HatchSelect"), entity => entity is HatchEntity);
        var hatch = (HatchEntity)pick.Entity;
        var parameters = await PromptAdvancedHatchAsync(
            hatch.Pattern,
            hatch.PatternScale,
            hatch.PatternAngleRadians * 180.0 / Math.PI,
            hatch.IslandDetection,
            hatch.Associative);
        if (parameters is null) { session.CommandSession.Cancel(); return; }

        var updated = CadHatchFactory.Update(
            hatch,
            pattern: parameters.Value.Pattern,
            patternScale: parameters.Value.Scale,
            patternAngleRadians: parameters.Value.AngleDegrees * Math.PI / 180.0,
            associative: parameters.Value.Associative,
            sourceEntityIds: parameters.Value.Associative ? hatch.SourceEntityIds : [],
            islandDetection: parameters.Value.IslandDetection);
        session.Document.ReplaceRange([updated]);
        session.Interaction.Selection.Replace(updated.Id);
        CompleteAdvancedAuthoring(session, AdvancedAuthoringText("HatchUpdated"));
    }

    private async Task RunBlockManagerAsync(CadWorkspaceSession session)
    {
        if (session.Document.Blocks.Count == 0)
        {
            session.CommandSession.Cancel();
            SetSessionStatus(session, AdvancedAuthoringText("NoBlocks"));
            return;
        }

        var selector = new ComboBox
        {
            Header = AdvancedAuthoringText("Block"),
            ItemsSource = session.Document.Blocks.Select(block => block.Name).ToArray(),
            SelectedIndex = 0,
            MinWidth = 320
        };
        var action = new ComboBox
        {
            Header = AdvancedAuthoringText("Action"),
            ItemsSource = new[] { AdvancedAuthoringText("Rename"), AdvancedAuthoringText("Delete") },
            SelectedIndex = 0
        };
        var newName = new TextBox { Header = AdvancedAuthoringText("NewName") };
        var panel = new StackPanel { MinWidth = 360, Spacing = 8 };
        panel.Children.Add(selector); panel.Children.Add(action); panel.Children.Add(newName);
        var dialog = new ContentDialog
        {
            XamlRoot = RootLayout.XamlRoot,
            Title = AdvancedAuthoringText("BlockManager"),
            Content = panel,
            PrimaryButtonText = AdvancedAuthoringText("Apply"),
            CloseButtonText = AdvancedAuthoringText("Cancel")
        };
        if (await dialog.ShowAsync() != ContentDialogResult.Primary)
        {
            session.CommandSession.Cancel();
            return;
        }

        var selected = selector.SelectedItem?.ToString();
        if (string.IsNullOrWhiteSpace(selected)) { session.CommandSession.Cancel(); return; }
        if (action.SelectedIndex == 1)
        {
            if (!session.Document.DeleteBlock(selected)) throw new InvalidOperationException(AdvancedAuthoringText("BlockDeleteFailed"));
            CompleteAdvancedAuthoring(session, AdvancedAuthoringText("BlockDeleted"));
            return;
        }

        if (string.IsNullOrWhiteSpace(newName.Text)) throw new InvalidOperationException(AdvancedAuthoringText("NameRequired"));
        session.Document.RenameBlock(selected, newName.Text.Trim());
        CompleteAdvancedAuthoring(session, AdvancedAuthoringText("BlockRenamed"));
    }

    private async Task RunAttributeDefinitionAsync(CadWorkspaceSession session)
    {
        if (session.Document.Blocks.Count == 0)
        {
            session.CommandSession.Cancel();
            SetSessionStatus(session, AdvancedAuthoringText("NoBlocks"));
            return;
        }

        var blockName = await PromptBlockNameAsync(session, AdvancedAuthoringText("AttributeDefinition"));
        if (blockName is null) { session.CommandSession.Cancel(); return; }
        var values = await PromptAttributeDefinitionAsync();
        if (values is null) { session.CommandSession.Cancel(); return; }
        var position = await RequestAdvancedPointAsync(session, AdvancedAuthoringText("AttributePosition"));
        var block = session.Document.GetBlock(blockName);
        var attributes = block.AttributeDefinitions
            .Where(attribute => !string.Equals(attribute.Tag, values.Value.Tag, StringComparison.OrdinalIgnoreCase))
            .Append(new CadBlockAttributeDefinition(
                values.Value.Tag,
                values.Value.Prompt,
                values.Value.DefaultValue,
                position,
                values.Value.Height,
                values.Value.Constant))
            .ToArray();
        session.Document.RedefineBlock(new CadBlockDefinition(block.Name, block.BasePoint, block.Entities, attributes, block.ExternalSourcePath));
        CompleteAdvancedAuthoring(session, AdvancedAuthoringText("AttributeDefined"));
    }

    private async Task RunAttributeEditAsync(CadWorkspaceSession session)
    {
        var pick = await RequestAdvancedEntityAsync(session, AdvancedAuthoringText("BlockReferenceSelect"), entity => entity is BlockReferenceEntity);
        var reference = (BlockReferenceEntity)pick.Entity;
        var definition = session.Document.GetBlock(reference.DefinitionName);
        if (definition.AttributeDefinitions.Count == 0)
        {
            session.CommandSession.Cancel();
            SetSessionStatus(session, AdvancedAuthoringText("NoAttributes"));
            return;
        }

        var panel = new StackPanel { MinWidth = 380, Spacing = 8 };
        var editors = new Dictionary<string, TextBox>(StringComparer.OrdinalIgnoreCase);
        foreach (var attribute in definition.AttributeDefinitions)
        {
            var box = new TextBox
            {
                Header = attribute.Prompt + "  [" + attribute.Tag + "]",
                Text = reference.AttributeValues.TryGetValue(attribute.Tag, out var current) ? current : attribute.DefaultValue,
                IsReadOnly = attribute.Constant
            };
            panel.Children.Add(box);
            editors[attribute.Tag] = box;
        }
        var dialog = new ContentDialog
        {
            XamlRoot = RootLayout.XamlRoot,
            Title = AdvancedAuthoringText("AttributeEdit"),
            Content = panel,
            PrimaryButtonText = AdvancedAuthoringText("Apply"),
            CloseButtonText = AdvancedAuthoringText("Cancel")
        };
        if (await dialog.ShowAsync() != ContentDialogResult.Primary)
        {
            session.CommandSession.Cancel();
            return;
        }
        session.Document.SetBlockReferenceAttributes(reference.Id, editors.ToDictionary(pair => pair.Key, pair => pair.Value.Text, StringComparer.OrdinalIgnoreCase));
        CompleteAdvancedAuthoring(session, AdvancedAuthoringText("AttributeUpdated"));
    }

    private async Task RunBlockRedefineAsync(CadWorkspaceSession session)
    {
        var selected = session.Interaction.Selection.SelectedEntities.ToArray();
        if (selected.Length == 0)
        {
            session.CommandSession.Cancel();
            SetSessionStatus(session, AdvancedAuthoringText("SelectGeometryFirst"));
            return;
        }
        var blockName = await PromptBlockNameAsync(session, AdvancedAuthoringText("BlockRedefine"));
        if (blockName is null) { session.CommandSession.Cancel(); return; }
        var basePoint = await RequestAdvancedPointAsync(session, AdvancedAuthoringText("BlockBasePoint"));
        var existing = session.Document.GetBlock(blockName);
        var definition = new CadBlockDefinition(
            existing.Name,
            basePoint,
            selected,
            existing.AttributeDefinitions,
            existing.ExternalSourcePath);
        session.Document.RedefineBlock(definition);
        CompleteAdvancedAuthoring(session, AdvancedAuthoringText("BlockRedefined"));
    }

    private async Task RunXrefAsync(CadWorkspaceSession session)
    {
        var picker = new FileOpenPicker();
        WinRT.Interop.InitializeWithWindow.Initialize(picker, WinRT.Interop.WindowNative.GetWindowHandle(this));
        picker.FileTypeFilter.Add(CadNativeDocumentCodec.FileExtension);
        picker.FileTypeFilter.Add(".dxf");
        var file = await picker.PickSingleFileAsync();
        if (file is null) { session.CommandSession.Cancel(); return; }

        CadDocument sourceDocument;
        if (string.Equals(Path.GetExtension(file.Path), ".dxf", StringComparison.OrdinalIgnoreCase))
        {
            var dxf = await File.ReadAllTextAsync(file.Path);
            sourceDocument = CadDxfCodec.Import(dxf).Document;
        }
        else
        {
            var native = await File.ReadAllTextAsync(file.Path);
            sourceDocument = CadNativeDocumentCodecV11.Deserialize(native);
        }
        if (sourceDocument.Entities.Count == 0) throw new InvalidOperationException(AdvancedAuthoringText("XrefEmpty"));

        var insertion = await RequestAdvancedPointAsync(session, AdvancedAuthoringText("XrefInsertion"));
        var baseName = "XREF_" + Path.GetFileNameWithoutExtension(file.Path);
        var name = UniqueBlockName(session.Document, baseName);
        var definition = new CadBlockDefinition(
            name,
            new CadPoint(0, 0),
            sourceDocument.Entities,
            attributeDefinitions: null,
            externalSourcePath: file.Path);
        session.Document.DefineBlock(definition);
        var reference = CadBlockFactory.CreateReference(definition, insertion);
        session.Document.Add(reference);
        session.Interaction.Selection.Replace(reference.Id);
        CompleteAdvancedAuthoring(session, AdvancedAuthoringText("XrefAttached"));
    }

    private async Task<HatchParameters?> PromptAdvancedHatchAsync(
        string pattern,
        double scale,
        double angleDegrees,
        HatchIslandDetection islandDetection,
        bool associative)
    {
        var patternBox = new ComboBox
        {
            Header = AdvancedAuthoringText("Pattern"),
            ItemsSource = new[] { "Solid", "ANSI31" },
            SelectedItem = string.Equals(pattern, "ANSI31", StringComparison.OrdinalIgnoreCase) ? "ANSI31" : "Solid"
        };
        var scaleBox = new NumberBox { Header = AdvancedAuthoringText("Scale"), Value = scale, Minimum = 0.0001, Maximum = 1_000_000 };
        var angleBox = new NumberBox { Header = AdvancedAuthoringText("Angle"), Value = angleDegrees, Minimum = -36000, Maximum = 36000 };
        var islandBox = new ComboBox
        {
            Header = AdvancedAuthoringText("IslandDetection"),
            ItemsSource = new[] { "Normal", "Outer", "Ignore" },
            SelectedItem = islandDetection.ToString()
        };
        var associativeBox = new ToggleSwitch { Header = AdvancedAuthoringText("Associative"), IsOn = associative };
        var panel = new StackPanel { MinWidth = 350, Spacing = 8 };
        panel.Children.Add(patternBox); panel.Children.Add(scaleBox); panel.Children.Add(angleBox); panel.Children.Add(islandBox); panel.Children.Add(associativeBox);
        var dialog = new ContentDialog
        {
            XamlRoot = RootLayout.XamlRoot,
            Title = AdvancedAuthoringText("HatchSettings"),
            Content = panel,
            PrimaryButtonText = AdvancedAuthoringText("Apply"),
            CloseButtonText = AdvancedAuthoringText("Cancel")
        };
        if (await dialog.ShowAsync() != ContentDialogResult.Primary) return null;
        var island = Enum.TryParse<HatchIslandDetection>(islandBox.SelectedItem?.ToString(), out var parsed) ? parsed : HatchIslandDetection.Normal;
        return new HatchParameters(
            patternBox.SelectedItem?.ToString() ?? "Solid",
            double.IsNaN(scaleBox.Value) ? 1 : scaleBox.Value,
            double.IsNaN(angleBox.Value) ? 0 : angleBox.Value,
            island,
            associativeBox.IsOn);
    }

    private async Task<string?> PromptBlockNameAsync(CadWorkspaceSession session, string title)
    {
        var combo = new ComboBox
        {
            Header = AdvancedAuthoringText("Block"),
            ItemsSource = session.Document.Blocks.Select(block => block.Name).ToArray(),
            SelectedIndex = session.Document.Blocks.Count > 0 ? 0 : -1,
            MinWidth = 320
        };
        var dialog = new ContentDialog
        {
            XamlRoot = RootLayout.XamlRoot,
            Title = title,
            Content = combo,
            PrimaryButtonText = AdvancedAuthoringText("Continue"),
            CloseButtonText = AdvancedAuthoringText("Cancel")
        };
        return await dialog.ShowAsync() == ContentDialogResult.Primary ? combo.SelectedItem?.ToString() : null;
    }

    private async Task<AttributeDefinitionParameters?> PromptAttributeDefinitionAsync()
    {
        var tag = new TextBox { Header = AdvancedAuthoringText("Tag") };
        var prompt = new TextBox { Header = AdvancedAuthoringText("Prompt") };
        var defaultValue = new TextBox { Header = AdvancedAuthoringText("DefaultValue") };
        var height = new NumberBox { Header = AdvancedAuthoringText("TextHeight"), Value = 2.5, Minimum = 0.1, Maximum = 100000 };
        var constant = new ToggleSwitch { Header = AdvancedAuthoringText("Constant") };
        var panel = new StackPanel { MinWidth = 360, Spacing = 8 };
        panel.Children.Add(tag); panel.Children.Add(prompt); panel.Children.Add(defaultValue); panel.Children.Add(height); panel.Children.Add(constant);
        var dialog = new ContentDialog
        {
            XamlRoot = RootLayout.XamlRoot,
            Title = AdvancedAuthoringText("AttributeDefinition"),
            Content = panel,
            PrimaryButtonText = AdvancedAuthoringText("Continue"),
            CloseButtonText = AdvancedAuthoringText("Cancel")
        };
        if (await dialog.ShowAsync() != ContentDialogResult.Primary || string.IsNullOrWhiteSpace(tag.Text)) return null;
        return new AttributeDefinitionParameters(
            tag.Text.Trim(),
            string.IsNullOrWhiteSpace(prompt.Text) ? tag.Text.Trim() : prompt.Text.Trim(),
            defaultValue.Text,
            double.IsNaN(height.Value) ? 2.5 : height.Value,
            constant.IsOn);
    }

    private async Task<(ICadEntity Entity, CadPoint PickPoint)> RequestAdvancedEntityAsync(
        CadWorkspaceSession session,
        string prompt,
        Func<ICadEntity, bool> predicate)
    {
        var tcs = new TaskCompletionSource<(ICadEntity Entity, CadPoint PickPoint)>(TaskCreationOptions.RunContinuationsAsynchronously);
        void Picked(Guid id, CadPoint point)
        {
            var entity = session.Document.SelectableEntities.FirstOrDefault(candidate => candidate.Id == id);
            if (entity is null || !predicate(entity))
            {
                SetSessionStatus(session, AdvancedAuthoringText("InvalidEntity"));
                session.Viewport.BeginModifyEntityPickInput();
                return;
            }
            tcs.TrySetResult((entity, point));
        }
        void Changed(object? sender, EventArgs e)
        {
            var active = session.CommandSession.ActiveCommand;
            if (active is null || !IsAdvancedAuthoringCommand(active.Name)) tcs.TrySetCanceled();
        }
        session.Viewport.ModifyEntityPicked += Picked;
        session.CommandSession.Changed += Changed;
        try
        {
            SetSessionStatus(session, prompt);
            session.Viewport.BeginModifyEntityPickInput();
            return await tcs.Task;
        }
        finally
        {
            session.Viewport.ModifyEntityPicked -= Picked;
            session.CommandSession.Changed -= Changed;
        }
    }

    private async Task<CadPoint> RequestAdvancedPointAsync(CadWorkspaceSession session, string prompt, CadPoint? basePoint = null)
    {
        var tcs = new TaskCompletionSource<CadPoint>(TaskCreationOptions.RunContinuationsAsynchronously);
        void Accepted(CadPoint point) => tcs.TrySetResult(point);
        void Changed(object? sender, EventArgs e)
        {
            var active = session.CommandSession.ActiveCommand;
            if (active is null || !IsAdvancedAuthoringCommand(active.Name)) tcs.TrySetCanceled();
        }
        session.Viewport.ModifyPointAccepted += Accepted;
        session.CommandSession.Changed += Changed;
        try
        {
            SetSessionStatus(session, prompt);
            session.Viewport.BeginModifyPointInput(basePoint, useOrtho: false);
            return await tcs.Task;
        }
        finally
        {
            session.Viewport.ModifyPointAccepted -= Accepted;
            session.CommandSession.Changed -= Changed;
        }
    }

    private void CompleteAdvancedAuthoring(CadWorkspaceSession session, string status)
    {
        session.Viewport.CancelModifyInput();
        session.CommandBasePoint = null;
        if (session.CommandSession.IsActive) session.CommandSession.Complete();
        SetSessionStatus(session, status);
        UpdateSessionUi(session);
    }

    private static string UniqueBlockName(CadDocument document, string proposed)
    {
        var sanitized = new string(proposed.Select(character => char.IsLetterOrDigit(character) || character is '_' or '-' ? character : '_').ToArray());
        if (!document.TryGetBlock(sanitized, out _)) return sanitized;
        var index = 2;
        while (document.TryGetBlock(sanitized + "_" + index, out _)) index++;
        return sanitized + "_" + index;
    }

    private static bool PointInPolygon(CadPoint point, IReadOnlyList<CadPoint> polygon)
    {
        var inside = false;
        for (var i = 0; i < polygon.Count; i++)
        {
            var j = (i + polygon.Count - 1) % polygon.Count;
            var a = polygon[i];
            var b = polygon[j];
            if ((a.Y > point.Y) == (b.Y > point.Y)) continue;
            var x = ((b.X - a.X) * (point.Y - a.Y) / (b.Y - a.Y)) + a.X;
            if (point.X < x) inside = !inside;
        }
        return inside;
    }

    private static string AdvancedAuthoringText(string key)
    {
        var language = Services.LocalizationService.Current.CurrentLanguageTag;
        var ja = language.StartsWith("ja", StringComparison.OrdinalIgnoreCase);
        var en = language.StartsWith("en", StringComparison.OrdinalIgnoreCase);
        return key switch
        {
            "HatchBoundary" => ja ? "閉じた外周ポリラインを選択:" : en ? "Select closed outer boundary polyline:" : "选择闭合外边界多段线：",
            "HatchSelect" => ja ? "ハッチを選択:" : en ? "Select hatch:" : "选择填充：",
            "HatchSettings" => ja ? "ハッチ設定" : en ? "Hatch settings" : "填充设置",
            "Pattern" => ja ? "パターン" : en ? "Pattern" : "图案",
            "Scale" => ja ? "尺度" : en ? "Scale" : "比例",
            "Angle" => ja ? "角度" : en ? "Angle" : "角度",
            "IslandDetection" => ja ? "島検出" : en ? "Island detection" : "孤岛检测",
            "Associative" => ja ? "関連付け" : en ? "Associative" : "关联填充",
            "HatchCreatedFormat" => ja ? "ハッチを作成しました（島 {0}）。" : en ? "Hatch created with {0} island(s)." : "填充已创建，检测到 {0} 个孤岛。",
            "HatchUpdated" => ja ? "ハッチ設定を更新しました。" : en ? "Hatch updated." : "填充已更新。",
            "BlockManager" => ja ? "ブロック管理" : en ? "Block Manager" : "块管理器",
            "Block" => ja ? "ブロック" : en ? "Block" : "块",
            "Action" => ja ? "操作" : en ? "Action" : "操作",
            "Rename" => ja ? "名前変更" : en ? "Rename" : "重命名",
            "Delete" => ja ? "削除" : en ? "Delete" : "删除",
            "NewName" => ja ? "新しい名前" : en ? "New name" : "新名称",
            "BlockRenamed" => ja ? "ブロック名を変更しました。" : en ? "Block renamed." : "块已重命名。",
            "BlockDeleted" => ja ? "ブロックを削除しました。" : en ? "Block deleted." : "块已删除。",
            "BlockDeleteFailed" => ja ? "使用中のブロックは削除できません。" : en ? "The block cannot be deleted while references exist." : "存在块参照时无法删除该块。",
            "NameRequired" => ja ? "名前を入力してください。" : en ? "A name is required." : "请输入名称。",
            "NoBlocks" => ja ? "図面にブロック定義がありません。" : en ? "The drawing has no block definitions." : "图纸中没有块定义。",
            "AttributeDefinition" => ja ? "属性定義" : en ? "Attribute Definition" : "属性定义",
            "AttributePosition" => ja ? "属性位置を指定:" : en ? "Specify attribute position:" : "指定属性位置：",
            "AttributeDefined" => ja ? "ブロック属性を定義しました。" : en ? "Block attribute defined." : "块属性已定义。",
            "BlockReferenceSelect" => ja ? "ブロック参照を選択:" : en ? "Select block reference:" : "选择块参照：",
            "AttributeEdit" => ja ? "属性編集" : en ? "Edit Attributes" : "编辑属性",
            "AttributeUpdated" => ja ? "属性値を更新しました。" : en ? "Attribute values updated." : "属性值已更新。",
            "NoAttributes" => ja ? "このブロックには属性がありません。" : en ? "This block has no attributes." : "该块没有属性。",
            "Tag" => ja ? "タグ" : en ? "Tag" : "标签",
            "Prompt" => ja ? "プロンプト" : en ? "Prompt" : "提示",
            "DefaultValue" => ja ? "既定値" : en ? "Default value" : "默认值",
            "TextHeight" => ja ? "文字高さ" : en ? "Text height" : "文字高度",
            "Constant" => ja ? "定数属性" : en ? "Constant" : "常量属性",
            "BlockRedefine" => ja ? "ブロック再定義" : en ? "Redefine Block" : "重定义块",
            "BlockBasePoint" => ja ? "ブロック基点を指定:" : en ? "Specify block base point:" : "指定块基点：",
            "BlockRedefined" => ja ? "ブロックを再定義しました。" : en ? "Block redefined." : "块已重定义。",
            "SelectGeometryFirst" => ja ? "先に再定義用ジオメトリを選択してください。" : en ? "Select replacement geometry first." : "请先选择用于重定义的几何对象。",
            "XrefInsertion" => ja ? "外部参照の挿入点を指定:" : en ? "Specify XREF insertion point:" : "指定外部参照插入点：",
            "XrefAttached" => ja ? "外部参照をアタッチしました。" : en ? "External reference attached." : "外部参照已附着。",
            "XrefEmpty" => ja ? "外部参照ファイルに図形がありません。" : en ? "The external reference contains no entities." : "外部参照文件中没有图元。",
            "InvalidEntity" => ja ? "このコマンドではそのオブジェクトを使用できません。" : en ? "That entity is not valid for this command." : "该对象不适用于此命令。",
            "Apply" => ja ? "適用" : en ? "Apply" : "应用",
            "Continue" => ja ? "続行" : en ? "Continue" : "继续",
            "Cancel" => ja ? "キャンセル" : en ? "Cancel" : "取消",
            "FailedFormat" => ja ? "{0} に失敗しました: {1}" : en ? "{0} failed: {1}" : "{0} 失败：{1}",
            _ => key
        };
    }

    private readonly record struct HatchParameters(
        string Pattern,
        double Scale,
        double AngleDegrees,
        HatchIslandDetection IslandDetection,
        bool Associative);

    private readonly record struct AttributeDefinitionParameters(
        string Tag,
        string Prompt,
        string DefaultValue,
        double Height,
        bool Constant);
}
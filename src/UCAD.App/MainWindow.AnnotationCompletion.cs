using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using UCAD.Core.Commands;
using UCAD.Core.Entities;
using UCAD.Core.Geometry;
using UCAD.Core.Styles;
using UCAD.Workspace;

namespace UCAD;

public sealed partial class MainWindow
{
    private readonly HashSet<CadWorkspaceSession> _annotationCompletionSubscribedSessions = [];
    private readonly HashSet<CadWorkspaceSession> _annotationCompletionRunningSessions = [];
    private bool _annotationCompletionUiInitialized;

    internal void EnsureAnnotationCompletionUiInitialized()
    {
        if (_annotationCompletionUiInitialized) return;
        _annotationCompletionUiInitialized = true;

        RegisterAnnotationCompletionCommands();
        AddAnnotationCompletionButtons();
        RefreshCommandSearchSource();
        RootLayout.Loaded += AnnotationCompletion_RootLoaded;
        DocumentTabs.SelectionChanged += AnnotationCompletion_DocumentTabsSelectionChanged;
    }

    private void RegisterAnnotationCompletionCommands()
    {
        RegisterAnnotationCommand("MTEXT", "MT");
        RegisterAnnotationCommand("DIMALIGNED", "DAL");
        RegisterAnnotationCommand("DIMANGULAR", "DAN");
        RegisterAnnotationCommand("DIMRADIUS", "DRA");
        RegisterAnnotationCommand("DIMDIAMETER", "DDI");
        RegisterAnnotationCommand("LEADER", "LE");
        RegisterAnnotationCommand("TEXTSTYLE", "ST");
        RegisterAnnotationCommand("DIMSTYLE", "D");
    }

    private void RegisterAnnotationCommand(string name, params string[] aliases)
    {
        if (_commandRegistry.TryResolve(name, out _)) return;
        // The v0.7 authoring controller owns every Annotate-category command wholesale.
        // Keep these completion commands in Edit until that controller is consolidated;
        // they still live and render in the ANNOTATE shelf.
        _commandRegistry.Register(new CadCommandDefinition(name, CadCommandCategory.Edit, aliases));
    }

    private void AddAnnotationCompletionButtons()
    {
        foreach (var item in new[]
        {
            ("MTEXT", "MT"),
            ("DIMALIGNED", "DAL"),
            ("DIMANGULAR", "DAN"),
            ("DIMRADIUS", "DRA"),
            ("DIMDIAMETER", "DDI"),
            ("LEADER", "LE"),
            ("TEXTSTYLE", "ST"),
            ("DIMSTYLE", "D")
        })
        {
            if (_extendedShelfButtons.Any(button => button.Tag?.ToString()?.EndsWith("|" + item.Item1, StringComparison.Ordinal) == true)) continue;
            AddExtendedShelfButton("ANNOTATE", item.Item1, item.Item2);
        }
    }

    private void AnnotationCompletion_RootLoaded(object sender, RoutedEventArgs e)
    {
        RootLayout.Loaded -= AnnotationCompletion_RootLoaded;
        if (ActiveSession is CadWorkspaceSession session) EnsureAnnotationCompletionSessionSubscribed(session);
    }

    private void AnnotationCompletion_DocumentTabsSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ActiveSession is CadWorkspaceSession session) EnsureAnnotationCompletionSessionSubscribed(session);
    }

    private void EnsureAnnotationCompletionSessionSubscribed(CadWorkspaceSession session)
    {
        session.Viewport.EnsureModifyInputHooks();
        session.Viewport.EnsureDraftingAidHooks();
        session.Viewport.EnsureAnnotationCompletionRenderHooks();
        if (!_annotationCompletionSubscribedSessions.Add(session)) return;
        session.CommandSession.Changed += (_, _) => AnnotationCompletion_CommandSessionChanged(session);
    }

    private void AnnotationCompletion_CommandSessionChanged(CadWorkspaceSession session)
    {
        var command = session.CommandSession.ActiveCommand;
        if (command is null || !IsAnnotationCompletionCommand(command.Name) || !_annotationCompletionRunningSessions.Add(session)) return;

        RootLayout.DispatcherQueue.TryEnqueue(async () =>
        {
            try
            {
                await RunAnnotationCompletionCommandAsync(session, command.Name);
            }
            catch (TaskCanceledException)
            {
                // Command cancellation owns its own visible state.
            }
            catch (Exception ex)
            {
                App.WriteStartupFailure($"AnnotationCompletion:{command.Name}", ex);
                SetSessionStatus(session, string.Format(AnnotationCompletionText("FailedFormat"), command.Name, ex.Message));
                if (session.CommandSession.IsActive) session.CommandSession.Cancel();
            }
            finally
            {
                session.Viewport.CancelModifyInput();
                session.CommandBasePoint = null;
                _annotationCompletionRunningSessions.Remove(session);
                UpdateSessionUi(session);
            }
        });
    }

    private static bool IsAnnotationCompletionCommand(string name) => name is
        "MTEXT" or "DIMALIGNED" or "DIMANGULAR" or "DIMRADIUS" or "DIMDIAMETER" or "LEADER" or "TEXTSTYLE" or "DIMSTYLE";

    private async Task RunAnnotationCompletionCommandAsync(CadWorkspaceSession session, string command)
    {
        switch (command)
        {
            case "MTEXT": await RunMTextAsync(session); break;
            case "DIMALIGNED": await RunAlignedDimensionAsync(session); break;
            case "DIMANGULAR": await RunAngularDimensionAsync(session); break;
            case "DIMRADIUS": await RunRadialDimensionAsync(session, diameter: false); break;
            case "DIMDIAMETER": await RunRadialDimensionAsync(session, diameter: true); break;
            case "LEADER": await RunLeaderAsync(session); break;
            case "TEXTSTYLE": await ShowTextStyleManagerAsync(session); break;
            case "DIMSTYLE": await ShowDimensionStyleManagerAsync(session); break;
        }
    }

    private async Task RunMTextAsync(CadWorkspaceSession session)
    {
        var insertion = await RequestAnnotationPointAsync(session, AnnotationCompletionText("MTextPoint"));
        var values = await PromptMTextAsync(session);
        if (values is null) { session.CommandSession.Cancel(); return; }

        var entity = new MTextEntity(
            insertion,
            values.Value.Text,
            values.Value.Height,
            values.Value.Width,
            values.Value.RotationDegrees * Math.PI / 180.0,
            values.Value.StyleName);
        session.Document.Add(entity);
        session.Interaction.Selection.Replace(entity.Id);
        CompleteAnnotationCompletion(session, AnnotationCompletionText("MTextComplete"));
    }

    private async Task RunAlignedDimensionAsync(CadWorkspaceSession session)
    {
        var first = await RequestAnnotationPointAsync(session, AnnotationCompletionText("DimFirst"));
        session.CommandBasePoint = first;
        var second = await RequestAnnotationPointAsync(session, AnnotationCompletionText("DimSecond"), first);
        session.CommandBasePoint = second;
        var linePoint = await RequestAnnotationPointAsync(session, AnnotationCompletionText("DimLine"));
        var entity = new LinearDimensionEntity(
            first,
            second,
            linePoint,
            textOverride: null,
            styleName: session.Document.CurrentDimensionStyleName);
        session.Document.Add(entity);
        session.Interaction.Selection.Replace(entity.Id);
        CompleteAnnotationCompletion(session, AnnotationCompletionText("DimComplete"));
    }

    private async Task RunAngularDimensionAsync(CadWorkspaceSession session)
    {
        var vertex = await RequestAnnotationPointAsync(session, AnnotationCompletionText("AngularVertex"));
        session.CommandBasePoint = vertex;
        var firstRay = await RequestAnnotationPointAsync(session, AnnotationCompletionText("AngularFirst"), vertex);
        var secondRay = await RequestAnnotationPointAsync(session, AnnotationCompletionText("AngularSecond"), vertex);
        var arcPoint = await RequestAnnotationPointAsync(
            session,
            AnnotationCompletionText("AngularArc"),
            vertex,
            previewFactory: pointer =>
            {
                if ((pointer - vertex).Length <= 1e-9) return [];
                return [new AngularDimensionEntity(vertex, firstRay, secondRay, pointer, styleName: session.Document.CurrentDimensionStyleName)];
            });
        var entity = new AngularDimensionEntity(
            vertex,
            firstRay,
            secondRay,
            arcPoint,
            styleName: session.Document.CurrentDimensionStyleName);
        session.Document.Add(entity);
        session.Interaction.Selection.Replace(entity.Id);
        CompleteAnnotationCompletion(session, AnnotationCompletionText("AngularComplete"));
    }

    private async Task RunRadialDimensionAsync(CadWorkspaceSession session, bool diameter)
    {
        var picked = await RequestAnnotationEntityAsync(
            session,
            diameter ? AnnotationCompletionText("DiameterSelect") : AnnotationCompletionText("RadiusSelect"),
            entity => entity is CircleEntity or ArcEntity);
        var (center, radius) = picked.Entity switch
        {
            CircleEntity circle => (circle.Center, circle.Radius),
            ArcEntity arc => (arc.Center, arc.Radius),
            _ => throw new InvalidOperationException(AnnotationCompletionText("CircleRequired"))
        };
        var radial = picked.PickPoint - center;
        if (radial.Length <= 1e-9) radial = new CadVector(radius, 0);
        var pointOnCircle = new CadPoint(
            center.X + (radial.X / radial.Length * radius),
            center.Y + (radial.Y / radial.Length * radius));
        var textPoint = await RequestAnnotationPointAsync(
            session,
            diameter ? AnnotationCompletionText("DiameterText") : AnnotationCompletionText("RadiusText"),
            pointOnCircle,
            previewFactory: pointer =>
            [new RadialDimensionEntity(center, pointOnCircle, pointer, diameter, styleName: session.Document.CurrentDimensionStyleName)]);
        var entity = new RadialDimensionEntity(
            center,
            pointOnCircle,
            textPoint,
            diameter,
            styleName: session.Document.CurrentDimensionStyleName);
        session.Document.Add(entity);
        session.Interaction.Selection.Replace(entity.Id);
        CompleteAnnotationCompletion(session, diameter ? AnnotationCompletionText("DiameterComplete") : AnnotationCompletionText("RadiusComplete"));
    }

    private async Task RunLeaderAsync(CadWorkspaceSession session)
    {
        var arrow = await RequestAnnotationPointAsync(session, AnnotationCompletionText("LeaderArrow"));
        session.CommandBasePoint = arrow;
        var landing = await RequestAnnotationPointAsync(session, AnnotationCompletionText("LeaderLanding"), arrow);
        var text = await PromptAnnotationTextAsync(AnnotationCompletionText("LeaderTitle"), AnnotationCompletionText("LeaderContent"));
        if (string.IsNullOrWhiteSpace(text)) { session.CommandSession.Cancel(); return; }
        var style = session.Document.GetTextStyle(session.Document.CurrentTextStyleName);
        var entity = new LeaderEntity([arrow, landing], text, textHeight: 2.5, styleName: style.Name);
        session.Document.Add(entity);
        session.Interaction.Selection.Replace(entity.Id);
        CompleteAnnotationCompletion(session, AnnotationCompletionText("LeaderComplete"));
    }

    private async Task ShowTextStyleManagerAsync(CadWorkspaceSession session)
    {
        var names = session.Document.TextStyles.Select(style => style.Name).ToList();
        var styleSelector = new ComboBox { Header = AnnotationCompletionText("CurrentStyle"), ItemsSource = names, SelectedItem = session.Document.CurrentTextStyleName, MinWidth = 300 };
        var name = new TextBox { Header = AnnotationCompletionText("StyleName"), Text = session.Document.CurrentTextStyleName };
        var font = new TextBox { Header = AnnotationCompletionText("FontFamily"), Text = session.Document.GetTextStyle(session.Document.CurrentTextStyleName).FontFamily };
        var width = new NumberBox { Header = AnnotationCompletionText("WidthFactor"), Value = 1, Minimum = 0.1, Maximum = 10 };
        var oblique = new NumberBox { Header = AnnotationCompletionText("Oblique"), Value = 0, Minimum = -84, Maximum = 84 };
        var panel = new StackPanel { MinWidth = 360, Spacing = 8 };
        panel.Children.Add(styleSelector); panel.Children.Add(name); panel.Children.Add(font); panel.Children.Add(width); panel.Children.Add(oblique);

        void LoadStyle(string styleName)
        {
            var style = session.Document.GetTextStyle(styleName);
            name.Text = style.Name;
            font.Text = style.FontFamily;
            width.Value = style.WidthFactor;
            oblique.Value = style.ObliqueAngleDegrees;
        }
        styleSelector.SelectionChanged += (_, _) => { if (styleSelector.SelectedItem is string selected) LoadStyle(selected); };

        var dialog = new ContentDialog
        {
            XamlRoot = RootLayout.XamlRoot,
            Title = AnnotationCompletionText("TextStyleTitle"),
            Content = panel,
            PrimaryButtonText = AnnotationCompletionText("SaveCurrent"),
            SecondaryButtonText = AnnotationCompletionText("SetCurrent"),
            CloseButtonText = AnnotationCompletionText("Cancel")
        };
        var result = await dialog.ShowAsync();
        if (result == ContentDialogResult.None) { session.CommandSession.Cancel(); return; }
        if (result == ContentDialogResult.Secondary)
        {
            if (styleSelector.SelectedItem is string selected) session.Document.SetCurrentTextStyle(selected);
        }
        else
        {
            var style = new CadTextStyle(
                string.IsNullOrWhiteSpace(name.Text) ? CadTextStyle.DefaultName : name.Text.Trim(),
                string.IsNullOrWhiteSpace(font.Text) ? "Segoe UI" : font.Text.Trim(),
                double.IsNaN(width.Value) ? 1 : width.Value,
                double.IsNaN(oblique.Value) ? 0 : oblique.Value);
            session.Document.DefineTextStyle(style, replaceExisting: true);
            session.Document.SetCurrentTextStyle(style.Name);
        }
        CompleteAnnotationCompletion(session, AnnotationCompletionText("TextStyleComplete"));
    }

    private async Task ShowDimensionStyleManagerAsync(CadWorkspaceSession session)
    {
        var names = session.Document.DimensionStyles.Select(style => style.Name).ToList();
        var styleSelector = new ComboBox { Header = AnnotationCompletionText("CurrentStyle"), ItemsSource = names, SelectedItem = session.Document.CurrentDimensionStyleName, MinWidth = 300 };
        var current = session.Document.GetDimensionStyle(session.Document.CurrentDimensionStyleName);
        var name = new TextBox { Header = AnnotationCompletionText("StyleName"), Text = current.Name };
        var textHeight = new NumberBox { Header = AnnotationCompletionText("TextHeight"), Value = current.TextHeight, Minimum = 0.1, Maximum = 100000 };
        var arrowSize = new NumberBox { Header = AnnotationCompletionText("ArrowSize"), Value = current.ArrowSize, Minimum = 0.1, Maximum = 100000 };
        var precision = new NumberBox { Header = AnnotationCompletionText("Precision"), Value = current.Precision, Minimum = 0, Maximum = 8, SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Inline };
        var prefix = new TextBox { Header = AnnotationCompletionText("Prefix"), Text = current.Prefix };
        var suffix = new TextBox { Header = AnnotationCompletionText("Suffix"), Text = current.Suffix };
        var panel = new StackPanel { MinWidth = 360, Spacing = 8 };
        panel.Children.Add(styleSelector); panel.Children.Add(name); panel.Children.Add(textHeight); panel.Children.Add(arrowSize); panel.Children.Add(precision); panel.Children.Add(prefix); panel.Children.Add(suffix);

        void LoadStyle(string styleName)
        {
            var style = session.Document.GetDimensionStyle(styleName);
            name.Text = style.Name; textHeight.Value = style.TextHeight; arrowSize.Value = style.ArrowSize;
            precision.Value = style.Precision; prefix.Text = style.Prefix; suffix.Text = style.Suffix;
        }
        styleSelector.SelectionChanged += (_, _) => { if (styleSelector.SelectedItem is string selected) LoadStyle(selected); };

        var dialog = new ContentDialog
        {
            XamlRoot = RootLayout.XamlRoot,
            Title = AnnotationCompletionText("DimStyleTitle"),
            Content = panel,
            PrimaryButtonText = AnnotationCompletionText("SaveCurrent"),
            SecondaryButtonText = AnnotationCompletionText("SetCurrent"),
            CloseButtonText = AnnotationCompletionText("Cancel")
        };
        var result = await dialog.ShowAsync();
        if (result == ContentDialogResult.None) { session.CommandSession.Cancel(); return; }
        if (result == ContentDialogResult.Secondary)
        {
            if (styleSelector.SelectedItem is string selected) session.Document.SetCurrentDimensionStyle(selected);
        }
        else
        {
            var style = new CadDimensionStyle(
                string.IsNullOrWhiteSpace(name.Text) ? CadDimensionStyle.DefaultName : name.Text.Trim(),
                double.IsNaN(textHeight.Value) ? 2.5 : textHeight.Value,
                double.IsNaN(arrowSize.Value) ? 2.5 : arrowSize.Value,
                double.IsNaN(precision.Value) ? 2 : Math.Clamp((int)Math.Round(precision.Value), 0, 8),
                prefix.Text,
                suffix.Text);
            session.Document.DefineDimensionStyle(style, replaceExisting: true);
            session.Document.SetCurrentDimensionStyle(style.Name);
        }
        CompleteAnnotationCompletion(session, AnnotationCompletionText("DimStyleComplete"));
    }

    private async Task<(string Text, double Height, double Width, double RotationDegrees, string StyleName)?> PromptMTextAsync(CadWorkspaceSession session)
    {
        var text = new TextBox { Header = AnnotationCompletionText("MTextContent"), AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, MinHeight = 120 };
        var height = new NumberBox { Header = AnnotationCompletionText("TextHeight"), Value = 2.5, Minimum = 0.1, Maximum = 100000 };
        var width = new NumberBox { Header = AnnotationCompletionText("MTextWidth"), Value = 40, Minimum = 0.1, Maximum = 1000000 };
        var rotation = new NumberBox { Header = AnnotationCompletionText("Rotation"), Value = 0, Minimum = -36000, Maximum = 36000 };
        var style = new ComboBox { Header = AnnotationCompletionText("TextStyle"), ItemsSource = session.Document.TextStyles.Select(item => item.Name).ToArray(), SelectedItem = session.Document.CurrentTextStyleName };
        var panel = new StackPanel { MinWidth = 390, Spacing = 8 };
        panel.Children.Add(text); panel.Children.Add(height); panel.Children.Add(width); panel.Children.Add(rotation); panel.Children.Add(style);
        var dialog = new ContentDialog
        {
            XamlRoot = RootLayout.XamlRoot,
            Title = AnnotationCompletionText("MTextTitle"),
            Content = panel,
            PrimaryButtonText = AnnotationCompletionText("Create"),
            CloseButtonText = AnnotationCompletionText("Cancel")
        };
        if (await dialog.ShowAsync() != ContentDialogResult.Primary || string.IsNullOrWhiteSpace(text.Text)) return null;
        return (
            text.Text,
            double.IsNaN(height.Value) ? 2.5 : height.Value,
            double.IsNaN(width.Value) ? 40 : width.Value,
            double.IsNaN(rotation.Value) ? 0 : rotation.Value,
            style.SelectedItem?.ToString() ?? session.Document.CurrentTextStyleName);
    }

    private async Task<string?> PromptAnnotationTextAsync(string title, string label)
    {
        var box = new TextBox { Header = label, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, MinWidth = 340, MinHeight = 80 };
        var dialog = new ContentDialog
        {
            XamlRoot = RootLayout.XamlRoot,
            Title = title,
            Content = box,
            PrimaryButtonText = AnnotationCompletionText("Create"),
            CloseButtonText = AnnotationCompletionText("Cancel")
        };
        return await dialog.ShowAsync() == ContentDialogResult.Primary ? box.Text.Trim() : null;
    }

    private async Task<CadPoint> RequestAnnotationPointAsync(
        CadWorkspaceSession session,
        string prompt,
        CadPoint? basePoint = null,
        bool useOrtho = false,
        Func<CadPoint, IReadOnlyList<ICadEntity>>? previewFactory = null)
    {
        var tcs = new TaskCompletionSource<CadPoint>(TaskCreationOptions.RunContinuationsAsynchronously);
        void Accepted(CadPoint point) => tcs.TrySetResult(point);
        void Changed(object? sender, EventArgs e)
        {
            var active = session.CommandSession.ActiveCommand;
            if (active is null || !IsAnnotationCompletionCommand(active.Name)) tcs.TrySetCanceled();
        }
        session.Viewport.ModifyPointAccepted += Accepted;
        session.CommandSession.Changed += Changed;
        try
        {
            SetSessionStatus(session, prompt);
            session.Viewport.BeginModifyPointInput(basePoint, useOrtho, previewFactory);
            return await tcs.Task;
        }
        finally
        {
            session.Viewport.ModifyPointAccepted -= Accepted;
            session.CommandSession.Changed -= Changed;
        }
    }

    private async Task<(ICadEntity Entity, CadPoint PickPoint)> RequestAnnotationEntityAsync(
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
                SetSessionStatus(session, AnnotationCompletionText("InvalidEntity"));
                session.Viewport.BeginModifyEntityPickInput();
                return;
            }
            tcs.TrySetResult((entity, point));
        }
        void Changed(object? sender, EventArgs e)
        {
            var active = session.CommandSession.ActiveCommand;
            if (active is null || !IsAnnotationCompletionCommand(active.Name)) tcs.TrySetCanceled();
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

    private void CompleteAnnotationCompletion(CadWorkspaceSession session, string status)
    {
        session.Viewport.CancelModifyInput();
        session.CommandBasePoint = null;
        if (session.CommandSession.IsActive) session.CommandSession.Complete();
        SetSessionStatus(session, status);
        UpdateSessionUi(session);
    }

    private static string AnnotationCompletionText(string key)
    {
        var language = Services.LocalizationService.Current.CurrentLanguageTag;
        var ja = language.StartsWith("ja", StringComparison.OrdinalIgnoreCase);
        var en = language.StartsWith("en", StringComparison.OrdinalIgnoreCase);
        return key switch
        {
            "MTextPoint" => ja ? "マルチテキストの挿入点を指定:" : en ? "Specify multiline text insertion point:" : "指定多行文字插入点：",
            "MTextComplete" => ja ? "マルチテキストを作成しました。" : en ? "Multiline text created." : "多行文字已创建。",
            "MTextTitle" => ja ? "マルチテキスト" : en ? "Multiline Text" : "多行文字",
            "MTextContent" => ja ? "内容" : en ? "Content" : "内容",
            "MTextWidth" => ja ? "幅" : en ? "Width" : "宽度",
            "DimFirst" => ja ? "第 1 寸法点を指定:" : en ? "Specify first dimension point:" : "指定第一标注点：",
            "DimSecond" => ja ? "第 2 寸法点を指定:" : en ? "Specify second dimension point:" : "指定第二标注点：",
            "DimLine" => ja ? "寸法線の位置を指定:" : en ? "Specify dimension line location:" : "指定尺寸线位置：",
            "DimComplete" => ja ? "平行寸法を作成しました。" : en ? "Aligned dimension created." : "对齐标注已创建。",
            "AngularVertex" => ja ? "角度寸法の頂点を指定:" : en ? "Specify angular dimension vertex:" : "指定角度标注顶点：",
            "AngularFirst" => ja ? "第 1 方向点を指定:" : en ? "Specify first ray point:" : "指定第一射线点：",
            "AngularSecond" => ja ? "第 2 方向点を指定:" : en ? "Specify second ray point:" : "指定第二射线点：",
            "AngularArc" => ja ? "寸法弧の位置を指定:" : en ? "Specify dimension arc location:" : "指定尺寸弧位置：",
            "AngularComplete" => ja ? "角度寸法を作成しました。" : en ? "Angular dimension created." : "角度标注已创建。",
            "RadiusSelect" => ja ? "円または円弧を選択:" : en ? "Select circle or arc:" : "选择圆或圆弧：",
            "DiameterSelect" => ja ? "直径寸法の円または円弧を選択:" : en ? "Select circle or arc for diameter:" : "选择用于直径标注的圆或圆弧：",
            "RadiusText" => ja ? "半径寸法文字の位置を指定:" : en ? "Specify radius dimension text location:" : "指定半径标注文字位置：",
            "DiameterText" => ja ? "直径寸法文字の位置を指定:" : en ? "Specify diameter dimension text location:" : "指定直径标注文字位置：",
            "RadiusComplete" => ja ? "半径寸法を作成しました。" : en ? "Radius dimension created." : "半径标注已创建。",
            "DiameterComplete" => ja ? "直径寸法を作成しました。" : en ? "Diameter dimension created." : "直径标注已创建。",
            "CircleRequired" => ja ? "円または円弧が必要です。" : en ? "A circle or arc is required." : "需要圆或圆弧。",
            "LeaderArrow" => ja ? "引出線の矢印点を指定:" : en ? "Specify leader arrow point:" : "指定引线箭头点：",
            "LeaderLanding" => ja ? "引出線の終点を指定:" : en ? "Specify leader landing point:" : "指定引线落点：",
            "LeaderTitle" => ja ? "引出線" : en ? "Leader" : "引线",
            "LeaderContent" => ja ? "注記" : en ? "Annotation" : "注释",
            "LeaderComplete" => ja ? "引出線を作成しました。" : en ? "Leader created." : "引线已创建。",
            "TextStyleTitle" => ja ? "文字スタイル" : en ? "Text Style" : "文字样式",
            "DimStyleTitle" => ja ? "寸法スタイル" : en ? "Dimension Style" : "标注样式",
            "CurrentStyle" => ja ? "現在のスタイル" : en ? "Current style" : "当前样式",
            "StyleName" => ja ? "スタイル名" : en ? "Style name" : "样式名称",
            "FontFamily" => ja ? "フォント" : en ? "Font family" : "字体",
            "WidthFactor" => ja ? "幅係数" : en ? "Width factor" : "宽度因子",
            "Oblique" => ja ? "傾斜角度" : en ? "Oblique angle" : "倾斜角",
            "TextHeight" => ja ? "文字高さ" : en ? "Text height" : "文字高度",
            "ArrowSize" => ja ? "矢印サイズ" : en ? "Arrow size" : "箭头大小",
            "Precision" => ja ? "精度" : en ? "Precision" : "精度",
            "Prefix" => ja ? "接頭辞" : en ? "Prefix" : "前缀",
            "Suffix" => ja ? "接尾辞" : en ? "Suffix" : "后缀",
            "TextStyle" => ja ? "文字スタイル" : en ? "Text style" : "文字样式",
            "Rotation" => ja ? "回転角度" : en ? "Rotation" : "旋转角度",
            "SaveCurrent" => ja ? "保存して現在に設定" : en ? "Save & set current" : "保存并设为当前",
            "SetCurrent" => ja ? "選択を現在に設定" : en ? "Set selected current" : "将所选设为当前",
            "TextStyleComplete" => ja ? "文字スタイルを更新しました。" : en ? "Text style updated." : "文字样式已更新。",
            "DimStyleComplete" => ja ? "寸法スタイルを更新しました。" : en ? "Dimension style updated." : "标注样式已更新。",
            "InvalidEntity" => ja ? "このコマンドではそのオブジェクトを使用できません。" : en ? "That entity is not valid for this command." : "该对象不适用于此命令。",
            "Create" => ja ? "作成" : en ? "Create" : "创建",
            "Cancel" => ja ? "キャンセル" : en ? "Cancel" : "取消",
            "FailedFormat" => ja ? "{0} に失敗しました: {1}" : en ? "{0} failed: {1}" : "{0} 失败：{1}",
            _ => key
        };
    }
}
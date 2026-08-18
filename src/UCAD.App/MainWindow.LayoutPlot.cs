using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using UCAD.Core.Commands;
using UCAD.Core.Geometry;
using UCAD.Core.Layout;
using UCAD.Core.Plot;
using UCAD.Services;
using UCAD.Views;
using UCAD.Workspace;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.System;

namespace UCAD;

public sealed partial class MainWindow
{
    private readonly CadPlotFileService _plotFileService = new();
    private readonly Dictionary<CadWorkspaceSession, LayoutSessionState> _layoutStates = [];
    private readonly HashSet<CadWorkspaceSession> _layoutPlotSubscribedSessions = [];
    private readonly HashSet<CadWorkspaceSession> _layoutPlotRunningSessions = [];
    private bool _layoutPlotUiInitialized;

    internal void EnsureLayoutPlotUiInitialized()
    {
        if (_layoutPlotUiInitialized) return;
        _layoutPlotUiInitialized = true;

        RegisterLayoutPlotCommand("PAGESETUP", "PS");
        RegisterLayoutPlotCommand("LAYOUT", "LO");
        RegisterLayoutPlotCommand("VIEWPORT", "VP");
        RegisterLayoutPlotCommand("PREVIEW", "PV");
        RegisterLayoutPlotCommand("PLOT");
        RegisterLayoutPlotCommand("PDF");
        RegisterLayoutPlotCommand("PRINT");

        var style = ViewToolShelf.Children.OfType<Button>().FirstOrDefault()?.Style;
        foreach (var item in new[]
        {
            ("PAGESETUP", "PS"),
            ("LAYOUT", "LO"),
            ("VIEWPORT", "VP"),
            ("PREVIEW", "PV"),
            ("PLOT", "PLOT"),
            ("PDF", "PDF"),
            ("PRINT", "PRINT")
        })
        {
            if (!ViewToolShelf.Children.OfType<Button>().Any(button => string.Equals(button.Tag?.ToString(), item.Item1, StringComparison.Ordinal)))
                ViewToolShelf.Children.Add(CreateLayoutPlotShelfButton(item.Item1, item.Item2, style));
        }
        RefreshCommandSearchSource();

        RootLayout.Loaded += LayoutPlot_RootLoaded;
        DocumentTabs.SelectionChanged += LayoutPlot_DocumentTabsSelectionChanged;
    }

    private void RegisterLayoutPlotCommand(string name, params string[] aliases)
    {
        if (_commandRegistry.TryResolve(name, out _)) return;
        _commandRegistry.Register(new CadCommandDefinition(name, CadCommandCategory.Edit, aliases));
    }

    private Button CreateLayoutPlotShelfButton(string command, string alias, Style? style)
    {
        var button = new Button
        {
            Tag = command,
            Style = style,
            IsEnabled = true,
            IsHitTestVisible = true,
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

    private void LayoutPlot_RootLoaded(object sender, RoutedEventArgs e)
    {
        RootLayout.Loaded -= LayoutPlot_RootLoaded;
        if (ActiveSession is CadWorkspaceSession session) EnsureLayoutPlotSessionSubscribed(session);
    }

    private void LayoutPlot_DocumentTabsSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ActiveSession is CadWorkspaceSession session) EnsureLayoutPlotSessionSubscribed(session);
    }

    private void EnsureLayoutPlotSessionSubscribed(CadWorkspaceSession session)
    {
        if (!_layoutStates.ContainsKey(session)) _layoutStates[session] = LayoutSessionState.CreateDefault();
        session.Viewport.EnsureModifyInputHooks();
        if (!_layoutPlotSubscribedSessions.Add(session)) return;
        session.CommandSession.Changed += (_, _) => LayoutPlot_CommandSessionChanged(session);
    }

    private void LayoutPlot_CommandSessionChanged(CadWorkspaceSession session)
    {
        var command = session.CommandSession.ActiveCommand;
        if (command is null || !IsLayoutPlotCommand(command.Name) || !_layoutPlotRunningSessions.Add(session)) return;
        RootLayout.DispatcherQueue.TryEnqueue(async () =>
        {
            try
            {
                await RunLayoutPlotCommandAsync(session, command.Name);
            }
            catch (TaskCanceledException)
            {
                // Esc/command replacement owns visible cancellation state.
            }
            catch (Exception ex)
            {
                App.WriteStartupFailure($"LayoutPlot:{command.Name}", ex);
                SetSessionStatus(session, string.Format(LayoutPlotText("FailedFormat"), command.Name, ex.Message));
                if (session.CommandSession.IsActive) session.CommandSession.Cancel();
            }
            finally
            {
                session.Viewport.CancelModifyInput();
                session.CommandBasePoint = null;
                _layoutPlotRunningSessions.Remove(session);
                UpdateSessionUi(session);
            }
        });
    }

    private static bool IsLayoutPlotCommand(string name) => name is
        "PAGESETUP" or "LAYOUT" or "VIEWPORT" or "PREVIEW" or "PLOT" or "PDF" or "PRINT";

    private async Task RunLayoutPlotCommandAsync(CadWorkspaceSession session, string command)
    {
        switch (command)
        {
            case "PAGESETUP": await RunPageSetupAsync(session); break;
            case "LAYOUT": await RunLayoutManagerAsync(session); break;
            case "VIEWPORT": await RunViewportAsync(session); break;
            case "PREVIEW": await RunPlotPreviewAsync(session); break;
            case "PLOT":
            case "PDF": await RunPdfExportAsync(session); break;
            case "PRINT": await RunPrintHandoffAsync(session); break;
        }
    }

    private async Task RunPageSetupAsync(CadWorkspaceSession session)
    {
        var state = GetLayoutState(session);
        var current = state.PageSetup;
        var paper = new ComboBox
        {
            Header = LayoutPlotText("Paper"),
            ItemsSource = CadPaperSize.IsoA.Select(item => item.Name).ToArray(),
            SelectedItem = current.PaperSize.Name
        };
        var landscape = new ToggleSwitch { Header = LayoutPlotText("Landscape"), IsOn = current.Landscape };
        var scale = new NumberBox { Header = LayoutPlotText("Scale"), Value = current.PlotScaleDenominator, Minimum = 0.001, Maximum = 1_000_000 };
        var margin = new NumberBox { Header = LayoutPlotText("Margin"), Value = current.MarginLeftMm, Minimum = 0, Maximum = 1000 };
        var area = new ComboBox
        {
            Header = LayoutPlotText("PlotArea"),
            ItemsSource = Enum.GetNames<CadPlotArea>(),
            SelectedItem = current.PlotArea.ToString()
        };
        var style = new ComboBox
        {
            Header = LayoutPlotText("PlotStyle"),
            ItemsSource = Enum.GetNames<CadPlotStyleMode>(),
            SelectedItem = current.PlotStyle.ToString()
        };
        var panel = new StackPanel { MinWidth = 360, Spacing = 8 };
        panel.Children.Add(paper); panel.Children.Add(landscape); panel.Children.Add(scale); panel.Children.Add(margin); panel.Children.Add(area); panel.Children.Add(style);
        var dialog = new ContentDialog
        {
            XamlRoot = RootLayout.XamlRoot,
            Title = LayoutPlotText("PageSetup"),
            Content = panel,
            PrimaryButtonText = LayoutPlotText("Apply"),
            CloseButtonText = LayoutPlotText("Cancel")
        };
        if (await dialog.ShowAsync() != ContentDialogResult.Primary)
        {
            session.CommandSession.Cancel();
            return;
        }

        var paperSize = CadPaperSize.IsoA.FirstOrDefault(item => item.Name == paper.SelectedItem?.ToString()) ?? CadPaperSize.A3;
        var uniformMargin = double.IsNaN(margin.Value) ? 10 : margin.Value;
        var plotArea = Enum.TryParse<CadPlotArea>(area.SelectedItem?.ToString(), out var parsedArea) ? parsedArea : CadPlotArea.Layout;
        // Window selection is a separate interactive operation; until a window exists,
        // fall back to Extents instead of creating an invalid setup.
        if (plotArea == CadPlotArea.Window) plotArea = CadPlotArea.Extents;
        var plotStyle = Enum.TryParse<CadPlotStyleMode>(style.SelectedItem?.ToString(), out var parsedStyle) ? parsedStyle : CadPlotStyleMode.Monochrome;
        var setup = new CadPageSetup(
            paperSize,
            landscape.IsOn,
            uniformMargin,
            uniformMargin,
            uniformMargin,
            uniformMargin,
            double.IsNaN(scale.Value) ? 100 : scale.Value,
            plotArea,
            plotStyle);
        state.PageSetup = setup;
        state.ReplaceActiveLayoutPageSetup(setup);
        CompleteLayoutPlot(session, LayoutPlotText("PageSetupUpdated"));
    }

    private async Task RunLayoutManagerAsync(CadWorkspaceSession session)
    {
        var state = GetLayoutState(session);
        var selector = new ComboBox
        {
            Header = LayoutPlotText("Layout"),
            ItemsSource = state.Layouts.Select(layout => layout.Name).ToArray(),
            SelectedItem = state.ActiveLayoutName,
            MinWidth = 320
        };
        var action = new ComboBox
        {
            Header = LayoutPlotText("Action"),
            ItemsSource = new[] { LayoutPlotText("Activate"), LayoutPlotText("Create"), LayoutPlotText("Rename"), LayoutPlotText("Delete") },
            SelectedIndex = 0
        };
        var name = new TextBox { Header = LayoutPlotText("LayoutName") };
        var panel = new StackPanel { MinWidth = 360, Spacing = 8 };
        panel.Children.Add(selector); panel.Children.Add(action); panel.Children.Add(name);
        var dialog = new ContentDialog
        {
            XamlRoot = RootLayout.XamlRoot,
            Title = LayoutPlotText("LayoutManager"),
            Content = panel,
            PrimaryButtonText = LayoutPlotText("Apply"),
            CloseButtonText = LayoutPlotText("Cancel")
        };
        if (await dialog.ShowAsync() != ContentDialogResult.Primary)
        {
            session.CommandSession.Cancel();
            return;
        }

        var selected = selector.SelectedItem?.ToString();
        switch (action.SelectedIndex)
        {
            case 0:
                if (!string.IsNullOrWhiteSpace(selected)) state.ActiveLayoutName = selected;
                break;
            case 1:
                if (string.IsNullOrWhiteSpace(name.Text)) throw new InvalidOperationException(LayoutPlotText("NameRequired"));
                state.CreateLayout(name.Text.Trim());
                break;
            case 2:
                if (string.IsNullOrWhiteSpace(selected) || string.IsNullOrWhiteSpace(name.Text)) throw new InvalidOperationException(LayoutPlotText("NameRequired"));
                state.RenameLayout(selected, name.Text.Trim());
                break;
            case 3:
                if (string.IsNullOrWhiteSpace(selected)) throw new InvalidOperationException(LayoutPlotText("LayoutRequired"));
                state.DeleteLayout(selected);
                break;
        }
        CompleteLayoutPlot(session, string.Format(LayoutPlotText("ActiveLayoutFormat"), state.ActiveLayoutName));
    }

    private async Task RunViewportAsync(CadWorkspaceSession session)
    {
        var state = GetLayoutState(session);
        var layout = state.ActiveLayout;
        var center = await RequestLayoutPointAsync(session, LayoutPlotText("ViewportCenter"));
        var scale = new NumberBox { Header = LayoutPlotText("ViewportScale"), Value = state.PageSetup.PlotScaleDenominator, Minimum = 0.001, Maximum = 1_000_000 };
        var name = new TextBox { Header = LayoutPlotText("ViewportName"), Text = "Viewport " + (layout.Viewports.Count + 1) };
        var locked = new ToggleSwitch { Header = LayoutPlotText("LockViewport"), IsOn = true };
        var panel = new StackPanel { MinWidth = 330, Spacing = 8 };
        panel.Children.Add(name); panel.Children.Add(scale); panel.Children.Add(locked);
        var dialog = new ContentDialog
        {
            XamlRoot = RootLayout.XamlRoot,
            Title = LayoutPlotText("Viewport"),
            Content = panel,
            PrimaryButtonText = LayoutPlotText("Create"),
            CloseButtonText = LayoutPlotText("Cancel")
        };
        if (await dialog.ShowAsync() != ContentDialogResult.Primary)
        {
            session.CommandSession.Cancel();
            return;
        }

        var printable = state.PageSetup.PrintablePaperRectMm;
        var insetX = Math.Min(12, printable.Width * 0.08);
        var insetY = Math.Min(12, printable.Height * 0.08);
        var paperRect = new CadRect(
            printable.Left + insetX,
            printable.Bottom + insetY,
            printable.Right - insetX,
            printable.Top - insetY);
        var viewport = new CadLayoutViewport(
            string.IsNullOrWhiteSpace(name.Text) ? "Viewport " + (layout.Viewports.Count + 1) : name.Text.Trim(),
            paperRect,
            center,
            double.IsNaN(scale.Value) ? state.PageSetup.PlotScaleDenominator : scale.Value,
            locked: locked.IsOn);
        state.ReplaceActiveLayout(layout.AddViewport(viewport));
        CompleteLayoutPlot(session, LayoutPlotText("ViewportCreated"));
    }

    private async Task RunPlotPreviewAsync(CadWorkspaceSession session)
    {
        var plan = CreateCurrentPlotPlan(session);
        var preview = new CadPlotPreviewControl();
        preview.SetPlot(session.Document, plan);
        var summary = new TextBlock
        {
            Text = string.Format(
                LayoutPlotText("PreviewSummaryFormat"),
                plan.PageSetup.PaperSize.Name,
                plan.PageSetup.Landscape ? LayoutPlotText("Landscape") : LayoutPlotText("Portrait"),
                plan.ScaleDenominator),
            Margin = new Thickness(0, 0, 0, 8),
            Foreground = (Brush)Application.Current.Resources["UcadTextSecondaryBrush"]
        };
        var panel = new StackPanel { MinWidth = 760, MinHeight = 580 };
        panel.Children.Add(summary);
        panel.Children.Add(preview);
        var dialog = new ContentDialog
        {
            XamlRoot = RootLayout.XamlRoot,
            Title = LayoutPlotText("Preview"),
            Content = panel,
            CloseButtonText = LayoutPlotText("Close")
        };
        await dialog.ShowAsync();
        CompleteLayoutPlot(session, LayoutPlotText("PreviewClosed"));
    }

    private async Task RunPdfExportAsync(CadWorkspaceSession session)
    {
        var plan = CreateCurrentPlotPlan(session);
        var picker = new FileSavePicker
        {
            SuggestedFileName = Path.GetFileNameWithoutExtension(session.DisplayName) + "-plot"
        };
        WinRT.Interop.InitializeWithWindow.Initialize(picker, WinRT.Interop.WindowNative.GetWindowHandle(this));
        picker.FileTypeChoices.Add("PDF", [".pdf"]);
        var file = await picker.PickSaveFileAsync();
        if (file is null) { session.CommandSession.Cancel(); return; }

        var result = await _plotFileService.ExportPdfAsync(file.Path, session.Document, plan, session.DisplayName);
        if (result.HasWarnings) await ShowPlotWarningsAsync(result.Warnings);
        CompleteLayoutPlot(session, string.Format(LayoutPlotText("PdfSavedFormat"), file.Path));
    }

    private async Task RunPrintHandoffAsync(CadWorkspaceSession session)
    {
        var plan = CreateCurrentPlotPlan(session);
        var directory = Path.Combine(Path.GetTempPath(), "UCAD", "Print");
        Directory.CreateDirectory(directory);
        var filePath = Path.Combine(directory, "ucad-print-" + Guid.NewGuid().ToString("N") + ".pdf");
        var result = await _plotFileService.ExportPdfAsync(filePath, session.Document, plan, session.DisplayName);
        var file = await StorageFile.GetFileFromPathAsync(filePath);
        if (!await Launcher.LaunchFileAsync(file)) throw new InvalidOperationException(LayoutPlotText("PrintLaunchFailed"));
        if (result.HasWarnings) await ShowPlotWarningsAsync(result.Warnings);
        CompleteLayoutPlot(session, LayoutPlotText("PrintHandoff"));
    }

    private CadPlotPlan CreateCurrentPlotPlan(CadWorkspaceSession session)
    {
        var state = GetLayoutState(session);
        var viewport = state.ActiveLayout.Viewports.FirstOrDefault();
        return viewport is not null
            ? CadPlotPlan.FromViewport(state.PageSetup, viewport)
            : _plotFileService.CreatePlan(session.Document, state.PageSetup);
    }

    private async Task ShowPlotWarningsAsync(IReadOnlyList<string> warnings)
    {
        var text = string.Join(Environment.NewLine, warnings.Take(12));
        if (warnings.Count > 12) text += Environment.NewLine + "…";
        await new ContentDialog
        {
            XamlRoot = RootLayout.XamlRoot,
            Title = LayoutPlotText("PlotWarnings"),
            Content = new ScrollViewer
            {
                MaxHeight = 360,
                Content = new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap }
            },
            CloseButtonText = LayoutPlotText("Close")
        }.ShowAsync();
    }

    private async Task<CadPoint> RequestLayoutPointAsync(CadWorkspaceSession session, string prompt)
    {
        var tcs = new TaskCompletionSource<CadPoint>(TaskCreationOptions.RunContinuationsAsynchronously);
        void Accepted(CadPoint point) => tcs.TrySetResult(point);
        void Changed(object? sender, EventArgs e)
        {
            var active = session.CommandSession.ActiveCommand;
            if (active is null || !IsLayoutPlotCommand(active.Name)) tcs.TrySetCanceled();
        }
        session.Viewport.ModifyPointAccepted += Accepted;
        session.CommandSession.Changed += Changed;
        try
        {
            SetSessionStatus(session, prompt);
            session.Viewport.BeginModifyPointInput();
            return await tcs.Task;
        }
        finally
        {
            session.Viewport.ModifyPointAccepted -= Accepted;
            session.CommandSession.Changed -= Changed;
        }
    }

    private LayoutSessionState GetLayoutState(CadWorkspaceSession session)
    {
        if (!_layoutStates.TryGetValue(session, out var state))
        {
            state = LayoutSessionState.CreateDefault();
            _layoutStates[session] = state;
        }
        return state;
    }

    private void CompleteLayoutPlot(CadWorkspaceSession session, string status)
    {
        session.Viewport.CancelModifyInput();
        if (session.CommandSession.IsActive) session.CommandSession.Complete();
        SetSessionStatus(session, status);
        UpdateSessionUi(session);
    }

    private static string LayoutPlotText(string key)
    {
        var language = Services.LocalizationService.Current.CurrentLanguageTag;
        var ja = language.StartsWith("ja", StringComparison.OrdinalIgnoreCase);
        var en = language.StartsWith("en", StringComparison.OrdinalIgnoreCase);
        return key switch
        {
            "PageSetup" => ja ? "ページ設定" : en ? "Page Setup" : "页面设置",
            "Paper" => ja ? "用紙" : en ? "Paper" : "纸张",
            "Landscape" => ja ? "横向き" : en ? "Landscape" : "横向",
            "Portrait" => ja ? "縦向き" : en ? "Portrait" : "纵向",
            "Scale" => ja ? "尺度 (1:n)" : en ? "Scale (1:n)" : "比例（1:n）",
            "Margin" => ja ? "余白 (mm)" : en ? "Margin (mm)" : "页边距（mm）",
            "PlotArea" => ja ? "印刷範囲" : en ? "Plot area" : "打印区域",
            "PlotStyle" => ja ? "印刷スタイル" : en ? "Plot style" : "打印样式",
            "PageSetupUpdated" => ja ? "ページ設定を更新しました。" : en ? "Page setup updated." : "页面设置已更新。",
            "LayoutManager" => ja ? "レイアウト管理" : en ? "Layout Manager" : "布局管理器",
            "Layout" => ja ? "レイアウト" : en ? "Layout" : "布局",
            "Action" => ja ? "操作" : en ? "Action" : "操作",
            "Activate" => ja ? "アクティブ化" : en ? "Activate" : "激活",
            "Create" => ja ? "作成" : en ? "Create" : "创建",
            "Rename" => ja ? "名前変更" : en ? "Rename" : "重命名",
            "Delete" => ja ? "削除" : en ? "Delete" : "删除",
            "LayoutName" => ja ? "レイアウト名" : en ? "Layout name" : "布局名称",
            "NameRequired" => ja ? "名前を入力してください。" : en ? "A name is required." : "请输入名称。",
            "LayoutRequired" => ja ? "レイアウトを選択してください。" : en ? "Select a layout." : "请选择布局。",
            "ActiveLayoutFormat" => ja ? "現在のレイアウト: {0}" : en ? "Active layout: {0}" : "当前布局：{0}",
            "Viewport" => ja ? "ビューポート" : en ? "Viewport" : "视口",
            "ViewportCenter" => ja ? "モデル空間の中心を指定:" : en ? "Specify model-space viewport center:" : "指定模型空间视口中心：",
            "ViewportScale" => ja ? "ビューポート尺度 (1:n)" : en ? "Viewport scale (1:n)" : "视口比例（1:n）",
            "ViewportName" => ja ? "ビューポート名" : en ? "Viewport name" : "视口名称",
            "LockViewport" => ja ? "ビューポートをロック" : en ? "Lock viewport" : "锁定视口",
            "ViewportCreated" => ja ? "ビューポートを作成しました。" : en ? "Viewport created." : "视口已创建。",
            "Preview" => ja ? "印刷プレビュー" : en ? "Plot Preview" : "打印预览",
            "PreviewSummaryFormat" => ja ? "{0} · {1} · 1:{2:0.###}" : en ? "{0} · {1} · 1:{2:0.###}" : "{0} · {1} · 1:{2:0.###}",
            "PreviewClosed" => ja ? "プレビューを閉じました。" : en ? "Preview closed." : "预览已关闭。",
            "PdfSavedFormat" => ja ? "PDF を保存しました: {0}" : en ? "PDF saved: {0}" : "PDF 已保存：{0}",
            "PrintHandoff" => ja ? "PDF をシステムビューアーに渡しました。ビューアーから印刷できます。" : en ? "PDF handed to the system viewer; print from the viewer." : "已将 PDF 交给系统查看器，可从查看器执行打印。",
            "PrintLaunchFailed" => ja ? "システム PDF ビューアーを起動できません。" : en ? "Could not launch the system PDF viewer." : "无法启动系统 PDF 查看器。",
            "PlotWarnings" => ja ? "印刷警告" : en ? "Plot warnings" : "打印警告",
            "Apply" => ja ? "適用" : en ? "Apply" : "应用",
            "Cancel" => ja ? "キャンセル" : en ? "Cancel" : "取消",
            "Close" => ja ? "閉じる" : en ? "Close" : "关闭",
            "FailedFormat" => ja ? "{0} に失敗しました: {1}" : en ? "{0} failed: {1}" : "{0} 失败：{1}",
            _ => key
        };
    }

    private sealed class LayoutSessionState
    {
        private readonly List<CadLayoutDefinition> _layouts;

        private LayoutSessionState(CadPageSetup pageSetup, IEnumerable<CadLayoutDefinition> layouts, string activeLayoutName)
        {
            PageSetup = pageSetup;
            _layouts = layouts.ToList();
            ActiveLayoutName = activeLayoutName;
        }

        public CadPageSetup PageSetup { get; set; }
        public IReadOnlyList<CadLayoutDefinition> Layouts => _layouts;
        public string ActiveLayoutName { get; set; }
        public CadLayoutDefinition ActiveLayout => _layouts.First(layout => string.Equals(layout.Name, ActiveLayoutName, StringComparison.OrdinalIgnoreCase));

        public static LayoutSessionState CreateDefault()
        {
            var setup = new CadPageSetup();
            var layout = new CadLayoutDefinition("Layout1", setup);
            return new LayoutSessionState(setup, [layout], layout.Name);
        }

        public void CreateLayout(string name)
        {
            if (_layouts.Any(layout => string.Equals(layout.Name, name, StringComparison.OrdinalIgnoreCase)))
                throw new InvalidOperationException($"Layout '{name}' already exists.");
            var layout = new CadLayoutDefinition(name, PageSetup);
            _layouts.Add(layout);
            ActiveLayoutName = layout.Name;
        }

        public void RenameLayout(string oldName, string newName)
        {
            if (_layouts.Any(layout => !string.Equals(layout.Name, oldName, StringComparison.OrdinalIgnoreCase) && string.Equals(layout.Name, newName, StringComparison.OrdinalIgnoreCase)))
                throw new InvalidOperationException($"Layout '{newName}' already exists.");
            var index = _layouts.FindIndex(layout => string.Equals(layout.Name, oldName, StringComparison.OrdinalIgnoreCase));
            if (index < 0) throw new KeyNotFoundException($"Layout '{oldName}' does not exist.");
            _layouts[index] = _layouts[index].Rename(newName);
            if (string.Equals(ActiveLayoutName, oldName, StringComparison.OrdinalIgnoreCase)) ActiveLayoutName = _layouts[index].Name;
        }

        public void DeleteLayout(string name)
        {
            if (_layouts.Count <= 1) throw new InvalidOperationException("At least one paper layout must remain.");
            var index = _layouts.FindIndex(layout => string.Equals(layout.Name, name, StringComparison.OrdinalIgnoreCase));
            if (index < 0) throw new KeyNotFoundException($"Layout '{name}' does not exist.");
            _layouts.RemoveAt(index);
            if (string.Equals(ActiveLayoutName, name, StringComparison.OrdinalIgnoreCase)) ActiveLayoutName = _layouts[0].Name;
        }

        public void ReplaceActiveLayout(CadLayoutDefinition layout)
        {
            var index = _layouts.FindIndex(existing => string.Equals(existing.Name, ActiveLayoutName, StringComparison.OrdinalIgnoreCase));
            if (index < 0) throw new InvalidOperationException("Active layout is missing.");
            _layouts[index] = layout;
            ActiveLayoutName = layout.Name;
        }

        public void ReplaceActiveLayoutPageSetup(CadPageSetup setup) => ReplaceActiveLayout(ActiveLayout.WithPageSetup(setup));
    }
}
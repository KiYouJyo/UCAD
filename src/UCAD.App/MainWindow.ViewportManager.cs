using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using UCAD.Core.Commands;
using UCAD.Core.Geometry;
using UCAD.Core.Layout;
using UCAD.Workspace;

namespace UCAD;

public sealed partial class MainWindow
{
    private readonly HashSet<CadWorkspaceSession> _viewportManagerSubscribedSessions = [];
    private readonly HashSet<CadWorkspaceSession> _viewportManagerRunningSessions = [];
    private bool _viewportManagerUiInitialized;

    internal void EnsureViewportManagerUiInitialized()
    {
        if (_viewportManagerUiInitialized) return;
        _viewportManagerUiInitialized = true;

        if (!_commandRegistry.TryResolve("VIEWPORTS", out _))
            _commandRegistry.Register(new CadCommandDefinition("VIEWPORTS", CadCommandCategory.Edit, ["VPM"]));

        var style = ViewToolShelf.Children.OfType<Button>().FirstOrDefault()?.Style;
        if (!ViewToolShelf.Children.OfType<Button>().Any(button => string.Equals(button.Tag?.ToString(), "VIEWPORTS", StringComparison.Ordinal)))
            ViewToolShelf.Children.Add(CreateLayoutPlotShelfButton("VIEWPORTS", "VPM", style));
        RefreshCommandSearchSource();

        RootLayout.Loaded += ViewportManager_RootLoaded;
        DocumentTabs.SelectionChanged += ViewportManager_DocumentTabsSelectionChanged;
    }

    private void ViewportManager_RootLoaded(object sender, RoutedEventArgs e)
    {
        RootLayout.Loaded -= ViewportManager_RootLoaded;
        if (ActiveSession is CadWorkspaceSession session) EnsureViewportManagerSessionSubscribed(session);
    }

    private void ViewportManager_DocumentTabsSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ActiveSession is CadWorkspaceSession session) EnsureViewportManagerSessionSubscribed(session);
    }

    private void EnsureViewportManagerSessionSubscribed(CadWorkspaceSession session)
    {
        if (!_viewportManagerSubscribedSessions.Add(session)) return;
        session.CommandSession.Changed += (_, _) => ViewportManager_CommandSessionChanged(session);
    }

    private void ViewportManager_CommandSessionChanged(CadWorkspaceSession session)
    {
        if (session.CommandSession.ActiveCommand?.Name != "VIEWPORTS" || !_viewportManagerRunningSessions.Add(session)) return;
        RootLayout.DispatcherQueue.TryEnqueue(async () =>
        {
            try
            {
                await RunViewportManagerAsync(session);
            }
            catch (TaskCanceledException)
            {
                // Command cancellation owns the visible status.
            }
            catch (Exception ex)
            {
                App.WriteStartupFailure("ViewportManager", ex);
                SetSessionStatus(session, string.Format(ViewportManagerText("FailedFormat"), ex.Message));
                if (session.CommandSession.IsActive) session.CommandSession.Cancel();
            }
            finally
            {
                _viewportManagerRunningSessions.Remove(session);
                UpdateSessionUi(session);
            }
        });
    }

    private async Task RunViewportManagerAsync(CadWorkspaceSession session)
    {
        var state = GetLayoutState(session);
        var layout = state.ActiveLayout;
        if (layout.Viewports.Count == 0)
        {
            SetSessionStatus(session, ViewportManagerText("NoViewports"));
            session.CommandSession.Cancel();
            return;
        }

        var selector = new ComboBox
        {
            Header = ViewportManagerText("Viewport"),
            ItemsSource = layout.Viewports.Select(viewport => $"{viewport.Name} · 1:{viewport.ScaleDenominator:0.###}").ToArray(),
            SelectedIndex = 0,
            MinWidth = 360
        };
        var action = new ComboBox
        {
            Header = ViewportManagerText("Action"),
            ItemsSource = new[] { ViewportManagerText("Edit"), ViewportManagerText("Delete"), ViewportManagerText("Tile") },
            SelectedIndex = 0
        };
        var panel = new StackPanel { MinWidth = 390, Spacing = 8 };
        panel.Children.Add(selector);
        panel.Children.Add(action);
        var dialog = new ContentDialog
        {
            XamlRoot = RootLayout.XamlRoot,
            Title = ViewportManagerText("Title"),
            Content = panel,
            PrimaryButtonText = ViewportManagerText("Continue"),
            CloseButtonText = ViewportManagerText("Cancel")
        };
        if (await dialog.ShowAsync() != ContentDialogResult.Primary)
        {
            session.CommandSession.Cancel();
            return;
        }

        if (action.SelectedIndex == 2)
        {
            state.ReplaceActiveLayout(CadLayoutViewportTiler.Tile(layout));
            PersistViewportManagerState(session, state);
            CompleteLayoutPlot(session, ViewportManagerText("Tiled"));
            return;
        }

        if (selector.SelectedIndex < 0 || selector.SelectedIndex >= layout.Viewports.Count)
            throw new InvalidOperationException(ViewportManagerText("SelectionRequired"));
        var selected = layout.Viewports[selector.SelectedIndex];

        if (action.SelectedIndex == 1)
        {
            state.ReplaceActiveLayout(layout.RemoveViewport(selected.Id));
            PersistViewportManagerState(session, state);
            CompleteLayoutPlot(session, string.Format(ViewportManagerText("DeletedFormat"), selected.Name));
            return;
        }

        var edited = await PromptViewportEditAsync(state.PageSetup, selected);
        if (edited is null)
        {
            session.CommandSession.Cancel();
            return;
        }
        state.ReplaceActiveLayout(layout.ReplaceViewport(edited));
        PersistViewportManagerState(session, state);
        CompleteLayoutPlot(session, string.Format(ViewportManagerText("UpdatedFormat"), edited.Name));
    }

    private async Task<CadLayoutViewport?> PromptViewportEditAsync(CadPageSetup setup, CadLayoutViewport viewport)
    {
        var name = new TextBox { Header = ViewportManagerText("Name"), Text = viewport.Name };
        var centerX = CoordinateNumber(ViewportManagerText("CenterX"), viewport.ModelCenter.X);
        var centerY = CoordinateNumber(ViewportManagerText("CenterY"), viewport.ModelCenter.Y);
        var scale = new NumberBox { Header = ViewportManagerText("Scale"), Value = viewport.ScaleDenominator, Minimum = 0.001, Maximum = 1_000_000_000 };
        var twist = new NumberBox { Header = ViewportManagerText("Twist"), Value = viewport.TwistAngleRadians * 180 / Math.PI, Minimum = -36000, Maximum = 36000 };
        var left = PaperNumber(ViewportManagerText("PaperLeft"), viewport.PaperRectMm.Left);
        var bottom = PaperNumber(ViewportManagerText("PaperBottom"), viewport.PaperRectMm.Bottom);
        var width = PaperNumber(ViewportManagerText("PaperWidth"), viewport.PaperRectMm.Width, minimum: 0.1);
        var height = PaperNumber(ViewportManagerText("PaperHeight"), viewport.PaperRectMm.Height, minimum: 0.1);
        var locked = new ToggleSwitch { Header = ViewportManagerText("Locked"), IsOn = viewport.Locked };

        var panel = new StackPanel { MinWidth = 430, Spacing = 8 };
        panel.Children.Add(name);
        panel.Children.Add(centerX); panel.Children.Add(centerY);
        panel.Children.Add(scale); panel.Children.Add(twist);
        panel.Children.Add(left); panel.Children.Add(bottom); panel.Children.Add(width); panel.Children.Add(height);
        panel.Children.Add(locked);

        var dialog = new ContentDialog
        {
            XamlRoot = RootLayout.XamlRoot,
            Title = ViewportManagerText("EditTitle"),
            Content = new ScrollViewer { MaxHeight = 560, Content = panel },
            PrimaryButtonText = ViewportManagerText("Apply"),
            CloseButtonText = ViewportManagerText("Cancel")
        };
        if (await dialog.ShowAsync() != ContentDialogResult.Primary) return null;
        if (string.IsNullOrWhiteSpace(name.Text)) throw new InvalidOperationException(ViewportManagerText("NameRequired"));

        var paperRect = new CadRect(
            Number(left, viewport.PaperRectMm.Left),
            Number(bottom, viewport.PaperRectMm.Bottom),
            Number(left, viewport.PaperRectMm.Left) + Number(width, viewport.PaperRectMm.Width),
            Number(bottom, viewport.PaperRectMm.Bottom) + Number(height, viewport.PaperRectMm.Height));
        if (!setup.PrintablePaperRectMm.Contains(paperRect, 1e-6))
            throw new InvalidOperationException(ViewportManagerText("OutsidePrintable"));

        return new CadLayoutViewport(
            name.Text.Trim(),
            paperRect,
            new CadPoint(Number(centerX, viewport.ModelCenter.X), Number(centerY, viewport.ModelCenter.Y)),
            Number(scale, viewport.ScaleDenominator),
            Number(twist, viewport.TwistAngleRadians * 180 / Math.PI) * Math.PI / 180,
            locked.IsOn,
            viewport.Id);
    }

    private static NumberBox CoordinateNumber(string header, double value) => new()
    {
        Header = header,
        Value = value,
        Minimum = -1_000_000_000_000,
        Maximum = 1_000_000_000_000
    };

    private static NumberBox PaperNumber(string header, double value, double minimum = 0) => new()
    {
        Header = header,
        Value = value,
        Minimum = minimum,
        Maximum = 100_000
    };

    private static double Number(NumberBox box, double fallback) => double.IsNaN(box.Value) ? fallback : box.Value;

    private static void PersistViewportManagerState(CadWorkspaceSession session, LayoutSessionState state)
    {
        state.PageSetup = state.ActiveLayout.PageSetup;
        session.Document.SetLayoutTable(state.Layouts, state.ActiveLayoutName);
    }

    private static string ViewportManagerText(string key)
    {
        var language = Services.LocalizationService.Current.CurrentLanguageTag;
        var ja = language.StartsWith("ja", StringComparison.OrdinalIgnoreCase);
        var en = language.StartsWith("en", StringComparison.OrdinalIgnoreCase);
        return key switch
        {
            "Title" => ja ? "ビューポート管理" : en ? "Viewport Manager" : "视口管理器",
            "Viewport" => ja ? "ビューポート" : en ? "Viewport" : "视口",
            "Action" => ja ? "操作" : en ? "Action" : "操作",
            "Edit" => ja ? "編集" : en ? "Edit" : "编辑",
            "Delete" => ja ? "削除" : en ? "Delete" : "删除",
            "Tile" => ja ? "自動整列" : en ? "Tile automatically" : "自动平铺",
            "Continue" => ja ? "続行" : en ? "Continue" : "继续",
            "Apply" => ja ? "適用" : en ? "Apply" : "应用",
            "Cancel" => ja ? "キャンセル" : en ? "Cancel" : "取消",
            "EditTitle" => ja ? "ビューポート編集" : en ? "Edit Viewport" : "编辑视口",
            "Name" => ja ? "名前" : en ? "Name" : "名称",
            "CenterX" => ja ? "モデル中心 X" : en ? "Model center X" : "模型中心 X",
            "CenterY" => ja ? "モデル中心 Y" : en ? "Model center Y" : "模型中心 Y",
            "Scale" => ja ? "尺度 (1:n)" : en ? "Scale (1:n)" : "比例（1:n）",
            "Twist" => ja ? "回転角 (度)" : en ? "Twist (degrees)" : "旋转角（度）",
            "PaperLeft" => ja ? "用紙 X (mm)" : en ? "Paper X (mm)" : "纸面 X（mm）",
            "PaperBottom" => ja ? "用紙 Y (mm)" : en ? "Paper Y (mm)" : "纸面 Y（mm）",
            "PaperWidth" => ja ? "幅 (mm)" : en ? "Width (mm)" : "宽度（mm）",
            "PaperHeight" => ja ? "高さ (mm)" : en ? "Height (mm)" : "高度（mm）",
            "Locked" => ja ? "ロック" : en ? "Locked" : "锁定",
            "NoViewports" => ja ? "このレイアウトにはビューポートがありません。VIEWPORT で作成してください。" : en ? "This layout has no viewports. Create one with VIEWPORT." : "当前布局没有视口，请先使用 VIEWPORT 创建。",
            "SelectionRequired" => ja ? "ビューポートを選択してください。" : en ? "Select a viewport." : "请选择视口。",
            "NameRequired" => ja ? "名前を入力してください。" : en ? "A name is required." : "请输入名称。",
            "OutsidePrintable" => ja ? "ビューポートは印刷可能領域内に配置してください。" : en ? "Viewport must stay inside the printable paper area." : "视口必须位于可打印纸面区域内。",
            "Tiled" => ja ? "ビューポートを自動整列しました。" : en ? "Viewports tiled." : "视口已自动平铺。",
            "DeletedFormat" => ja ? "ビューポート '{0}' を削除しました。" : en ? "Viewport '{0}' deleted." : "已删除视口“{0}”。",
            "UpdatedFormat" => ja ? "ビューポート '{0}' を更新しました。" : en ? "Viewport '{0}' updated." : "已更新视口“{0}”。",
            "FailedFormat" => ja ? "ビューポート管理に失敗しました: {0}" : en ? "Viewport manager failed: {0}" : "视口管理失败：{0}",
            _ => key
        };
    }
}

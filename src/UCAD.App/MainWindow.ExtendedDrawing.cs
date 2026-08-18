using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using UCAD.Core.Commands;
using UCAD.Core.Entities;
using UCAD.Core.Geometry;
using UCAD.Workspace;

namespace UCAD;

public sealed partial class MainWindow
{
    private readonly HashSet<CadWorkspaceSession> _extendedDrawingSubscribedSessions = [];
    private readonly HashSet<CadWorkspaceSession> _extendedDrawingRunningSessions = [];
    private bool _extendedDrawingUiInitialized;

    internal void EnsureExtendedDrawingUiInitialized()
    {
        if (_extendedDrawingUiInitialized) return;
        _extendedDrawingUiInitialized = true;

        RegisterExtendedDrawingCommands();
        PromoteReservedDrawButtons();
        AppendExtendedDrawButtons();
        RefreshCommandSearchSource();

        RootLayout.Loaded += ExtendedDrawing_RootLoaded;
        DocumentTabs.SelectionChanged += ExtendedDrawing_DocumentTabsSelectionChanged;
    }

    private void RegisterExtendedDrawingCommands()
    {
        RegisterIfMissing(new CadCommandDefinition("ELLIPSE", CadCommandCategory.Draw, "EL"));
        RegisterIfMissing(new CadCommandDefinition("POLYGON", CadCommandCategory.Draw, "POL"));
        RegisterIfMissing(new CadCommandDefinition("SPLINE", CadCommandCategory.Draw, "SPL"));
        RegisterIfMissing(new CadCommandDefinition("POINT", CadCommandCategory.Draw, "PO"));
        RegisterIfMissing(new CadCommandDefinition("XLINE", CadCommandCategory.Draw, "XL"));
        RegisterIfMissing(new CadCommandDefinition("RAY", CadCommandCategory.Draw));
    }

    private void RegisterIfMissing(CadCommandDefinition definition)
    {
        if (_commandRegistry.TryResolve(definition.Name, out _)) return;
        _commandRegistry.Register(definition);
    }

    private void RefreshCommandSearchSource()
    {
        CommandSearch.ItemsSource = _commandRegistry.Commands
            .SelectMany(command => command.Tokens)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(token => token, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private void PromoteReservedDrawButtons()
    {
        foreach (var button in Descendants<Button>(DrawToolShelf))
        {
            var shortcut = Descendants<TextBlock>(button)
                .Select(text => text.Text?.Trim().ToUpperInvariant())
                .FirstOrDefault(text => text is "RAY" or "XL");
            if (shortcut is null) continue;
            ConfigureExtendedDrawButton(button, shortcut == "XL" ? "XLINE" : "RAY");
        }
    }

    private void AppendExtendedDrawButtons()
    {
        var inheritedStyle = DrawToolShelf.Children.OfType<Button>().FirstOrDefault()?.Style;
        foreach (var command in new[] { "ELLIPSE", "POLYGON", "SPLINE", "POINT" })
        {
            if (DrawToolShelf.Children.OfType<Button>().Any(button => string.Equals(button.Tag?.ToString(), command, StringComparison.Ordinal)))
                continue;
            DrawToolShelf.Children.Add(CreateExtendedDrawShelfButton(command, inheritedStyle));
        }
    }

    private Button CreateExtendedDrawShelfButton(string command, Style? inheritedStyle)
    {
        var alias = command switch
        {
            "ELLIPSE" => "EL",
            "POLYGON" => "POL",
            "SPLINE" => "SPL",
            "POINT" => "PO",
            _ => command
        };
        var button = new Button
        {
            Tag = command,
            Style = inheritedStyle,
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
        ConfigureExtendedDrawButton(button, command);
        return button;
    }

    private void ConfigureExtendedDrawButton(Button button, string command)
    {
        button.Tag = command;
        button.IsHitTestVisible = true;
        button.IsEnabled = true;
        button.Opacity = 1;
        button.Click -= RunCommand_Click;
        button.Click += RunCommand_Click;
    }

    private void ExtendedDrawing_RootLoaded(object sender, RoutedEventArgs e)
    {
        RootLayout.Loaded -= ExtendedDrawing_RootLoaded;
        if (ActiveSession is CadWorkspaceSession session) EnsureExtendedDrawingSessionSubscribed(session);
    }

    private void ExtendedDrawing_DocumentTabsSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ActiveSession is CadWorkspaceSession session) EnsureExtendedDrawingSessionSubscribed(session);
    }

    private void EnsureExtendedDrawingSessionSubscribed(CadWorkspaceSession session)
    {
        session.Viewport.EnsureModifyInputHooks();
        session.Viewport.EnsureExtendedDrawingRenderHooks();
        if (!_extendedDrawingSubscribedSessions.Add(session)) return;
        session.CommandSession.Changed += (_, _) => ExtendedDrawing_CommandSessionChanged(session);
    }

    private void ExtendedDrawing_CommandSessionChanged(CadWorkspaceSession session)
    {
        var command = session.CommandSession.ActiveCommand;
        if (command is null)
        {
            if (_extendedDrawingRunningSessions.Contains(session)) session.Viewport.CancelModifyInput();
            return;
        }
        if (!IsExtendedDrawingCommand(command) || !_extendedDrawingRunningSessions.Add(session)) return;

        RootLayout.DispatcherQueue.TryEnqueue(async () =>
        {
            try
            {
                await RunExtendedDrawingCommandAsync(session, command);
            }
            catch (TaskCanceledException)
            {
                // Escape or command replacement owns the visible cancellation state.
            }
            catch (Exception ex)
            {
                App.WriteStartupFailure($"ExtendedDrawing:{command.Name}", ex);
                SetSessionStatus(session, string.Format(ExtendedDrawingText("FailedFormat"), command.Name, ex.Message));
                if (session.CommandSession.IsActive) session.CommandSession.Cancel();
            }
            finally
            {
                session.Viewport.CancelModifyInput();
                session.CommandBasePoint = null;
                _extendedDrawingRunningSessions.Remove(session);
                UpdateSessionUi(session);
            }
        });
    }

    private static bool IsExtendedDrawingCommand(CadCommandDefinition command) => command.Name is
        "ELLIPSE" or "POLYGON" or "SPLINE" or "POINT" or "XLINE" or "RAY";

    private async Task RunExtendedDrawingCommandAsync(CadWorkspaceSession session, CadCommandDefinition command)
    {
        switch (command.Name)
        {
            case "POINT":
                await RunPointCommandAsync(session);
                break;
            case "RAY":
                await RunDirectedLineCommandAsync(session, rayOnly: true);
                break;
            case "XLINE":
                await RunDirectedLineCommandAsync(session, rayOnly: false);
                break;
            case "ELLIPSE":
                await RunEllipseCommandAsync(session);
                break;
            case "POLYGON":
                await RunPolygonCommandAsync(session);
                break;
            case "SPLINE":
                await RunSplineCommandAsync(session);
                break;
        }
    }

    private async Task RunPointCommandAsync(CadWorkspaceSession session)
    {
        var point = await RequestExtendedPointAsync(session, ExtendedDrawingText("PointPosition"));
        var entity = new PointEntity(point);
        session.Document.Add(entity);
        session.Interaction.Selection.Replace(entity.Id);
        CompleteExtendedDrawing(session, ExtendedDrawingText("PointComplete"));
    }

    private async Task RunDirectedLineCommandAsync(CadWorkspaceSession session, bool rayOnly)
    {
        var firstPrompt = rayOnly ? ExtendedDrawingText("RayOrigin") : ExtendedDrawingText("XLinePoint");
        var origin = await RequestExtendedPointAsync(session, firstPrompt);
        session.CommandBasePoint = origin;
        var secondPrompt = rayOnly ? ExtendedDrawingText("RayDirection") : ExtendedDrawingText("XLineDirection");
        var directionPoint = await RequestExtendedPointAsync(
            session,
            secondPrompt,
            origin,
            useOrtho: true,
            previewFactory: pointer =>
            {
                var vector = pointer - origin;
                if (vector.Length <= 1e-9) return [];
                return rayOnly ? [new RayEntity(origin, vector)] : [new XLineEntity(origin, vector)];
            });
        var direction = directionPoint - origin;
        if (direction.Length <= 1e-9) throw new InvalidOperationException(ExtendedDrawingText("DirectionRequired"));
        ICadEntity entity = rayOnly ? new RayEntity(origin, direction) : new XLineEntity(origin, direction);
        session.Document.Add(entity);
        session.Interaction.Selection.Replace(entity.Id);
        CompleteExtendedDrawing(session, rayOnly ? ExtendedDrawingText("RayComplete") : ExtendedDrawingText("XLineComplete"));
    }

    private async Task RunEllipseCommandAsync(CadWorkspaceSession session)
    {
        var center = await RequestExtendedPointAsync(session, ExtendedDrawingText("EllipseCenter"));
        session.CommandBasePoint = center;
        var majorEnd = await RequestExtendedPointAsync(
            session,
            ExtendedDrawingText("EllipseAxisEnd"),
            center,
            useOrtho: false,
            previewFactory: pointer => (pointer - center).Length > 1e-9
                ? [new LineEntity(center, pointer)]
                : []);
        var firstAxis = majorEnd - center;
        if (firstAxis.Length <= 1e-9) throw new InvalidOperationException(ExtendedDrawingText("AxisRequired"));
        session.CommandBasePoint = center;
        var minorPoint = await RequestExtendedPointAsync(
            session,
            ExtendedDrawingText("EllipseOtherAxis"),
            center,
            useOrtho: false,
            previewFactory: pointer => TryCreateEllipse(center, firstAxis, pointer, out var preview) && preview is not null ? [preview] : []);
        if (!TryCreateEllipse(center, firstAxis, minorPoint, out var ellipse) || ellipse is null)
            throw new InvalidOperationException(ExtendedDrawingText("AxisRequired"));
        session.Document.Add(ellipse);
        session.Interaction.Selection.Replace(ellipse.Id);
        CompleteExtendedDrawing(session, ExtendedDrawingText("EllipseComplete"));
    }

    private async Task RunPolygonCommandAsync(CadWorkspaceSession session)
    {
        var sides = await PromptIntegerAsync(ExtendedDrawingText("PolygonSidesTitle"), ExtendedDrawingText("PolygonSidesLabel"), 6, 3, 1024);
        if (sides is null)
        {
            session.CommandSession.Cancel();
            return;
        }
        var center = await RequestExtendedPointAsync(session, ExtendedDrawingText("PolygonCenter"));
        session.CommandBasePoint = center;
        var vertex = await RequestExtendedPointAsync(
            session,
            ExtendedDrawingText("PolygonVertex"),
            center,
            useOrtho: false,
            previewFactory: pointer => (pointer - center).Length > 1e-9 ? [CreatePolygon(center, pointer, sides.Value)] : []);
        var polygon = CreatePolygon(center, vertex, sides.Value);
        session.Document.Add(polygon);
        session.Interaction.Selection.Replace(polygon.Id);
        CompleteExtendedDrawing(session, ExtendedDrawingText("PolygonComplete"));
    }

    private async Task RunSplineCommandAsync(CadWorkspaceSession session)
    {
        var count = await PromptIntegerAsync(ExtendedDrawingText("SplinePointsTitle"), ExtendedDrawingText("SplinePointsLabel"), 4, 2, 64);
        if (count is null)
        {
            session.CommandSession.Cancel();
            return;
        }

        var points = new List<CadPoint>(count.Value);
        for (var i = 0; i < count.Value; i++)
        {
            var basePoint = points.Count > 0 ? points[^1] : (CadPoint?)null;
            var point = await RequestExtendedPointAsync(
                session,
                string.Format(ExtendedDrawingText("SplinePointFormat"), i + 1, count.Value),
                basePoint,
                useOrtho: false,
                previewFactory: pointer => BuildSplinePreview(points, pointer));
            points.Add(point);
            session.CommandBasePoint = point;
        }
        var spline = new SplineEntity(points);
        session.Document.Add(spline);
        session.Interaction.Selection.Replace(spline.Id);
        CompleteExtendedDrawing(session, ExtendedDrawingText("SplineComplete"));
    }

    private async Task<CadPoint> RequestExtendedPointAsync(
        CadWorkspaceSession session,
        string prompt,
        CadPoint? basePoint = null,
        bool useOrtho = false,
        Func<CadPoint, IReadOnlyList<ICadEntity>>? previewFactory = null)
    {
        var tcs = new TaskCompletionSource<CadPoint>(TaskCreationOptions.RunContinuationsAsynchronously);
        void PointAccepted(CadPoint point) => tcs.TrySetResult(point);
        void CommandChanged(object? sender, EventArgs e)
        {
            if (session.CommandSession.ActiveCommand is not { } active || !IsExtendedDrawingCommand(active))
                tcs.TrySetCanceled();
        }

        session.Viewport.ModifyPointAccepted += PointAccepted;
        session.CommandSession.Changed += CommandChanged;
        try
        {
            SetSessionStatus(session, prompt);
            session.Viewport.BeginModifyPointInput(basePoint, useOrtho, previewFactory);
            return await tcs.Task;
        }
        finally
        {
            session.Viewport.ModifyPointAccepted -= PointAccepted;
            session.CommandSession.Changed -= CommandChanged;
        }
    }

    private async Task<int?> PromptIntegerAsync(string title, string label, int initial, int minimum, int maximum)
    {
        var number = new NumberBox
        {
            Value = initial,
            Minimum = minimum,
            Maximum = maximum,
            SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Inline,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        var content = new StackPanel
        {
            Width = 280,
            Spacing = 8,
            Children =
            {
                new TextBlock { Text = label, TextWrapping = TextWrapping.Wrap },
                number
            }
        };
        var dialog = new ContentDialog
        {
            XamlRoot = RootLayout.XamlRoot,
            Title = title,
            Content = content,
            PrimaryButtonText = ExtendedDrawingText("Confirm"),
            CloseButtonText = ExtendedDrawingText("Cancel"),
            DefaultButton = ContentDialogButton.Primary
        };
        if (await dialog.ShowAsync() != ContentDialogResult.Primary) return null;
        if (double.IsNaN(number.Value)) return initial;
        return Math.Clamp((int)Math.Round(number.Value), minimum, maximum);
    }

    private static bool TryCreateEllipse(CadPoint center, CadVector firstAxis, CadPoint otherAxisPoint, out EllipseEntity? ellipse)
    {
        ellipse = null;
        var firstRadius = firstAxis.Length;
        if (firstRadius <= 1e-9) return false;
        var ux = firstAxis.X / firstRadius;
        var uy = firstAxis.Y / firstRadius;
        var fromCenter = otherAxisPoint - center;
        var signedPerpendicular = (-uy * fromCenter.X) + (ux * fromCenter.Y);
        var secondRadius = Math.Abs(signedPerpendicular);
        if (secondRadius <= 1e-9) return false;

        if (secondRadius <= firstRadius)
        {
            ellipse = new EllipseEntity(center, firstAxis, secondRadius / firstRadius);
            return true;
        }

        var sign = signedPerpendicular < 0 ? -1.0 : 1.0;
        var secondMajor = new CadVector(-uy * secondRadius * sign, ux * secondRadius * sign);
        ellipse = new EllipseEntity(center, secondMajor, firstRadius / secondRadius);
        return true;
    }

    private static PolylineEntity CreatePolygon(CadPoint center, CadPoint vertex, int sides)
    {
        var vector = vertex - center;
        var radius = vector.Length;
        if (radius <= 1e-9) throw new ArgumentException("Polygon radius must be positive.", nameof(vertex));
        var start = Math.Atan2(vector.Y, vector.X);
        var points = Enumerable.Range(0, sides)
            .Select(i =>
            {
                var angle = start + (Math.Tau * i / sides);
                return new CadPoint(center.X + (Math.Cos(angle) * radius), center.Y + (Math.Sin(angle) * radius));
            })
            .ToArray();
        return new PolylineEntity(points, closed: true);
    }

    private static IReadOnlyList<ICadEntity> BuildSplinePreview(IReadOnlyList<CadPoint> accepted, CadPoint pointer)
    {
        if (accepted.Count == 0) return [];
        if (accepted.Count == 1)
        {
            if ((pointer - accepted[0]).Length <= 1e-9) return [];
            return [new LineEntity(accepted[0], pointer)];
        }
        var points = accepted.Concat([pointer]).ToArray();
        return [new SplineEntity(points)];
    }

    private void CompleteExtendedDrawing(CadWorkspaceSession session, string status)
    {
        session.Viewport.CancelModifyInput();
        session.CommandBasePoint = null;
        session.CommandSession.Complete();
        SetSessionStatus(session, status);
    }

    private static string ExtendedDrawingText(string key)
    {
        var language = Services.LocalizationService.Current.CurrentLanguageTag;
        var ja = language.StartsWith("ja", StringComparison.OrdinalIgnoreCase);
        var en = language.StartsWith("en", StringComparison.OrdinalIgnoreCase);
        return key switch
        {
            "PointPosition" => ja ? "点の位置を指定:" : en ? "Specify point position:" : "指定点的位置：",
            "PointComplete" => ja ? "点を作成しました。" : en ? "Point created." : "点已创建。",
            "RayOrigin" => ja ? "放射線の始点を指定:" : en ? "Specify ray origin:" : "指定射线起点：",
            "RayDirection" => ja ? "放射線の方向を指定:" : en ? "Specify ray direction:" : "指定射线方向：",
            "RayComplete" => ja ? "放射線を作成しました。" : en ? "Ray created." : "射线已创建。",
            "XLinePoint" => ja ? "構築線上の点を指定:" : en ? "Specify point on construction line:" : "指定构造线上的点：",
            "XLineDirection" => ja ? "構築線の方向を指定:" : en ? "Specify construction-line direction:" : "指定构造线方向：",
            "XLineComplete" => ja ? "構築線を作成しました。" : en ? "Construction line created." : "构造线已创建。",
            "DirectionRequired" => ja ? "方向には異なる 2 点が必要です。" : en ? "Direction requires two distinct points." : "方向需要两个不同的点。",
            "EllipseCenter" => ja ? "楕円の中心を指定:" : en ? "Specify ellipse center:" : "指定椭圆中心：",
            "EllipseAxisEnd" => ja ? "第 1 軸の端点を指定:" : en ? "Specify first axis endpoint:" : "指定第一轴端点：",
            "EllipseOtherAxis" => ja ? "もう一方の軸距離を指定:" : en ? "Specify other-axis distance:" : "指定另一轴距离：",
            "EllipseComplete" => ja ? "楕円を作成しました。" : en ? "Ellipse created." : "椭圆已创建。",
            "AxisRequired" => ja ? "楕円軸は 0 より大きい必要があります。" : en ? "Ellipse axes must be greater than zero." : "椭圆轴长度必须大于 0。",
            "PolygonSidesTitle" => ja ? "ポリゴン" : en ? "Polygon" : "多边形",
            "PolygonSidesLabel" => ja ? "辺の数 (3–1024)" : en ? "Number of sides (3–1024)" : "边数（3–1024）",
            "PolygonCenter" => ja ? "ポリゴンの中心を指定:" : en ? "Specify polygon center:" : "指定多边形中心：",
            "PolygonVertex" => ja ? "頂点を指定:" : en ? "Specify polygon vertex:" : "指定多边形顶点：",
            "PolygonComplete" => ja ? "ポリゴンを作成しました。" : en ? "Polygon created." : "多边形已创建。",
            "SplinePointsTitle" => ja ? "スプライン" : en ? "Spline" : "样条曲线",
            "SplinePointsLabel" => ja ? "フィット点の数 (2–64)" : en ? "Fit-point count (2–64)" : "拟合点数量（2–64）",
            "SplinePointFormat" => ja ? "フィット点 {0}/{1} を指定:" : en ? "Specify fit point {0}/{1}:" : "指定拟合点 {0}/{1}：",
            "SplineComplete" => ja ? "スプラインを作成しました。" : en ? "Spline created." : "样条曲线已创建。",
            "Confirm" => ja ? "確定" : en ? "OK" : "确定",
            "Cancel" => ja ? "キャンセル" : en ? "Cancel" : "取消",
            "FailedFormat" => ja ? "{0} に失敗しました: {1}" : en ? "{0} failed: {1}" : "{0} 失败：{1}",
            _ => key
        };
    }
}
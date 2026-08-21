using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using UCAD.Core.Commands;
using UCAD.Core.Entities;
using UCAD.Core.Gis;
using UCAD.Workspace;
using Windows.Storage.Pickers;

namespace UCAD;

public sealed partial class MainWindow
{
    private readonly HashSet<CadWorkspaceSession> _gisExchangeSubscribedSessions = [];
    private readonly HashSet<CadWorkspaceSession> _gisExchangeRunningSessions = [];
    private bool _gisExchangeUiInitialized;
    private MenuFlyoutSubItem? _gisExchangeMenu;

    internal void EnsureGisExchangeUiInitialized()
    {
        if (_gisExchangeUiInitialized) return;
        _gisExchangeUiInitialized = true;
        RegisterGisCommand("GEOJSONIMPORT", "GJI");
        RegisterGisCommand("GEOJSONEXPORT", "GJE");
        RegisterGisCommand("CSVPOINTIMPORT", "CPI");
        RegisterGisCommand("CSVPOINTEXPORT", "CPE");
        RefreshCommandSearchSource();
        RootLayout.Loaded += GisExchange_RootLoaded;
        DocumentTabs.SelectionChanged += GisExchange_DocumentTabsSelectionChanged;
    }

    private void RegisterGisCommand(string name, params string[] aliases)
    {
        if (_commandRegistry.TryResolve(name, out _)) return;
        _commandRegistry.Register(new CadCommandDefinition(name, CadCommandCategory.Edit, aliases));
    }

    private void GisExchange_RootLoaded(object sender, RoutedEventArgs e)
    {
        RootLayout.Loaded -= GisExchange_RootLoaded;
        BuildGisExchangeMenu();
        if (ActiveSession is CadWorkspaceSession session) EnsureGisExchangeSessionSubscribed(session);
    }

    private void GisExchange_DocumentTabsSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ActiveSession is CadWorkspaceSession session) EnsureGisExchangeSessionSubscribed(session);
    }

    private void BuildGisExchangeMenu()
    {
        if (FileMenuButton.Flyout is not MenuFlyout menu || _gisExchangeMenu is not null) return;
        _gisExchangeMenu = new MenuFlyoutSubItem { Text = GisText("Menu") };
        _gisExchangeMenu.Items.Add(CreateGisMenuItem("ImportGeoJson", async () => await ImportGeoJsonAsync()));
        _gisExchangeMenu.Items.Add(CreateGisMenuItem("ExportGeoJson", async () => await ExportGeoJsonAsync()));
        _gisExchangeMenu.Items.Add(new MenuFlyoutSeparator());
        _gisExchangeMenu.Items.Add(CreateGisMenuItem("ImportCsvPoints", async () => await ImportCsvPointsAsync()));
        _gisExchangeMenu.Items.Add(CreateGisMenuItem("ExportCsvPoints", async () => await ExportCsvPointsAsync()));
        menu.Items.Add(new MenuFlyoutSeparator());
        menu.Items.Add(_gisExchangeMenu);
    }

    private MenuFlyoutItem CreateGisMenuItem(string textKey, Func<Task> action)
    {
        var item = new MenuFlyoutItem { Text = GisText(textKey) };
        item.Click += async (_, _) => await action();
        return item;
    }

    private void EnsureGisExchangeSessionSubscribed(CadWorkspaceSession session)
    {
        if (!_gisExchangeSubscribedSessions.Add(session)) return;
        session.CommandSession.Changed += (_, _) => GisExchange_CommandSessionChanged(session);
    }

    private void GisExchange_CommandSessionChanged(CadWorkspaceSession session)
    {
        var command = session.CommandSession.ActiveCommand;
        if (command is null || !IsGisCommand(command.Name) || !_gisExchangeRunningSessions.Add(session)) return;
        RootLayout.DispatcherQueue.TryEnqueue(async () =>
        {
            try
            {
                switch (command.Name)
                {
                    case "GEOJSONIMPORT": await ImportGeoJsonAsync(session); break;
                    case "GEOJSONEXPORT": await ExportGeoJsonAsync(session); break;
                    case "CSVPOINTIMPORT": await ImportCsvPointsAsync(session); break;
                    case "CSVPOINTEXPORT": await ExportCsvPointsAsync(session); break;
                }
            }
            catch (Exception ex)
            {
                App.WriteStartupFailure($"GisExchange:{command.Name}", ex);
                SetSessionStatus(session, string.Format(GisText("FailedFormat"), command.Name, ex.Message));
                if (session.CommandSession.IsActive) session.CommandSession.Cancel();
            }
            finally
            {
                _gisExchangeRunningSessions.Remove(session);
                UpdateSessionUi(session);
            }
        });
    }

    private static bool IsGisCommand(string name) => name is
        "GEOJSONIMPORT" or "GEOJSONEXPORT" or "CSVPOINTIMPORT" or "CSVPOINTEXPORT";

    private async Task ImportGeoJsonAsync(CadWorkspaceSession? commandSession = null)
    {
        var picker = new FileOpenPicker
        {
            ViewMode = PickerViewMode.List,
            SuggestedStartLocation = PickerLocationId.DocumentsLibrary
        };
        picker.FileTypeFilter.Add(".geojson");
        picker.FileTypeFilter.Add(".json");
        InitializePicker(picker);
        var file = await picker.PickSingleFileAsync();
        if (file is null)
        {
            if (commandSession?.CommandSession.IsActive == true) commandSession.CommandSession.Cancel();
            return;
        }

        var text = await File.ReadAllTextAsync(file.Path);
        var imported = CadGeoJsonCodec.Import(text);
        var built = CadGisDocumentBuilder.FromGeoJson(imported);
        var created = CreateWorkspaceForFile(
            built.Document,
            Path.GetFileNameWithoutExtension(file.Name) + " · GeoJSON",
            nativeFilePath: null);
        SetSessionStatus(created, string.Format(GisText("ImportedFormat"), built.Document.Entities.Count, Path.GetFileName(file.Path)));
        if (built.Warnings.Count > 0) await ShowGisWarningsAsync(built.Warnings);
        if (commandSession?.CommandSession.IsActive == true) commandSession.CommandSession.Complete();
    }

    private async Task ExportGeoJsonAsync(CadWorkspaceSession? session = null)
    {
        session ??= ActiveSession;
        if (session is null) return;
        var picker = new FileSavePicker
        {
            SuggestedFileName = Path.GetFileNameWithoutExtension(session.DisplayName) + "-gis"
        };
        WinRT.Interop.InitializeWithWindow.Initialize(picker, WinRT.Interop.WindowNative.GetWindowHandle(this));
        picker.FileTypeChoices.Add("GeoJSON", [".geojson"]);
        var file = await picker.PickSaveFileAsync();
        if (file is null)
        {
            if (session.CommandSession.IsActive && session.CommandSession.ActiveCommand?.Name == "GEOJSONEXPORT") session.CommandSession.Cancel();
            return;
        }

        var result = CadGeoJsonCodec.Export(session.Document);
        await File.WriteAllTextAsync(file.Path, result.Json);
        if (result.Warnings.Count > 0) await ShowGisWarningsAsync(result.Warnings);
        if (session.CommandSession.IsActive && session.CommandSession.ActiveCommand?.Name == "GEOJSONEXPORT") session.CommandSession.Complete();
        SetSessionStatus(session, string.Format(GisText("ExportedFormat"), file.Path));
    }

    private async Task ImportCsvPointsAsync(CadWorkspaceSession? commandSession = null)
    {
        var picker = new FileOpenPicker
        {
            ViewMode = PickerViewMode.List,
            SuggestedStartLocation = PickerLocationId.DocumentsLibrary
        };
        picker.FileTypeFilter.Add(".csv");
        InitializePicker(picker);
        var file = await picker.PickSingleFileAsync();
        if (file is null)
        {
            if (commandSession?.CommandSession.IsActive == true) commandSession.CommandSession.Cancel();
            return;
        }

        var text = await File.ReadAllTextAsync(file.Path);
        var imported = CadCsvPointCodec.Import(text);
        var built = CadGisDocumentBuilder.FromCsvPoints(imported);
        var created = CreateWorkspaceForFile(
            built.Document,
            Path.GetFileNameWithoutExtension(file.Name) + " · CSV",
            nativeFilePath: null);
        SetSessionStatus(created, string.Format(GisText("ImportedFormat"), built.Document.Entities.Count, Path.GetFileName(file.Path)));
        if (built.Warnings.Count > 0) await ShowGisWarningsAsync(built.Warnings);
        if (commandSession?.CommandSession.IsActive == true) commandSession.CommandSession.Complete();
    }

    private async Task ExportCsvPointsAsync(CadWorkspaceSession? session = null)
    {
        session ??= ActiveSession;
        if (session is null) return;
        var pointEntities = session.Document.VisibleEntities.OfType<PointEntity>().ToArray();
        if (pointEntities.Length == 0)
            throw new InvalidOperationException(GisText("NoPoints"));
        var records = pointEntities.Select(point => new CadCsvPointRecord(
            point,
            Name: null,
            SuggestedLayerName: session.Document.GetEntityProperties(point.Id).LayerName,
            Properties: new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase))).ToArray();
        var csv = CadCsvPointCodec.Export(records);

        var picker = new FileSavePicker
        {
            SuggestedFileName = Path.GetFileNameWithoutExtension(session.DisplayName) + "-points"
        };
        WinRT.Interop.InitializeWithWindow.Initialize(picker, WinRT.Interop.WindowNative.GetWindowHandle(this));
        picker.FileTypeChoices.Add("CSV", [".csv"]);
        var file = await picker.PickSaveFileAsync();
        if (file is null)
        {
            if (session.CommandSession.IsActive && session.CommandSession.ActiveCommand?.Name == "CSVPOINTEXPORT") session.CommandSession.Cancel();
            return;
        }
        await File.WriteAllTextAsync(file.Path, csv, Encoding.UTF8);
        if (session.CommandSession.IsActive && session.CommandSession.ActiveCommand?.Name == "CSVPOINTEXPORT") session.CommandSession.Complete();
        SetSessionStatus(session, string.Format(GisText("ExportedFormat"), file.Path));
    }

    private async Task ShowGisWarningsAsync(IReadOnlyList<string> warnings)
    {
        var text = string.Join(Environment.NewLine, warnings.Take(16));
        if (warnings.Count > 16) text += Environment.NewLine + "…";
        await new ContentDialog
        {
            XamlRoot = RootLayout.XamlRoot,
            Title = GisText("Warnings"),
            Content = new ScrollViewer
            {
                MaxHeight = 400,
                Content = new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap }
            },
            CloseButtonText = GisText("Close")
        }.ShowAsync();
    }

    private static string GisText(string key)
    {
        var language = Services.LocalizationService.Current.CurrentLanguageTag;
        var ja = language.StartsWith("ja", StringComparison.OrdinalIgnoreCase);
        var en = language.StartsWith("en", StringComparison.OrdinalIgnoreCase);
        return key switch
        {
            "Menu" => ja ? "GIS 交換" : en ? "GIS Exchange" : "GIS 交换",
            "ImportGeoJson" => ja ? "GeoJSON を読み込み" : en ? "Import GeoJSON" : "导入 GeoJSON",
            "ExportGeoJson" => ja ? "GeoJSON を書き出し" : en ? "Export GeoJSON" : "导出 GeoJSON",
            "ImportCsvPoints" => ja ? "CSV 点群を読み込み" : en ? "Import CSV Points" : "导入 CSV 点",
            "ExportCsvPoints" => ja ? "CSV 点群を書き出し" : en ? "Export CSV Points" : "导出 CSV 点",
            "ImportedFormat" => ja ? "{0} 個の要素を {1} から読み込みました。" : en ? "Imported {0} entities from {1}." : "已从 {1} 导入 {0} 个图元。",
            "ExportedFormat" => ja ? "GIS データを書き出しました: {0}" : en ? "GIS data exported: {0}" : "GIS 数据已导出：{0}",
            "NoPoints" => ja ? "表示中の点要素がありません。" : en ? "The drawing has no visible point entities to export." : "当前图纸没有可导出的可见点图元。",
            "Warnings" => ja ? "GIS 交換の警告" : en ? "GIS exchange warnings" : "GIS 交换警告",
            "Close" => ja ? "閉じる" : en ? "Close" : "关闭",
            "FailedFormat" => ja ? "{0} に失敗しました: {1}" : en ? "{0} failed: {1}" : "{0} 失败：{1}",
            _ => key
        };
    }
}

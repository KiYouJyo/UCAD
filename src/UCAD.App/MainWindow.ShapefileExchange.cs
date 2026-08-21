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
    private static readonly CadDbfFieldDefinition[] ShapefileCadFields =
    [
        new("UCAD_ID", CadDbfFieldType.Character, 36),
        new("LAYER", CadDbfFieldType.Character, 32),
        new("COLOR", CadDbfFieldType.Character, 12),
        new("LWEIGHT", CadDbfFieldType.Numeric, 10, 2),
        new("LTYPE", CadDbfFieldType.Character, 16)
    ];

    private readonly HashSet<CadWorkspaceSession> _shapefileSubscribedSessions = [];
    private readonly HashSet<CadWorkspaceSession> _shapefileRunningSessions = [];
    private bool _shapefileUiInitialized;

    internal void EnsureShapefileExchangeUiInitialized()
    {
        if (_shapefileUiInitialized) return;
        _shapefileUiInitialized = true;
        RegisterShapefileCommand("SHAPEFILEIMPORT", "SHPI");
        RegisterShapefileCommand("SHAPEFILEEXPORT", "SHPE");
        RefreshCommandSearchSource();
        RootLayout.Loaded += Shapefile_RootLoaded;
        DocumentTabs.SelectionChanged += Shapefile_DocumentTabsSelectionChanged;
    }

    private void RegisterShapefileCommand(string name, params string[] aliases)
    {
        if (_commandRegistry.TryResolve(name, out _)) return;
        _commandRegistry.Register(new CadCommandDefinition(name, CadCommandCategory.Edit, aliases));
    }

    private void Shapefile_RootLoaded(object sender, RoutedEventArgs e)
    {
        RootLayout.Loaded -= Shapefile_RootLoaded;
        if (_gisExchangeMenu is not null)
        {
            _gisExchangeMenu.Items.Add(new MenuFlyoutSeparator());
            _gisExchangeMenu.Items.Add(CreateGisMenuItem("ImportShapefile", async () => await ImportShapefileAsync()));
            _gisExchangeMenu.Items.Add(CreateGisMenuItem("ExportShapefile", async () => await ExportShapefileAsync()));
        }
        if (ActiveSession is CadWorkspaceSession session) EnsureShapefileSessionSubscribed(session);
    }

    private void Shapefile_DocumentTabsSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ActiveSession is CadWorkspaceSession session) EnsureShapefileSessionSubscribed(session);
    }

    private void EnsureShapefileSessionSubscribed(CadWorkspaceSession session)
    {
        if (!_shapefileSubscribedSessions.Add(session)) return;
        session.CommandSession.Changed += (_, _) => Shapefile_CommandSessionChanged(session);
    }

    private void Shapefile_CommandSessionChanged(CadWorkspaceSession session)
    {
        var command = session.CommandSession.ActiveCommand;
        if (command is null || !IsShapefileCommand(command.Name) || !_shapefileRunningSessions.Add(session)) return;
        RootLayout.DispatcherQueue.TryEnqueue(async () =>
        {
            try
            {
                if (command.Name == "SHAPEFILEIMPORT") await ImportShapefileAsync(session);
                else await ExportShapefileAsync(session);
            }
            catch (Exception ex)
            {
                App.WriteStartupFailure($"ShapefileExchange:{command.Name}", ex);
                SetSessionStatus(session, string.Format(ShapefileText("FailedFormat"), command.Name, ex.Message));
                if (session.CommandSession.IsActive) session.CommandSession.Cancel();
            }
            finally
            {
                _shapefileRunningSessions.Remove(session);
                UpdateSessionUi(session);
            }
        });
    }

    private static bool IsShapefileCommand(string name) => name is "SHAPEFILEIMPORT" or "SHAPEFILEEXPORT";

    private async Task ImportShapefileAsync(CadWorkspaceSession? commandSession = null)
    {
        var picker = new FileOpenPicker
        {
            ViewMode = PickerViewMode.List,
            SuggestedStartLocation = PickerLocationId.DocumentsLibrary
        };
        picker.FileTypeFilter.Add(".shp");
        InitializePicker(picker);
        var file = await picker.PickSingleFileAsync();
        if (file is null)
        {
            if (commandSession?.CommandSession.IsActive == true) commandSession.CommandSession.Cancel();
            return;
        }

        var basePath = Path.Combine(Path.GetDirectoryName(file.Path)!, Path.GetFileNameWithoutExtension(file.Path));
        var shp = await File.ReadAllBytesAsync(file.Path);
        var shx = await ReadOptionalBytesAsync(basePath + ".shx");
        var dbf = await ReadOptionalBytesAsync(basePath + ".dbf");
        var cpg = await ReadOptionalBytesAsync(basePath + ".cpg");
        var prj = await ReadOptionalBytesAsync(basePath + ".prj");
        var imported = CadShapefilePackage.Import(shp, shx, dbf, cpg, prj);
        var built = CadShapefileDocumentBuilder.Build(imported);
        var created = CreateWorkspaceForFile(
            built.Document,
            Path.GetFileNameWithoutExtension(file.Name) + " · SHP",
            null);
        var crs = imported.Bundle.IdentifiedCrs?.ToString() ?? ShapefileText("UnknownCrs");
        SetSessionStatus(
            created,
            string.Format(ShapefileText("ImportedFormat"), built.Document.Entities.Count, Path.GetFileName(file.Path), crs));
        if (built.Warnings.Count > 0) await ShowGisWarningsAsync(built.Warnings);
        if (commandSession?.CommandSession.IsActive == true) commandSession.CommandSession.Complete();
    }

    private async Task ExportShapefileAsync(CadWorkspaceSession? session = null)
    {
        session ??= ActiveSession;
        if (session is null) return;
        var selected = session.Interaction.Selection.SelectedEntities;
        var entities = (selected.Count > 0 ? selected : session.Document.VisibleEntities).ToArray();
        if (entities.Length == 0) throw new InvalidOperationException(ShapefileText("NoGeometry"));

        var crs = await PromptShapefileCrsAsync();
        if (crs is null)
        {
            if (session.CommandSession.IsActive && session.CommandSession.ActiveCommand?.Name == "SHAPEFILEEXPORT") session.CommandSession.Cancel();
            return;
        }

        var features = entities.Select(entity => ToShapefileFeature(session, entity)).ToArray();
        var package = CadShapefilePackage.Export(features, ShapefileCadFields, crs);

        var picker = new FileSavePicker
        {
            SuggestedFileName = Path.GetFileNameWithoutExtension(session.DisplayName) + "-gis"
        };
        WinRT.Interop.InitializeWithWindow.Initialize(picker, WinRT.Interop.WindowNative.GetWindowHandle(this));
        picker.FileTypeChoices.Add("ESRI Shapefile", [".shp"]);
        var file = await picker.PickSaveFileAsync();
        if (file is null)
        {
            if (session.CommandSession.IsActive && session.CommandSession.ActiveCommand?.Name == "SHAPEFILEEXPORT") session.CommandSession.Cancel();
            return;
        }

        var basePath = Path.Combine(Path.GetDirectoryName(file.Path)!, Path.GetFileNameWithoutExtension(file.Path));
        await WriteAtomicBytesAsync(file.Path, package.ShpContent);
        await WriteAtomicBytesAsync(basePath + ".shx", package.ShxContent);
        await WriteAtomicBytesAsync(basePath + ".dbf", package.DbfContent);
        await WriteAtomicBytesAsync(basePath + ".cpg", package.CpgContent);
        if (package.PrjContent is not null) await WriteAtomicBytesAsync(basePath + ".prj", package.PrjContent);
        else if (File.Exists(basePath + ".prj")) File.Delete(basePath + ".prj");

        if (package.Warnings.Count > 0) await ShowGisWarningsAsync(package.Warnings);
        if (session.CommandSession.IsActive && session.CommandSession.ActiveCommand?.Name == "SHAPEFILEEXPORT") session.CommandSession.Complete();
        SetSessionStatus(session, string.Format(ShapefileText("ExportedFormat"), file.Path, package.ShapeType));
    }

    private static CadShapefileFeature ToShapefileFeature(CadWorkspaceSession session, ICadEntity entity)
    {
        var properties = session.Document.GetEntityProperties(entity.Id);
        var values = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["UCAD_ID"] = entity.Id.ToString("D"),
            ["LAYER"] = properties.LayerName,
            ["COLOR"] = properties.ColorHex ?? "ByLayer",
            ["LWEIGHT"] = properties.LineWeight?.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture),
            ["LTYPE"] = properties.LineType
        };
        return new CadShapefileFeature(entity, values);
    }

    private async Task<CadCoordinateReferenceSystem?> PromptShapefileCrsAsync()
    {
        var selector = new ComboBox
        {
            Header = ShapefileText("Crs"),
            ItemsSource = new[]
            {
                ShapefileText("LocalPlanar"),
                "WGS84 (EPSG:4326)",
                "Web Mercator (EPSG:3857)"
            },
            SelectedIndex = 0,
            MinWidth = 340
        };
        var dialog = new ContentDialog
        {
            XamlRoot = RootLayout.XamlRoot,
            Title = ShapefileText("CrsTitle"),
            Content = selector,
            PrimaryButtonText = ShapefileText("Continue"),
            CloseButtonText = ShapefileText("Cancel")
        };
        if (await dialog.ShowAsync() != ContentDialogResult.Primary) return null;
        return selector.SelectedIndex switch
        {
            1 => CadCoordinateReferenceSystem.Wgs84LongitudeLatitude,
            2 => CadCoordinateReferenceSystem.WebMercator,
            _ => CadCoordinateReferenceSystem.LocalPlanar
        };
    }

    private static async Task<byte[]> ReadOptionalBytesAsync(string path) =>
        File.Exists(path) ? await File.ReadAllBytesAsync(path) : [];

    private static async Task WriteAtomicBytesAsync(string path, byte[] content)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory)) Directory.CreateDirectory(directory);
        var temp = path + ".tmp";
        try
        {
            await File.WriteAllBytesAsync(temp, content);
            File.Move(temp, path, overwrite: true);
        }
        finally
        {
            if (File.Exists(temp)) File.Delete(temp);
        }
    }

    private static string ShapefileText(string key)
    {
        var language = Services.LocalizationService.Current.CurrentLanguageTag;
        var ja = language.StartsWith("ja", StringComparison.OrdinalIgnoreCase);
        var en = language.StartsWith("en", StringComparison.OrdinalIgnoreCase);
        return key switch
        {
            "ImportShapefile" => ja ? "Shapefile を読み込み" : en ? "Import Shapefile" : "导入 Shapefile",
            "ExportShapefile" => ja ? "Shapefile を書き出し" : en ? "Export Shapefile" : "导出 Shapefile",
            "NoGeometry" => ja ? "書き出せるジオメトリがありません。" : en ? "There is no geometry to export." : "没有可导出的几何。",
            "Crs" => ja ? "座標参照系" : en ? "Coordinate reference system" : "坐标参考系",
            "CrsTitle" => ja ? "Shapefile CRS" : en ? "Shapefile CRS" : "Shapefile 坐标系",
            "LocalPlanar" => ja ? "ローカル平面座標（PRJ なし）" : en ? "Local planar (no PRJ)" : "本地平面坐标（不生成 PRJ）",
            "Continue" => ja ? "続行" : en ? "Continue" : "继续",
            "Cancel" => ja ? "キャンセル" : en ? "Cancel" : "取消",
            "UnknownCrs" => ja ? "不明" : en ? "Unknown" : "未知",
            "ImportedFormat" => ja ? "{1} から {0} 個の要素を読み込みました。CRS: {2}" : en ? "Imported {0} entities from {1}. CRS: {2}" : "已从 {1} 导入 {0} 个图元。CRS：{2}",
            "ExportedFormat" => ja ? "Shapefile を保存しました: {0} ({1})" : en ? "Shapefile saved: {0} ({1})" : "Shapefile 已保存：{0}（{1}）",
            "FailedFormat" => ja ? "{0} に失敗しました: {1}" : en ? "{0} failed: {1}" : "{0} 失败：{1}",
            _ => key
        };
    }
}

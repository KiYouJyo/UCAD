using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using UCAD.Core;
using UCAD.Core.IO;
using UCAD.Models;
using UCAD.Services;
using UCAD.Workspace;
using Windows.Storage.Pickers;
using Windows.System;

namespace UCAD;

public sealed partial class MainWindow
{
    private readonly CadDocumentFileService _documentFileService = new();
    private bool _fileUiInitialized;
    private MenuFlyoutItem? _openDrawingFileItem;
    private MenuFlyoutItem? _saveDrawingFileItem;
    private MenuFlyoutItem? _saveAsDrawingFileItem;
    private MenuFlyoutItem? _importDxfFileItem;
    private MenuFlyoutItem? _exportDxfFileItem;

    internal void EnsureFileUiInitialized()
    {
        if (_fileUiInitialized) return;
        _fileUiInitialized = true;
        RootLayout.Loaded += FileIO_RootLoaded;
        DocumentTabs.SelectionChanged += FileIO_DocumentTabsSelectionChanged;
        _settingsService.SettingsChanged += FileIO_SettingsChanged;
    }

    private void FileIO_RootLoaded(object sender, RoutedEventArgs e)
    {
        RootLayout.Loaded -= FileIO_RootLoaded;
        ConfigureFileMenu();
        ConfigureStartPageFileAction();
    }

    private void FileIO_DocumentTabsSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        ConfigureStartPageFileAction();
        UpdateFileCommandAvailability();
    }

    private void FileIO_SettingsChanged(object? sender, EventArgs e)
    {
        RefreshFileMenuText();
    }

    private void ConfigureStartPageFileAction()
    {
        if (_startPage is not null)
        {
            _startPage.OpenDrawingAction = OpenDrawingFromPickerAsync;
        }
    }

    private void ConfigureFileMenu()
    {
        if (FileMenuButton.Flyout is not MenuFlyout menu) return;

        _openDrawingFileItem = CreateFileMenuItem("Open", OpenDrawingFileItem_Click, VirtualKey.O, VirtualKeyModifiers.Control);
        _saveDrawingFileItem = CreateFileMenuItem("Save", SaveDrawingFileItem_Click, VirtualKey.S, VirtualKeyModifiers.Control);
        _saveAsDrawingFileItem = CreateFileMenuItem("SaveAs", SaveAsDrawingFileItem_Click, VirtualKey.S, VirtualKeyModifiers.Control | VirtualKeyModifiers.Shift);
        _importDxfFileItem = CreateFileMenuItem("ImportDxf", ImportDxfFileItem_Click);
        _exportDxfFileItem = CreateFileMenuItem("ExportDxf", ExportDxfFileItem_Click);

        menu.Items.Clear();
        menu.Items.Add(NewDrawingMenuItem);
        menu.Items.Add(_openDrawingFileItem);
        menu.Items.Add(new MenuFlyoutSeparator());
        menu.Items.Add(_saveDrawingFileItem);
        menu.Items.Add(_saveAsDrawingFileItem);
        menu.Items.Add(new MenuFlyoutSeparator());
        menu.Items.Add(_importDxfFileItem);
        menu.Items.Add(_exportDxfFileItem);
        menu.Items.Add(new MenuFlyoutSeparator());
        menu.Items.Add(CloseDrawingMenuItem);

        RefreshFileMenuText();
        UpdateFileCommandAvailability();
    }

    private MenuFlyoutItem CreateFileMenuItem(
        string textKey,
        RoutedEventHandler click,
        VirtualKey? key = null,
        VirtualKeyModifiers modifiers = VirtualKeyModifiers.None)
    {
        var item = new MenuFlyoutItem { Text = FileText(textKey) };
        item.Click += click;
        if (key is VirtualKey acceleratorKey)
        {
            item.KeyboardAccelerators.Add(new KeyboardAccelerator
            {
                Key = acceleratorKey,
                Modifiers = modifiers
            });
        }
        return item;
    }

    private void RefreshFileMenuText()
    {
        if (_openDrawingFileItem is not null) _openDrawingFileItem.Text = FileText("Open");
        if (_saveDrawingFileItem is not null) _saveDrawingFileItem.Text = FileText("Save");
        if (_saveAsDrawingFileItem is not null) _saveAsDrawingFileItem.Text = FileText("SaveAs");
        if (_importDxfFileItem is not null) _importDxfFileItem.Text = FileText("ImportDxf");
        if (_exportDxfFileItem is not null) _exportDxfFileItem.Text = FileText("ExportDxf");
    }

    private void UpdateFileCommandAvailability()
    {
        var drawingActive = ActiveSession is not null;
        if (_saveDrawingFileItem is not null) _saveDrawingFileItem.IsEnabled = drawingActive;
        if (_saveAsDrawingFileItem is not null) _saveAsDrawingFileItem.IsEnabled = drawingActive;
        if (_exportDxfFileItem is not null) _exportDxfFileItem.IsEnabled = drawingActive;
    }

    private async void OpenDrawingFileItem_Click(object sender, RoutedEventArgs e) =>
        await OpenDrawingFromPickerAsync();

    private async void SaveDrawingFileItem_Click(object sender, RoutedEventArgs e)
    {
        if (ActiveSession is CadWorkspaceSession session) await SaveSessionAsync(session, saveAs: false);
    }

    private async void SaveAsDrawingFileItem_Click(object sender, RoutedEventArgs e)
    {
        if (ActiveSession is CadWorkspaceSession session) await SaveSessionAsync(session, saveAs: true);
    }

    private async void ImportDxfFileItem_Click(object sender, RoutedEventArgs e) =>
        await ImportDxfFromPickerAsync();

    private async void ExportDxfFileItem_Click(object sender, RoutedEventArgs e)
    {
        if (ActiveSession is CadWorkspaceSession session) await ExportDxfFromPickerAsync(session);
    }

    private async Task OpenDrawingFromPickerAsync()
    {
        var picker = new FileOpenPicker
        {
            ViewMode = PickerViewMode.List,
            SuggestedStartLocation = PickerLocationId.DocumentsLibrary
        };
        picker.FileTypeFilter.Add(CadNativeDocumentCodec.FileExtension);
        picker.FileTypeFilter.Add(".dxf");
        InitializePicker(picker);
        var file = await picker.PickSingleFileAsync();
        if (file is null) return;

        try
        {
            if (string.Equals(Path.GetExtension(file.Path), ".dxf", StringComparison.OrdinalIgnoreCase))
            {
                await OpenImportedDxfAsync(file.Path);
                return;
            }

            var document = await _documentFileService.OpenNativeAsync(file.Path);
            var session = CreateWorkspaceForFile(document, Path.GetFileName(file.Path), file.Path);
            SetSessionStatus(session, FileText("Opened"));
            await RecentFilesService.Current.RecordAsync(file.Path, _settingsService.Settings.RecentFileCount);
            RefreshStartRecentFiles();
        }
        catch (Exception ex)
        {
            App.WriteStartupFailure("OpenDrawing", ex);
            await ShowFileMessageAsync(FileText("OpenFailedTitle"), ex.Message);
        }
    }

    private async Task ImportDxfFromPickerAsync()
    {
        var picker = new FileOpenPicker
        {
            ViewMode = PickerViewMode.List,
            SuggestedStartLocation = PickerLocationId.DocumentsLibrary
        };
        picker.FileTypeFilter.Add(".dxf");
        InitializePicker(picker);
        var file = await picker.PickSingleFileAsync();
        if (file is null) return;

        try
        {
            await OpenImportedDxfAsync(file.Path);
        }
        catch (Exception ex)
        {
            App.WriteStartupFailure("ImportDxf", ex);
            await ShowFileMessageAsync(FileText("ImportFailedTitle"), ex.Message);
        }
    }

    private async Task OpenImportedDxfAsync(string filePath)
    {
        var import = await _documentFileService.OpenDxfAsync(filePath);
        var session = CreateWorkspaceForFile(import.Document, Path.GetFileName(filePath), nativeFilePath: null);
        SetSessionStatus(session, FileText("DxfImported"));
        await RecentFilesService.Current.RecordAsync(filePath, _settingsService.Settings.RecentFileCount);
        RefreshStartRecentFiles();
        if (import.HasWarnings) await ShowDxfWarningsAsync(FileText("ImportWarningsTitle"), import.Warnings);
    }

    private async Task<bool> SaveSessionAsync(CadWorkspaceSession session, bool saveAs)
    {
        var targetPath = !saveAs && session.HasFilePath ? session.FilePath : null;
        if (string.IsNullOrWhiteSpace(targetPath))
        {
            var picker = new FileSavePicker
            {
                SuggestedStartLocation = PickerLocationId.DocumentsLibrary,
                SuggestedFileName = Path.GetFileNameWithoutExtension(session.DisplayName)
            };
            picker.FileTypeChoices.Add(FileText("NativeDrawingType"), [CadNativeDocumentCodec.FileExtension]);
            picker.DefaultFileExtension = CadNativeDocumentCodec.FileExtension;
            InitializePicker(picker);
            var file = await picker.PickSaveFileAsync();
            if (file is null) return false;
            targetPath = file.Path;
        }

        try
        {
            await _documentFileService.SaveNativeAsync(
                targetPath,
                session.Document,
                _settingsService.Settings.BackupOnSave);
            session.MarkSaved(targetPath);
            var tab = FindTab(session);
            if (tab is not null) UpdateTabHeader(tab, session);
            SetSessionStatus(session, FileText("Saved"));
            await RecentFilesService.Current.RecordAsync(targetPath, _settingsService.Settings.RecentFileCount);
            RefreshStartRecentFiles();
            return true;
        }
        catch (Exception ex)
        {
            App.WriteStartupFailure("SaveDrawing", ex);
            await ShowFileMessageAsync(FileText("SaveFailedTitle"), ex.Message);
            return false;
        }
    }

    private async Task ExportDxfFromPickerAsync(CadWorkspaceSession session)
    {
        var picker = new FileSavePicker
        {
            SuggestedStartLocation = PickerLocationId.DocumentsLibrary,
            SuggestedFileName = Path.GetFileNameWithoutExtension(session.DisplayName)
        };
        picker.FileTypeChoices.Add(FileText("DxfDrawingType"), [".dxf"]);
        picker.DefaultFileExtension = ".dxf";
        InitializePicker(picker);
        var file = await picker.PickSaveFileAsync();
        if (file is null) return;

        try
        {
            var export = await _documentFileService.ExportDxfAsync(
                file.Path,
                session.Document,
                _settingsService.Settings.BackupOnSave);
            SetSessionStatus(session, FileText("DxfExported"));
            if (export.HasWarnings) await ShowDxfWarningsAsync(FileText("ExportWarningsTitle"), export.Warnings);
        }
        catch (Exception ex)
        {
            App.WriteStartupFailure("ExportDxf", ex);
            await ShowFileMessageAsync(FileText("ExportFailedTitle"), ex.Message);
        }
    }

    private CadWorkspaceSession CreateWorkspaceForFile(CadDocument document, string displayName, string? nativeFilePath)
    {
        var ordinal = _nextDocumentOrdinal++;
        var session = new CadWorkspaceSession(ordinal, displayName, _commandRegistry, document)
        {
            StatusText = GetString("Status_Ready")
        };
        if (!string.IsNullOrWhiteSpace(nativeFilePath)) session.MarkOpened(nativeFilePath);
        else session.UpdateDisplayName(displayName);
        session.Viewport.ApplySettings(_settingsService.Settings);

        var tab = new TabViewItem
        {
            Tag = session,
            Header = session.DisplayName,
            IsClosable = true,
            Width = DocumentTabWidth,
            Height = DocumentTabHeight
        };
        _sessions[tab] = session;
        _tabKinds[tab] = WorkspacePageKind.Drawing;

        session.Document.Changed += (_, _) =>
        {
            UpdateTabHeader(tab, session);
            if (ReferenceEquals(_activeSession, session)) UpdateSessionUi(session);
        };
        session.Viewport.PointerWorldPositionChanged += point =>
        {
            session.PointerWorldPosition = point;
            if (ReferenceEquals(_activeSession, session)) UpdateCoordinateText(session);
        };
        session.Viewport.DrawingPointAccepted += (kind, count, point) =>
        {
            session.CommandBasePoint = point;
            SetDrawingPrompt(session, kind, count);
        };
        session.Viewport.DrawingCommandCompleted += kind =>
        {
            if (session.CommandSession.ActiveCommand?.DrawingKind == kind) session.CommandSession.Complete();
            session.CommandBasePoint = null;
            SetSessionStatus(session, GetString("Status_Ready"));
            if (ReferenceEquals(_activeSession, session)) UpdateSessionUi(session);
        };
        session.Viewport.ZoomChanged += _ =>
        {
            if (ReferenceEquals(_activeSession, session)) UpdateZoomText(session);
        };

        DocumentTabs.TabItems.Add(tab);
        UpdateTabStripWidth();
        DocumentTabs.SelectedItem = tab;
        ActivateSession(session);
        EnsureSessionInteractionSubscribed(session);
        EnsureAuthoringSessionSubscribed(session);
        UpdateFileCommandAvailability();
        return session;
    }

    private TabViewItem? FindTab(CadWorkspaceSession session) =>
        _sessions.FirstOrDefault(pair => ReferenceEquals(pair.Value, session)).Key;

    private void InitializePicker(object picker)
    {
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
        WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);
    }

    private async Task ShowDxfWarningsAsync(string title, IReadOnlyList<string> warnings)
    {
        var visible = warnings.Take(8).ToArray();
        var content = string.Join(Environment.NewLine, visible.Select(warning => "• " + warning));
        if (warnings.Count > visible.Length)
            content += Environment.NewLine + string.Format(FileText("MoreWarningsFormat"), warnings.Count - visible.Length);
        await ShowFileMessageAsync(title, content);
    }

    private async Task ShowFileMessageAsync(string title, string content)
    {
        var dialog = new ContentDialog
        {
            XamlRoot = RootLayout.XamlRoot,
            Title = title,
            Content = content,
            CloseButtonText = FileText("Close")
        };
        await dialog.ShowAsync();
    }

    private string FileText(string key)
    {
        var language = LocalizationService.Current.CurrentLanguageTag;
        var ja = language.StartsWith("ja", StringComparison.OrdinalIgnoreCase);
        var en = language.StartsWith("en", StringComparison.OrdinalIgnoreCase);
        return key switch
        {
            "Open" => ja ? "開く…" : en ? "Open…" : "打开…",
            "Save" => ja ? "保存" : en ? "Save" : "保存",
            "SaveAs" => ja ? "名前を付けて保存…" : en ? "Save As…" : "另存为…",
            "ImportDxf" => ja ? "DXF を読み込み…" : en ? "Import DXF…" : "导入 DXF…",
            "ExportDxf" => ja ? "DXF を書き出し…" : en ? "Export DXF…" : "导出 DXF…",
            "NativeDrawingType" => ja ? "UCAD 図面" : en ? "UCAD Drawing" : "UCAD 图纸",
            "DxfDrawingType" => ja ? "DXF 図面" : en ? "DXF Drawing" : "DXF 图纸",
            "Opened" => ja ? "図面を開きました。" : en ? "Drawing opened." : "图纸已打开。",
            "Saved" => ja ? "図面を保存しました。" : en ? "Drawing saved." : "图纸已保存。",
            "DxfImported" => ja ? "DXF を読み込みました。編集内容を保持するには UCAD 形式で保存してください。" : en ? "DXF imported. Save as UCAD to preserve the full authoring model." : "DXF 已导入。请另存为 UCAD 格式以完整保留编辑模型。",
            "DxfExported" => ja ? "DXF を書き出しました。" : en ? "DXF exported." : "DXF 已导出。",
            "OpenFailedTitle" => ja ? "図面を開けません" : en ? "Couldn’t open drawing" : "无法打开图纸",
            "SaveFailedTitle" => ja ? "図面を保存できません" : en ? "Couldn’t save drawing" : "无法保存图纸",
            "ImportFailedTitle" => ja ? "DXF を読み込めません" : en ? "Couldn’t import DXF" : "无法导入 DXF",
            "ExportFailedTitle" => ja ? "DXF を書き出せません" : en ? "Couldn’t export DXF" : "无法导出 DXF",
            "ImportWarningsTitle" => ja ? "DXF 読み込みの警告" : en ? "DXF import warnings" : "DXF 导入警告",
            "ExportWarningsTitle" => ja ? "DXF 書き出しの警告" : en ? "DXF export warnings" : "DXF 导出警告",
            "MoreWarningsFormat" => ja ? "ほか {0} 件" : en ? "…and {0} more" : "另有 {0} 条警告",
            "Close" => ja ? "閉じる" : en ? "Close" : "关闭",
            _ => key
        };
    }
}

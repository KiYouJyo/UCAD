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
        StartDeferredFileActivation();
    }

    private void FileIO_DocumentTabsSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (DocumentTabs.SelectedItem is not TabViewItem selected || !_sessions.TryGetValue(selected, out var session)) return;
        UpdateFileMenuState(session);
    }

    private void FileIO_SettingsChanged(object? sender, AppSettings settings)
    {
        foreach (var session in _sessions.Values) session.Viewport.ApplySettings(settings);
    }

    private void ConfigureFileMenu()
    {
        _openDrawingFileItem = CreateFileMenuItem("OpenDrawing", async (_, _) => await OpenDrawingAsync());
        _saveDrawingFileItem = CreateFileMenuItem("SaveDrawing", async (_, _) => await SaveDrawingAsync());
        _saveAsDrawingFileItem = CreateFileMenuItem("SaveAsDrawing", async (_, _) => await SaveDrawingAsAsync());
        _importDxfFileItem = CreateFileMenuItem("ImportDxf", async (_, _) => await ImportDxfAsync());
        _exportDxfFileItem = CreateFileMenuItem("ExportDxf", async (_, _) => await ExportDxfAsync());
        FileMenu.Items.Insert(0, _openDrawingFileItem);
        FileMenu.Items.Insert(1, _saveDrawingFileItem);
        FileMenu.Items.Insert(2, _saveAsDrawingFileItem);
        FileMenu.Items.Insert(3, new MenuFlyoutSeparator());
        FileMenu.Items.Insert(4, _importDxfFileItem);
        FileMenu.Items.Insert(5, _exportDxfFileItem);
        FileMenu.Items.Insert(6, new MenuFlyoutSeparator());
        UpdateFileMenuState(ActiveSession);
    }

    private MenuFlyoutItem CreateFileMenuItem(string resourceKey, RoutedEventHandler click)
    {
        var item = new MenuFlyoutItem { Text = FileText(resourceKey) };
        item.Click += click;
        return item;
    }

    private void UpdateFileMenuState(CadWorkspaceSession? session)
    {
        var enabled = session is not null;
        if (_saveDrawingFileItem is not null) _saveDrawingFileItem.IsEnabled = enabled;
        if (_saveAsDrawingFileItem is not null) _saveAsDrawingFileItem.IsEnabled = enabled;
        if (_exportDxfFileItem is not null) _exportDxfFileItem.IsEnabled = enabled;
    }

    private async Task OpenDrawingAsync()
    {
        var picker = new FileOpenPicker
        {
            ViewMode = PickerViewMode.List,
            SuggestedStartLocation = PickerLocationId.DocumentsLibrary
        };
        picker.FileTypeFilter.Add(".ucad");
        picker.FileTypeFilter.Add(".dxf");
        InitializePicker(picker);
        var file = await picker.PickSingleFileAsync();
        if (file is null) return;
        await OpenDrawingPathAsync(file.Path);
    }

    private async Task OpenDrawingPathAsync(string path)
    {
        try
        {
            var extension = Path.GetExtension(path);
            if (string.Equals(extension, ".dxf", StringComparison.OrdinalIgnoreCase))
            {
                var imported = await _documentFileService.ImportDxfAsync(path);
                var session = CreateWorkspaceForFile(imported.Document, Path.GetFileName(path), nativeFilePath: null);
                SetSessionStatus(session, FileText("DxfImported"));
                if (imported.HasWarnings) await ShowDxfWarningsAsync(FileText("ImportWarningsTitle"), imported.Warnings);
                return;
            }

            var document = await _documentFileService.LoadNativeAsync(path);
            var opened = CreateWorkspaceForFile(document, Path.GetFileName(path), path);
            _recentFilesService.Record(path);
            RefreshRecentFiles();
            SetSessionStatus(opened, FileText("Opened"));
        }
        catch (Exception ex)
        {
            App.WriteStartupFailure("OpenDrawing", ex);
            await ShowFileMessageAsync(FileText("OpenFailedTitle"), ex.Message);
        }
    }

    private async Task SaveDrawingAsync()
    {
        var session = ActiveSession;
        if (session is null) return;
        if (string.IsNullOrWhiteSpace(session.NativeFilePath))
        {
            await SaveDrawingAsAsync();
            return;
        }

        await SaveSessionToPathAsync(session, session.NativeFilePath);
    }

    private async Task SaveDrawingAsAsync()
    {
        var session = ActiveSession;
        if (session is null) return;
        var picker = new FileSavePicker
        {
            SuggestedStartLocation = PickerLocationId.DocumentsLibrary,
            SuggestedFileName = Path.GetFileNameWithoutExtension(session.DisplayName)
        };
        picker.FileTypeChoices.Add(FileText("UcadDrawingType"), [".ucad"]);
        picker.DefaultFileExtension = ".ucad";
        InitializePicker(picker);
        var file = await picker.PickSaveFileAsync();
        if (file is null) return;
        await SaveSessionToPathAsync(session, file.Path);
    }

    private async Task SaveSessionToPathAsync(CadWorkspaceSession session, string path)
    {
        try
        {
            await _documentFileService.SaveNativeAsync(path, session.Document, _settingsService.Settings.BackupOnSave);
            session.MarkSaved(path);
            _recentFilesService.Record(path);
            RefreshRecentFiles();
            UpdateTabHeaderForSession(session);
            SetSessionStatus(session, FileText("Saved"));
        }
        catch (Exception ex)
        {
            App.WriteStartupFailure("SaveDrawing", ex);
            await ShowFileMessageAsync(FileText("SaveFailedTitle"), ex.Message);
        }
    }

    private async Task ImportDxfAsync()
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
            var imported = await _documentFileService.ImportDxfAsync(file.Path);
            var session = CreateWorkspaceForFile(imported.Document, Path.GetFileName(file.Path), nativeFilePath: null);
            SetSessionStatus(session, FileText("DxfImported"));
            if (imported.HasWarnings) await ShowDxfWarningsAsync(FileText("ImportWarningsTitle"), imported.Warnings);
        }
        catch (Exception ex)
        {
            App.WriteStartupFailure("ImportDxf", ex);
            await ShowFileMessageAsync(FileText("ImportFailedTitle"), ex.Message);
        }
    }

    private async Task ExportDxfAsync()
    {
        var session = ActiveSession;
        if (session is null) return;
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
        return session;
    }

    private void UpdateTabHeaderForSession(CadWorkspaceSession session)
    {
        var pair = _sessions.FirstOrDefault(item => ReferenceEquals(item.Value, session));
        if (pair.Key is not null) UpdateTabHeader(pair.Key, session);
    }

    private void InitializePicker(object picker)
    {
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
        WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);
    }

    private async Task ShowDxfWarningsAsync(string title, IReadOnlyList<string> warnings)
    {
        if (warnings.Count == 0) return;
        await ShowFileMessageAsync(title, string.Join(Environment.NewLine, warnings));
    }

    private async Task ShowFileMessageAsync(string title, string message)
    {
        var dialog = new ContentDialog
        {
            XamlRoot = RootLayout.XamlRoot,
            Title = title,
            Content = message,
            CloseButtonText = FileText("Close")
        };
        await dialog.ShowAsync();
    }

    private static string FileText(string key) => key switch
    {
        "OpenDrawing" => "Open drawing…",
        "SaveDrawing" => "Save",
        "SaveAsDrawing" => "Save as…",
        "ImportDxf" => "Import DXF…",
        "ExportDxf" => "Export DXF…",
        "UcadDrawingType" => "UCAD drawing",
        "DxfDrawingType" => "DXF drawing",
        "DxfImported" => "DXF imported",
        "DxfExported" => "DXF exported",
        "Opened" => "Drawing opened",
        "Saved" => "Drawing saved",
        "ImportWarningsTitle" => "DXF import warnings",
        "ExportWarningsTitle" => "DXF export warnings",
        "OpenFailedTitle" => "Open failed",
        "SaveFailedTitle" => "Save failed",
        "ImportFailedTitle" => "DXF import failed",
        "ExportFailedTitle" => "DXF export failed",
        "Close" => "Close",
        _ => key
    };
}

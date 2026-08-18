using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using UCAD.Core.IO;
using UCAD.Services;
using UCAD.Views;

namespace UCAD;

public sealed partial class MainWindow
{
    private bool _recentFilesUiInitialized;
    private StartPage? _recentFilesBoundPage;

    internal void EnsureRecentFilesInitialized()
    {
        if (_recentFilesUiInitialized) return;
        _recentFilesUiInitialized = true;
        RootLayout.Loaded += RecentFiles_RootLoaded;
        DocumentTabs.SelectionChanged += RecentFiles_DocumentTabsSelectionChanged;
        _settingsService.SettingsChanged += RecentFiles_SettingsChanged;
    }

    private void RecentFiles_RootLoaded(object sender, RoutedEventArgs e)
    {
        RootLayout.Loaded -= RecentFiles_RootLoaded;
        BindRecentStartPage();
        RefreshStartRecentFiles();
    }

    private void RecentFiles_DocumentTabsSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        BindRecentStartPage();
        RefreshStartRecentFiles();
    }

    private void RecentFiles_SettingsChanged(object? sender, EventArgs e) => RefreshStartRecentFiles();

    private void BindRecentStartPage()
    {
        if (ReferenceEquals(_recentFilesBoundPage, _startPage)) return;
        if (_recentFilesBoundPage is not null)
            _recentFilesBoundPage.RecentFileRequested -= RecentStartPage_RecentFileRequested;
        _recentFilesBoundPage = _startPage;
        if (_recentFilesBoundPage is not null)
            _recentFilesBoundPage.RecentFileRequested += RecentStartPage_RecentFileRequested;
    }

    private async void RefreshStartRecentFiles()
    {
        BindRecentStartPage();
        if (_recentFilesBoundPage is null) return;
        if (!_settingsService.Settings.ShowRecentFiles)
        {
            _recentFilesBoundPage.SetRecentFiles([]);
            return;
        }

        try
        {
            var recent = await RecentFilesService.Current.GetAsync(_settingsService.Settings.RecentFileCount);
            if (ReferenceEquals(_recentFilesBoundPage, _startPage))
                _recentFilesBoundPage.SetRecentFiles(recent);
        }
        catch (Exception ex)
        {
            App.WriteStartupFailure("RecentFilesRefresh", ex);
            _recentFilesBoundPage.SetRecentFiles([]);
        }
    }

    private async void RecentStartPage_RecentFileRequested(object? sender, string path)
    {
        if (!File.Exists(path))
        {
            await RecentFilesService.Current.RemoveAsync(path);
            RefreshStartRecentFiles();
            await ShowFileMessageAsync(FileText("OpenFailedTitle"), FileText("RecentMissing"));
            return;
        }

        try
        {
            if (string.Equals(Path.GetExtension(path), ".dxf", StringComparison.OrdinalIgnoreCase))
            {
                await OpenImportedDxfAsync(path);
                return;
            }

            if (!string.Equals(Path.GetExtension(path), CadNativeDocumentCodec.FileExtension, StringComparison.OrdinalIgnoreCase))
            {
                await ShowFileMessageAsync(FileText("OpenFailedTitle"), FileText("UnsupportedRecentType"));
                return;
            }

            var document = await _documentFileService.OpenNativeAsync(path);
            var session = CreateWorkspaceForFile(document, Path.GetFileName(path), path);
            SetSessionStatus(session, FileText("Opened"));
            await RecentFilesService.Current.RecordAsync(path, _settingsService.Settings.RecentFileCount);
            RefreshStartRecentFiles();
        }
        catch (Exception ex)
        {
            App.WriteStartupFailure("OpenRecentDrawing", ex);
            await ShowFileMessageAsync(FileText("OpenFailedTitle"), ex.Message);
        }
    }
}

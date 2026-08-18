using Microsoft.UI.Xaml;
using Microsoft.Windows.AppLifecycle;
using UCAD.Core.IO;
using UCAD.Workspace;
using Windows.ApplicationModel.Activation;
using Windows.Storage;

namespace UCAD;

public sealed partial class MainWindow
{
    private bool _initialFileActivationScheduled;

    internal void HandleInitialFileActivation()
    {
        if (_initialFileActivationScheduled) return;
        _initialFileActivationScheduled = true;

        var activation = AppInstance.GetCurrent().GetActivatedEventArgs();
        if (activation.Kind != ExtendedActivationKind.File || activation.Data is not IFileActivatedEventArgs fileArgs)
            return;

        var paths = fileArgs.Files
            .OfType<StorageFile>()
            .Select(file => file.Path)
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (paths.Length == 0) return;

        if (RootLayout.IsLoaded)
        {
            RootLayout.DispatcherQueue.TryEnqueue(async () => await OpenActivatedPathsAsync(paths));
        }
        else
        {
            RoutedEventHandler? loaded = null;
            loaded = (_, _) =>
            {
                RootLayout.Loaded -= loaded;
                RootLayout.DispatcherQueue.TryEnqueue(async () => await OpenActivatedPathsAsync(paths));
            };
            RootLayout.Loaded += loaded;
        }
    }

    private async Task OpenActivatedPathsAsync(IReadOnlyList<string> paths)
    {
        foreach (var path in paths)
        {
            try
            {
                var extension = Path.GetExtension(path);
                if (string.Equals(extension, CadNativeDocumentCodec.FileExtension, StringComparison.OrdinalIgnoreCase))
                {
                    var document = await _documentFileService.OpenNativeAsync(path);
                    var session = CreateWorkspaceForFile(document, Path.GetFileName(path), path);
                    SetSessionStatus(session, FileText("Opened"));
                    await Services.RecentFilesService.Current.RecordAsync(path, _settingsService.Settings.RecentFileCount);
                    RefreshStartRecentFiles();
                    continue;
                }

                if (!CadAcadFileFormatRegistry.TryGetByPath(path, out var format) || !format.CanOpen || format.Family != CadFileFormatFamily.AutoCadDrawing)
                    continue;

                await OpenImportedAutoCadAsync(path);
            }
            catch (Exception ex)
            {
                App.WriteStartupFailure("FileActivation", ex);
                await ShowFileMessageAsync(FileText("OpenFailedTitle"), ex.Message);
            }
        }
    }
}

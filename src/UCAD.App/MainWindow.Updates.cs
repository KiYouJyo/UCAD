using System.Globalization;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using UCAD.Core.Updates;
using UCAD.Services;
using Windows.System;

namespace UCAD;

public sealed partial class MainWindow
{
    private readonly SemaphoreSlim _updateGate = new(1, 1);

    private string UpdateString(string key)
    {
        var value = LocalizationService.Current.GetStringFromMap(key, "UpdateLive");
        return string.IsNullOrWhiteSpace(value) ? key : value;
    }

    private async Task CheckForUpdatesAsync(bool showUpToDate)
    {
        if (!await _updateGate.WaitAsync(0))
        {
            return;
        }

        try
        {
            App.WriteStartupEvent($"Update check started: current={AppVersionInfo.Version}");
            var result = await GitHubUpdateService.Current.CheckForUpdatesAsync(AppVersionInfo.Version);
            App.WriteStartupEvent($"Update check completed: latest={result.Release.Version}; available={result.IsUpdateAvailable}");

            if (!result.IsUpdateAvailable)
            {
                if (showUpToDate)
                {
                    await ShowUpdateInformationAsync(
                        UpdateString("UpToDateTitle"),
                        string.Format(CultureInfo.CurrentCulture, UpdateString("UpToDateMessage"), AppVersionInfo.Version));
                }
                return;
            }

            await ShowAvailableUpdateAsync(result.Release);
        }
        catch (Exception ex)
        {
            App.WriteStartupFailure("GitHubUpdate", ex);
            if (showUpToDate)
            {
                await ShowUpdateErrorAsync(ex, releaseUri: null);
            }
        }
        finally
        {
            _updateGate.Release();
        }
    }

    private async Task ShowAvailableUpdateAsync(GitHubReleaseUpdateManifest release)
    {
        var version = $"{release.Version.Major}.{release.Version.Minor}.{release.Version.Build}";
        var content = new StackPanel
        {
            Spacing = 10,
            MaxWidth = 520
        };
        content.Children.Add(new TextBlock
        {
            Text = UpdateString("AvailableMessage"),
            TextWrapping = TextWrapping.Wrap
        });

        if (!string.IsNullOrWhiteSpace(release.Name))
        {
            content.Children.Add(new TextBlock
            {
                Text = release.Name,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                TextWrapping = TextWrapping.Wrap
            });
        }

        var dialog = new ContentDialog
        {
            XamlRoot = RootLayout.XamlRoot,
            Title = string.Format(CultureInfo.CurrentCulture, UpdateString("AvailableTitle"), version),
            Content = content,
            PrimaryButtonText = UpdateString("DownloadInstall"),
            SecondaryButtonText = UpdateString("ViewRelease"),
            CloseButtonText = UpdateString("Cancel"),
            DefaultButton = ContentDialogButton.Primary
        };

        var choice = await dialog.ShowAsync();
        if (choice == ContentDialogResult.Secondary)
        {
            await Launcher.LaunchUriAsync(release.HtmlUri);
            return;
        }
        if (choice != ContentDialogResult.Primary)
        {
            return;
        }

        await DownloadAndLaunchUpdateAsync(release);
    }

    private async Task DownloadAndLaunchUpdateAsync(GitHubReleaseUpdateManifest release)
    {
        var version = $"{release.Version.Major}.{release.Version.Minor}.{release.Version.Build}";
        var progressBar = new ProgressBar
        {
            Width = 430,
            Minimum = 0,
            Maximum = 100,
            IsIndeterminate = true
        };
        var status = new TextBlock
        {
            Text = UpdateString("Downloading"),
            TextWrapping = TextWrapping.Wrap,
            MaxWidth = 430
        };
        var content = new StackPanel
        {
            Spacing = 12
        };
        content.Children.Add(status);
        content.Children.Add(progressBar);

        var progressDialog = new ContentDialog
        {
            XamlRoot = RootLayout.XamlRoot,
            Title = string.Format(CultureInfo.CurrentCulture, UpdateString("DownloadingTitle"), version),
            Content = content
        };

        var showOperation = progressDialog.ShowAsync();
        await Task.Yield();

        try
        {
            var progress = new Progress<GitHubUpdateDownloadProgress>(value =>
            {
                RootLayout.DispatcherQueue.TryEnqueue(() =>
                {
                    if (value.Percent is double percent && value.TotalBytes is long total)
                    {
                        progressBar.IsIndeterminate = false;
                        progressBar.Value = percent;
                        status.Text = string.Format(
                            CultureInfo.CurrentCulture,
                            UpdateString("DownloadingPercent"),
                            percent,
                            ToMegabytes(value.BytesReceived),
                            ToMegabytes(total));
                    }
                    else
                    {
                        progressBar.IsIndeterminate = true;
                        status.Text = string.Format(
                            CultureInfo.CurrentCulture,
                            UpdateString("DownloadingBytes"),
                            ToMegabytes(value.BytesReceived));
                    }
                });
            });

            var path = await GitHubUpdateService.Current.DownloadUpdateAsync(release, progress);
            status.Text = UpdateString("Launching");
            progressBar.IsIndeterminate = true;
            progressDialog.Hide();
            await showOperation;

            var launched = await GitHubUpdateService.Current.LaunchInstallerAsync(path);
            if (!launched)
            {
                await ShowUpdateErrorMessageAsync(UpdateString("InstallerLaunchFailed"), release.HtmlUri);
            }
        }
        catch (Exception ex)
        {
            try
            {
                progressDialog.Hide();
                await showOperation;
            }
            catch
            {
                // Preserve the real downloader/verification exception.
            }
            App.WriteStartupFailure("GitHubUpdateDownload", ex);
            await ShowUpdateErrorAsync(ex, release.HtmlUri);
        }
    }

    private async Task ShowUpdateInformationAsync(string title, string message)
    {
        var dialog = new ContentDialog
        {
            XamlRoot = RootLayout.XamlRoot,
            Title = title,
            Content = new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap, MaxWidth = 500 },
            CloseButtonText = UpdateString("Close")
        };
        await dialog.ShowAsync();
    }

    private async Task ShowUpdateErrorAsync(Exception exception, Uri? releaseUri)
    {
        var message = $"{UpdateString("ErrorMessage")}\n\n{string.Format(CultureInfo.CurrentCulture, UpdateString("ErrorDetails"), exception.Message)}";
        await ShowUpdateErrorMessageAsync(message, releaseUri);
    }

    private async Task ShowUpdateErrorMessageAsync(string message, Uri? releaseUri)
    {
        var dialog = new ContentDialog
        {
            XamlRoot = RootLayout.XamlRoot,
            Title = UpdateString("ErrorTitle"),
            Content = new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap, MaxWidth = 520 },
            SecondaryButtonText = releaseUri is null ? string.Empty : UpdateString("ViewRelease"),
            CloseButtonText = UpdateString("Close")
        };
        var result = await dialog.ShowAsync();
        if (result == ContentDialogResult.Secondary && releaseUri is not null)
        {
            await Launcher.LaunchUriAsync(releaseUri);
        }
    }

    private static double ToMegabytes(long bytes) => bytes / 1024d / 1024d;
}

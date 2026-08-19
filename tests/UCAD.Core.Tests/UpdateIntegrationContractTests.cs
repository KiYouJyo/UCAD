using Xunit;

namespace UCAD.Core.Tests;

public sealed class UpdateIntegrationContractTests
{
    [Fact]
    public void AppUpdateServiceIsConnectedToSettingsAndPackagedNetworking()
    {
        var root = LocateRepositoryRoot();
        var service = File.ReadAllText(Path.Combine(root, "src", "UCAD.App", "Services", "GitHubUpdateService.cs"));
        var updateUi = File.ReadAllText(Path.Combine(root, "src", "UCAD.App", "MainWindow.Updates.cs"));
        var shell = File.ReadAllText(Path.Combine(root, "src", "UCAD.App", "MainWindow.xaml.cs"));
        var settingsLoaded = File.ReadAllText(Path.Combine(root, "src", "UCAD.App", "Views", "UcadSettingsPage.CadPointerLoaded.cs"));
        var manifest = File.ReadAllText(Path.Combine(root, "src", "UCAD.App", "Package.appxmanifest"));

        Assert.Contains("https://api.github.com/repos/KiYouJyo/UCAD/releases/latest", service, StringComparison.Ordinal);
        Assert.Contains("SHA256SUMS.txt", service, StringComparison.Ordinal);
        Assert.Contains("DownloadUpdateAsync", service, StringComparison.Ordinal);
        Assert.Contains("ComputeSha256Async", service, StringComparison.Ordinal);
        Assert.Contains("LaunchInstallerAsync", service, StringComparison.Ordinal);
        Assert.Contains("StorageFile.GetFileFromPathAsync", service, StringComparison.Ordinal);
        Assert.Contains("Launcher.LaunchFileAsync", service, StringComparison.Ordinal);
        Assert.Contains(".msixbundle", service, StringComparison.Ordinal);

        Assert.Contains("CheckForUpdatesAsync(showUpToDate: true)", shell, StringComparison.Ordinal);
        Assert.Contains("AutoCheckUpdates", shell, StringComparison.Ordinal);
        Assert.Contains("ShowAvailableUpdateAsync", updateUi, StringComparison.Ordinal);
        Assert.Contains("DownloadAndLaunchUpdateAsync", updateUi, StringComparison.Ordinal);
        Assert.Contains("UpdateLive", updateUi, StringComparison.Ordinal);
        Assert.Contains("EnsureUpdateControlEnabled", settingsLoaded, StringComparison.Ordinal);
        Assert.Contains("<Capability Name=\"internetClient\" />", manifest, StringComparison.Ordinal);
    }

    private static string LocateRepositoryRoot()
    {
        foreach (var seed in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
        {
            var current = new DirectoryInfo(seed);
            while (current is not null)
            {
                if (File.Exists(Path.Combine(current.FullName, "VERSION")) &&
                    Directory.Exists(Path.Combine(current.FullName, "src", "UCAD.App")))
                {
                    return current.FullName;
                }
                current = current.Parent;
            }
        }
        throw new DirectoryNotFoundException("Could not locate the UCAD repository root from the test host.");
    }
}

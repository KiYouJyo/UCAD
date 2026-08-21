using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using UCAD.Core.Updates;
using Windows.Storage;
using Windows.System;

namespace UCAD.Services;

public sealed record GitHubUpdateCheckResult(
    GitHubReleaseUpdateManifest Release,
    bool IsUpdateAvailable);

public readonly record struct GitHubUpdateDownloadProgress(long BytesReceived, long? TotalBytes)
{
    public double? Percent => TotalBytes is > 0
        ? Math.Clamp(BytesReceived * 100d / TotalBytes.Value, 0d, 100d)
        : null;
}

/// <summary>
/// GitHub-backed update transport for UCAD.  It checks the repository's latest stable
/// Release, downloads the x64 MSIX bundle into UCAD's local update cache, verifies the
/// published SHA256SUMS manifest, and finally delegates installation to Windows App
/// Installer.  No update is silently installed and no executable is launched before the
/// user explicitly accepts the update dialog.
/// </summary>
public sealed class GitHubUpdateService
{
    private const string LatestReleaseEndpoint = "https://api.github.com/repos/KiYouJyo/UCAD/releases/latest";
    private static readonly Uri LatestReleaseUri = new(LatestReleaseEndpoint);
    private readonly HttpClient _httpClient;

    public GitHubUpdateService(HttpClient? httpClient = null)
    {
        _httpClient = httpClient ?? CreateHttpClient();
    }

    public static GitHubUpdateService Current { get; } = new();

    public async Task<GitHubUpdateCheckResult> CheckForUpdatesAsync(
        string currentVersion,
        CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient.GetAsync(LatestReleaseUri, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        var release = GitHubReleaseUpdateManifest.Parse(json, "x64");
        return new GitHubUpdateCheckResult(release, release.IsNewerThan(currentVersion));
    }

    public async Task<string> DownloadUpdateAsync(
        GitHubReleaseUpdateManifest release,
        IProgress<GitHubUpdateDownloadProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(release);
        EnsureSafeFileName(release.Bundle.Name);

        var updateRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "UCAD",
            "Updates",
            $"v{release.Version.Major}.{release.Version.Minor}.{release.Version.Build}");
        Directory.CreateDirectory(updateRoot);

        var targetPath = Path.Combine(updateRoot, release.Bundle.Name);
        var partialPath = targetPath + ".partial";
        TryDelete(partialPath);

        try
        {
            using var response = await _httpClient.GetAsync(release.Bundle.DownloadUri, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            response.EnsureSuccessStatusCode();

            var responseLength = response.Content.Headers.ContentLength;
            var expectedLength = release.Bundle.Size > 0 ? release.Bundle.Size : responseLength;
            if (release.Bundle.Size > 0 && responseLength is > 0 && responseLength.Value != release.Bundle.Size)
            {
                throw new InvalidDataException(
                    $"GitHub asset length changed. Release metadata={release.Bundle.Size}; response={responseLength.Value}.");
            }

            await using (var input = await response.Content.ReadAsStreamAsync(cancellationToken))
            await using (var output = new FileStream(partialPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 1024 * 128, useAsync: true))
            {
                var buffer = new byte[1024 * 128];
                long received = 0;
                while (true)
                {
                    var read = await input.ReadAsync(buffer, cancellationToken);
                    if (read == 0) break;
                    await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
                    received += read;
                    progress?.Report(new GitHubUpdateDownloadProgress(received, expectedLength));
                }
                await output.FlushAsync(cancellationToken);
            }

            var actualLength = new FileInfo(partialPath).Length;
            if (release.Bundle.Size > 0 && actualLength != release.Bundle.Size)
            {
                throw new InvalidDataException(
                    $"Downloaded update length mismatch. Expected {release.Bundle.Size}; actual {actualLength}.");
            }

            if (release.Checksums is null)
            {
                throw new InvalidDataException("The GitHub Release does not contain SHA256SUMS.txt; UCAD will not install an unverified update.");
            }

            var expectedHash = await GetExpectedSha256Async(release.Checksums, release.Bundle.Name, cancellationToken);
            var actualHash = await ComputeSha256Async(partialPath, cancellationToken);
            if (!string.Equals(expectedHash, actualHash, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException($"Downloaded update SHA-256 mismatch for {release.Bundle.Name}.");
            }

            File.Move(partialPath, targetPath, overwrite: true);
            progress?.Report(new GitHubUpdateDownloadProgress(actualLength, actualLength));
            return targetPath;
        }
        catch
        {
            TryDelete(partialPath);
            throw;
        }
    }

    public async Task<bool> LaunchInstallerAsync(string bundlePath)
    {
        if (string.IsNullOrWhiteSpace(bundlePath)) throw new ArgumentException("Bundle path is empty.", nameof(bundlePath));
        if (!File.Exists(bundlePath)) throw new FileNotFoundException("Downloaded UCAD update was not found.", bundlePath);
        if (!bundlePath.EndsWith(".msixbundle", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("UCAD only launches verified .msixbundle update packages.");
        }

        var file = await StorageFile.GetFileFromPathAsync(bundlePath);
        var options = new LauncherOptions
        {
            DisplayApplicationPicker = false
        };
        return await Launcher.LaunchFileAsync(file, options);
    }

    private async Task<string> GetExpectedSha256Async(
        GitHubReleaseAsset checksums,
        string bundleName,
        CancellationToken cancellationToken)
    {
        using var response = await _httpClient.GetAsync(checksums.DownloadUri, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();
        var text = await response.Content.ReadAsStringAsync(cancellationToken);

        foreach (var rawLine in text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var separator = rawLine.IndexOf("  ", StringComparison.Ordinal);
            if (separator <= 0) continue;
            var hash = rawLine[..separator].Trim();
            var name = rawLine[(separator + 2)..].Trim();
            if (!string.Equals(name, bundleName, StringComparison.Ordinal)) continue;
            if (hash.Length != 64 || hash.Any(character => !Uri.IsHexDigit(character)))
            {
                throw new InvalidDataException($"Invalid SHA-256 value published for {bundleName}.");
            }
            return hash.ToLowerInvariant();
        }

        throw new InvalidDataException($"SHA256SUMS.txt does not contain {bundleName}.");
    }

    private static async Task<string> ComputeSha256Async(string path, CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 1024 * 128, useAsync: true);
        using var sha = SHA256.Create();
        var hash = await sha.ComputeHashAsync(stream, cancellationToken);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static HttpClient CreateHttpClient()
    {
        var client = new HttpClient
        {
            Timeout = TimeSpan.FromMinutes(10)
        };
        client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("UCAD", AppVersionInfo.Version));
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        client.DefaultRequestHeaders.TryAddWithoutValidation("X-GitHub-Api-Version", "2022-11-28");
        return client;
    }

    private static void EnsureSafeFileName(string name)
    {
        if (string.IsNullOrWhiteSpace(name) || !string.Equals(Path.GetFileName(name), name, StringComparison.Ordinal) ||
            name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            throw new InvalidDataException("GitHub Release returned an unsafe update asset name.");
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path)) File.Delete(path);
        }
        catch
        {
            // A stale partial file must never mask the real network/verification error.
        }
    }
}

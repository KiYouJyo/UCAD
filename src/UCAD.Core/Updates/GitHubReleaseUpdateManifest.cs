using System.Text.Json;

namespace UCAD.Core.Updates;

/// <summary>
/// A release asset that can be consumed by UCAD's update layer.
/// </summary>
public sealed record GitHubReleaseAsset(
    string Name,
    Uri DownloadUri,
    long Size,
    string ContentType);

/// <summary>
/// Normalized GitHub release metadata used by both the UI and downloader.  The parser
/// deliberately accepts only a stable, non-draft release with a semantic vX.Y.Z tag and
/// an architecture-matching MSIX bundle; this keeps the network boundary strict and
/// makes update decisions deterministic.
/// </summary>
public sealed record GitHubReleaseUpdateManifest(
    Version Version,
    string TagName,
    string Name,
    string Body,
    Uri HtmlUri,
    DateTimeOffset? PublishedAt,
    GitHubReleaseAsset Bundle,
    GitHubReleaseAsset? Checksums)
{
    public bool IsNewerThan(string currentVersion)
    {
        if (!TryParseVersion(currentVersion, out var current))
        {
            throw new FormatException($"Invalid current UCAD version: {currentVersion}");
        }
        return Version > current;
    }

    public static GitHubReleaseUpdateManifest Parse(string json, string architecture = "x64")
    {
        if (string.IsNullOrWhiteSpace(json)) throw new ArgumentException("Release JSON is empty.", nameof(json));
        if (string.IsNullOrWhiteSpace(architecture)) throw new ArgumentException("Architecture is empty.", nameof(architecture));

        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        if (GetBoolean(root, "draft")) throw new InvalidDataException("Draft GitHub releases cannot be used for updates.");
        if (GetBoolean(root, "prerelease")) throw new InvalidDataException("Prerelease GitHub releases cannot be used for stable updates.");

        var tag = GetRequiredString(root, "tag_name");
        if (!TryParseVersion(tag, out var version))
        {
            throw new InvalidDataException($"Release tag is not a supported semantic version: {tag}");
        }

        var html = new Uri(GetRequiredString(root, "html_url"), UriKind.Absolute);
        var name = GetOptionalString(root, "name") ?? tag;
        var body = GetOptionalString(root, "body") ?? string.Empty;
        var publishedAt = TryGetDateTimeOffset(root, "published_at");

        if (!root.TryGetProperty("assets", out var assetsElement) || assetsElement.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidDataException("GitHub release has no assets array.");
        }

        var assets = assetsElement.EnumerateArray().Select(ParseAsset).ToArray();
        var arch = architecture.Trim().ToLowerInvariant();
        var bundleCandidates = assets
            .Where(asset => asset.Name.EndsWith(".msixbundle", StringComparison.OrdinalIgnoreCase))
            .ToArray();
        var bundle = bundleCandidates.FirstOrDefault(asset =>
                asset.Name.Contains($"_{arch}", StringComparison.OrdinalIgnoreCase) ||
                asset.Name.Contains($"-{arch}", StringComparison.OrdinalIgnoreCase))
            ?? (bundleCandidates.Length == 1 ? bundleCandidates[0] : null)
            ?? throw new InvalidDataException($"Release does not contain an unambiguous {architecture} MSIX bundle.");

        var checksums = assets.FirstOrDefault(asset =>
            string.Equals(asset.Name, "SHA256SUMS.txt", StringComparison.OrdinalIgnoreCase));

        return new GitHubReleaseUpdateManifest(version, tag, name, body, html, publishedAt, bundle, checksums);
    }

    public static bool TryParseVersion(string? value, out Version version)
    {
        version = new Version(0, 0, 0);
        if (string.IsNullOrWhiteSpace(value)) return false;
        var normalized = value.Trim();
        if (normalized.StartsWith('v') || normalized.StartsWith('V')) normalized = normalized[1..];
        if (!Version.TryParse(normalized, out var parsed)) return false;
        if (parsed.Major < 0 || parsed.Minor < 0 || parsed.Build < 0) return false;
        version = new Version(parsed.Major, parsed.Minor, parsed.Build);
        return true;
    }

    private static GitHubReleaseAsset ParseAsset(JsonElement asset)
    {
        var name = GetRequiredString(asset, "name");
        var download = new Uri(GetRequiredString(asset, "browser_download_url"), UriKind.Absolute);
        var size = asset.TryGetProperty("size", out var sizeElement) && sizeElement.TryGetInt64(out var parsedSize)
            ? parsedSize
            : 0;
        var contentType = GetOptionalString(asset, "content_type") ?? "application/octet-stream";
        return new GitHubReleaseAsset(name, download, size, contentType);
    }

    private static string GetRequiredString(JsonElement element, string name) =>
        GetOptionalString(element, name) is { Length: > 0 } value
            ? value
            : throw new InvalidDataException($"GitHub release field '{name}' is missing.");

    private static string? GetOptionalString(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static bool GetBoolean(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind is JsonValueKind.True or JsonValueKind.False && value.GetBoolean();

    private static DateTimeOffset? TryGetDateTimeOffset(JsonElement element, string name) =>
        GetOptionalString(element, name) is { Length: > 0 } value && DateTimeOffset.TryParse(value, out var parsed)
            ? parsed
            : null;
}

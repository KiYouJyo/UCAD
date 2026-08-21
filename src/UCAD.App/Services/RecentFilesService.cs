using System.Text.Json;

namespace UCAD.Services;

public sealed record RecentFileEntry(string Path, DateTimeOffset LastOpenedUtc)
{
    public string DisplayName => System.IO.Path.GetFileName(Path);
}

/// <summary>
/// Small local-only MRU store for the Start page. No cloud/account dependency and no
/// document contents are copied into the list; only absolute paths and timestamps persist.
/// </summary>
public sealed class RecentFilesService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private readonly SemaphoreSlim _gate = new(1, 1);

    private RecentFilesService()
    {
        StoragePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "UCAD",
            "recent-files.json");
    }

    public static RecentFilesService Current { get; } = new();
    public string StoragePath { get; }

    public async Task<IReadOnlyList<RecentFileEntry>> GetAsync(int maximum, CancellationToken cancellationToken = default)
    {
        maximum = Math.Clamp(maximum, 1, 100);
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var entries = await ReadUnsafeAsync(cancellationToken);
            return entries
                .Where(entry => File.Exists(entry.Path))
                .OrderByDescending(entry => entry.LastOpenedUtc)
                .Take(maximum)
                .ToArray();
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task RecordAsync(string filePath, int maximum, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        maximum = Math.Clamp(maximum, 1, 100);
        var fullPath = Path.GetFullPath(filePath);

        await _gate.WaitAsync(cancellationToken);
        try
        {
            var entries = await ReadUnsafeAsync(cancellationToken);
            entries.RemoveAll(entry => string.Equals(entry.Path, fullPath, StringComparison.OrdinalIgnoreCase));
            entries.Add(new RecentFileEntry(fullPath, DateTimeOffset.UtcNow));
            entries = entries
                .Where(entry => File.Exists(entry.Path))
                .OrderByDescending(entry => entry.LastOpenedUtc)
                .Take(maximum)
                .ToList();
            await WriteUnsafeAsync(entries, cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task RemoveAsync(string filePath, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        var fullPath = Path.GetFullPath(filePath);
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var entries = await ReadUnsafeAsync(cancellationToken);
            if (entries.RemoveAll(entry => string.Equals(entry.Path, fullPath, StringComparison.OrdinalIgnoreCase)) > 0)
                await WriteUnsafeAsync(entries, cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<List<RecentFileEntry>> ReadUnsafeAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(StoragePath)) return [];
        try
        {
            await using var stream = File.OpenRead(StoragePath);
            return await JsonSerializer.DeserializeAsync<List<RecentFileEntry>>(stream, JsonOptions, cancellationToken) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private async Task WriteUnsafeAsync(List<RecentFileEntry> entries, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(StoragePath)!);
        var tempPath = StoragePath + ".tmp";
        try
        {
            await using (var stream = File.Create(tempPath))
                await JsonSerializer.SerializeAsync(stream, entries, JsonOptions, cancellationToken);
            File.Move(tempPath, StoragePath, overwrite: true);
        }
        finally
        {
            if (File.Exists(tempPath)) File.Delete(tempPath);
        }
    }
}

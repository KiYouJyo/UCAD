using System.Text.Json;
using UCAD.Core;
using UCAD.Core.IO;

namespace UCAD.Services;

public sealed record RecoveryCandidate(
    Guid Id,
    string DisplayName,
    string? SourcePath,
    DateTimeOffset UpdatedUtc,
    string DocumentPath);

/// <summary>
/// Local crash/autosave recovery store. Recovery payloads use the lossless native codec
/// even when the original drawing came from DXF, so no authoring data is discarded.
/// </summary>
public sealed class RecoveryService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private readonly SemaphoreSlim _gate = new(1, 1);

    private RecoveryService()
    {
        RootPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "UCAD",
            "Recovery");
    }

    public static RecoveryService Current { get; } = new();
    public string RootPath { get; }

    public async Task SaveAsync(
        Guid id,
        string displayName,
        string? sourcePath,
        CadDocument document,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);
        await _gate.WaitAsync(cancellationToken);
        try
        {
            Directory.CreateDirectory(RootPath);
            var nativePath = NativePath(id);
            var metadataPath = MetadataPath(id);
            var nativeTemp = nativePath + ".tmp";
            var metadataTemp = metadataPath + ".tmp";
            var updated = DateTimeOffset.UtcNow;
            var metadata = new RecoveryMetadata
            {
                Id = id,
                DisplayName = string.IsNullOrWhiteSpace(displayName) ? "Recovered drawing" : displayName,
                SourcePath = string.IsNullOrWhiteSpace(sourcePath) ? null : Path.GetFullPath(sourcePath),
                UpdatedUtc = updated
            };
            try
            {
                await File.WriteAllTextAsync(nativeTemp, CadNativeDocumentCodec.Serialize(document), cancellationToken);
                await File.WriteAllTextAsync(metadataTemp, JsonSerializer.Serialize(metadata, JsonOptions), cancellationToken);
                File.Move(nativeTemp, nativePath, overwrite: true);
                File.Move(metadataTemp, metadataPath, overwrite: true);
            }
            finally
            {
                if (File.Exists(nativeTemp)) File.Delete(nativeTemp);
                if (File.Exists(metadataTemp)) File.Delete(metadataTemp);
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<IReadOnlyList<RecoveryCandidate>> GetCandidatesAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (!Directory.Exists(RootPath)) return [];
            var candidates = new List<RecoveryCandidate>();
            foreach (var metadataPath in Directory.EnumerateFiles(RootPath, "*.json"))
            {
                try
                {
                    var metadata = JsonSerializer.Deserialize<RecoveryMetadata>(
                        await File.ReadAllTextAsync(metadataPath, cancellationToken), JsonOptions);
                    if (metadata is null || metadata.Id == Guid.Empty) continue;
                    var nativePath = NativePath(metadata.Id);
                    if (!File.Exists(nativePath)) continue;
                    candidates.Add(new RecoveryCandidate(
                        metadata.Id,
                        metadata.DisplayName ?? "Recovered drawing",
                        metadata.SourcePath,
                        metadata.UpdatedUtc,
                        nativePath));
                }
                catch (JsonException)
                {
                    // One damaged recovery record must not suppress other candidates.
                }
            }
            return candidates.OrderByDescending(candidate => candidate.UpdatedUtc).ToArray();
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<CadDocument> LoadAsync(RecoveryCandidate candidate, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        var json = await File.ReadAllTextAsync(candidate.DocumentPath, cancellationToken);
        var document = CadNativeDocumentCodec.Deserialize(json);
        document.ResetHistory();
        return document;
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            DeleteIfExists(NativePath(id));
            DeleteIfExists(MetadataPath(id));
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task DeleteAllAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (Directory.Exists(RootPath)) Directory.Delete(RootPath, recursive: true);
        }
        finally
        {
            _gate.Release();
        }
    }

    private string NativePath(Guid id) => Path.Combine(RootPath, id.ToString("N") + CadNativeDocumentCodec.FileExtension);
    private string MetadataPath(Guid id) => Path.Combine(RootPath, id.ToString("N") + ".json");
    private static void DeleteIfExists(string path) { if (File.Exists(path)) File.Delete(path); }

    private sealed class RecoveryMetadata
    {
        public Guid Id { get; set; }
        public string? DisplayName { get; set; }
        public string? SourcePath { get; set; }
        public DateTimeOffset UpdatedUtc { get; set; }
    }
}
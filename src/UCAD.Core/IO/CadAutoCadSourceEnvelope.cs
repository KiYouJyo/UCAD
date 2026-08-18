using System.Security.Cryptography;

namespace UCAD.Core.IO;

/// <summary>
/// Immutable copy of an imported AutoCAD source container. UCAD uses this envelope as a safety net
/// for ObjectARX/proxy/custom data that the editable 2D semantic model cannot currently reconstruct.
/// </summary>
public sealed class CadAutoCadSourceEnvelope
{
    private readonly byte[] _content;

    public CadAutoCadSourceEnvelope(
        ReadOnlyMemory<byte> content,
        string sourceExtension,
        string sourceCadVersion,
        int proxyEntityCount = 0,
        int proxyObjectCount = 0,
        int customClassCount = 0,
        IEnumerable<string>? preservationReasons = null,
        long baselineRevision = 0)
    {
        if (content.IsEmpty) throw new ArgumentException("Opaque AutoCAD source content cannot be empty.", nameof(content));
        if (string.IsNullOrWhiteSpace(sourceExtension)) throw new ArgumentException("Source extension cannot be empty.", nameof(sourceExtension));
        if (string.IsNullOrWhiteSpace(sourceCadVersion)) throw new ArgumentException("Source CAD version cannot be empty.", nameof(sourceCadVersion));
        if (proxyEntityCount < 0) throw new ArgumentOutOfRangeException(nameof(proxyEntityCount));
        if (proxyObjectCount < 0) throw new ArgumentOutOfRangeException(nameof(proxyObjectCount));
        if (customClassCount < 0) throw new ArgumentOutOfRangeException(nameof(customClassCount));
        if (baselineRevision < 0) throw new ArgumentOutOfRangeException(nameof(baselineRevision));

        _content = content.ToArray();
        SourceExtension = sourceExtension.StartsWith('.') ? sourceExtension.ToLowerInvariant() : "." + sourceExtension.ToLowerInvariant();
        SourceCadVersion = sourceCadVersion.Trim();
        ProxyEntityCount = proxyEntityCount;
        ProxyObjectCount = proxyObjectCount;
        CustomClassCount = customClassCount;
        PreservationReasons = (preservationReasons ?? [])
            .Where(reason => !string.IsNullOrWhiteSpace(reason))
            .Select(reason => reason.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        Sha256 = Convert.ToHexString(SHA256.HashData(_content)).ToLowerInvariant();
        BaselineRevision = baselineRevision;
    }

    public string SourceExtension { get; }
    public string SourceCadVersion { get; }
    public string Sha256 { get; }
    public int ProxyEntityCount { get; }
    public int ProxyObjectCount { get; }
    public int CustomClassCount { get; }
    public IReadOnlyList<string> PreservationReasons { get; }
    public long BaselineRevision { get; }
    public int ByteLength => _content.Length;
    public bool HasOpaqueRisk => ProxyEntityCount > 0 || ProxyObjectCount > 0 || CustomClassCount > 0 || PreservationReasons.Count > 0;
    public ReadOnlyMemory<byte> Content => _content;

    public byte[] CopyContent() => _content.ToArray();

    public bool IsDocumentUnmodified(CadDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        return document.Revision == BaselineRevision;
    }

    internal CadAutoCadSourceEnvelope Rebase(long revision) => new(
        _content,
        SourceExtension,
        SourceCadVersion,
        ProxyEntityCount,
        ProxyObjectCount,
        CustomClassCount,
        PreservationReasons,
        revision);
}

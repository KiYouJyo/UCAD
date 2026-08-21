using System.Text.Json;
using System.Text.Json.Nodes;

namespace UCAD.Core.IO;

/// <summary>
/// Top-level UCAD native persistence wrapper for AutoCAD source recovery metadata. The editable
/// document remains serialized by the current/layout codecs; an opaque original container is stored
/// in a namespaced extension so unsupported ObjectARX/custom data can be recovered after save/reopen.
/// </summary>
public static class CadNativeDocumentCodecAutoCad
{
    private const string ExtensionsProperty = "extensions";
    private const string ExtensionName = "ucad.autocadOpaque";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    public static string Serialize(CadDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        var root = JsonNode.Parse(CadNativeDocumentCodecLayout.Serialize(document))?.AsObject()
            ?? throw new InvalidOperationException("Layout-aware UCAD native codec returned invalid JSON.");

        if (document.AutoCadSourceEnvelope is { } envelope)
        {
            var extensions = root[ExtensionsProperty] as JsonObject ?? new JsonObject();
            extensions[ExtensionName] = JsonSerializer.SerializeToNode(new AutoCadOpaqueExtensionDto
            {
                SourceExtension = envelope.SourceExtension,
                SourceCadVersion = envelope.SourceCadVersion,
                Sha256 = envelope.Sha256,
                ContentBase64 = Convert.ToBase64String(envelope.CopyContent()),
                ProxyEntityCount = envelope.ProxyEntityCount,
                ProxyObjectCount = envelope.ProxyObjectCount,
                CustomClassCount = envelope.CustomClassCount,
                PreservationReasons = envelope.PreservationReasons.ToList()
            }, JsonOptions);
            root[ExtensionsProperty] = extensions;
        }

        return root.ToJsonString(JsonOptions);
    }

    public static CadDocument Deserialize(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);
        var root = JsonNode.Parse(json)?.AsObject() ?? throw new FormatException("UCAD document JSON is empty.");
        AutoCadOpaqueExtensionDto? extension = null;

        if (root[ExtensionsProperty] is JsonObject extensions && extensions[ExtensionName] is JsonNode node)
        {
            extension = node.Deserialize<AutoCadOpaqueExtensionDto>(JsonOptions)
                ?? throw new FormatException("AutoCAD opaque source extension is empty.");
            extensions.Remove(ExtensionName);
            if (extensions.Count == 0) root.Remove(ExtensionsProperty);
        }

        var document = CadNativeDocumentCodecLayout.Deserialize(root.ToJsonString(JsonOptions));
        if (extension is not null)
        {
            byte[] content;
            try
            {
                content = Convert.FromBase64String(Require(extension.ContentBase64, "extensions.ucad.autocadOpaque.contentBase64"));
            }
            catch (FormatException ex)
            {
                throw new FormatException("AutoCAD opaque source extension contains invalid base64 content.", ex);
            }

            var envelope = new CadAutoCadSourceEnvelope(
                content,
                Require(extension.SourceExtension, "extensions.ucad.autocadOpaque.sourceExtension"),
                Require(extension.SourceCadVersion, "extensions.ucad.autocadOpaque.sourceCadVersion"),
                NonNegative(extension.ProxyEntityCount, "extensions.ucad.autocadOpaque.proxyEntityCount"),
                NonNegative(extension.ProxyObjectCount, "extensions.ucad.autocadOpaque.proxyObjectCount"),
                NonNegative(extension.CustomClassCount, "extensions.ucad.autocadOpaque.customClassCount"),
                extension.PreservationReasons ?? []);

            var expectedHash = Require(extension.Sha256, "extensions.ucad.autocadOpaque.sha256");
            if (!string.Equals(envelope.Sha256, expectedHash, StringComparison.OrdinalIgnoreCase))
                throw new FormatException("AutoCAD opaque source extension SHA-256 does not match its embedded content.");

            document.AttachAutoCadSourceEnvelope(envelope);
        }

        return document;
    }

    public static bool HasAutoCadOpaqueExtension(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) return false;
        try
        {
            var root = JsonNode.Parse(json)?.AsObject();
            return root?[ExtensionsProperty]?[ExtensionName] is not null;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static string Require(string? value, string path)
    {
        if (string.IsNullOrWhiteSpace(value)) throw new FormatException($"Missing {path}.");
        return value.Trim();
    }

    private static int NonNegative(int value, string path)
    {
        if (value < 0) throw new FormatException($"{path} must be non-negative.");
        return value;
    }

    private sealed class AutoCadOpaqueExtensionDto
    {
        public string? SourceExtension { get; set; }
        public string? SourceCadVersion { get; set; }
        public string? Sha256 { get; set; }
        public string? ContentBase64 { get; set; }
        public int ProxyEntityCount { get; set; }
        public int ProxyObjectCount { get; set; }
        public int CustomClassCount { get; set; }
        public List<string>? PreservationReasons { get; set; }
    }
}

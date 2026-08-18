using System.Text.Json;
using System.Text.Json.Nodes;
using UCAD.Core.Blocks;
using UCAD.Core.Entities;
using UCAD.Core.Geometry;

namespace UCAD.Core.IO;

/// <summary>
/// v0.11 compatibility wrapper around the format-v1 native codec. It keeps the accepted
/// v1 geometry payload intact and stores advanced Hatch/Block metadata in a namespaced
/// extension object. The wrapper strips that extension before delegating to the strict
/// v1 decoder, then reapplies advanced state against stable entity order/block names.
/// </summary>
public static class CadNativeDocumentCodecV11
{
    private const string ExtensionsProperty = "extensions";
    private const string ExtensionName = "ucad.v11";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    public static string Serialize(CadDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        var root = JsonNode.Parse(CadNativeDocumentCodec.Serialize(document))?.AsObject()
            ?? throw new InvalidOperationException("Base UCAD native codec returned invalid JSON.");

        var extension = BuildExtension(document);
        if (extension is not null)
        {
            root[ExtensionsProperty] = new JsonObject { [ExtensionName] = extension };
        }
        return root.ToJsonString(JsonOptions);
    }

    public static CadDocument Deserialize(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);
        var root = JsonNode.Parse(json)?.AsObject()
            ?? throw new FormatException("UCAD document JSON is empty.");

        V11ExtensionDto? extension = null;
        if (root[ExtensionsProperty] is JsonObject extensions && extensions[ExtensionName] is JsonNode node)
        {
            extension = node.Deserialize<V11ExtensionDto>(JsonOptions);
        }
        root.Remove(ExtensionsProperty);

        var document = CadNativeDocumentCodec.Deserialize(root.ToJsonString(JsonOptions));
        if (extension is not null) ApplyExtension(document, extension);
        return document;
    }

    public static bool HasV11Extension(string json)
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

    private static V11ExtensionDto? BuildExtension(CadDocument document)
    {
        var hatchMetadata = new List<HatchMetadataDto>();
        var blockReferenceMetadata = new List<BlockReferenceMetadataDto>();

        for (var index = 0; index < document.Entities.Count; index++)
        {
            switch (document.Entities[index])
            {
                case HatchEntity hatch when HatchNeedsExtension(hatch):
                    hatchMetadata.Add(new HatchMetadataDto
                    {
                        EntityIndex = index,
                        Islands = hatch.Islands.Select(loop => loop.Select(ToDto).ToList()).ToList(),
                        Associative = hatch.Associative,
                        SourceEntityIds = hatch.SourceEntityIds.ToList(),
                        IslandDetection = hatch.IslandDetection.ToString()
                    });
                    break;
                case BlockReferenceEntity reference when reference.AttributeValues.Count > 0:
                    blockReferenceMetadata.Add(new BlockReferenceMetadataDto
                    {
                        EntityIndex = index,
                        AttributeValues = reference.AttributeValues.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase)
                    });
                    break;
            }
        }

        var blockDefinitions = document.Blocks
            .Where(BlockNeedsExtension)
            .Select(block => new BlockDefinitionMetadataDto
            {
                Name = block.Name,
                ExternalSourcePath = block.ExternalSourcePath,
                AttributeDefinitions = block.AttributeDefinitions.Select(attribute => new BlockAttributeDto
                {
                    Tag = attribute.Tag,
                    Prompt = attribute.Prompt,
                    DefaultValue = attribute.DefaultValue,
                    Position = ToDto(attribute.Position),
                    TextHeight = attribute.TextHeight,
                    Constant = attribute.Constant
                }).ToList()
            })
            .ToList();

        if (hatchMetadata.Count == 0 && blockDefinitions.Count == 0 && blockReferenceMetadata.Count == 0) return null;
        return new V11ExtensionDto
        {
            Hatches = hatchMetadata,
            BlockDefinitions = blockDefinitions,
            BlockReferences = blockReferenceMetadata
        };
    }

    private static void ApplyExtension(CadDocument document, V11ExtensionDto extension)
    {
        foreach (var metadata in extension.Hatches ?? [])
        {
            if (metadata.EntityIndex < 0 || metadata.EntityIndex >= document.Entities.Count)
                throw new FormatException($"v0.11 hatch extension index {metadata.EntityIndex} is outside the entity table.");
            if (document.Entities[metadata.EntityIndex] is not HatchEntity hatch)
                throw new FormatException($"v0.11 hatch extension index {metadata.EntityIndex} does not reference a hatch entity.");

            var islands = (metadata.Islands ?? [])
                .Select((loop, loopIndex) => (IEnumerable<CadPoint>)(loop ?? throw new FormatException($"Hatch island {loopIndex} is null."))
                    .Select((point, pointIndex) => FromDto(point, $"hatches[{metadata.EntityIndex}].islands[{loopIndex}][{pointIndex}]"))
                    .ToArray())
                .ToArray();
            var sourceIds = metadata.SourceEntityIds ?? [];
            var islandDetection = ParseIslandDetection(metadata.IslandDetection);
            var advanced = new HatchEntity(
                hatch.Boundary,
                hatch.Pattern,
                hatch.PatternScale,
                hatch.PatternAngleRadians,
                islands,
                metadata.Associative,
                sourceIds,
                islandDetection,
                hatch.Id);
            document.ReplaceRange([advanced]);
        }

        foreach (var metadata in extension.BlockDefinitions ?? [])
        {
            if (string.IsNullOrWhiteSpace(metadata.Name)) throw new FormatException("v0.11 block extension has no block name.");
            var existing = document.GetBlock(metadata.Name);
            var attributes = (metadata.AttributeDefinitions ?? [])
                .Select(attribute => new CadBlockAttributeDefinition(
                    Require(attribute.Tag, "blockAttribute.tag"),
                    attribute.Prompt ?? attribute.Tag ?? string.Empty,
                    attribute.DefaultValue ?? string.Empty,
                    FromDto(attribute.Position, $"block[{metadata.Name}].attribute.position"),
                    attribute.TextHeight,
                    attribute.Constant))
                .ToArray();
            var advanced = new CadBlockDefinition(
                existing.Name,
                existing.BasePoint,
                existing.Entities,
                attributes,
                metadata.ExternalSourcePath);
            document.RedefineBlock(advanced);
        }

        foreach (var metadata in extension.BlockReferences ?? [])
        {
            if (metadata.EntityIndex < 0 || metadata.EntityIndex >= document.Entities.Count)
                throw new FormatException($"v0.11 block-reference extension index {metadata.EntityIndex} is outside the entity table.");
            if (document.Entities[metadata.EntityIndex] is not BlockReferenceEntity reference)
                throw new FormatException($"v0.11 block-reference extension index {metadata.EntityIndex} does not reference a block reference.");
            document.SetBlockReferenceAttributes(reference.Id, metadata.AttributeValues ?? new Dictionary<string, string>());
        }
    }

    private static bool HatchNeedsExtension(HatchEntity hatch) =>
        hatch.Islands.Count > 0 ||
        hatch.Associative ||
        hatch.SourceEntityIds.Count > 0 ||
        hatch.IslandDetection != HatchIslandDetection.Normal;

    private static bool BlockNeedsExtension(CadBlockDefinition block) =>
        block.AttributeDefinitions.Count > 0 || block.IsExternalReference;

    private static HatchIslandDetection ParseIslandDetection(string? value) =>
        Enum.TryParse<HatchIslandDetection>(value, ignoreCase: true, out var parsed)
            ? parsed
            : HatchIslandDetection.Normal;

    private static PointDto ToDto(CadPoint point) => new() { X = point.X, Y = point.Y };

    private static CadPoint FromDto(PointDto? point, string path)
    {
        if (point is null) throw new FormatException($"Missing {path}.");
        if (!double.IsFinite(point.X) || !double.IsFinite(point.Y)) throw new FormatException($"{path} must contain finite coordinates.");
        return new CadPoint(point.X, point.Y);
    }

    private static string Require(string? value, string path)
    {
        if (string.IsNullOrWhiteSpace(value)) throw new FormatException($"Missing {path}.");
        return value;
    }

    private sealed class V11ExtensionDto
    {
        public List<HatchMetadataDto>? Hatches { get; set; }
        public List<BlockDefinitionMetadataDto>? BlockDefinitions { get; set; }
        public List<BlockReferenceMetadataDto>? BlockReferences { get; set; }
    }

    private sealed class HatchMetadataDto
    {
        public int EntityIndex { get; set; }
        public List<List<PointDto>?>? Islands { get; set; }
        public bool Associative { get; set; }
        public List<Guid>? SourceEntityIds { get; set; }
        public string? IslandDetection { get; set; }
    }

    private sealed class BlockDefinitionMetadataDto
    {
        public string? Name { get; set; }
        public string? ExternalSourcePath { get; set; }
        public List<BlockAttributeDto>? AttributeDefinitions { get; set; }
    }

    private sealed class BlockAttributeDto
    {
        public string? Tag { get; set; }
        public string? Prompt { get; set; }
        public string? DefaultValue { get; set; }
        public PointDto? Position { get; set; }
        public double TextHeight { get; set; } = 2.5;
        public bool Constant { get; set; }
    }

    private sealed class BlockReferenceMetadataDto
    {
        public int EntityIndex { get; set; }
        public Dictionary<string, string>? AttributeValues { get; set; }
    }

    private sealed class PointDto
    {
        public double X { get; set; }
        public double Y { get; set; }
    }
}
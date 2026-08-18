using System.Text.Json;
using System.Text.Json.Nodes;
using UCAD.Core.Blocks;
using UCAD.Core.Entities;
using UCAD.Core.Geometry;

namespace UCAD.Core.IO;

/// <summary>
/// Current lossless UCAD authoring wrapper. The base v1 payload intentionally stays
/// stable; advanced metadata is namespaced. Associative references are serialized as
/// entity-table indices because the strict v1 decoder regenerates entity identities.
/// </summary>
public static class CadNativeDocumentCodecCurrent
{
    private const string ExtensionsProperty = "extensions";
    private const string ExtensionName = "ucad.current";
    private const string LegacyExtensionName = "ucad.v11";

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
            root[ExtensionsProperty] = new JsonObject
            {
                [ExtensionName] = JsonSerializer.SerializeToNode(extension, JsonOptions)
            };
        }
        return root.ToJsonString(JsonOptions);
    }

    public static CadDocument Deserialize(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);
        var root = JsonNode.Parse(json)?.AsObject() ?? throw new FormatException("UCAD document JSON is empty.");
        CurrentExtensionDto? extension = null;
        if (root[ExtensionsProperty] is JsonObject extensions)
        {
            if (extensions[ExtensionName] is JsonNode current)
                extension = current.Deserialize<CurrentExtensionDto>(JsonOptions);
            else if (extensions[LegacyExtensionName] is not null)
                return CadNativeDocumentCodecV11.Deserialize(json);
        }
        root.Remove(ExtensionsProperty);

        var document = CadNativeDocumentCodec.Deserialize(root.ToJsonString(JsonOptions));
        if (extension is not null) ApplyExtension(document, extension);
        return document;
    }

    public static bool HasCurrentExtension(string json)
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

    private static CurrentExtensionDto? BuildExtension(CadDocument document)
    {
        var indexById = document.Entities
            .Select((entity, index) => (entity.Id, index))
            .ToDictionary(pair => pair.Id, pair => pair.index);
        var hatches = new List<HatchMetadataDto>();
        var references = new List<BlockReferenceMetadataDto>();

        for (var index = 0; index < document.Entities.Count; index++)
        {
            switch (document.Entities[index])
            {
                case HatchEntity hatch when HatchNeedsExtension(hatch):
                {
                    var sourceIndices = new List<int>();
                    foreach (var sourceId in hatch.SourceEntityIds)
                    {
                        if (!indexById.TryGetValue(sourceId, out var sourceIndex))
                            throw new InvalidOperationException($"Associative hatch {hatch.Id} references missing entity {sourceId}.");
                        sourceIndices.Add(sourceIndex);
                    }
                    hatches.Add(new HatchMetadataDto
                    {
                        EntityIndex = index,
                        Islands = hatch.Islands.Select(loop => loop.Select(ToDto).ToList()).ToList(),
                        Associative = hatch.Associative,
                        SourceEntityIndices = sourceIndices,
                        IslandDetection = hatch.IslandDetection.ToString()
                    });
                    break;
                }
                case BlockReferenceEntity reference when reference.AttributeValues.Count > 0:
                    references.Add(new BlockReferenceMetadataDto
                    {
                        EntityIndex = index,
                        AttributeValues = reference.AttributeValues.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase)
                    });
                    break;
            }
        }

        var blocks = document.Blocks
            .Where(block => block.AttributeDefinitions.Count > 0 || block.IsExternalReference)
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

        if (hatches.Count == 0 && references.Count == 0 && blocks.Count == 0) return null;
        return new CurrentExtensionDto { Hatches = hatches, BlockDefinitions = blocks, BlockReferences = references };
    }

    private static void ApplyExtension(CadDocument document, CurrentExtensionDto extension)
    {
        foreach (var blockMetadata in extension.BlockDefinitions ?? [])
        {
            var name = Require(blockMetadata.Name, "block.name");
            var existing = document.GetBlock(name);
            var attributes = (blockMetadata.AttributeDefinitions ?? []).Select(attribute => new CadBlockAttributeDefinition(
                Require(attribute.Tag, "block.attribute.tag"),
                attribute.Prompt ?? attribute.Tag ?? string.Empty,
                attribute.DefaultValue ?? string.Empty,
                FromDto(attribute.Position, $"block[{name}].attribute.position"),
                attribute.TextHeight,
                attribute.Constant)).ToArray();
            document.RedefineBlock(new CadBlockDefinition(
                existing.Name,
                existing.BasePoint,
                existing.Entities,
                attributes,
                blockMetadata.ExternalSourcePath));
        }

        foreach (var hatchMetadata in extension.Hatches ?? [])
        {
            var hatch = RequireEntity<HatchEntity>(document, hatchMetadata.EntityIndex, "hatch");
            var sourceIds = (hatchMetadata.SourceEntityIndices ?? []).Select(sourceIndex =>
            {
                if (sourceIndex < 0 || sourceIndex >= document.Entities.Count)
                    throw new FormatException($"Associative hatch source index {sourceIndex} is outside the entity table.");
                return document.Entities[sourceIndex].Id;
            }).ToArray();
            var islands = (hatchMetadata.Islands ?? []).Select((loop, loopIndex) =>
                (IEnumerable<CadPoint>)(loop ?? throw new FormatException($"Hatch island {loopIndex} is null."))
                    .Select((point, pointIndex) => FromDto(point, $"hatch[{hatchMetadata.EntityIndex}].island[{loopIndex}][{pointIndex}]"))
                    .ToArray()).ToArray();
            var detection = Enum.TryParse<HatchIslandDetection>(hatchMetadata.IslandDetection, true, out var parsed)
                ? parsed
                : HatchIslandDetection.Normal;
            var advanced = new HatchEntity(
                hatch.Boundary,
                hatch.Pattern,
                hatch.PatternScale,
                hatch.PatternAngleRadians,
                islands,
                hatchMetadata.Associative,
                sourceIds,
                detection,
                hatch.Id);
            document.ReplaceRange([advanced]);
        }

        foreach (var referenceMetadata in extension.BlockReferences ?? [])
        {
            var reference = RequireEntity<BlockReferenceEntity>(document, referenceMetadata.EntityIndex, "blockReference");
            document.SetBlockReferenceAttributes(reference.Id, referenceMetadata.AttributeValues ?? new Dictionary<string, string>());
        }
    }

    private static T RequireEntity<T>(CadDocument document, int index, string kind) where T : class, ICadEntity
    {
        if (index < 0 || index >= document.Entities.Count)
            throw new FormatException($"{kind} extension index {index} is outside the entity table.");
        if (document.Entities[index] is not T entity)
            throw new FormatException($"{kind} extension index {index} references {document.Entities[index].GetType().Name}.");
        return entity;
    }

    private static bool HatchNeedsExtension(HatchEntity hatch) =>
        hatch.Islands.Count > 0 || hatch.Associative || hatch.SourceEntityIds.Count > 0 || hatch.IslandDetection != HatchIslandDetection.Normal;

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

    private sealed class CurrentExtensionDto
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
        public List<int>? SourceEntityIndices { get; set; }
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

    private sealed class PointDto { public double X { get; set; } public double Y { get; set; } }
}
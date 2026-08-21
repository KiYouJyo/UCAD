using UCAD.Core.Blocks;
using UCAD.Core.Entities;

namespace UCAD.Core;

public sealed partial class CadDocument
{
    public void RenameBlock(string oldName, string newName)
    {
        if (string.IsNullOrWhiteSpace(oldName)) throw new ArgumentException("Block name cannot be empty.", nameof(oldName));
        if (string.IsNullOrWhiteSpace(newName)) throw new ArgumentException("New block name cannot be empty.", nameof(newName));
        var existing = GetBlock(oldName);
        var trimmed = newName.Trim();
        if (TryGetBlock(trimmed, out _)) throw new InvalidOperationException($"Block '{trimmed}' already exists.");

        RecordMutation();
        var renamed = existing.Rename(trimmed);
        _blocks[_blocks.IndexOf(existing)] = renamed;
        for (var i = 0; i < _entities.Count; i++)
        {
            if (_entities[i] is not BlockReferenceEntity reference ||
                !string.Equals(reference.DefinitionName, oldName, StringComparison.OrdinalIgnoreCase)) continue;
            _entities[i] = new BlockReferenceEntity(
                trimmed,
                reference.InsertionPoint,
                reference.Contents,
                reference.Scale,
                reference.RotationRadians,
                reference.AttributeValues,
                reference.Id);
        }
        RaiseChanged(CadDocumentChangeKind.BlockTable);
    }

    public void RedefineBlock(CadBlockDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        var existing = GetBlock(definition.Name);
        RecordMutation();
        _blocks[_blocks.IndexOf(existing)] = definition;
        for (var i = 0; i < _entities.Count; i++)
        {
            if (_entities[i] is not BlockReferenceEntity reference ||
                !string.Equals(reference.DefinitionName, definition.Name, StringComparison.OrdinalIgnoreCase)) continue;
            _entities[i] = CadBlockFactory.RefreshReference(definition, reference);
        }
        RaiseChanged(CadDocumentChangeKind.BlockTable);
    }

    public bool SetBlockReferenceAttributes(Guid referenceId, IReadOnlyDictionary<string, string> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        var index = _entities.FindIndex(entity => entity.Id == referenceId && entity is BlockReferenceEntity);
        if (index < 0) return false;
        var reference = (BlockReferenceEntity)_entities[index];
        var definition = GetBlock(reference.DefinitionName);
        var validTags = definition.AttributeDefinitions.Select(attribute => attribute.Tag).ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (values.Keys.Any(tag => !validTags.Contains(tag)))
            throw new ArgumentException("Attribute values contain a tag not defined by the block.", nameof(values));

        var merged = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var attribute in definition.AttributeDefinitions)
        {
            if (attribute.Constant)
                merged[attribute.Tag] = attribute.DefaultValue;
            else if (values.TryGetValue(attribute.Tag, out var value))
                merged[attribute.Tag] = value ?? string.Empty;
            else if (reference.AttributeValues.TryGetValue(attribute.Tag, out var existingValue))
                merged[attribute.Tag] = existingValue;
            else
                merged[attribute.Tag] = attribute.DefaultValue;
        }

        if (DictionariesEqual(reference.AttributeValues, merged)) return false;
        RecordMutation();
        _entities[index] = reference.WithAttributes(merged);
        RaiseChanged(CadDocumentChangeKind.BlockTable);
        return true;
    }

    public void ReloadExternalReference(string blockName, IEnumerable<ICadEntity> sourceEntities)
    {
        ArgumentNullException.ThrowIfNull(sourceEntities);
        var existing = GetBlock(blockName);
        if (!existing.IsExternalReference) throw new InvalidOperationException($"Block '{blockName}' is not an external reference.");
        var refreshed = new CadBlockDefinition(
            existing.Name,
            existing.BasePoint,
            sourceEntities,
            existing.AttributeDefinitions,
            existing.ExternalSourcePath);
        RedefineBlock(refreshed);
    }

    private static bool DictionariesEqual(IReadOnlyDictionary<string, string> first, IReadOnlyDictionary<string, string> second)
    {
        if (first.Count != second.Count) return false;
        foreach (var pair in first)
            if (!second.TryGetValue(pair.Key, out var value) || !string.Equals(value, pair.Value, StringComparison.Ordinal)) return false;
        return true;
    }
}
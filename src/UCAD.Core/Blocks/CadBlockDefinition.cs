using UCAD.Core.Entities;
using UCAD.Core.Geometry;

namespace UCAD.Core.Blocks;

public sealed class CadBlockDefinition
{
    private readonly IReadOnlyList<ICadEntity> _entities;
    private readonly IReadOnlyList<CadBlockAttributeDefinition> _attributeDefinitions;

    public CadBlockDefinition(string name, CadPoint basePoint, IEnumerable<ICadEntity> entities)
        : this(name, basePoint, entities, attributeDefinitions: null, externalSourcePath: null)
    {
    }

    public CadBlockDefinition(
        string name,
        CadPoint basePoint,
        IEnumerable<ICadEntity> entities,
        IEnumerable<CadBlockAttributeDefinition>? attributeDefinitions,
        string? externalSourcePath = null)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Block name cannot be empty.", nameof(name));
        ArgumentNullException.ThrowIfNull(entities);
        var snapshot = entities.ToArray();
        if (snapshot.Length == 0) throw new ArgumentException("A block requires at least one entity.", nameof(entities));

        var attributes = attributeDefinitions?.ToArray() ?? [];
        if (attributes.Select(attribute => attribute.Tag).Distinct(StringComparer.OrdinalIgnoreCase).Count() != attributes.Length)
            throw new ArgumentException("Block attribute tags must be unique.", nameof(attributeDefinitions));

        Name = name.Trim();
        BasePoint = basePoint;
        _entities = Array.AsReadOnly(snapshot);
        _attributeDefinitions = Array.AsReadOnly(attributes);
        ExternalSourcePath = string.IsNullOrWhiteSpace(externalSourcePath) ? null : Path.GetFullPath(externalSourcePath);
    }

    public string Name { get; }
    public CadPoint BasePoint { get; }
    public IReadOnlyList<ICadEntity> Entities => _entities;
    public IReadOnlyList<CadBlockAttributeDefinition> AttributeDefinitions => _attributeDefinitions;
    public string? ExternalSourcePath { get; }
    public bool IsExternalReference => ExternalSourcePath is not null;

    public CadBlockDefinition Rename(string newName) =>
        new(newName, BasePoint, Entities, AttributeDefinitions, ExternalSourcePath);

    public CadBlockDefinition Redefine(
        CadPoint basePoint,
        IEnumerable<ICadEntity> entities,
        IEnumerable<CadBlockAttributeDefinition>? attributeDefinitions = null) =>
        new(Name, basePoint, entities, attributeDefinitions ?? AttributeDefinitions, ExternalSourcePath);
}
using UCAD.Core.Entities;
using UCAD.Core.Geometry;
using UCAD.Core.Modify;

namespace UCAD.Core.Blocks;

public static class CadBlockFactory
{
    private static readonly CadPoint Origin = new(0, 0);

    public static BlockReferenceEntity CreateReference(
        CadBlockDefinition definition,
        CadPoint insertionPoint,
        double scale = 1,
        double rotationRadians = 0) =>
        CreateReferenceCore(definition, insertionPoint, scale, rotationRadians, attributeValues: null, preserveId: null);

    public static BlockReferenceEntity CreateReference(
        CadBlockDefinition definition,
        CadPoint insertionPoint,
        IReadOnlyDictionary<string, string>? attributeValues,
        double scale = 1,
        double rotationRadians = 0) =>
        CreateReferenceCore(definition, insertionPoint, scale, rotationRadians, attributeValues, preserveId: null);

    public static BlockReferenceEntity CreateReference(
        CadBlockDefinition definition,
        CadPoint insertionPoint,
        double scale,
        double rotationRadians,
        IReadOnlyDictionary<string, string>? attributeValues) =>
        CreateReferenceCore(definition, insertionPoint, scale, rotationRadians, attributeValues, preserveId: null);

    public static BlockReferenceEntity RefreshReference(CadBlockDefinition definition, BlockReferenceEntity reference)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(reference);
        if (!string.Equals(definition.Name, reference.DefinitionName, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Block definition name does not match the reference.", nameof(definition));

        return CreateReferenceCore(
            definition,
            reference.InsertionPoint,
            reference.Scale,
            reference.RotationRadians,
            reference.AttributeValues,
            reference.Id);
    }

    public static IReadOnlyList<ICadEntity> Explode(BlockReferenceEntity reference) =>
        reference.Contents
            .Select(entity => CadEntityTransform.Translate(entity, new CadVector(0, 0), preserveIdentity: false))
            .ToArray();

    private static BlockReferenceEntity CreateReferenceCore(
        CadBlockDefinition definition,
        CadPoint insertionPoint,
        double scale,
        double rotationRadians,
        IReadOnlyDictionary<string, string>? attributeValues,
        Guid? preserveId)
    {
        ArgumentNullException.ThrowIfNull(definition);
        if (!double.IsFinite(scale) || scale <= 1e-9) throw new ArgumentOutOfRangeException(nameof(scale));
        if (!double.IsFinite(rotationRadians)) throw new ArgumentOutOfRangeException(nameof(rotationRadians));

        var toOrigin = new CadVector(-definition.BasePoint.X, -definition.BasePoint.Y);
        var toInsertion = new CadVector(insertionPoint.X, insertionPoint.Y);
        var contents = definition.Entities.Select(entity =>
        {
            var local = CadEntityTransform.Translate(entity, toOrigin, preserveIdentity: false);
            var scaled = CadEntityTransform.Scale(local, Origin, scale);
            var rotated = CadEntityTransform.Rotate(scaled, Origin, rotationRadians);
            return CadEntityTransform.Translate(rotated, toInsertion);
        }).ToArray();

        var values = ResolveAttributes(definition, attributeValues);
        return preserveId is Guid id
            ? new BlockReferenceEntity(definition.Name, insertionPoint, contents, scale, rotationRadians, values, id)
            : new BlockReferenceEntity(definition.Name, insertionPoint, contents, scale, rotationRadians, values);
    }

    private static IReadOnlyDictionary<string, string> ResolveAttributes(
        CadBlockDefinition definition,
        IReadOnlyDictionary<string, string>? requested)
    {
        var validTags = definition.AttributeDefinitions
            .Select(attribute => attribute.Tag)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (requested is not null && requested.Keys.Any(tag => !validTags.Contains(tag)))
            throw new ArgumentException("Attribute values contain a tag not defined by the block.", nameof(requested));

        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var attribute in definition.AttributeDefinitions)
        {
            if (attribute.Constant)
            {
                values[attribute.Tag] = attribute.DefaultValue;
                continue;
            }
            values[attribute.Tag] = requested is not null && requested.TryGetValue(attribute.Tag, out var value)
                ? value ?? string.Empty
                : attribute.DefaultValue;
        }
        return values;
    }
}

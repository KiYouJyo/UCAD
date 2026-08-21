using UCAD.Core.Geometry;

namespace UCAD.Core.Entities;

/// <summary>
/// Self-contained block reference. Contents are the transformed world-space snapshot
/// created from a document block definition. Attribute values are stored per reference
/// so redefining a block does not erase instance-specific data.
/// </summary>
public sealed class BlockReferenceEntity : ICadEntity
{
    private readonly IReadOnlyList<ICadEntity> _contents;
    private readonly IReadOnlyDictionary<string, string> _attributeValues;

    public BlockReferenceEntity(
        string definitionName,
        CadPoint insertionPoint,
        IEnumerable<ICadEntity> contents,
        double scale = 1,
        double rotationRadians = 0)
        : this(definitionName, insertionPoint, contents, scale, rotationRadians, attributeValues: null, Guid.NewGuid())
    {
    }

    public BlockReferenceEntity(
        string definitionName,
        CadPoint insertionPoint,
        IEnumerable<ICadEntity> contents,
        double scale,
        double rotationRadians,
        IReadOnlyDictionary<string, string>? attributeValues)
        : this(definitionName, insertionPoint, contents, scale, rotationRadians, attributeValues, Guid.NewGuid())
    {
    }

    internal BlockReferenceEntity(
        string definitionName,
        CadPoint insertionPoint,
        IEnumerable<ICadEntity> contents,
        double scale,
        double rotationRadians,
        Guid id)
        : this(definitionName, insertionPoint, contents, scale, rotationRadians, attributeValues: null, id)
    {
    }

    internal BlockReferenceEntity(
        string definitionName,
        CadPoint insertionPoint,
        IEnumerable<ICadEntity> contents,
        double scale,
        double rotationRadians,
        IReadOnlyDictionary<string, string>? attributeValues,
        Guid id)
    {
        if (string.IsNullOrWhiteSpace(definitionName)) throw new ArgumentException("Block definition name cannot be empty.", nameof(definitionName));
        ArgumentNullException.ThrowIfNull(contents);
        var snapshot = contents.ToArray();
        if (snapshot.Length == 0) throw new ArgumentException("A block reference requires content geometry.", nameof(contents));
        if (!double.IsFinite(scale) || scale <= 0) throw new ArgumentOutOfRangeException(nameof(scale));
        if (!double.IsFinite(rotationRadians)) throw new ArgumentOutOfRangeException(nameof(rotationRadians));

        var attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (attributeValues is not null)
        {
            foreach (var pair in attributeValues)
            {
                if (string.IsNullOrWhiteSpace(pair.Key)) throw new ArgumentException("Attribute tag cannot be empty.", nameof(attributeValues));
                attributes[pair.Key.Trim().ToUpperInvariant()] = pair.Value ?? string.Empty;
            }
        }

        DefinitionName = definitionName.Trim();
        InsertionPoint = insertionPoint;
        _contents = Array.AsReadOnly(snapshot);
        _attributeValues = new System.Collections.ObjectModel.ReadOnlyDictionary<string, string>(attributes);
        Scale = scale;
        RotationRadians = rotationRadians;
        Id = id;
    }

    public Guid Id { get; }
    public string DefinitionName { get; }
    public CadPoint InsertionPoint { get; }
    public IReadOnlyList<ICadEntity> Contents => _contents;
    public double Scale { get; }
    public double RotationRadians { get; }
    public IReadOnlyDictionary<string, string> AttributeValues => _attributeValues;

    public BlockReferenceEntity WithAttributes(IReadOnlyDictionary<string, string> values) =>
        new(DefinitionName, InsertionPoint, Contents, Scale, RotationRadians, values, Id);
}
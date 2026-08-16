using UCAD.Core.Geometry;

namespace UCAD.Core.Entities;

/// <summary>
/// Self-contained block reference. Contents are the transformed world-space snapshot
/// created from a document block definition, which keeps rendering/hit-testing Core-only
/// without coupling generic geometry helpers back to CadDocument.
/// </summary>
public sealed class BlockReferenceEntity : ICadEntity
{
    private readonly IReadOnlyList<ICadEntity> _contents;

    public BlockReferenceEntity(
        string definitionName,
        CadPoint insertionPoint,
        IEnumerable<ICadEntity> contents,
        double scale = 1,
        double rotationRadians = 0)
        : this(definitionName, insertionPoint, contents, scale, rotationRadians, Guid.NewGuid())
    {
    }

    internal BlockReferenceEntity(
        string definitionName,
        CadPoint insertionPoint,
        IEnumerable<ICadEntity> contents,
        double scale,
        double rotationRadians,
        Guid id)
    {
        if (string.IsNullOrWhiteSpace(definitionName)) throw new ArgumentException("Block definition name cannot be empty.", nameof(definitionName));
        ArgumentNullException.ThrowIfNull(contents);
        var snapshot = contents.ToArray();
        if (snapshot.Length == 0) throw new ArgumentException("A block reference requires content geometry.", nameof(contents));
        if (!double.IsFinite(scale) || scale <= 0) throw new ArgumentOutOfRangeException(nameof(scale));
        if (!double.IsFinite(rotationRadians)) throw new ArgumentOutOfRangeException(nameof(rotationRadians));
        DefinitionName = definitionName.Trim();
        InsertionPoint = insertionPoint;
        _contents = Array.AsReadOnly(snapshot);
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
}

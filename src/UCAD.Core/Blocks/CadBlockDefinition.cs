using UCAD.Core.Entities;
using UCAD.Core.Geometry;

namespace UCAD.Core.Blocks;

public sealed class CadBlockDefinition
{
    private readonly IReadOnlyList<ICadEntity> _entities;

    public CadBlockDefinition(string name, CadPoint basePoint, IEnumerable<ICadEntity> entities)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Block name cannot be empty.", nameof(name));
        ArgumentNullException.ThrowIfNull(entities);
        var snapshot = entities.ToArray();
        if (snapshot.Length == 0) throw new ArgumentException("A block requires at least one entity.", nameof(entities));
        Name = name.Trim();
        BasePoint = basePoint;
        _entities = Array.AsReadOnly(snapshot);
    }

    public string Name { get; }
    public CadPoint BasePoint { get; }
    public IReadOnlyList<ICadEntity> Entities => _entities;
}

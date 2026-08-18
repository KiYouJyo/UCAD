using UCAD.Core.Geometry;

namespace UCAD.Core.Entities;

public sealed record PointEntity : ICadEntity
{
    public PointEntity(CadPoint position) : this(position, Guid.NewGuid()) { }

    internal PointEntity(CadPoint position, Guid id)
    {
        Position = position;
        Id = id;
    }

    public Guid Id { get; }
    public CadPoint Position { get; }
}
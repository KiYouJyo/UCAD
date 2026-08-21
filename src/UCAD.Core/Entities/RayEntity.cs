using UCAD.Core.Geometry;

namespace UCAD.Core.Entities;

public sealed record RayEntity : ICadEntity
{
    private const double Epsilon = 1e-9;

    public RayEntity(CadPoint origin, CadVector direction)
        : this(origin, direction, Guid.NewGuid()) { }

    internal RayEntity(CadPoint origin, CadVector direction, Guid id)
    {
        if (!double.IsFinite(direction.X) || !double.IsFinite(direction.Y) || direction.Length <= Epsilon)
            throw new ArgumentException("Ray direction must be non-zero and finite.", nameof(direction));
        var length = direction.Length;
        Origin = origin;
        Direction = new CadVector(direction.X / length, direction.Y / length);
        Id = id;
    }

    public Guid Id { get; }
    public CadPoint Origin { get; }
    public CadVector Direction { get; }
}
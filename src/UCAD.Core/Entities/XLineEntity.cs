using UCAD.Core.Geometry;

namespace UCAD.Core.Entities;

public sealed record XLineEntity : ICadEntity
{
    private const double Epsilon = 1e-9;

    public XLineEntity(CadPoint point, CadVector direction)
        : this(point, direction, Guid.NewGuid()) { }

    internal XLineEntity(CadPoint point, CadVector direction, Guid id)
    {
        if (!double.IsFinite(direction.X) || !double.IsFinite(direction.Y) || direction.Length <= Epsilon)
            throw new ArgumentException("Construction-line direction must be non-zero and finite.", nameof(direction));
        var length = direction.Length;
        Point = point;
        Direction = new CadVector(direction.X / length, direction.Y / length);
        Id = id;
    }

    public Guid Id { get; }
    public CadPoint Point { get; }
    public CadVector Direction { get; }
}
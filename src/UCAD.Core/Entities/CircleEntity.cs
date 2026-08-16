using UCAD.Core.Geometry;

namespace UCAD.Core.Entities;

public sealed class CircleEntity : ICadEntity
{
    public CircleEntity(CadPoint center, double radius)
        : this(center, radius, Guid.NewGuid())
    {
    }

    internal CircleEntity(CadPoint center, double radius, Guid id)
    {
        if (!double.IsFinite(radius) || radius <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(radius), "Circle radius must be a positive finite value.");
        }

        Center = center;
        Radius = radius;
        Id = id;
    }

    public Guid Id { get; }

    public CadPoint Center { get; }

    public double Radius { get; }

    public double Circumference => Math.Tau * Radius;
}

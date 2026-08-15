using UCAD.Core.Geometry;

namespace UCAD.Core.Entities;

public sealed class CircleEntity : ICadEntity
{
    public CircleEntity(CadPoint center, double radius)
    {
        if (!double.IsFinite(radius) || radius <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(radius), "Circle radius must be a positive finite value.");
        }

        Center = center;
        Radius = radius;
    }

    public Guid Id { get; } = Guid.NewGuid();

    public CadPoint Center { get; }

    public double Radius { get; }

    public double Circumference => Math.Tau * Radius;
}

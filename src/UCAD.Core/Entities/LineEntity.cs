using UCAD.Core.Geometry;

namespace UCAD.Core.Entities;

public sealed record LineEntity(CadPoint Start, CadPoint End) : ICadEntity
{
    public Guid Id { get; } = Guid.NewGuid();

    public double Length => (End - Start).Length;
}

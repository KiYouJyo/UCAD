using UCAD.Core.Geometry;

namespace UCAD.Core.Entities;

public sealed record LineEntity : ICadEntity
{
    public LineEntity(CadPoint start, CadPoint end)
        : this(start, end, Guid.NewGuid())
    {
    }

    internal LineEntity(CadPoint start, CadPoint end, Guid id)
    {
        Start = start;
        End = end;
        Id = id;
    }

    public CadPoint Start { get; }

    public CadPoint End { get; }

    public Guid Id { get; }

    public double Length => (End - Start).Length;
}

using UCAD.Core.Geometry;

namespace UCAD.Core.Entities;

public sealed class PolylineEntity : ICadEntity
{
    private readonly IReadOnlyList<CadPoint> _points;

    public PolylineEntity(IEnumerable<CadPoint> points, bool closed = false)
    {
        ArgumentNullException.ThrowIfNull(points);
        var snapshot = points.ToArray();
        if (snapshot.Length < 2)
        {
            throw new ArgumentException("A polyline requires at least two points.", nameof(points));
        }

        _points = Array.AsReadOnly(snapshot);
        Closed = closed;
    }

    public Guid Id { get; } = Guid.NewGuid();

    public IReadOnlyList<CadPoint> Points => _points;

    public bool Closed { get; }

    public double Length
    {
        get
        {
            var length = 0.0;
            for (var i = 1; i < _points.Count; i++)
            {
                length += (_points[i] - _points[i - 1]).Length;
            }

            if (Closed)
            {
                length += (_points[0] - _points[^1]).Length;
            }

            return length;
        }
    }
}

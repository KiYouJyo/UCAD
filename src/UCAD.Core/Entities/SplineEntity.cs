using UCAD.Core.Geometry;

namespace UCAD.Core.Entities;

/// <summary>
/// Lightweight fit-point spline. UCAD stores the authoring fit points losslessly and
/// samples a centripetal-style Catmull-Rom curve for viewport/selection use.
/// </summary>
public sealed class SplineEntity : ICadEntity
{
    private readonly IReadOnlyList<CadPoint> _fitPoints;

    public SplineEntity(IEnumerable<CadPoint> fitPoints, bool closed = false)
        : this(fitPoints, closed, Guid.NewGuid())
    {
    }

    internal SplineEntity(IEnumerable<CadPoint> fitPoints, bool closed, Guid id)
    {
        ArgumentNullException.ThrowIfNull(fitPoints);
        var points = fitPoints.ToArray();
        if (points.Length < 2) throw new ArgumentException("A spline requires at least two fit points.", nameof(fitPoints));
        if (points.Any(point => !double.IsFinite(point.X) || !double.IsFinite(point.Y)))
            throw new ArgumentException("Spline fit points must be finite.", nameof(fitPoints));
        _fitPoints = Array.AsReadOnly(points);
        Closed = closed;
        Id = id;
    }

    public Guid Id { get; }
    public IReadOnlyList<CadPoint> FitPoints => _fitPoints;
    public bool Closed { get; }

    public IReadOnlyList<CadPoint> SamplePoints(int samplesPerSegment = 16)
    {
        samplesPerSegment = Math.Clamp(samplesPerSegment, 4, 64);
        if (_fitPoints.Count == 2)
            return [_fitPoints[0], _fitPoints[1]];

        var result = new List<CadPoint>();
        var segmentCount = Closed ? _fitPoints.Count : _fitPoints.Count - 1;
        for (var segment = 0; segment < segmentCount; segment++)
        {
            var p1 = PointAtIndex(segment);
            var p2 = PointAtIndex(segment + 1);
            var p0 = Closed || segment > 0 ? PointAtIndex(segment - 1) : p1;
            var p3 = Closed || segment + 2 < _fitPoints.Count ? PointAtIndex(segment + 2) : p2;

            for (var step = 0; step < samplesPerSegment; step++)
            {
                if (segment > 0 && step == 0) continue;
                var t = step / (double)samplesPerSegment;
                result.Add(CatmullRom(p0, p1, p2, p3, t));
            }
        }
        result.Add(Closed ? result[0] : _fitPoints[^1]);
        return result;
    }

    private CadPoint PointAtIndex(int index)
    {
        if (Closed)
        {
            var normalized = ((index % _fitPoints.Count) + _fitPoints.Count) % _fitPoints.Count;
            return _fitPoints[normalized];
        }
        return _fitPoints[Math.Clamp(index, 0, _fitPoints.Count - 1)];
    }

    private static CadPoint CatmullRom(CadPoint p0, CadPoint p1, CadPoint p2, CadPoint p3, double t)
    {
        var t2 = t * t;
        var t3 = t2 * t;
        static double Blend(double a0, double a1, double a2, double a3, double t, double t2, double t3) =>
            0.5 * ((2 * a1) + (-a0 + a2) * t + (2 * a0 - 5 * a1 + 4 * a2 - a3) * t2 + (-a0 + 3 * a1 - 3 * a2 + a3) * t3);
        return new CadPoint(
            Blend(p0.X, p1.X, p2.X, p3.X, t, t2, t3),
            Blend(p0.Y, p1.Y, p2.Y, p3.Y, t, t2, t3));
    }
}
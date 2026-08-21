using UCAD.Core.Entities;
using UCAD.Core.Geometry;

namespace UCAD.Core.Architecture;

public sealed record CadClosedPolylineMeasurement(
    double SignedArea,
    double Area,
    double Perimeter,
    CadPoint Centroid,
    bool Clockwise);

public static class CadClosedPolylineMetrics
{
    private const double Epsilon = 1e-12;

    public static CadClosedPolylineMeasurement Measure(PolylineEntity polyline)
    {
        ArgumentNullException.ThrowIfNull(polyline);
        if (!polyline.Closed) throw new ArgumentException("Area measurement requires a closed polyline.", nameof(polyline));
        if (polyline.Points.Count < 3) throw new ArgumentException("Area measurement requires at least three vertices.", nameof(polyline));

        var twiceArea = 0.0;
        var centroidXAccumulator = 0.0;
        var centroidYAccumulator = 0.0;
        for (var index = 0; index < polyline.Points.Count; index++)
        {
            var first = polyline.Points[index];
            var second = polyline.Points[(index + 1) % polyline.Points.Count];
            var cross = (first.X * second.Y) - (second.X * first.Y);
            twiceArea += cross;
            centroidXAccumulator += (first.X + second.X) * cross;
            centroidYAccumulator += (first.Y + second.Y) * cross;
        }

        var signedArea = twiceArea / 2.0;
        if (!double.IsFinite(signedArea) || Math.Abs(signedArea) <= Epsilon)
            throw new ArgumentException("Closed polyline has zero or invalid area.", nameof(polyline));

        var centroidFactor = 1.0 / (3.0 * twiceArea);
        var centroid = new CadPoint(
            centroidXAccumulator * centroidFactor,
            centroidYAccumulator * centroidFactor);
        return new CadClosedPolylineMeasurement(
            signedArea,
            Math.Abs(signedArea),
            polyline.Length,
            centroid,
            signedArea < 0);
    }
}

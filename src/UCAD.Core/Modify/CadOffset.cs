using UCAD.Core.Entities;
using UCAD.Core.Geometry;

namespace UCAD.Core.Modify;

public static class CadOffset
{
    private const double Epsilon = 1e-9;

    public static bool TryCreate(ICadEntity source, double distance, CadPoint sidePoint, out ICadEntity? offset)
    {
        ArgumentNullException.ThrowIfNull(source);
        offset = null;
        if (!double.IsFinite(distance) || distance <= Epsilon)
        {
            return false;
        }

        offset = source switch
        {
            LineEntity line => OffsetLine(line, distance, sidePoint),
            PolylineEntity polyline => OffsetPolyline(polyline, distance, sidePoint),
            CircleEntity circle => OffsetCircle(circle, distance, sidePoint),
            ArcEntity arc => OffsetArc(arc, distance, sidePoint),
            _ => null
        };
        return offset is not null;
    }

    private static LineEntity? OffsetLine(LineEntity line, double distance, CadPoint sidePoint)
    {
        var direction = line.End - line.Start;
        var length = direction.Length;
        if (length <= Epsilon)
        {
            return null;
        }

        var side = Cross(direction, sidePoint - line.Start) >= 0 ? 1.0 : -1.0;
        var normal = new CadVector(-direction.Y / length * distance * side, direction.X / length * distance * side);
        return new LineEntity(line.Start + normal, line.End + normal);
    }

    private static CircleEntity? OffsetCircle(CircleEntity circle, double distance, CadPoint sidePoint)
    {
        var outside = (sidePoint - circle.Center).Length >= circle.Radius;
        var radius = outside ? circle.Radius + distance : circle.Radius - distance;
        return radius > Epsilon ? new CircleEntity(circle.Center, radius) : null;
    }

    private static ArcEntity? OffsetArc(ArcEntity arc, double distance, CadPoint sidePoint)
    {
        var outside = (sidePoint - arc.Center).Length >= arc.Radius;
        var radius = outside ? arc.Radius + distance : arc.Radius - distance;
        return radius > Epsilon
            ? ArcEntity.Create(arc.Center, radius, arc.StartAngleRadians, arc.SweepAngleRadians)
            : null;
    }

    private static PolylineEntity? OffsetPolyline(PolylineEntity polyline, double distance, CadPoint sidePoint)
    {
        var points = polyline.Points;
        var segmentCount = polyline.Closed ? points.Count : points.Count - 1;
        if (segmentCount <= 0)
        {
            return null;
        }

        var side = DeterminePolylineSide(points, polyline.Closed, sidePoint);
        var segments = new OffsetSegment[segmentCount];
        for (var i = 0; i < segmentCount; i++)
        {
            var start = points[i];
            var end = points[(i + 1) % points.Count];
            var direction = end - start;
            var length = direction.Length;
            if (length <= Epsilon)
            {
                return null;
            }

            var normal = new CadVector(-direction.Y / length * distance * side, direction.X / length * distance * side);
            segments[i] = new OffsetSegment(start + normal, end + normal);
        }

        var result = new List<CadPoint>(points.Count);
        if (!polyline.Closed)
        {
            result.Add(segments[0].Start);
            for (var i = 1; i < points.Count - 1; i++)
            {
                result.Add(Join(segments[i - 1], segments[i]));
            }
            result.Add(segments[^1].End);
        }
        else
        {
            for (var i = 0; i < points.Count; i++)
            {
                var previous = segments[(i - 1 + segments.Length) % segments.Length];
                var next = segments[i % segments.Length];
                result.Add(Join(previous, next));
            }
        }

        return result.Count >= 2 ? new PolylineEntity(result, polyline.Closed) : null;
    }

    private static double DeterminePolylineSide(IReadOnlyList<CadPoint> points, bool closed, CadPoint sidePoint)
    {
        var segmentCount = closed ? points.Count : points.Count - 1;
        var bestDistance = double.PositiveInfinity;
        var bestCross = 1.0;
        for (var i = 0; i < segmentCount; i++)
        {
            var start = points[i];
            var end = points[(i + 1) % points.Count];
            var direction = end - start;
            var lengthSquared = (direction.X * direction.X) + (direction.Y * direction.Y);
            if (lengthSquared <= Epsilon)
            {
                continue;
            }

            var fromStart = sidePoint - start;
            var t = Math.Clamp(((fromStart.X * direction.X) + (fromStart.Y * direction.Y)) / lengthSquared, 0, 1);
            var closest = new CadPoint(start.X + (direction.X * t), start.Y + (direction.Y * t));
            var distance = (sidePoint - closest).Length;
            if (distance < bestDistance)
            {
                bestDistance = distance;
                bestCross = Cross(direction, fromStart);
            }
        }
        return bestCross >= 0 ? 1.0 : -1.0;
    }

    private static CadPoint Join(OffsetSegment first, OffsetSegment second)
    {
        if (TryInfiniteLineIntersection(first.Start, first.End, second.Start, second.End, out var intersection))
        {
            return intersection;
        }
        return new CadPoint((first.End.X + second.Start.X) / 2, (first.End.Y + second.Start.Y) / 2);
    }

    private static bool TryInfiniteLineIntersection(CadPoint a1, CadPoint a2, CadPoint b1, CadPoint b2, out CadPoint point)
    {
        point = default;
        var r = a2 - a1;
        var s = b2 - b1;
        var denominator = Cross(r, s);
        if (Math.Abs(denominator) <= Epsilon)
        {
            return false;
        }

        var delta = b1 - a1;
        var t = Cross(delta, s) / denominator;
        point = new CadPoint(a1.X + (r.X * t), a1.Y + (r.Y * t));
        return double.IsFinite(point.X) && double.IsFinite(point.Y);
    }

    private static double Cross(CadVector first, CadVector second) =>
        (first.X * second.Y) - (first.Y * second.X);

    private readonly record struct OffsetSegment(CadPoint Start, CadPoint End);
}

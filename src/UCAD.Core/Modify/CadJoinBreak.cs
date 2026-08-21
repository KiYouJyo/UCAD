using UCAD.Core.Entities;
using UCAD.Core.Geometry;

namespace UCAD.Core.Modify;

public static class CadJoinBreak
{
    private const double Epsilon = 1e-9;

    public static bool TryJoin(
        ICadEntity first,
        ICadEntity second,
        double tolerance,
        out PolylineEntity? joined)
    {
        ArgumentNullException.ThrowIfNull(first);
        ArgumentNullException.ThrowIfNull(second);
        joined = null;
        if (!double.IsFinite(tolerance) || tolerance < 0) throw new ArgumentOutOfRangeException(nameof(tolerance));

        var firstPoints = OpenChain(first);
        var secondPoints = OpenChain(second);
        if (firstPoints is null || secondPoints is null) return false;

        if (!TryOrientChains(firstPoints, secondPoints, tolerance, out var left, out var right)) return false;
        var points = new List<CadPoint>(left.Count + right.Count - 1);
        points.AddRange(left);
        if ((left[^1] - right[0]).Length <= tolerance)
        {
            // Snap the shared topological vertex to the midpoint so small drafting
            // tolerances do not leave a microscopic kink/gap in the joined polyline.
            var shared = new CadPoint(
                (left[^1].X + right[0].X) / 2,
                (left[^1].Y + right[0].Y) / 2);
            points[^1] = shared;
            points.AddRange(right.Skip(1));
        }
        else
        {
            points.AddRange(right);
        }

        var closed = points.Count > 2 && (points[0] - points[^1]).Length <= tolerance;
        if (closed) points.RemoveAt(points.Count - 1);
        if (points.Count < 2) return false;
        joined = new PolylineEntity(points, closed, first.Id);
        return true;
    }

    public static bool TryBreak(
        ICadEntity entity,
        CadPoint firstPoint,
        CadPoint secondPoint,
        out IReadOnlyList<ICadEntity> replacements)
    {
        ArgumentNullException.ThrowIfNull(entity);
        return entity switch
        {
            LineEntity line => TryBreakLine(line, firstPoint, secondPoint, out replacements),
            PolylineEntity polyline when !polyline.Closed => TryBreakPolyline(polyline, firstPoint, secondPoint, out replacements),
            _ => Fail(out replacements)
        };
    }

    private static bool TryBreakLine(
        LineEntity line,
        CadPoint firstPoint,
        CadPoint secondPoint,
        out IReadOnlyList<ICadEntity> replacements)
    {
        replacements = [];
        if (line.Length <= Epsilon) return false;
        var t1 = ProjectParameter(line.Start, line.End, firstPoint);
        var t2 = ProjectParameter(line.Start, line.End, secondPoint);
        if (t1 is null || t2 is null) return false;
        var a = Math.Clamp(Math.Min(t1.Value, t2.Value), 0, 1);
        var b = Math.Clamp(Math.Max(t1.Value, t2.Value), 0, 1);
        var firstBreak = Interpolate(line.Start, line.End, a);
        var secondBreak = Interpolate(line.Start, line.End, b);

        var output = new List<ICadEntity>(2);
        if (a > Epsilon)
            output.Add(new LineEntity(line.Start, firstBreak, line.Id));
        if (b < 1 - Epsilon)
        {
            var id = output.Count == 0 ? line.Id : Guid.NewGuid();
            output.Add(new LineEntity(secondBreak, line.End, id));
        }
        if (Math.Abs(a - b) <= Epsilon && a > Epsilon && a < 1 - Epsilon)
        {
            output.Clear();
            output.Add(new LineEntity(line.Start, firstBreak, line.Id));
            output.Add(new LineEntity(firstBreak, line.End));
        }
        replacements = output;
        return output.Count > 0;
    }

    private static bool TryBreakPolyline(
        PolylineEntity polyline,
        CadPoint firstPoint,
        CadPoint secondPoint,
        out IReadOnlyList<ICadEntity> replacements)
    {
        replacements = [];
        var first = LocateOnPolyline(polyline, firstPoint);
        var second = LocateOnPolyline(polyline, secondPoint);
        if (first is null || second is null) return false;
        if (first.Value.DistanceAlong > second.Value.DistanceAlong)
            (first, second) = (second, first);

        if (Math.Abs(first.Value.DistanceAlong - second.Value.DistanceAlong) <= Epsilon)
        {
            var prefix = BuildPolylineRange(polyline, 0, first.Value.DistanceAlong);
            var suffix = BuildPolylineRange(polyline, first.Value.DistanceAlong, polyline.Length);
            var split = new List<ICadEntity>();
            if (prefix.Count >= 2) split.Add(new PolylineEntity(prefix, false, polyline.Id));
            if (suffix.Count >= 2) split.Add(new PolylineEntity(suffix));
            replacements = split;
            return split.Count > 0;
        }

        var before = BuildPolylineRange(polyline, 0, first.Value.DistanceAlong);
        var after = BuildPolylineRange(polyline, second.Value.DistanceAlong, polyline.Length);
        var output = new List<ICadEntity>();
        if (before.Count >= 2) output.Add(new PolylineEntity(before, false, polyline.Id));
        if (after.Count >= 2)
        {
            var id = output.Count == 0 ? polyline.Id : Guid.NewGuid();
            output.Add(new PolylineEntity(after, false, id));
        }
        replacements = output;
        return output.Count > 0;
    }

    private static IReadOnlyList<CadPoint>? OpenChain(ICadEntity entity) => entity switch
    {
        LineEntity line => [line.Start, line.End],
        PolylineEntity polyline when !polyline.Closed => polyline.Points.ToArray(),
        _ => null
    };

    private static bool TryOrientChains(
        IReadOnlyList<CadPoint> first,
        IReadOnlyList<CadPoint> second,
        double tolerance,
        out IReadOnlyList<CadPoint> left,
        out IReadOnlyList<CadPoint> right)
    {
        var candidates = new[]
        {
            (Left: first, Right: second, Distance: (first[^1] - second[0]).Length),
            (Left: first, Right: second.Reverse().ToArray(), Distance: (first[^1] - second[^1]).Length),
            (Left: first.Reverse().ToArray(), Right: second, Distance: (first[0] - second[0]).Length),
            (Left: first.Reverse().ToArray(), Right: second.Reverse().ToArray(), Distance: (first[0] - second[^1]).Length)
        };
        var best = candidates.OrderBy(candidate => candidate.Distance).First();
        if (best.Distance > tolerance)
        {
            left = [];
            right = [];
            return false;
        }
        left = best.Left;
        right = best.Right;
        return true;
    }

    private static double? ProjectParameter(CadPoint start, CadPoint end, CadPoint point)
    {
        var direction = end - start;
        var denominator = (direction.X * direction.X) + (direction.Y * direction.Y);
        if (denominator <= Epsilon) return null;
        var offset = point - start;
        return ((offset.X * direction.X) + (offset.Y * direction.Y)) / denominator;
    }

    private static PolylineLocation? LocateOnPolyline(PolylineEntity polyline, CadPoint point)
    {
        var bestDistance = double.PositiveInfinity;
        PolylineLocation? best = null;
        var cumulative = 0.0;
        for (var i = 1; i < polyline.Points.Count; i++)
        {
            var start = polyline.Points[i - 1];
            var end = polyline.Points[i];
            var segmentLength = (end - start).Length;
            if (segmentLength <= Epsilon) continue;
            var t = Math.Clamp(ProjectParameter(start, end, point) ?? 0, 0, 1);
            var projected = Interpolate(start, end, t);
            var distance = (point - projected).Length;
            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = new PolylineLocation(cumulative + (segmentLength * t), projected);
            }
            cumulative += segmentLength;
        }
        return best;
    }

    private static List<CadPoint> BuildPolylineRange(PolylineEntity polyline, double fromDistance, double toDistance)
    {
        fromDistance = Math.Clamp(fromDistance, 0, polyline.Length);
        toDistance = Math.Clamp(toDistance, 0, polyline.Length);
        if (toDistance < fromDistance) (fromDistance, toDistance) = (toDistance, fromDistance);

        var result = new List<CadPoint>();
        var cumulative = 0.0;
        for (var i = 1; i < polyline.Points.Count; i++)
        {
            var start = polyline.Points[i - 1];
            var end = polyline.Points[i];
            var segmentLength = (end - start).Length;
            if (segmentLength <= Epsilon) continue;
            var segmentStart = cumulative;
            var segmentEnd = cumulative + segmentLength;
            if (segmentEnd < fromDistance - Epsilon)
            {
                cumulative = segmentEnd;
                continue;
            }
            if (segmentStart > toDistance + Epsilon) break;

            var localFrom = Math.Clamp((fromDistance - segmentStart) / segmentLength, 0, 1);
            var localTo = Math.Clamp((toDistance - segmentStart) / segmentLength, 0, 1);
            if (localTo < 0 || localFrom > 1)
            {
                cumulative = segmentEnd;
                continue;
            }
            var a = Interpolate(start, end, localFrom);
            var b = Interpolate(start, end, localTo);
            AddDistinct(result, a);
            AddDistinct(result, b);
            cumulative = segmentEnd;
        }
        return result;
    }

    private static CadPoint Interpolate(CadPoint start, CadPoint end, double t) =>
        new(start.X + ((end.X - start.X) * t), start.Y + ((end.Y - start.Y) * t));

    private static void AddDistinct(List<CadPoint> points, CadPoint point)
    {
        if (points.Count == 0 || (points[^1] - point).Length > Epsilon) points.Add(point);
    }

    private static bool Fail(out IReadOnlyList<ICadEntity> replacements)
    {
        replacements = [];
        return false;
    }

    private readonly record struct PolylineLocation(double DistanceAlong, CadPoint Point);
}
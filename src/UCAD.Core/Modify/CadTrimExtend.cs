using UCAD.Core.Entities;
using UCAD.Core.Geometry;
using UCAD.Core.Interaction;

namespace UCAD.Core.Modify;

/// <summary>
/// Geometry engine for AutoCAD-style quick TRIM/EXTEND: every other visible entity
/// can act as a boundary, while the picked point chooses the segment/end to edit.
/// </summary>
public static class CadTrimExtend
{
    private const double Epsilon = 1e-8;

    public static bool TryTrim(
        ICadEntity target,
        IEnumerable<ICadEntity> boundaries,
        CadPoint pickPoint,
        out IReadOnlyList<ICadEntity> replacements)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(boundaries);
        var boundarySnapshot = boundaries.Where(boundary => boundary.Id != target.Id).ToArray();

        replacements = target switch
        {
            LineEntity line => TrimLine(line, boundarySnapshot, pickPoint),
            PolylineEntity polyline => TrimPolyline(polyline, boundarySnapshot, pickPoint),
            CircleEntity circle => TrimCircle(circle, boundarySnapshot, pickPoint),
            ArcEntity arc => TrimArc(arc, boundarySnapshot, pickPoint),
            _ => []
        };
        return replacements.Count > 0;
    }

    public static bool TryExtend(
        ICadEntity target,
        IEnumerable<ICadEntity> boundaries,
        CadPoint pickPoint,
        out ICadEntity? replacement)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(boundaries);
        var boundarySnapshot = boundaries.Where(boundary => boundary.Id != target.Id).ToArray();
        replacement = target switch
        {
            LineEntity line => ExtendLine(line, boundarySnapshot, pickPoint),
            PolylineEntity polyline => ExtendPolyline(polyline, boundarySnapshot, pickPoint),
            ArcEntity arc => ExtendArc(arc, boundarySnapshot, pickPoint),
            _ => null
        };
        return replacement is not null;
    }

    private static IReadOnlyList<ICadEntity> TrimLine(LineEntity line, IReadOnlyList<ICadEntity> boundaries, CadPoint pickPoint)
    {
        var direction = line.End - line.Start;
        var lengthSquared = Dot(direction, direction);
        if (lengthSquared <= Epsilon)
        {
            return [];
        }

        var cuts = new List<double>();
        foreach (var boundary in boundaries)
        foreach (var point in CadEntityGeometry.Intersections(line, boundary))
        {
            var t = Dot(point - line.Start, direction) / lengthSquared;
            AddDistinct(cuts, t, 0, 1);
        }

        if (cuts.Count == 0)
        {
            return [];
        }

        var breaks = new List<double> { 0 };
        breaks.AddRange(cuts.OrderBy(value => value));
        breaks.Add(1);
        var pickT = Math.Clamp(Dot(pickPoint - line.Start, direction) / lengthSquared, 0, 1);
        var removeIndex = ClosestIntervalIndex(breaks, pickT);
        var result = new List<ICadEntity>();
        for (var i = 0; i < breaks.Count - 1; i++)
        {
            if (i == removeIndex || breaks[i + 1] - breaks[i] <= Epsilon)
            {
                continue;
            }
            var start = Lerp(line.Start, line.End, breaks[i]);
            var end = Lerp(line.Start, line.End, breaks[i + 1]);
            result.Add(new LineEntity(start, end, result.Count == 0 ? line.Id : Guid.NewGuid()));
        }
        return result;
    }

    private static IReadOnlyList<ICadEntity> TrimPolyline(PolylineEntity polyline, IReadOnlyList<ICadEntity> boundaries, CadPoint pickPoint)
    {
        var totalSegments = polyline.Closed ? polyline.Points.Count : polyline.Points.Count - 1;
        if (totalSegments <= 0)
        {
            return [];
        }

        var cuts = new List<double>();
        foreach (var boundary in boundaries)
        {
            for (var i = 0; i < totalSegments; i++)
            {
                var start = polyline.Points[i];
                var end = polyline.Points[(i + 1) % polyline.Points.Count];
                var probe = new LineEntity(start, end);
                foreach (var point in CadEntityGeometry.Intersections(probe, boundary))
                {
                    var t = SegmentParameter(start, end, point);
                    AddDistinct(cuts, i + t, 0, totalSegments);
                }
            }
        }

        if ((!polyline.Closed && cuts.Count == 0) || (polyline.Closed && cuts.Count < 2))
        {
            return [];
        }

        var pickParameter = ClosestPolylineParameter(polyline, pickPoint);
        if (!polyline.Closed)
        {
            var breaks = new List<double> { 0 };
            breaks.AddRange(cuts.OrderBy(value => value));
            breaks.Add(totalSegments);
            var removeIndex = ClosestIntervalIndex(breaks, pickParameter);
            var removeStart = breaks[removeIndex];
            var removeEnd = breaks[removeIndex + 1];
            var pieces = new List<ICadEntity>();
            AddPolylinePiece(pieces, polyline, 0, removeStart, totalSegments, polyline.Id);
            AddPolylinePiece(pieces, polyline, removeEnd, totalSegments, totalSegments,
                pieces.Count == 0 ? polyline.Id : Guid.NewGuid());
            return pieces;
        }

        var sorted = cuts.OrderBy(value => value).ToArray();
        var (removeStartClosed, removeEndClosed) = FindClosedInterval(sorted, totalSegments, pickParameter);
        var keepStart = removeEndClosed;
        var keepEnd = removeStartClosed <= removeEndClosed
            ? removeStartClosed + totalSegments
            : removeStartClosed;
        var kept = BuildPolylinePiece(polyline, keepStart, keepEnd, totalSegments);
        return kept.Count >= 2 ? [new PolylineEntity(kept, closed: false, polyline.Id)] : [];
    }

    private static IReadOnlyList<ICadEntity> TrimCircle(CircleEntity circle, IReadOnlyList<ICadEntity> boundaries, CadPoint pickPoint)
    {
        var cuts = new List<double>();
        foreach (var boundary in boundaries)
        foreach (var point in CadEntityGeometry.Intersections(circle, boundary))
        {
            AddDistinctAngle(cuts, Math.Atan2(point.Y - circle.Center.Y, point.X - circle.Center.X));
        }
        if (cuts.Count < 2)
        {
            return [];
        }

        var sorted = cuts.OrderBy(value => value).ToArray();
        var pickAngle = NormalizePositive(Math.Atan2(pickPoint.Y - circle.Center.Y, pickPoint.X - circle.Center.X));
        var (removeStart, removeEnd) = FindClosedAngleInterval(sorted, pickAngle);
        var result = new List<ICadEntity>();

        for (var i = 0; i < sorted.Length; i++)
        {
            var start = sorted[i];
            var end = i == sorted.Length - 1 ? sorted[0] + Math.Tau : sorted[i + 1];
            if (SameInterval(start, end, removeStart, removeEnd))
            {
                continue;
            }
            var sweep = end - start;
            if (sweep <= Epsilon)
            {
                continue;
            }
            result.Add(ArcEntity.Create(
                circle.Center,
                circle.Radius,
                NormalizeSigned(start),
                sweep,
                result.Count == 0 ? circle.Id : Guid.NewGuid()));
        }
        return result;
    }

    private static IReadOnlyList<ICadEntity> TrimArc(ArcEntity arc, IReadOnlyList<ICadEntity> boundaries, CadPoint pickPoint)
    {
        var cuts = new List<double>();
        foreach (var boundary in boundaries)
        foreach (var point in CadEntityGeometry.Intersections(arc, boundary))
        {
            var fraction = ArcFraction(arc, point);
            AddDistinct(cuts, fraction, 0, 1);
        }
        if (cuts.Count == 0)
        {
            return [];
        }

        var breaks = new List<double> { 0 };
        breaks.AddRange(cuts.OrderBy(value => value));
        breaks.Add(1);
        var pickFraction = Math.Clamp(ArcFraction(arc, pickPoint), 0, 1);
        var removeIndex = ClosestIntervalIndex(breaks, pickFraction);
        var result = new List<ICadEntity>();
        for (var i = 0; i < breaks.Count - 1; i++)
        {
            if (i == removeIndex || breaks[i + 1] - breaks[i] <= Epsilon)
            {
                continue;
            }
            var startAngle = arc.StartAngleRadians + (arc.SweepAngleRadians * breaks[i]);
            var sweep = arc.SweepAngleRadians * (breaks[i + 1] - breaks[i]);
            result.Add(ArcEntity.Create(
                arc.Center,
                arc.Radius,
                startAngle,
                sweep,
                result.Count == 0 ? arc.Id : Guid.NewGuid()));
        }
        return result;
    }

    private static LineEntity? ExtendLine(LineEntity line, IReadOnlyList<ICadEntity> boundaries, CadPoint pickPoint)
    {
        var extendStart = (pickPoint - line.Start).Length <= (pickPoint - line.End).Length;
        var anchor = extendStart ? line.Start : line.End;
        var previous = extendStart ? line.End : line.Start;
        if (!TryFindRayBoundary(anchor, anchor - previous, boundaries, out var targetPoint))
        {
            return null;
        }
        return extendStart
            ? new LineEntity(targetPoint, line.End, line.Id)
            : new LineEntity(line.Start, targetPoint, line.Id);
    }

    private static PolylineEntity? ExtendPolyline(PolylineEntity polyline, IReadOnlyList<ICadEntity> boundaries, CadPoint pickPoint)
    {
        if (polyline.Closed || polyline.Points.Count < 2)
        {
            return null;
        }

        var points = polyline.Points.ToArray();
        var extendStart = (pickPoint - points[0]).Length <= (pickPoint - points[^1]).Length;
        var anchor = extendStart ? points[0] : points[^1];
        var previous = extendStart ? points[1] : points[^2];
        if (!TryFindRayBoundary(anchor, anchor - previous, boundaries, out var targetPoint))
        {
            return null;
        }

        if (extendStart) points[0] = targetPoint;
        else points[^1] = targetPoint;
        return new PolylineEntity(points, closed: false, polyline.Id);
    }

    private static ArcEntity? ExtendArc(ArcEntity arc, IReadOnlyList<ICadEntity> boundaries, CadPoint pickPoint)
    {
        var extendStart = (pickPoint - arc.StartPoint).Length <= (pickPoint - arc.EndPoint).Length;
        var sign = Math.Sign(arc.SweepAngleRadians);
        var referenceAngle = extendStart
            ? arc.StartAngleRadians
            : arc.StartAngleRadians + arc.SweepAngleRadians;
        var bestDelta = double.PositiveInfinity;

        var circle = new CircleEntity(arc.Center, arc.Radius);
        foreach (var boundary in boundaries)
        foreach (var point in CadEntityGeometry.Intersections(circle, boundary))
        {
            var angle = Math.Atan2(point.Y - arc.Center.Y, point.X - arc.Center.X);
            var delta = extendStart
                ? NormalizePositive(-sign * (angle - referenceAngle))
                : NormalizePositive(sign * (angle - referenceAngle));
            if (delta <= Epsilon || Math.Abs(arc.SweepAngleRadians) + delta > Math.Tau + Epsilon)
            {
                continue;
            }
            bestDelta = Math.Min(bestDelta, delta);
        }

        if (!double.IsFinite(bestDelta))
        {
            return null;
        }

        var newStart = extendStart ? arc.StartAngleRadians - (sign * bestDelta) : arc.StartAngleRadians;
        var newSweep = arc.SweepAngleRadians + (sign * bestDelta);
        return ArcEntity.Create(arc.Center, arc.Radius, newStart, newSweep, arc.Id);
    }

    private static bool TryFindRayBoundary(
        CadPoint origin,
        CadVector direction,
        IReadOnlyList<ICadEntity> boundaries,
        out CadPoint targetPoint)
    {
        targetPoint = default;
        var directionLength = direction.Length;
        if (directionLength <= Epsilon)
        {
            return false;
        }
        var ray = new CadVector(direction.X / directionLength, direction.Y / directionLength);
        var bestDistance = double.PositiveInfinity;

        foreach (var boundary in boundaries)
        foreach (var candidate in RayIntersections(origin, ray, boundary))
        {
            var delta = candidate - origin;
            var distance = Dot(delta, ray);
            if (distance > Epsilon && distance < bestDistance)
            {
                bestDistance = distance;
                targetPoint = candidate;
            }
        }
        return double.IsFinite(bestDistance);
    }

    private static IEnumerable<CadPoint> RayIntersections(CadPoint origin, CadVector ray, ICadEntity boundary)
    {
        switch (boundary)
        {
            case LineEntity line:
                if (TryRaySegmentIntersection(origin, ray, line.Start, line.End, out var linePoint))
                    yield return linePoint;
                yield break;

            case PolylineEntity polyline:
                var count = polyline.Closed ? polyline.Points.Count : polyline.Points.Count - 1;
                for (var i = 0; i < count; i++)
                {
                    var start = polyline.Points[i];
                    var end = polyline.Points[(i + 1) % polyline.Points.Count];
                    if (TryRaySegmentIntersection(origin, ray, start, end, out var polylinePoint))
                        yield return polylinePoint;
                }
                yield break;

            case CircleEntity circle:
                foreach (var point in RayCircleIntersections(origin, ray, circle.Center, circle.Radius))
                    yield return point;
                yield break;

            case ArcEntity arc:
                foreach (var point in RayCircleIntersections(origin, ray, arc.Center, arc.Radius))
                    if (CadEntityGeometry.IsPointOnArc(arc, point)) yield return point;
                yield break;
        }
    }

    private static bool TryRaySegmentIntersection(
        CadPoint origin,
        CadVector ray,
        CadPoint segmentStart,
        CadPoint segmentEnd,
        out CadPoint point)
    {
        point = default;
        var segment = segmentEnd - segmentStart;
        var denominator = Cross(ray, segment);
        if (Math.Abs(denominator) <= Epsilon)
        {
            return false;
        }
        var delta = segmentStart - origin;
        var t = Cross(delta, segment) / denominator;
        var u = Cross(delta, ray) / denominator;
        if (t <= Epsilon || u < -Epsilon || u > 1 + Epsilon)
        {
            return false;
        }
        point = new CadPoint(origin.X + (ray.X * t), origin.Y + (ray.Y * t));
        return true;
    }

    private static IEnumerable<CadPoint> RayCircleIntersections(
        CadPoint origin,
        CadVector ray,
        CadPoint center,
        double radius)
    {
        var offset = origin - center;
        var b = 2 * Dot(offset, ray);
        var c = Dot(offset, offset) - (radius * radius);
        var discriminant = (b * b) - (4 * c);
        if (discriminant < -Epsilon)
        {
            yield break;
        }

        var root = Math.Sqrt(Math.Max(0, discriminant));
        foreach (var t in new[] { (-b - root) / 2, (-b + root) / 2 })
        {
            if (t > Epsilon)
                yield return new CadPoint(origin.X + (ray.X * t), origin.Y + (ray.Y * t));
        }
    }

    private static void AddPolylinePiece(
        ICollection<ICadEntity> output,
        PolylineEntity source,
        double start,
        double end,
        int totalSegments,
        Guid identity)
    {
        if (end - start <= Epsilon)
        {
            return;
        }
        var points = BuildPolylinePiece(source, start, end, totalSegments);
        if (points.Count < 2)
        {
            return;
        }
        output.Add(points.Count == 2
            ? new LineEntity(points[0], points[1], identity)
            : new PolylineEntity(points, closed: false, identity));
    }

    private static List<CadPoint> BuildPolylinePiece(PolylineEntity source, double start, double end, int totalSegments)
    {
        var points = new List<CadPoint> { PointAtPolylineParameter(source, start, totalSegments) };
        var firstVertex = (int)Math.Floor(start + Epsilon) + 1;
        var lastVertex = (int)Math.Ceiling(end - Epsilon) - 1;
        for (var vertex = firstVertex; vertex <= lastVertex; vertex++)
        {
            var index = Mod(vertex, source.Points.Count);
            var point = source.Points[index];
            if ((point - points[^1]).Length > Epsilon) points.Add(point);
        }
        var final = PointAtPolylineParameter(source, end, totalSegments);
        if ((final - points[^1]).Length > Epsilon) points.Add(final);
        return points;
    }

    private static CadPoint PointAtPolylineParameter(PolylineEntity source, double parameter, int totalSegments)
    {
        if (source.Closed)
        {
            parameter %= totalSegments;
            if (parameter < 0) parameter += totalSegments;
        }
        else
        {
            parameter = Math.Clamp(parameter, 0, totalSegments);
        }

        if (!source.Closed && parameter >= totalSegments - Epsilon)
        {
            return source.Points[^1];
        }

        var segment = Math.Min((int)Math.Floor(parameter), totalSegments - 1);
        var local = parameter - Math.Floor(parameter);
        var start = source.Points[segment % source.Points.Count];
        var end = source.Points[(segment + 1) % source.Points.Count];
        return Lerp(start, end, local);
    }

    private static double ClosestPolylineParameter(PolylineEntity polyline, CadPoint point)
    {
        var count = polyline.Closed ? polyline.Points.Count : polyline.Points.Count - 1;
        var bestDistance = double.PositiveInfinity;
        var bestParameter = 0.0;
        for (var i = 0; i < count; i++)
        {
            var start = polyline.Points[i];
            var end = polyline.Points[(i + 1) % polyline.Points.Count];
            var t = SegmentParameter(start, end, point);
            var candidate = Lerp(start, end, t);
            var distance = (point - candidate).Length;
            if (distance < bestDistance)
            {
                bestDistance = distance;
                bestParameter = i + t;
            }
        }
        return bestParameter;
    }

    private static double SegmentParameter(CadPoint start, CadPoint end, CadPoint point)
    {
        var direction = end - start;
        var lengthSquared = Dot(direction, direction);
        if (lengthSquared <= Epsilon) return 0;
        return Math.Clamp(Dot(point - start, direction) / lengthSquared, 0, 1);
    }

    private static (double Start, double End) FindClosedInterval(IReadOnlyList<double> sorted, double total, double pick)
    {
        for (var i = 0; i < sorted.Count; i++)
        {
            var start = sorted[i];
            var end = i == sorted.Count - 1 ? sorted[0] + total : sorted[i + 1];
            var normalizedPick = pick < start ? pick + total : pick;
            if (normalizedPick >= start - Epsilon && normalizedPick <= end + Epsilon)
                return (start, end > total ? end - total : end);
        }
        return (sorted[0], sorted[1]);
    }

    private static (double Start, double End) FindClosedAngleInterval(IReadOnlyList<double> sorted, double pick)
    {
        for (var i = 0; i < sorted.Count; i++)
        {
            var start = sorted[i];
            var end = i == sorted.Count - 1 ? sorted[0] + Math.Tau : sorted[i + 1];
            var normalizedPick = pick < start ? pick + Math.Tau : pick;
            if (normalizedPick >= start - Epsilon && normalizedPick <= end + Epsilon)
                return (start, end);
        }
        return (sorted[0], sorted[1]);
    }

    private static int ClosestIntervalIndex(IReadOnlyList<double> breaks, double pick)
    {
        for (var i = 0; i < breaks.Count - 1; i++)
        {
            if (pick >= breaks[i] - Epsilon && pick <= breaks[i + 1] + Epsilon)
                return i;
        }

        var bestIndex = 0;
        var bestDistance = double.PositiveInfinity;
        for (var i = 0; i < breaks.Count - 1; i++)
        {
            var midpoint = (breaks[i] + breaks[i + 1]) / 2;
            var distance = Math.Abs(pick - midpoint);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                bestIndex = i;
            }
        }
        return bestIndex;
    }

    private static double ArcFraction(ArcEntity arc, CadPoint point)
    {
        var angle = Math.Atan2(point.Y - arc.Center.Y, point.X - arc.Center.X);
        return arc.SweepAngleRadians >= 0
            ? NormalizePositive(angle - arc.StartAngleRadians) / arc.SweepAngleRadians
            : NormalizePositive(arc.StartAngleRadians - angle) / -arc.SweepAngleRadians;
    }

    private static bool SameInterval(double start, double end, double otherStart, double otherEnd) =>
        Math.Abs(start - otherStart) <= Epsilon && Math.Abs(end - otherEnd) <= Epsilon;

    private static void AddDistinct(List<double> values, double value, double min, double max)
    {
        if (!double.IsFinite(value) || value <= min + Epsilon || value >= max - Epsilon) return;
        if (values.All(existing => Math.Abs(existing - value) > Epsilon)) values.Add(value);
    }

    private static void AddDistinctAngle(List<double> values, double angle)
    {
        angle = NormalizePositive(angle);
        if (values.All(existing => AngularDistance(existing, angle) > Epsilon)) values.Add(angle);
    }

    private static double AngularDistance(double first, double second)
    {
        var difference = Math.Abs(first - second) % Math.Tau;
        return Math.Min(difference, Math.Tau - difference);
    }

    private static CadPoint Lerp(CadPoint start, CadPoint end, double t) =>
        new(start.X + ((end.X - start.X) * t), start.Y + ((end.Y - start.Y) * t));

    private static double Dot(CadVector first, CadVector second) =>
        (first.X * second.X) + (first.Y * second.Y);

    private static double Cross(CadVector first, CadVector second) =>
        (first.X * second.Y) - (first.Y * second.X);

    private static double NormalizePositive(double angle)
    {
        var normalized = angle % Math.Tau;
        return normalized < 0 ? normalized + Math.Tau : normalized;
    }

    private static double NormalizeSigned(double angle)
    {
        var normalized = NormalizePositive(angle);
        return normalized > Math.PI ? normalized - Math.Tau : normalized;
    }

    private static int Mod(int value, int modulus)
    {
        var result = value % modulus;
        return result < 0 ? result + modulus : result;
    }
}

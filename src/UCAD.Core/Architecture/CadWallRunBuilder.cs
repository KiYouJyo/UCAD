using UCAD.Core.Entities;
using UCAD.Core.Geometry;

namespace UCAD.Core.Architecture;

public static class CadWallRunBuilder
{
    private const double Epsilon = 1e-9;
    private const double DefaultMiterLimit = 8.0;

    public static PolylineEntity Create(
        IEnumerable<CadPoint> centerline,
        double thickness,
        double miterLimit = DefaultMiterLimit)
    {
        ArgumentNullException.ThrowIfNull(centerline);
        if (!double.IsFinite(thickness) || thickness <= Epsilon) throw new ArgumentOutOfRangeException(nameof(thickness));
        if (!double.IsFinite(miterLimit) || miterLimit < 1) throw new ArgumentOutOfRangeException(nameof(miterLimit));

        var points = RemoveConsecutiveDuplicates(centerline).ToArray();
        if (points.Length < 2) throw new ArgumentException("A wall run requires at least two distinct centerline points.", nameof(centerline));

        var directions = new CadVector[points.Length - 1];
        var normals = new CadVector[points.Length - 1];
        for (var index = 0; index < directions.Length; index++)
        {
            var vector = points[index + 1] - points[index];
            var length = vector.Length;
            if (!double.IsFinite(length) || length <= Epsilon)
                throw new ArgumentException("Wall centerline contains a zero-length segment.", nameof(centerline));
            directions[index] = new CadVector(vector.X / length, vector.Y / length);
            normals[index] = new CadVector(-directions[index].Y, directions[index].X);
        }

        var half = thickness / 2.0;
        var left = new CadPoint[points.Length];
        var right = new CadPoint[points.Length];
        left[0] = Add(points[0], normals[0], half);
        right[0] = Add(points[0], normals[0], -half);
        left[^1] = Add(points[^1], normals[^1], half);
        right[^1] = Add(points[^1], normals[^1], -half);

        for (var index = 1; index < points.Length - 1; index++)
        {
            left[index] = ResolveOffsetJoin(
                points[index],
                directions[index - 1], normals[index - 1],
                directions[index], normals[index],
                half,
                miterLimit);
            right[index] = ResolveOffsetJoin(
                points[index],
                directions[index - 1], normals[index - 1],
                directions[index], normals[index],
                -half,
                miterLimit);
        }

        var outline = new List<CadPoint>(points.Length * 2);
        outline.AddRange(left);
        for (var index = right.Length - 1; index >= 0; index--) outline.Add(right[index]);
        return new PolylineEntity(outline, closed: true);
    }

    private static CadPoint ResolveOffsetJoin(
        CadPoint vertex,
        CadVector previousDirection,
        CadVector previousNormal,
        CadVector nextDirection,
        CadVector nextNormal,
        double offset,
        double miterLimit)
    {
        var previousPoint = Add(vertex, previousNormal, offset);
        var nextPoint = Add(vertex, nextNormal, offset);
        if (TryLineIntersection(previousPoint, previousDirection, nextPoint, nextDirection, out var intersection))
        {
            var miterLength = (intersection - vertex).Length;
            if (miterLength <= Math.Abs(offset) * miterLimit + Epsilon) return intersection;
        }

        // Parallel, near-180-degree or excessively sharp joins fall back to a bounded
        // averaged-normal corner instead of producing an arbitrarily distant spike.
        var average = new CadVector(previousNormal.X + nextNormal.X, previousNormal.Y + nextNormal.Y);
        var averageLength = average.Length;
        if (averageLength <= Epsilon) return nextPoint;
        var unit = new CadVector(average.X / averageLength, average.Y / averageLength);
        return Add(vertex, unit, offset);
    }

    private static bool TryLineIntersection(
        CadPoint firstPoint,
        CadVector firstDirection,
        CadPoint secondPoint,
        CadVector secondDirection,
        out CadPoint intersection)
    {
        var denominator = Cross(firstDirection, secondDirection);
        if (Math.Abs(denominator) <= Epsilon)
        {
            intersection = default;
            return false;
        }
        var delta = secondPoint - firstPoint;
        var t = Cross(delta, secondDirection) / denominator;
        intersection = new CadPoint(
            firstPoint.X + (firstDirection.X * t),
            firstPoint.Y + (firstDirection.Y * t));
        return double.IsFinite(intersection.X) && double.IsFinite(intersection.Y);
    }

    private static IEnumerable<CadPoint> RemoveConsecutiveDuplicates(IEnumerable<CadPoint> points)
    {
        CadPoint? previous = null;
        foreach (var point in points)
        {
            if (previous is CadPoint value && (point - value).Length <= Epsilon) continue;
            yield return point;
            previous = point;
        }
    }

    private static double Cross(CadVector first, CadVector second) =>
        (first.X * second.Y) - (first.Y * second.X);

    private static CadPoint Add(CadPoint point, CadVector direction, double distance) =>
        new(point.X + (direction.X * distance), point.Y + (direction.Y * distance));
}

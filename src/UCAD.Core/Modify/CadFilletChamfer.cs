using UCAD.Core.Entities;
using UCAD.Core.Geometry;

namespace UCAD.Core.Modify;

/// <summary>
/// Foundational two-line corner editing. Lines are allowed to extend to the computed
/// corner, matching CAD FILLET/CHAMFER behavior instead of requiring pre-intersection.
/// Pick points choose which ray from the corner is retained.
/// </summary>
public static class CadFilletChamfer
{
    private const double Epsilon = 1e-9;

    public static bool TryFillet(
        LineEntity first,
        CadPoint firstPick,
        LineEntity second,
        CadPoint secondPick,
        double radius,
        out LineEntity? firstResult,
        out LineEntity? secondResult,
        out ArcEntity? fillet)
    {
        ArgumentNullException.ThrowIfNull(first);
        ArgumentNullException.ThrowIfNull(second);
        firstResult = null;
        secondResult = null;
        fillet = null;
        if (!double.IsFinite(radius) || radius <= Epsilon) return false;
        if (!TryCorner(first, firstPick, second, secondPick, out var corner)) return false;

        var dot = Math.Clamp(Dot(corner.FirstRay, corner.SecondRay), -1.0, 1.0);
        var angle = Math.Acos(dot);
        if (angle <= 1e-7 || Math.Abs(Math.PI - angle) <= 1e-7) return false;

        var tangentDistance = radius / Math.Tan(angle / 2.0);
        if (!double.IsFinite(tangentDistance) || tangentDistance <= Epsilon) return false;

        var firstTangent = Add(corner.Intersection, corner.FirstRay, tangentDistance);
        var secondTangent = Add(corner.Intersection, corner.SecondRay, tangentDistance);
        var bisector = Normalize(new CadVector(
            corner.FirstRay.X + corner.SecondRay.X,
            corner.FirstRay.Y + corner.SecondRay.Y));
        if (bisector is null) return false;
        var centerDistance = radius / Math.Sin(angle / 2.0);
        var center = Add(corner.Intersection, bisector.Value, centerDistance);

        if (!TryBuildRetainedLine(first, corner.Intersection, corner.FirstRay, firstTangent, out firstResult) ||
            !TryBuildRetainedLine(second, corner.Intersection, corner.SecondRay, secondTangent, out secondResult))
            return false;

        var start = Math.Atan2(firstTangent.Y - center.Y, firstTangent.X - center.X);
        var end = Math.Atan2(secondTangent.Y - center.Y, secondTangent.X - center.X);
        var sweep = NormalizeSignedMinor(end - start);
        if (Math.Abs(sweep) <= Epsilon) return false;
        fillet = ArcEntity.Create(center, radius, start, sweep);
        return true;
    }

    public static bool TryChamfer(
        LineEntity first,
        CadPoint firstPick,
        LineEntity second,
        CadPoint secondPick,
        double firstDistance,
        double secondDistance,
        out LineEntity? firstResult,
        out LineEntity? secondResult,
        out LineEntity? chamfer)
    {
        ArgumentNullException.ThrowIfNull(first);
        ArgumentNullException.ThrowIfNull(second);
        firstResult = null;
        secondResult = null;
        chamfer = null;
        if (!double.IsFinite(firstDistance) || firstDistance <= Epsilon ||
            !double.IsFinite(secondDistance) || secondDistance <= Epsilon)
            return false;
        if (!TryCorner(first, firstPick, second, secondPick, out var corner)) return false;

        var firstPoint = Add(corner.Intersection, corner.FirstRay, firstDistance);
        var secondPoint = Add(corner.Intersection, corner.SecondRay, secondDistance);
        if ((secondPoint - firstPoint).Length <= Epsilon) return false;

        if (!TryBuildRetainedLine(first, corner.Intersection, corner.FirstRay, firstPoint, out firstResult) ||
            !TryBuildRetainedLine(second, corner.Intersection, corner.SecondRay, secondPoint, out secondResult))
            return false;

        chamfer = new LineEntity(firstPoint, secondPoint);
        return true;
    }

    private static bool TryCorner(
        LineEntity first,
        CadPoint firstPick,
        LineEntity second,
        CadPoint secondPick,
        out Corner corner)
    {
        corner = default;
        var firstDirection = first.End - first.Start;
        var secondDirection = second.End - second.Start;
        if (firstDirection.Length <= Epsilon || secondDirection.Length <= Epsilon) return false;
        if (!TryInfiniteLineIntersection(first.Start, firstDirection, second.Start, secondDirection, out var intersection)) return false;

        var firstRay = RayTowardPick(intersection, firstDirection, firstPick);
        var secondRay = RayTowardPick(intersection, secondDirection, secondPick);
        if (firstRay is null || secondRay is null) return false;
        if (Math.Abs(Cross(firstRay.Value, secondRay.Value)) <= Epsilon) return false;

        corner = new Corner(intersection, firstRay.Value, secondRay.Value);
        return true;
    }

    private static CadVector? RayTowardPick(CadPoint intersection, CadVector lineDirection, CadPoint pick)
    {
        var normalized = Normalize(lineDirection);
        if (normalized is null) return null;
        var towardPick = pick - intersection;
        var projection = Dot(towardPick, normalized.Value);
        if (Math.Abs(projection) <= Epsilon)
        {
            // If the pick is exactly at the mathematical corner, retain the direction
            // containing the farther existing endpoint for deterministic behavior.
            return normalized;
        }
        return projection >= 0
            ? normalized
            : new CadVector(-normalized.Value.X, -normalized.Value.Y);
    }

    private static bool TryBuildRetainedLine(
        LineEntity source,
        CadPoint intersection,
        CadVector retainedRay,
        CadPoint cornerPoint,
        out LineEntity? result)
    {
        result = null;
        var startProjection = Dot(source.Start - intersection, retainedRay);
        var endProjection = Dot(source.End - intersection, retainedRay);
        var retainedEndpoint = startProjection >= endProjection ? source.Start : source.End;

        // FILLET/CHAMFER may extend a short source line. If neither original endpoint is
        // on the retained ray, use whichever projects less negatively and still extend.
        if ((retainedEndpoint - cornerPoint).Length <= Epsilon) return false;
        result = ReferenceEqualsEndpoint(source.Start, retainedEndpoint)
            ? new LineEntity(retainedEndpoint, cornerPoint, source.Id)
            : new LineEntity(cornerPoint, retainedEndpoint, source.Id);
        return true;
    }

    private static bool ReferenceEqualsEndpoint(CadPoint first, CadPoint second) => first == second;

    private static bool TryInfiniteLineIntersection(
        CadPoint firstOrigin,
        CadVector firstDirection,
        CadPoint secondOrigin,
        CadVector secondDirection,
        out CadPoint intersection)
    {
        var denominator = Cross(firstDirection, secondDirection);
        if (Math.Abs(denominator) <= Epsilon)
        {
            intersection = default;
            return false;
        }
        var offset = secondOrigin - firstOrigin;
        var t = Cross(offset, secondDirection) / denominator;
        intersection = Add(firstOrigin, firstDirection, t);
        return true;
    }

    private static CadVector? Normalize(CadVector vector)
    {
        var length = vector.Length;
        if (!double.IsFinite(length) || length <= Epsilon) return null;
        return new CadVector(vector.X / length, vector.Y / length);
    }

    private static CadPoint Add(CadPoint point, CadVector direction, double distance) =>
        new(point.X + (direction.X * distance), point.Y + (direction.Y * distance));

    private static double Dot(CadVector first, CadVector second) =>
        (first.X * second.X) + (first.Y * second.Y);

    private static double Cross(CadVector first, CadVector second) =>
        (first.X * second.Y) - (first.Y * second.X);

    private static double NormalizeSignedMinor(double angle)
    {
        var normalized = angle % Math.Tau;
        if (normalized > Math.PI) normalized -= Math.Tau;
        if (normalized < -Math.PI) normalized += Math.Tau;
        return normalized;
    }

    private readonly record struct Corner(CadPoint Intersection, CadVector FirstRay, CadVector SecondRay);
}
using UCAD.Core.Entities;
using UCAD.Core.Geometry;

namespace UCAD.Core.Architecture;

public enum CadDoorSwingSide
{
    Left,
    Right
}

public static class CadArchitecturalGeometry
{
    private const double Epsilon = 1e-9;

    public static PolylineEntity CreateWallSegment(
        CadPoint start,
        CadPoint end,
        double thickness)
    {
        if (!double.IsFinite(thickness) || thickness <= Epsilon)
            throw new ArgumentOutOfRangeException(nameof(thickness), "Wall thickness must be positive and finite.");
        var axis = end - start;
        var length = axis.Length;
        if (length <= Epsilon) throw new ArgumentException("Wall requires two distinct points.", nameof(end));
        var normal = new CadVector(-axis.Y / length, axis.X / length);
        var offset = thickness / 2.0;
        return new PolylineEntity(
        [
            Add(start, normal, offset),
            Add(end, normal, offset),
            Add(end, normal, -offset),
            Add(start, normal, -offset)
        ], closed: true);
    }

    public static IReadOnlyList<ICadEntity> CreateDoorSymbol(
        CadPoint hinge,
        CadVector wallDirection,
        double width,
        double openingAngleRadians = Math.PI / 2,
        CadDoorSwingSide swingSide = CadDoorSwingSide.Left)
    {
        if (!double.IsFinite(width) || width <= Epsilon)
            throw new ArgumentOutOfRangeException(nameof(width), "Door width must be positive and finite.");
        if (!double.IsFinite(openingAngleRadians) || openingAngleRadians <= Epsilon || openingAngleRadians > Math.PI)
            throw new ArgumentOutOfRangeException(nameof(openingAngleRadians), "Door opening angle must be between 0 and 180 degrees.");
        var axis = Unit(wallDirection, nameof(wallDirection));
        var sign = swingSide == CadDoorSwingSide.Left ? 1.0 : -1.0;
        var openDirection = Rotate(axis, sign * openingAngleRadians);
        var leafEnd = Add(hinge, openDirection, width);
        var closedEnd = Add(hinge, axis, width);
        var leaf = new LineEntity(hinge, leafEnd);
        var arc = ArcEntity.Create(
            hinge,
            width,
            Math.Atan2(axis.Y, axis.X),
            sign * openingAngleRadians);
        var openingGuide = new LineEntity(hinge, closedEnd);
        return [openingGuide, leaf, arc];
    }

    public static IReadOnlyList<ICadEntity> CreateWindowSymbol(
        CadPoint center,
        CadVector wallDirection,
        double width,
        double wallThickness,
        double frameOffsetRatio = 0.22)
    {
        if (!double.IsFinite(width) || width <= Epsilon)
            throw new ArgumentOutOfRangeException(nameof(width));
        if (!double.IsFinite(wallThickness) || wallThickness <= Epsilon)
            throw new ArgumentOutOfRangeException(nameof(wallThickness));
        if (!double.IsFinite(frameOffsetRatio) || frameOffsetRatio <= 0 || frameOffsetRatio >= 0.5)
            throw new ArgumentOutOfRangeException(nameof(frameOffsetRatio));

        var axis = Unit(wallDirection, nameof(wallDirection));
        var normal = new CadVector(-axis.Y, axis.X);
        var halfWidth = width / 2.0;
        var halfThickness = wallThickness / 2.0;
        var frameOffset = wallThickness * frameOffsetRatio;
        var leftCenter = Add(center, axis, -halfWidth);
        var rightCenter = Add(center, axis, halfWidth);

        var outerA = new LineEntity(
            Add(leftCenter, normal, halfThickness),
            Add(rightCenter, normal, halfThickness));
        var outerB = new LineEntity(
            Add(leftCenter, normal, -halfThickness),
            Add(rightCenter, normal, -halfThickness));
        var glassA = new LineEntity(
            Add(leftCenter, normal, frameOffset),
            Add(rightCenter, normal, frameOffset));
        var glassB = new LineEntity(
            Add(leftCenter, normal, -frameOffset),
            Add(rightCenter, normal, -frameOffset));
        var jambA = new LineEntity(
            Add(leftCenter, normal, halfThickness),
            Add(leftCenter, normal, -halfThickness));
        var jambB = new LineEntity(
            Add(rightCenter, normal, halfThickness),
            Add(rightCenter, normal, -halfThickness));
        return [outerA, outerB, glassA, glassB, jambA, jambB];
    }

    public static PolylineEntity CreateRectangularColumn(
        CadPoint center,
        double width,
        double depth,
        double rotationRadians = 0)
    {
        if (!double.IsFinite(width) || width <= Epsilon) throw new ArgumentOutOfRangeException(nameof(width));
        if (!double.IsFinite(depth) || depth <= Epsilon) throw new ArgumentOutOfRangeException(nameof(depth));
        if (!double.IsFinite(rotationRadians)) throw new ArgumentOutOfRangeException(nameof(rotationRadians));

        var halfWidth = width / 2.0;
        var halfDepth = depth / 2.0;
        var local = new[]
        {
            new CadVector(-halfWidth, -halfDepth),
            new CadVector(halfWidth, -halfDepth),
            new CadVector(halfWidth, halfDepth),
            new CadVector(-halfWidth, halfDepth)
        };
        var points = local.Select(vector =>
        {
            var rotated = Rotate(vector, rotationRadians);
            return new CadPoint(center.X + rotated.X, center.Y + rotated.Y);
        });
        return new PolylineEntity(points, closed: true);
    }

    public static CircleEntity CreateCircularColumn(CadPoint center, double diameter)
    {
        if (!double.IsFinite(diameter) || diameter <= Epsilon) throw new ArgumentOutOfRangeException(nameof(diameter));
        return new CircleEntity(center, diameter / 2.0);
    }

    private static CadVector Unit(CadVector vector, string parameterName)
    {
        var length = vector.Length;
        if (!double.IsFinite(length) || length <= Epsilon)
            throw new ArgumentException("Direction vector must be non-zero and finite.", parameterName);
        return new CadVector(vector.X / length, vector.Y / length);
    }

    private static CadVector Rotate(CadVector vector, double radians)
    {
        var cosine = Math.Cos(radians);
        var sine = Math.Sin(radians);
        return new CadVector(
            (vector.X * cosine) - (vector.Y * sine),
            (vector.X * sine) + (vector.Y * cosine));
    }

    private static CadPoint Add(CadPoint point, CadVector direction, double distance) =>
        new(point.X + (direction.X * distance), point.Y + (direction.Y * distance));
}

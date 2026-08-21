using UCAD.Core.Entities;
using UCAD.Core.Geometry;

namespace UCAD.Core.Interaction;

/// <summary>
/// Geometry adapter for v0.11 annotation entities. Keeping this beside the accepted
/// v0.7 geometry helper avoids destabilizing older selection/intersection behavior.
/// </summary>
public static class CadAnnotationEntityGeometry
{
    private const double Epsilon = 1e-9;

    public static bool Supports(ICadEntity entity) => entity is
        MTextEntity or AngularDimensionEntity or RadialDimensionEntity or LeaderEntity;

    public static double DistanceTo(ICadEntity entity, CadPoint point) => entity switch
    {
        MTextEntity text => DistanceToSegmentsOrInterior(GetClosedSegments(GetMTextCorners(text)), point, GetMTextCorners(text)),
        AngularDimensionEntity dimension => DistanceToAngularDimension(dimension, point),
        RadialDimensionEntity dimension => DistanceToRadialDimension(dimension, point),
        LeaderEntity leader => DistanceToChain(leader.Points, point),
        _ => double.PositiveInfinity
    };

    public static bool TryGetBounds(ICadEntity entity, out CadRect bounds)
    {
        switch (entity)
        {
            case MTextEntity text:
                bounds = BoundsOfPoints(GetMTextCorners(text));
                return true;
            case AngularDimensionEntity dimension:
                bounds = BoundsOfPoints(GetAngularGeometryPoints(dimension));
                return true;
            case RadialDimensionEntity dimension:
                bounds = BoundsOfPoints([dimension.Center, dimension.PointOnCircle, dimension.TextPoint]);
                return true;
            case LeaderEntity leader:
                bounds = BoundsOfPoints(leader.Points);
                return true;
            default:
                bounds = default;
                return false;
        }
    }

    public static bool IsContainedBy(ICadEntity entity, CadRect rectangle, double tolerance = 0) =>
        TryGetBounds(entity, out var bounds) && rectangle.Contains(bounds, tolerance);

    public static bool IntersectsRectangle(ICadEntity entity, CadRect rectangle, double tolerance = 0)
    {
        if (!TryGetBounds(entity, out var bounds) || !bounds.Intersects(rectangle, tolerance)) return false;
        return entity switch
        {
            MTextEntity text => GetClosedSegments(GetMTextCorners(text))
                .Any(segment => SegmentIntersectsRectangle(segment.Start, segment.End, rectangle, tolerance)) ||
                rectangle.Contains(text.Position, tolerance),
            AngularDimensionEntity dimension => AngularSegments(dimension)
                .Any(segment => SegmentIntersectsRectangle(segment.Start, segment.End, rectangle, tolerance)) ||
                dimension.GetArcSamplePoints().Any(point => rectangle.Contains(point, tolerance)),
            RadialDimensionEntity dimension =>
                SegmentIntersectsRectangle(dimension.Center, dimension.PointOnCircle, rectangle, tolerance) ||
                SegmentIntersectsRectangle(dimension.PointOnCircle, dimension.TextPoint, rectangle, tolerance),
            LeaderEntity leader => EnumerateSegments(leader.Points)
                .Any(segment => SegmentIntersectsRectangle(segment.Start, segment.End, rectangle, tolerance)),
            _ => false
        };
    }

    public static IReadOnlyList<CadPoint> GetEndpoints(ICadEntity entity) => entity switch
    {
        MTextEntity text => [text.Position],
        AngularDimensionEntity dimension =>
            [dimension.Vertex, dimension.FirstRayPoint, dimension.SecondRayPoint, dimension.ArcPoint],
        RadialDimensionEntity dimension => [dimension.Center, dimension.PointOnCircle, dimension.TextPoint],
        LeaderEntity leader => leader.Points.ToArray(),
        _ => []
    };

    public static IReadOnlyList<CadPoint> GetMidpoints(ICadEntity entity) => entity switch
    {
        AngularDimensionEntity dimension => [dimension.GetArcMidpoint()],
        RadialDimensionEntity dimension => [Midpoint(dimension.Center, dimension.PointOnCircle)],
        LeaderEntity leader => EnumerateSegments(leader.Points).Select(segment => Midpoint(segment.Start, segment.End)).ToArray(),
        _ => []
    };

    public static CadPoint? GetCenter(ICadEntity entity) => entity switch
    {
        AngularDimensionEntity dimension => dimension.Vertex,
        RadialDimensionEntity dimension => dimension.Center,
        _ => null
    };

    public static IReadOnlyList<CadPoint> GetGripPoints(ICadEntity entity) => entity switch
    {
        MTextEntity text => [text.Position, text.GetWidthGripPoint()],
        AngularDimensionEntity dimension =>
            [dimension.Vertex, dimension.FirstRayPoint, dimension.SecondRayPoint, dimension.ArcPoint],
        RadialDimensionEntity dimension => [dimension.Center, dimension.PointOnCircle, dimension.TextPoint],
        LeaderEntity leader => leader.Points.ToArray(),
        _ => []
    };

    private static double DistanceToAngularDimension(AngularDimensionEntity dimension, CadPoint point)
    {
        var segmentDistance = AngularSegments(dimension)
            .Select(segment => DistancePointToSegment(point, segment.Start, segment.End))
            .DefaultIfEmpty(double.PositiveInfinity)
            .Min();
        var arcDistance = DistanceToChain(dimension.GetArcSamplePoints(), point);
        return Math.Min(segmentDistance, arcDistance);
    }

    private static double DistanceToRadialDimension(RadialDimensionEntity dimension, CadPoint point) =>
        Math.Min(
            DistancePointToSegment(point, dimension.Center, dimension.PointOnCircle),
            DistancePointToSegment(point, dimension.PointOnCircle, dimension.TextPoint));

    private static IReadOnlyList<(CadPoint Start, CadPoint End)> AngularSegments(AngularDimensionEntity dimension)
    {
        var radius = dimension.Radius;
        var firstDirection = Normalize(dimension.FirstRayPoint - dimension.Vertex);
        var secondDirection = Normalize(dimension.SecondRayPoint - dimension.Vertex);
        var firstArc = Add(dimension.Vertex, firstDirection, radius);
        var secondArc = Add(dimension.Vertex, secondDirection, radius);
        return
        [
            (dimension.Vertex, firstArc),
            (dimension.Vertex, secondArc)
        ];
    }

    private static IReadOnlyList<CadPoint> GetAngularGeometryPoints(AngularDimensionEntity dimension) =>
        [dimension.Vertex, .. dimension.GetArcSamplePoints(), dimension.FirstRayPoint, dimension.SecondRayPoint];

    private static IReadOnlyList<CadPoint> GetMTextCorners(MTextEntity text)
    {
        var lines = Math.Max(1, text.ApproximateLines().Count);
        var height = lines * text.TextHeight * 1.2;
        var cosine = Math.Cos(text.RotationRadians);
        var sine = Math.Sin(text.RotationRadians);
        CadPoint Map(double x, double y) => new(
            text.Position.X + (x * cosine) - (y * sine),
            text.Position.Y + (x * sine) + (y * cosine));
        return [Map(0, 0), Map(text.Width, 0), Map(text.Width, height), Map(0, height)];
    }

    private static double DistanceToChain(IReadOnlyList<CadPoint> points, CadPoint point)
    {
        if (points.Count == 0) return double.PositiveInfinity;
        if (points.Count == 1) return (point - points[0]).Length;
        var best = double.PositiveInfinity;
        for (var i = 1; i < points.Count; i++) best = Math.Min(best, DistancePointToSegment(point, points[i - 1], points[i]));
        return best;
    }

    private static double DistanceToSegmentsOrInterior(
        IReadOnlyList<(CadPoint Start, CadPoint End)> segments,
        CadPoint point,
        IReadOnlyList<CadPoint> polygon)
    {
        if (PointInPolygon(point, polygon)) return 0;
        return segments.Select(segment => DistancePointToSegment(point, segment.Start, segment.End)).DefaultIfEmpty(double.PositiveInfinity).Min();
    }

    private static IReadOnlyList<(CadPoint Start, CadPoint End)> GetClosedSegments(IReadOnlyList<CadPoint> points)
    {
        if (points.Count < 2) return [];
        var segments = new List<(CadPoint Start, CadPoint End)>();
        for (var i = 1; i < points.Count; i++) segments.Add((points[i - 1], points[i]));
        segments.Add((points[^1], points[0]));
        return segments;
    }

    private static IEnumerable<(CadPoint Start, CadPoint End)> EnumerateSegments(IReadOnlyList<CadPoint> points)
    {
        for (var i = 1; i < points.Count; i++) yield return (points[i - 1], points[i]);
    }

    private static double DistancePointToSegment(CadPoint point, CadPoint start, CadPoint end)
    {
        var segment = end - start;
        var lengthSquared = (segment.X * segment.X) + (segment.Y * segment.Y);
        if (lengthSquared <= Epsilon) return (point - start).Length;
        var offset = point - start;
        var t = Math.Clamp(((offset.X * segment.X) + (offset.Y * segment.Y)) / lengthSquared, 0, 1);
        var closest = new CadPoint(start.X + (segment.X * t), start.Y + (segment.Y * t));
        return (point - closest).Length;
    }

    private static bool SegmentIntersectsRectangle(CadPoint start, CadPoint end, CadRect rectangle, double tolerance)
    {
        if (rectangle.Contains(start, tolerance) || rectangle.Contains(end, tolerance)) return true;
        var corners = new[]
        {
            new CadPoint(rectangle.Left, rectangle.Bottom),
            new CadPoint(rectangle.Right, rectangle.Bottom),
            new CadPoint(rectangle.Right, rectangle.Top),
            new CadPoint(rectangle.Left, rectangle.Top)
        };
        for (var i = 0; i < corners.Length; i++)
            if (TrySegmentIntersection(start, end, corners[i], corners[(i + 1) % corners.Length])) return true;
        return false;
    }

    private static bool TrySegmentIntersection(CadPoint a1, CadPoint a2, CadPoint b1, CadPoint b2)
    {
        var r = a2 - a1;
        var s = b2 - b1;
        var denominator = Cross(r, s);
        if (Math.Abs(denominator) <= Epsilon) return false;
        var offset = b1 - a1;
        var t = Cross(offset, s) / denominator;
        var u = Cross(offset, r) / denominator;
        return t >= -Epsilon && t <= 1 + Epsilon && u >= -Epsilon && u <= 1 + Epsilon;
    }

    private static bool PointInPolygon(CadPoint point, IReadOnlyList<CadPoint> polygon)
    {
        var inside = false;
        for (var i = 0; i < polygon.Count; i++)
        {
            var j = (i + polygon.Count - 1) % polygon.Count;
            var first = polygon[i];
            var second = polygon[j];
            if ((first.Y > point.Y) == (second.Y > point.Y)) continue;
            var x = ((second.X - first.X) * (point.Y - first.Y) / (second.Y - first.Y)) + first.X;
            if (point.X < x) inside = !inside;
        }
        return inside;
    }

    private static CadRect BoundsOfPoints(IReadOnlyList<CadPoint> points)
    {
        if (points.Count == 0) return default;
        return new CadRect(
            points.Min(point => point.X),
            points.Min(point => point.Y),
            points.Max(point => point.X),
            points.Max(point => point.Y));
    }

    private static CadVector Normalize(CadVector vector)
    {
        var length = vector.Length;
        if (length <= Epsilon) return new CadVector(1, 0);
        return new CadVector(vector.X / length, vector.Y / length);
    }

    private static CadPoint Add(CadPoint point, CadVector direction, double distance) =>
        new(point.X + (direction.X * distance), point.Y + (direction.Y * distance));

    private static CadPoint Midpoint(CadPoint first, CadPoint second) =>
        new((first.X + second.X) / 2, (first.Y + second.Y) / 2);

    private static double Cross(CadVector first, CadVector second) =>
        (first.X * second.Y) - (first.Y * second.X);
}
using UCAD.Core.Entities;
using UCAD.Core.Geometry;

namespace UCAD.Core.Interaction;

/// <summary>
/// Geometry adapter for v0.9 drawing entities. It deliberately lives beside the
/// existing frozen authoring geometry helper so v0.4-v0.7 behavior is not rewritten.
/// </summary>
public static class CadExtendedEntityGeometry
{
    private const double Epsilon = 1e-9;

    public static bool Supports(ICadEntity entity) => entity is
        PointEntity or EllipseEntity or SplineEntity or RayEntity or XLineEntity;

    public static double DistanceTo(ICadEntity entity, CadPoint point) => entity switch
    {
        PointEntity marker => (point - marker.Position).Length,
        EllipseEntity ellipse => DistanceToChain(ellipse.SamplePoints(), point, ellipse.IsFullEllipse),
        SplineEntity spline => DistanceToChain(spline.SamplePoints(), point, spline.Closed),
        RayEntity ray => DistanceToDirectedLine(ray.Origin, ray.Direction, point, rayOnly: true),
        XLineEntity xline => DistanceToDirectedLine(xline.Point, xline.Direction, point, rayOnly: false),
        _ => double.PositiveInfinity
    };

    public static bool TryGetBounds(ICadEntity entity, out CadRect bounds)
    {
        switch (entity)
        {
            case PointEntity point:
                bounds = new CadRect(point.Position.X, point.Position.Y, point.Position.X, point.Position.Y);
                return true;
            case EllipseEntity ellipse:
                bounds = BoundsOfPoints(ellipse.SamplePoints());
                return true;
            case SplineEntity spline:
                bounds = BoundsOfPoints(spline.SamplePoints());
                return true;
            default:
                bounds = default;
                return false;
        }
    }

    public static bool IsContainedBy(ICadEntity entity, CadRect rectangle, double tolerance = 0)
    {
        if (entity is RayEntity or XLineEntity) return false;
        if (!TryGetBounds(entity, out var bounds)) return false;
        return rectangle.Contains(bounds, tolerance);
    }

    public static bool IntersectsRectangle(ICadEntity entity, CadRect rectangle, double tolerance = 0)
    {
        switch (entity)
        {
            case PointEntity point:
                return rectangle.Contains(point.Position, tolerance);
            case EllipseEntity ellipse:
                return ChainIntersectsRectangle(ellipse.SamplePoints(), ellipse.IsFullEllipse, rectangle, tolerance);
            case SplineEntity spline:
                return ChainIntersectsRectangle(spline.SamplePoints(), spline.Closed, rectangle, tolerance);
            case RayEntity ray:
                return DirectedLineIntersectsRectangle(ray.Origin, ray.Direction, rectangle, rayOnly: true, tolerance);
            case XLineEntity xline:
                return DirectedLineIntersectsRectangle(xline.Point, xline.Direction, rectangle, rayOnly: false, tolerance);
            default:
                return false;
        }
    }

    public static IReadOnlyList<CadPoint> GetEndpoints(ICadEntity entity) => entity switch
    {
        PointEntity point => [point.Position],
        EllipseEntity ellipse when !ellipse.IsFullEllipse =>
            [ellipse.PointAtParameter(ellipse.StartParameter), ellipse.PointAtParameter(ellipse.StartParameter + ellipse.SweepParameter)],
        SplineEntity spline when !spline.Closed => [spline.FitPoints[0], spline.FitPoints[^1]],
        RayEntity ray => [ray.Origin],
        _ => []
    };

    public static IReadOnlyList<CadPoint> GetMidpoints(ICadEntity entity) => entity switch
    {
        EllipseEntity ellipse when !ellipse.IsFullEllipse =>
            [ellipse.PointAtParameter(ellipse.StartParameter + (ellipse.SweepParameter / 2))],
        SplineEntity spline when !spline.Closed =>
            [spline.SamplePoints()[spline.SamplePoints().Count / 2]],
        _ => []
    };

    public static CadPoint? GetCenter(ICadEntity entity) => entity switch
    {
        EllipseEntity ellipse => ellipse.Center,
        _ => null
    };

    public static IReadOnlyList<CadPoint> GetGripPoints(ICadEntity entity) => entity switch
    {
        PointEntity point => [point.Position],
        EllipseEntity ellipse =>
        [
            ellipse.Center,
            ellipse.PointAtParameter(0),
            ellipse.PointAtParameter(Math.PI / 2),
            ellipse.PointAtParameter(Math.PI),
            ellipse.PointAtParameter(Math.PI * 1.5)
        ],
        SplineEntity spline => spline.FitPoints.ToArray(),
        RayEntity ray => [ray.Origin, ray.Origin + ray.Direction],
        XLineEntity xline => [xline.Point, xline.Point + xline.Direction],
        _ => []
    };

    private static double DistanceToChain(IReadOnlyList<CadPoint> points, CadPoint point, bool closed)
    {
        if (points.Count == 0) return double.PositiveInfinity;
        if (points.Count == 1) return (point - points[0]).Length;
        var best = double.PositiveInfinity;
        for (var i = 1; i < points.Count; i++)
            best = Math.Min(best, DistancePointToSegment(point, points[i - 1], points[i]));
        if (closed && (points[0] - points[^1]).Length > Epsilon)
            best = Math.Min(best, DistancePointToSegment(point, points[^1], points[0]));
        return best;
    }

    private static double DistanceToDirectedLine(CadPoint anchor, CadVector direction, CadPoint point, bool rayOnly)
    {
        var from = point - anchor;
        var projection = (from.X * direction.X) + (from.Y * direction.Y);
        if (rayOnly && projection < 0) return from.Length;
        return Math.Abs((from.X * direction.Y) - (from.Y * direction.X));
    }

    private static double DistancePointToSegment(CadPoint point, CadPoint start, CadPoint end)
    {
        var segment = end - start;
        var lengthSquared = (segment.X * segment.X) + (segment.Y * segment.Y);
        if (lengthSquared <= Epsilon) return (point - start).Length;
        var fromStart = point - start;
        var t = Math.Clamp(((fromStart.X * segment.X) + (fromStart.Y * segment.Y)) / lengthSquared, 0, 1);
        var closest = new CadPoint(start.X + (segment.X * t), start.Y + (segment.Y * t));
        return (point - closest).Length;
    }

    private static CadRect BoundsOfPoints(IReadOnlyList<CadPoint> points)
    {
        var left = points.Min(point => point.X);
        var right = points.Max(point => point.X);
        var bottom = points.Min(point => point.Y);
        var top = points.Max(point => point.Y);
        return new CadRect(left, bottom, right, top);
    }

    private static bool ChainIntersectsRectangle(
        IReadOnlyList<CadPoint> points,
        bool closed,
        CadRect rectangle,
        double tolerance)
    {
        if (points.Any(point => rectangle.Contains(point, tolerance))) return true;
        for (var i = 1; i < points.Count; i++)
            if (SegmentIntersectsRectangle(points[i - 1], points[i], rectangle, tolerance)) return true;
        return closed && points.Count > 2 && SegmentIntersectsRectangle(points[^1], points[0], rectangle, tolerance);
    }

    private static bool DirectedLineIntersectsRectangle(
        CadPoint anchor,
        CadVector direction,
        CadRect rectangle,
        bool rayOnly,
        double tolerance)
    {
        if (rectangle.Contains(anchor, tolerance)) return true;
        var corners = new[]
        {
            new CadPoint(rectangle.Left, rectangle.Bottom),
            new CadPoint(rectangle.Right, rectangle.Bottom),
            new CadPoint(rectangle.Right, rectangle.Top),
            new CadPoint(rectangle.Left, rectangle.Top)
        };
        for (var i = 0; i < corners.Length; i++)
        {
            var a = corners[i];
            var b = corners[(i + 1) % corners.Length];
            if (TryDirectedLineSegmentIntersection(anchor, direction, a, b, rayOnly, out _)) return true;
        }
        return false;
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
            if (TrySegmentIntersection(start, end, corners[i], corners[(i + 1) % corners.Length], out _)) return true;
        return false;
    }

    private static bool TryDirectedLineSegmentIntersection(
        CadPoint anchor,
        CadVector direction,
        CadPoint segmentStart,
        CadPoint segmentEnd,
        bool rayOnly,
        out CadPoint point)
    {
        var segment = segmentEnd - segmentStart;
        var denominator = Cross(direction, segment);
        if (Math.Abs(denominator) <= Epsilon)
        {
            point = default;
            return false;
        }
        var offset = segmentStart - anchor;
        var lineT = Cross(offset, segment) / denominator;
        var segmentT = Cross(offset, direction) / denominator;
        if ((rayOnly && lineT < -Epsilon) || segmentT < -Epsilon || segmentT > 1 + Epsilon)
        {
            point = default;
            return false;
        }
        point = new CadPoint(anchor.X + (direction.X * lineT), anchor.Y + (direction.Y * lineT));
        return true;
    }

    private static bool TrySegmentIntersection(CadPoint a1, CadPoint a2, CadPoint b1, CadPoint b2, out CadPoint point)
    {
        var r = a2 - a1;
        var s = b2 - b1;
        var denominator = Cross(r, s);
        if (Math.Abs(denominator) <= Epsilon)
        {
            point = default;
            return false;
        }
        var offset = b1 - a1;
        var t = Cross(offset, s) / denominator;
        var u = Cross(offset, r) / denominator;
        if (t < -Epsilon || t > 1 + Epsilon || u < -Epsilon || u > 1 + Epsilon)
        {
            point = default;
            return false;
        }
        point = new CadPoint(a1.X + (r.X * t), a1.Y + (r.Y * t));
        return true;
    }

    private static double Cross(CadVector a, CadVector b) => (a.X * b.Y) - (a.Y * b.X);
}
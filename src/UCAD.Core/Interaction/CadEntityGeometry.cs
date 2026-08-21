using UCAD.Core.Entities;
using UCAD.Core.Geometry;

namespace UCAD.Core.Interaction;

public static class CadEntityGeometry
{
    private const double Epsilon = 1e-9;

    public static CadRect GetBounds(ICadEntity entity)
    {
        ArgumentNullException.ThrowIfNull(entity);
        if (CadAnnotationEntityGeometry.TryGetBounds(entity, out var annotationBounds)) return annotationBounds;
        if (CadExtendedEntityGeometry.TryGetBounds(entity, out var extendedBounds)) return extendedBounds;
        return entity switch
        {
            LineEntity line => BoundsOfPoints([line.Start, line.End]),
            PolylineEntity polyline => BoundsOfPoints(polyline.Points),
            CircleEntity circle => new CadRect(circle.Center.X - circle.Radius, circle.Center.Y - circle.Radius, circle.Center.X + circle.Radius, circle.Center.Y + circle.Radius),
            ArcEntity arc => GetArcBounds(arc),
            TextEntity text => BoundsOfPoints(GetTextCorners(text)),
            LinearDimensionEntity dimension => BoundsOfPoints(GetDimensionSegments(dimension).SelectMany(segment => new[] { segment.Start, segment.End }).ToArray()),
            HatchEntity hatch => BoundsOfPoints(hatch.Boundary),
            BlockReferenceEntity block => GetBlockBounds(block),
            _ => throw new NotSupportedException($"Unsupported CAD entity type: {entity.GetType().Name}")
        };
    }

    public static double DistanceTo(ICadEntity entity, CadPoint point)
    {
        ArgumentNullException.ThrowIfNull(entity);
        if (CadAnnotationEntityGeometry.Supports(entity)) return CadAnnotationEntityGeometry.DistanceTo(entity, point);
        if (CadExtendedEntityGeometry.Supports(entity)) return CadExtendedEntityGeometry.DistanceTo(entity, point);
        return entity switch
        {
            LineEntity line => DistancePointToSegment(point, line.Start, line.End),
            PolylineEntity polyline => EnumerateSegments(polyline).Select(segment => DistancePointToSegment(point, segment.Start, segment.End)).DefaultIfEmpty(double.PositiveInfinity).Min(),
            CircleEntity circle => Math.Abs((point - circle.Center).Length - circle.Radius),
            ArcEntity arc => DistanceToArc(arc, point),
            TextEntity text => DistanceToSegmentsOrInterior(GetClosedSegments(GetTextCorners(text)), point, GetTextCorners(text)),
            LinearDimensionEntity dimension => GetDimensionSegments(dimension).Select(segment => DistancePointToSegment(point, segment.Start, segment.End)).DefaultIfEmpty(double.PositiveInfinity).Min(),
            HatchEntity hatch => DistanceToSegmentsOrInterior(GetClosedSegments(hatch.Boundary), point, hatch.Boundary),
            BlockReferenceEntity block => block.Contents.Select(child => DistanceTo(child, point)).DefaultIfEmpty(double.PositiveInfinity).Min(),
            _ => double.PositiveInfinity
        };
    }

    public static bool IsContainedBy(ICadEntity entity, CadRect rectangle, double tolerance = 0)
    {
        if (CadAnnotationEntityGeometry.Supports(entity)) return CadAnnotationEntityGeometry.IsContainedBy(entity, rectangle, tolerance);
        if (CadExtendedEntityGeometry.Supports(entity)) return CadExtendedEntityGeometry.IsContainedBy(entity, rectangle, tolerance);
        return rectangle.Contains(GetBounds(entity), tolerance);
    }

    public static bool IntersectsRectangle(ICadEntity entity, CadRect rectangle, double tolerance = 0)
    {
        ArgumentNullException.ThrowIfNull(entity);
        if (CadAnnotationEntityGeometry.Supports(entity)) return CadAnnotationEntityGeometry.IntersectsRectangle(entity, rectangle, tolerance);
        if (CadExtendedEntityGeometry.Supports(entity)) return CadExtendedEntityGeometry.IntersectsRectangle(entity, rectangle, tolerance);
        if (!GetBounds(entity).Intersects(rectangle, tolerance)) return false;

        return entity switch
        {
            LineEntity line => SegmentIntersectsRectangle(line.Start, line.End, rectangle, tolerance),
            PolylineEntity polyline => EnumerateSegments(polyline).Any(segment => SegmentIntersectsRectangle(segment.Start, segment.End, rectangle, tolerance)),
            CircleEntity circle => CircleIntersectsRectangle(circle.Center, circle.Radius, rectangle, tolerance),
            ArcEntity arc => ArcIntersectsRectangle(arc, rectangle, tolerance),
            TextEntity text => GetClosedSegments(GetTextCorners(text)).Any(segment => SegmentIntersectsRectangle(segment.Start, segment.End, rectangle, tolerance)) || rectangle.Contains(text.Position, tolerance),
            LinearDimensionEntity dimension => GetDimensionSegments(dimension).Any(segment => SegmentIntersectsRectangle(segment.Start, segment.End, rectangle, tolerance)),
            HatchEntity hatch => GetClosedSegments(hatch.Boundary).Any(segment => SegmentIntersectsRectangle(segment.Start, segment.End, rectangle, tolerance)) || hatch.Boundary.Any(point => rectangle.Contains(point, tolerance)),
            BlockReferenceEntity block => block.Contents.Any(child => IntersectsRectangle(child, rectangle, tolerance)),
            _ => false
        };
    }

    public static IReadOnlyList<CadPoint> Intersections(ICadEntity first, ICadEntity second)
    {
        ArgumentNullException.ThrowIfNull(first);
        ArgumentNullException.ThrowIfNull(second);
        if (ReferenceEquals(first, second) || first.Id == second.Id) return [];

        if (first is BlockReferenceEntity firstBlock)
        {
            var blockResult = new List<CadPoint>();
            foreach (var child in firstBlock.Contents)
                foreach (var point in Intersections(child, second)) AddDistinct(blockResult, point);
            return blockResult;
        }
        if (second is BlockReferenceEntity secondBlock)
        {
            var blockResult = new List<CadPoint>();
            foreach (var child in secondBlock.Contents)
                foreach (var point in Intersections(first, child)) AddDistinct(blockResult, point);
            return blockResult;
        }

        var result = new List<CadPoint>();
        var firstSegments = GetLinearSegments(first);
        var secondSegments = GetLinearSegments(second);

        if (firstSegments.Count > 0 && secondSegments.Count > 0)
        {
            foreach (var a in firstSegments)
            foreach (var b in secondSegments)
                if (TrySegmentIntersection(a.Start, a.End, b.Start, b.End, out var point)) AddDistinct(result, point);
            return result;
        }

        if (firstSegments.Count > 0)
        {
            AddLinearCurveIntersections(result, firstSegments, second);
            return result;
        }
        if (secondSegments.Count > 0)
        {
            AddLinearCurveIntersections(result, secondSegments, first);
            return result;
        }

        switch (first, second)
        {
            case (CircleEntity a, CircleEntity b):
                AddCircleCircleIntersections(result, a.Center, a.Radius, b.Center, b.Radius);
                break;
            case (CircleEntity circle, ArcEntity arc):
                AddCircleCircleIntersections(result, circle.Center, circle.Radius, arc.Center, arc.Radius);
                result.RemoveAll(point => !IsPointOnArc(arc, point));
                break;
            case (ArcEntity arc, CircleEntity circle):
                AddCircleCircleIntersections(result, arc.Center, arc.Radius, circle.Center, circle.Radius);
                result.RemoveAll(point => !IsPointOnArc(arc, point));
                break;
            case (ArcEntity a, ArcEntity b):
                AddCircleCircleIntersections(result, a.Center, a.Radius, b.Center, b.Radius);
                result.RemoveAll(point => !IsPointOnArc(a, point) || !IsPointOnArc(b, point));
                break;
        }
        return result;
    }

    public static IReadOnlyList<CadPoint> GetEndpoints(ICadEntity entity) => entity switch
    {
        LineEntity line => [line.Start, line.End],
        PolylineEntity polyline => polyline.Points.ToArray(),
        ArcEntity arc => [arc.StartPoint, arc.EndPoint],
        TextEntity text => [text.Position],
        LinearDimensionEntity dimension => [dimension.FirstExtensionPoint, dimension.SecondExtensionPoint, dimension.DimensionLinePoint],
        HatchEntity hatch => hatch.Boundary.ToArray(),
        BlockReferenceEntity block => [block.InsertionPoint],
        _ => []
    };

    public static IReadOnlyList<CadPoint> GetMidpoints(ICadEntity entity) => entity switch
    {
        LineEntity line => [Midpoint(line.Start, line.End)],
        PolylineEntity polyline => EnumerateSegments(polyline).Select(segment => Midpoint(segment.Start, segment.End)).ToArray(),
        ArcEntity arc => [arc.PointAt(0.5)],
        LinearDimensionEntity dimension => [Midpoint(dimension.FirstExtensionPoint, dimension.SecondExtensionPoint)],
        HatchEntity hatch => GetClosedSegments(hatch.Boundary).Select(segment => Midpoint(segment.Start, segment.End)).ToArray(),
        BlockReferenceEntity block => block.Contents.SelectMany(GetMidpoints).ToArray(),
        _ => []
    };

    public static IReadOnlyList<CadPoint> GetGripPoints(ICadEntity entity) => entity switch
    {
        CircleEntity circle =>
        [
            circle.Center,
            new CadPoint(circle.Center.X + circle.Radius, circle.Center.Y),
            new CadPoint(circle.Center.X - circle.Radius, circle.Center.Y),
            new CadPoint(circle.Center.X, circle.Center.Y + circle.Radius),
            new CadPoint(circle.Center.X, circle.Center.Y - circle.Radius)
        ],
        ArcEntity arc => [arc.StartPoint, arc.PointAt(0.5), arc.EndPoint, arc.Center],
        TextEntity text => [text.Position],
        LinearDimensionEntity dimension => [dimension.FirstExtensionPoint, dimension.SecondExtensionPoint, dimension.DimensionLinePoint],
        HatchEntity hatch => hatch.Boundary.ToArray(),
        BlockReferenceEntity block => [block.InsertionPoint],
        _ => GetEndpoints(entity)
    };

    public static bool IsPointOnArc(ArcEntity arc, CadPoint point, double radialTolerance = 1e-6)
    {
        var radialDistance = Math.Abs((point - arc.Center).Length - arc.Radius);
        if (radialDistance > radialTolerance) return false;
        var angle = Math.Atan2(point.Y - arc.Center.Y, point.X - arc.Center.X);
        return IsAngleOnArc(arc, angle);
    }

    private static CadRect GetBlockBounds(BlockReferenceEntity block)
    {
        var points = new List<CadPoint>();
        foreach (var child in block.Contents)
        {
            var bounds = GetBounds(child);
            points.Add(new CadPoint(bounds.Left, bounds.Bottom));
            points.Add(new CadPoint(bounds.Right, bounds.Top));
        }
        return BoundsOfPoints(points);
    }

    private static IReadOnlyList<CadPoint> GetTextCorners(TextEntity text)
    {
        var cosine = Math.Cos(text.RotationRadians);
        var sine = Math.Sin(text.RotationRadians);
        CadPoint Map(double x, double y) => new(
            text.Position.X + (x * cosine) - (y * sine),
            text.Position.Y + (x * sine) + (y * cosine));
        return [Map(0, 0), Map(text.ApproximateWidth, 0), Map(text.ApproximateWidth, text.Height), Map(0, text.Height)];
    }

    private static IReadOnlyList<(CadPoint Start, CadPoint End)> GetDimensionSegments(LinearDimensionEntity dimension)
    {
        var endpoints = dimension.GetDimensionLineEndpoints();
        return
        [
            (dimension.FirstExtensionPoint, endpoints.First),
            (dimension.SecondExtensionPoint, endpoints.Second),
            (endpoints.First, endpoints.Second)
        ];
    }

    private static CadRect BoundsOfPoints(IReadOnlyList<CadPoint> points)
    {
        if (points.Count == 0) return default;
        var left = points[0].X;
        var right = points[0].X;
        var bottom = points[0].Y;
        var top = points[0].Y;
        for (var i = 1; i < points.Count; i++)
        {
            left = Math.Min(left, points[i].X);
            right = Math.Max(right, points[i].X);
            bottom = Math.Min(bottom, points[i].Y);
            top = Math.Max(top, points[i].Y);
        }
        return new CadRect(left, bottom, right, top);
    }

    private static CadRect GetArcBounds(ArcEntity arc)
    {
        var points = new List<CadPoint> { arc.StartPoint, arc.EndPoint };
        foreach (var angle in new[] { 0.0, Math.PI / 2, Math.PI, Math.PI * 1.5 })
        {
            if (IsAngleOnArc(arc, angle))
                points.Add(new CadPoint(arc.Center.X + (Math.Cos(angle) * arc.Radius), arc.Center.Y + (Math.Sin(angle) * arc.Radius)));
        }
        return BoundsOfPoints(points);
    }

    private static double DistanceToArc(ArcEntity arc, CadPoint point)
    {
        var angle = Math.Atan2(point.Y - arc.Center.Y, point.X - arc.Center.X);
        if (IsAngleOnArc(arc, angle)) return Math.Abs((point - arc.Center).Length - arc.Radius);
        return Math.Min((point - arc.StartPoint).Length, (point - arc.EndPoint).Length);
    }

    private static bool IsAngleOnArc(ArcEntity arc, double angle)
    {
        if (arc.SweepAngleRadians >= 0) return NormalizePositive(angle - arc.StartAngleRadians) <= arc.SweepAngleRadians + Epsilon;
        return NormalizePositive(arc.StartAngleRadians - angle) <= -arc.SweepAngleRadians + Epsilon;
    }

    private static double NormalizePositive(double angle)
    {
        var normalized = angle % Math.Tau;
        return normalized < 0 ? normalized + Math.Tau : normalized;
    }

    private static CadPoint Midpoint(CadPoint a, CadPoint b) => new((a.X + b.X) / 2, (a.Y + b.Y) / 2);

    private static double DistancePointToSegment(CadPoint point, CadPoint start, CadPoint end)
    {
        var segment = end - start;
        var lengthSquared = (segment.X * segment.X) + (segment.Y * segment.Y);
        if (lengthSquared < Epsilon) return (point - start).Length;
        var fromStart = point - start;
        var t = Math.Clamp(((fromStart.X * segment.X) + (fromStart.Y * segment.Y)) / lengthSquared, 0, 1);
        var closest = new CadPoint(start.X + (segment.X * t), start.Y + (segment.Y * t));
        return (point - closest).Length;
    }

    private static double DistanceToSegmentsOrInterior(
        IEnumerable<(CadPoint Start, CadPoint End)> segments,
        CadPoint point,
        IReadOnlyList<CadPoint> polygon)
    {
        if (PointInPolygon(point, polygon)) return 0;
        return segments.Select(segment => DistancePointToSegment(point, segment.Start, segment.End)).DefaultIfEmpty(double.PositiveInfinity).Min();
    }

    private static List<(CadPoint Start, CadPoint End)> GetLinearSegments(ICadEntity entity) => entity switch
    {
        LineEntity line => [(line.Start, line.End)],
        PolylineEntity polyline => EnumerateSegments(polyline).ToList(),
        TextEntity text => GetClosedSegments(GetTextCorners(text)).ToList(),
        LinearDimensionEntity dimension => GetDimensionSegments(dimension).ToList(),
        HatchEntity hatch => GetClosedSegments(hatch.Boundary).ToList(),
        _ => []
    };

    private static IEnumerable<(CadPoint Start, CadPoint End)> EnumerateSegments(PolylineEntity polyline)
    {
        for (var i = 1; i < polyline.Points.Count; i++) yield return (polyline.Points[i - 1], polyline.Points[i]);
        if (polyline.Closed && polyline.Points.Count > 2) yield return (polyline.Points[^1], polyline.Points[0]);
    }

    private static IEnumerable<(CadPoint Start, CadPoint End)> GetClosedSegments(IReadOnlyList<CadPoint> points)
    {
        for (var i = 1; i < points.Count; i++) yield return (points[i - 1], points[i]);
        if (points.Count > 2) yield return (points[^1], points[0]);
    }

    private static bool PointInPolygon(CadPoint point, IReadOnlyList<CadPoint> polygon)
    {
        if (polygon.Count < 3) return false;
        var inside = false;
        for (var i = 0; i < polygon.Count; i++)
        {
            var j = (i + polygon.Count - 1) % polygon.Count;
            var a = polygon[i];
            var b = polygon[j];
            if (((a.Y > point.Y) != (b.Y > point.Y)) &&
                point.X < ((b.X - a.X) * (point.Y - a.Y) / ((b.Y - a.Y) + Epsilon)) + a.X)
                inside = !inside;
        }
        return inside;
    }

    private static bool SegmentIntersectsRectangle(CadPoint start, CadPoint end, CadRect rectangle, double tolerance)
    {
        if (rectangle.Contains(start, tolerance) || rectangle.Contains(end, tolerance)) return true;
        var bottomLeft = new CadPoint(rectangle.Left, rectangle.Bottom);
        var bottomRight = new CadPoint(rectangle.Right, rectangle.Bottom);
        var topRight = new CadPoint(rectangle.Right, rectangle.Top);
        var topLeft = new CadPoint(rectangle.Left, rectangle.Top);
        return TrySegmentIntersection(start, end, bottomLeft, bottomRight, out _) ||
               TrySegmentIntersection(start, end, bottomRight, topRight, out _) ||
               TrySegmentIntersection(start, end, topRight, topLeft, out _) ||
               TrySegmentIntersection(start, end, topLeft, bottomLeft, out _);
    }

    private static bool CircleIntersectsRectangle(CadPoint center, double radius, CadRect rectangle, double tolerance)
    {
        var circleBounds = new CadRect(center.X - radius, center.Y - radius, center.X + radius, center.Y + radius);
        if (rectangle.Contains(circleBounds, tolerance)) return true;
        foreach (var edge in RectangleEdges(rectangle))
            if (SegmentCircleIntersections(edge.Start, edge.End, center, radius).Count > 0) return true;
        return false;
    }

    private static bool ArcIntersectsRectangle(ArcEntity arc, CadRect rectangle, double tolerance)
    {
        if (rectangle.Contains(arc.StartPoint, tolerance) || rectangle.Contains(arc.EndPoint, tolerance)) return true;
        foreach (var edge in RectangleEdges(rectangle))
            if (SegmentCircleIntersections(edge.Start, edge.End, arc.Center, arc.Radius).Any(point => IsPointOnArc(arc, point))) return true;
        return false;
    }

    private static IEnumerable<(CadPoint Start, CadPoint End)> RectangleEdges(CadRect rectangle)
    {
        var bottomLeft = new CadPoint(rectangle.Left, rectangle.Bottom);
        var bottomRight = new CadPoint(rectangle.Right, rectangle.Bottom);
        var topRight = new CadPoint(rectangle.Right, rectangle.Top);
        var topLeft = new CadPoint(rectangle.Left, rectangle.Top);
        yield return (bottomLeft, bottomRight);
        yield return (bottomRight, topRight);
        yield return (topRight, topLeft);
        yield return (topLeft, bottomLeft);
    }

    private static bool TrySegmentIntersection(CadPoint a1, CadPoint a2, CadPoint b1, CadPoint b2, out CadPoint point)
    {
        point = default;
        var r = a2 - a1;
        var s = b2 - b1;
        var denominator = Cross(r.X, r.Y, s.X, s.Y);
        if (Math.Abs(denominator) < Epsilon) return false;
        var qp = b1 - a1;
        var t = Cross(qp.X, qp.Y, s.X, s.Y) / denominator;
        var u = Cross(qp.X, qp.Y, r.X, r.Y) / denominator;
        if (t < -Epsilon || t > 1 + Epsilon || u < -Epsilon || u > 1 + Epsilon) return false;
        point = new CadPoint(a1.X + (r.X * t), a1.Y + (r.Y * t));
        return true;
    }

    private static double Cross(double ax, double ay, double bx, double by) => (ax * by) - (ay * bx);

    private static void AddLinearCurveIntersections(ICollection<CadPoint> result, IReadOnlyList<(CadPoint Start, CadPoint End)> segments, ICadEntity curve)
    {
        foreach (var segment in segments)
        {
            switch (curve)
            {
                case CircleEntity circle:
                    foreach (var point in SegmentCircleIntersections(segment.Start, segment.End, circle.Center, circle.Radius)) AddDistinct(result, point);
                    break;
                case ArcEntity arc:
                    foreach (var point in SegmentCircleIntersections(segment.Start, segment.End, arc.Center, arc.Radius)) if (IsPointOnArc(arc, point)) AddDistinct(result, point);
                    break;
            }
        }
    }

    private static IReadOnlyList<CadPoint> SegmentCircleIntersections(CadPoint start, CadPoint end, CadPoint center, double radius)
    {
        var direction = end - start;
        var offset = start - center;
        var a = (direction.X * direction.X) + (direction.Y * direction.Y);
        if (a < Epsilon) return [];
        var b = 2 * ((offset.X * direction.X) + (offset.Y * direction.Y));
        var c = (offset.X * offset.X) + (offset.Y * offset.Y) - (radius * radius);
        var discriminant = (b * b) - (4 * a * c);
        if (discriminant < -Epsilon) return [];
        var points = new List<CadPoint>();
        var root = Math.Sqrt(Math.Max(0, discriminant));
        foreach (var t in new[] { (-b - root) / (2 * a), (-b + root) / (2 * a) })
            if (t >= -Epsilon && t <= 1 + Epsilon) AddDistinct(points, new CadPoint(start.X + (direction.X * t), start.Y + (direction.Y * t)));
        return points;
    }

    private static void AddCircleCircleIntersections(ICollection<CadPoint> result, CadPoint firstCenter, double firstRadius, CadPoint secondCenter, double secondRadius)
    {
        var delta = secondCenter - firstCenter;
        var distance = delta.Length;
        if (distance < Epsilon || distance > firstRadius + secondRadius + Epsilon || distance < Math.Abs(firstRadius - secondRadius) - Epsilon) return;
        var a = ((firstRadius * firstRadius) - (secondRadius * secondRadius) + (distance * distance)) / (2 * distance);
        var hSquared = (firstRadius * firstRadius) - (a * a);
        if (hSquared < -Epsilon) return;
        var h = Math.Sqrt(Math.Max(0, hSquared));
        var ux = delta.X / distance;
        var uy = delta.Y / distance;
        var basePoint = new CadPoint(firstCenter.X + (a * ux), firstCenter.Y + (a * uy));
        AddDistinct(result, new CadPoint(basePoint.X - (h * uy), basePoint.Y + (h * ux)));
        if (h > Epsilon) AddDistinct(result, new CadPoint(basePoint.X + (h * uy), basePoint.Y - (h * ux)));
    }

    private static void AddDistinct(ICollection<CadPoint> points, CadPoint candidate)
    {
        if (points.Any(point => (point - candidate).Length <= 1e-7)) return;
        points.Add(candidate);
    }
}

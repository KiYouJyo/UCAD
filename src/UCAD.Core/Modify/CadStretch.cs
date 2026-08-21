using UCAD.Core.Entities;
using UCAD.Core.Geometry;

namespace UCAD.Core.Modify;

/// <summary>
/// Crossing-window STRETCH semantics: editable grips inside the window move by the
/// displacement; rigid entities with a single anchor move as a whole when that anchor
/// is inside. Identity is preserved for document replacement/Undo.
/// </summary>
public static class CadStretch
{
    public static bool TryStretch(
        ICadEntity entity,
        CadRect crossingWindow,
        CadVector displacement,
        out ICadEntity? stretched)
    {
        ArgumentNullException.ThrowIfNull(entity);
        stretched = entity switch
        {
            LineEntity line => StretchLine(line, crossingWindow, displacement),
            PolylineEntity polyline => StretchPolyline(polyline, crossingWindow, displacement),
            PointEntity point => crossingWindow.Contains(point.Position)
                ? new PointEntity(Move(point.Position, displacement), point.Id)
                : point,
            SplineEntity spline => StretchSpline(spline, crossingWindow, displacement),
            HatchEntity hatch => StretchHatch(hatch, crossingWindow, displacement),
            LinearDimensionEntity dimension => StretchDimension(dimension, crossingWindow, displacement),
            CircleEntity circle => crossingWindow.Contains(circle.Center)
                ? new CircleEntity(Move(circle.Center, displacement), circle.Radius, circle.Id)
                : circle,
            ArcEntity arc => crossingWindow.Contains(arc.Center)
                ? ArcEntity.Create(Move(arc.Center, displacement), arc.Radius, arc.StartAngleRadians, arc.SweepAngleRadians, arc.Id)
                : arc,
            EllipseEntity ellipse => crossingWindow.Contains(ellipse.Center)
                ? new EllipseEntity(Move(ellipse.Center, displacement), ellipse.MajorAxis, ellipse.Ratio, ellipse.StartParameter, ellipse.EndParameter, ellipse.Id)
                : ellipse,
            RayEntity ray => crossingWindow.Contains(ray.Origin)
                ? new RayEntity(Move(ray.Origin, displacement), ray.Direction, ray.Id)
                : ray,
            XLineEntity xline => crossingWindow.Contains(xline.Point)
                ? new XLineEntity(Move(xline.Point, displacement), xline.Direction, xline.Id)
                : xline,
            TextEntity text => crossingWindow.Contains(text.Position)
                ? new TextEntity(Move(text.Position, displacement), text.Text, text.Height, text.RotationRadians, text.Id)
                : text,
            BlockReferenceEntity block => crossingWindow.Contains(block.InsertionPoint)
                ? CadEntityTransform.Translate(block, displacement)
                : block,
            _ => entity
        };
        return !ReferenceEquals(stretched, entity) && stretched != entity;
    }

    private static ICadEntity StretchLine(LineEntity line, CadRect window, CadVector displacement)
    {
        var startInside = window.Contains(line.Start);
        var endInside = window.Contains(line.End);
        if (!startInside && !endInside) return line;
        var start = startInside ? Move(line.Start, displacement) : line.Start;
        var end = endInside ? Move(line.End, displacement) : line.End;
        if ((end - start).Length <= 1e-9) return line;
        return new LineEntity(start, end, line.Id);
    }

    private static ICadEntity StretchPolyline(PolylineEntity polyline, CadRect window, CadVector displacement)
    {
        var changed = false;
        var points = polyline.Points.Select(point =>
        {
            if (!window.Contains(point)) return point;
            changed = true;
            return Move(point, displacement);
        }).ToArray();
        return changed ? new PolylineEntity(points, polyline.Closed, polyline.Id) : polyline;
    }

    private static ICadEntity StretchSpline(SplineEntity spline, CadRect window, CadVector displacement)
    {
        var changed = false;
        var points = spline.FitPoints.Select(point =>
        {
            if (!window.Contains(point)) return point;
            changed = true;
            return Move(point, displacement);
        }).ToArray();
        return changed ? new SplineEntity(points, spline.Closed, spline.Id) : spline;
    }

    private static ICadEntity StretchHatch(HatchEntity hatch, CadRect window, CadVector displacement)
    {
        var changed = false;
        var points = hatch.Boundary.Select(point =>
        {
            if (!window.Contains(point)) return point;
            changed = true;
            return Move(point, displacement);
        }).ToArray();
        return changed
            ? new HatchEntity(points, hatch.Pattern, hatch.PatternScale, hatch.PatternAngleRadians, hatch.Id)
            : hatch;
    }

    private static ICadEntity StretchDimension(LinearDimensionEntity dimension, CadRect window, CadVector displacement)
    {
        var first = window.Contains(dimension.FirstExtensionPoint)
            ? Move(dimension.FirstExtensionPoint, displacement)
            : dimension.FirstExtensionPoint;
        var second = window.Contains(dimension.SecondExtensionPoint)
            ? Move(dimension.SecondExtensionPoint, displacement)
            : dimension.SecondExtensionPoint;
        var linePoint = window.Contains(dimension.DimensionLinePoint)
            ? Move(dimension.DimensionLinePoint, displacement)
            : dimension.DimensionLinePoint;
        if (first == dimension.FirstExtensionPoint && second == dimension.SecondExtensionPoint && linePoint == dimension.DimensionLinePoint)
            return dimension;
        if ((second - first).Length <= 1e-9) return dimension;
        return new LinearDimensionEntity(first, second, linePoint, dimension.TextOverride, dimension.Id);
    }

    private static CadPoint Move(CadPoint point, CadVector displacement) =>
        new(point.X + displacement.X, point.Y + displacement.Y);
}
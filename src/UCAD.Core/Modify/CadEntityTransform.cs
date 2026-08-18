using UCAD.Core.Entities;
using UCAD.Core.Geometry;

namespace UCAD.Core.Modify;

/// <summary>
/// Shared immutable geometry transforms used by Modify commands, annotation entities,
/// hatches, blocks and extended drawing entities. Edit commands preserve entity identity;
/// copy/insert paths request fresh identities.
/// </summary>
public static class CadEntityTransform
{
    private const double Epsilon = 1e-9;

    public static ICadEntity Translate(ICadEntity entity, CadVector displacement, bool preserveIdentity = true)
    {
        ArgumentNullException.ThrowIfNull(entity);
        return entity switch
        {
            LineEntity line => new LineEntity(TranslatePoint(line.Start, displacement), TranslatePoint(line.End, displacement), Identity(line.Id, preserveIdentity)),
            PolylineEntity polyline => new PolylineEntity(polyline.Points.Select(point => TranslatePoint(point, displacement)), polyline.Closed, Identity(polyline.Id, preserveIdentity)),
            CircleEntity circle => new CircleEntity(TranslatePoint(circle.Center, displacement), circle.Radius, Identity(circle.Id, preserveIdentity)),
            ArcEntity arc => ArcEntity.Create(TranslatePoint(arc.Center, displacement), arc.Radius, arc.StartAngleRadians, arc.SweepAngleRadians, Identity(arc.Id, preserveIdentity)),
            PointEntity point => new PointEntity(TranslatePoint(point.Position, displacement), Identity(point.Id, preserveIdentity)),
            EllipseEntity ellipse => new EllipseEntity(TranslatePoint(ellipse.Center, displacement), ellipse.MajorAxis, ellipse.Ratio, ellipse.StartParameter, ellipse.EndParameter, Identity(ellipse.Id, preserveIdentity)),
            SplineEntity spline => new SplineEntity(spline.FitPoints.Select(point => TranslatePoint(point, displacement)), spline.Closed, Identity(spline.Id, preserveIdentity)),
            RayEntity ray => new RayEntity(TranslatePoint(ray.Origin, displacement), ray.Direction, Identity(ray.Id, preserveIdentity)),
            XLineEntity xline => new XLineEntity(TranslatePoint(xline.Point, displacement), xline.Direction, Identity(xline.Id, preserveIdentity)),
            TextEntity text => new TextEntity(TranslatePoint(text.Position, displacement), text.Text, text.Height, text.RotationRadians, Identity(text.Id, preserveIdentity)),
            LinearDimensionEntity dimension => new LinearDimensionEntity(
                TranslatePoint(dimension.FirstExtensionPoint, displacement),
                TranslatePoint(dimension.SecondExtensionPoint, displacement),
                TranslatePoint(dimension.DimensionLinePoint, displacement),
                dimension.TextOverride,
                Identity(dimension.Id, preserveIdentity)),
            HatchEntity hatch => new HatchEntity(
                hatch.Boundary.Select(point => TranslatePoint(point, displacement)),
                hatch.Pattern,
                hatch.PatternScale,
                hatch.PatternAngleRadians,
                Identity(hatch.Id, preserveIdentity)),
            BlockReferenceEntity block => new BlockReferenceEntity(
                block.DefinitionName,
                TranslatePoint(block.InsertionPoint, displacement),
                block.Contents.Select(child => Translate(child, displacement, preserveIdentity)),
                block.Scale,
                block.RotationRadians,
                Identity(block.Id, preserveIdentity)),
            _ => throw Unsupported(entity)
        };
    }

    public static ICadEntity Rotate(ICadEntity entity, CadPoint basePoint, double angleRadians, bool preserveIdentity = true)
    {
        ArgumentNullException.ThrowIfNull(entity);
        if (!double.IsFinite(angleRadians)) throw new ArgumentOutOfRangeException(nameof(angleRadians));

        return entity switch
        {
            LineEntity line => new LineEntity(RotatePoint(line.Start, basePoint, angleRadians), RotatePoint(line.End, basePoint, angleRadians), Identity(line.Id, preserveIdentity)),
            PolylineEntity polyline => new PolylineEntity(polyline.Points.Select(point => RotatePoint(point, basePoint, angleRadians)), polyline.Closed, Identity(polyline.Id, preserveIdentity)),
            CircleEntity circle => new CircleEntity(RotatePoint(circle.Center, basePoint, angleRadians), circle.Radius, Identity(circle.Id, preserveIdentity)),
            ArcEntity arc => ArcEntity.Create(RotatePoint(arc.Center, basePoint, angleRadians), arc.Radius, arc.StartAngleRadians + angleRadians, arc.SweepAngleRadians, Identity(arc.Id, preserveIdentity)),
            PointEntity point => new PointEntity(RotatePoint(point.Position, basePoint, angleRadians), Identity(point.Id, preserveIdentity)),
            EllipseEntity ellipse => new EllipseEntity(
                RotatePoint(ellipse.Center, basePoint, angleRadians),
                RotateVector(ellipse.MajorAxis, angleRadians),
                ellipse.Ratio,
                ellipse.StartParameter,
                ellipse.EndParameter,
                Identity(ellipse.Id, preserveIdentity)),
            SplineEntity spline => new SplineEntity(spline.FitPoints.Select(point => RotatePoint(point, basePoint, angleRadians)), spline.Closed, Identity(spline.Id, preserveIdentity)),
            RayEntity ray => new RayEntity(RotatePoint(ray.Origin, basePoint, angleRadians), RotateVector(ray.Direction, angleRadians), Identity(ray.Id, preserveIdentity)),
            XLineEntity xline => new XLineEntity(RotatePoint(xline.Point, basePoint, angleRadians), RotateVector(xline.Direction, angleRadians), Identity(xline.Id, preserveIdentity)),
            TextEntity text => new TextEntity(RotatePoint(text.Position, basePoint, angleRadians), text.Text, text.Height, text.RotationRadians + angleRadians, Identity(text.Id, preserveIdentity)),
            LinearDimensionEntity dimension => new LinearDimensionEntity(
                RotatePoint(dimension.FirstExtensionPoint, basePoint, angleRadians),
                RotatePoint(dimension.SecondExtensionPoint, basePoint, angleRadians),
                RotatePoint(dimension.DimensionLinePoint, basePoint, angleRadians),
                dimension.TextOverride,
                Identity(dimension.Id, preserveIdentity)),
            HatchEntity hatch => new HatchEntity(
                hatch.Boundary.Select(point => RotatePoint(point, basePoint, angleRadians)),
                hatch.Pattern,
                hatch.PatternScale,
                hatch.PatternAngleRadians + angleRadians,
                Identity(hatch.Id, preserveIdentity)),
            BlockReferenceEntity block => new BlockReferenceEntity(
                block.DefinitionName,
                RotatePoint(block.InsertionPoint, basePoint, angleRadians),
                block.Contents.Select(child => Rotate(child, basePoint, angleRadians, preserveIdentity)),
                block.Scale,
                block.RotationRadians + angleRadians,
                Identity(block.Id, preserveIdentity)),
            _ => throw Unsupported(entity)
        };
    }

    public static ICadEntity Scale(ICadEntity entity, CadPoint basePoint, double factor, bool preserveIdentity = true)
    {
        ArgumentNullException.ThrowIfNull(entity);
        if (!double.IsFinite(factor) || factor <= Epsilon)
            throw new ArgumentOutOfRangeException(nameof(factor), "Scale factor must be positive and finite.");

        return entity switch
        {
            LineEntity line => new LineEntity(ScalePoint(line.Start, basePoint, factor), ScalePoint(line.End, basePoint, factor), Identity(line.Id, preserveIdentity)),
            PolylineEntity polyline => new PolylineEntity(polyline.Points.Select(point => ScalePoint(point, basePoint, factor)), polyline.Closed, Identity(polyline.Id, preserveIdentity)),
            CircleEntity circle => new CircleEntity(ScalePoint(circle.Center, basePoint, factor), circle.Radius * factor, Identity(circle.Id, preserveIdentity)),
            ArcEntity arc => ArcEntity.Create(ScalePoint(arc.Center, basePoint, factor), arc.Radius * factor, arc.StartAngleRadians, arc.SweepAngleRadians, Identity(arc.Id, preserveIdentity)),
            PointEntity point => new PointEntity(ScalePoint(point.Position, basePoint, factor), Identity(point.Id, preserveIdentity)),
            EllipseEntity ellipse => new EllipseEntity(
                ScalePoint(ellipse.Center, basePoint, factor),
                new CadVector(ellipse.MajorAxis.X * factor, ellipse.MajorAxis.Y * factor),
                ellipse.Ratio,
                ellipse.StartParameter,
                ellipse.EndParameter,
                Identity(ellipse.Id, preserveIdentity)),
            SplineEntity spline => new SplineEntity(spline.FitPoints.Select(point => ScalePoint(point, basePoint, factor)), spline.Closed, Identity(spline.Id, preserveIdentity)),
            RayEntity ray => new RayEntity(ScalePoint(ray.Origin, basePoint, factor), ray.Direction, Identity(ray.Id, preserveIdentity)),
            XLineEntity xline => new XLineEntity(ScalePoint(xline.Point, basePoint, factor), xline.Direction, Identity(xline.Id, preserveIdentity)),
            TextEntity text => new TextEntity(ScalePoint(text.Position, basePoint, factor), text.Text, text.Height * factor, text.RotationRadians, Identity(text.Id, preserveIdentity)),
            LinearDimensionEntity dimension => new LinearDimensionEntity(
                ScalePoint(dimension.FirstExtensionPoint, basePoint, factor),
                ScalePoint(dimension.SecondExtensionPoint, basePoint, factor),
                ScalePoint(dimension.DimensionLinePoint, basePoint, factor),
                dimension.TextOverride,
                Identity(dimension.Id, preserveIdentity)),
            HatchEntity hatch => new HatchEntity(
                hatch.Boundary.Select(point => ScalePoint(point, basePoint, factor)),
                hatch.Pattern,
                hatch.PatternScale * factor,
                hatch.PatternAngleRadians,
                Identity(hatch.Id, preserveIdentity)),
            BlockReferenceEntity block => new BlockReferenceEntity(
                block.DefinitionName,
                ScalePoint(block.InsertionPoint, basePoint, factor),
                block.Contents.Select(child => Scale(child, basePoint, factor, preserveIdentity)),
                block.Scale * factor,
                block.RotationRadians,
                Identity(block.Id, preserveIdentity)),
            _ => throw Unsupported(entity)
        };
    }

    public static ICadEntity Mirror(ICadEntity entity, CadPoint firstAxisPoint, CadPoint secondAxisPoint, bool preserveIdentity = true)
    {
        ArgumentNullException.ThrowIfNull(entity);
        var axis = secondAxisPoint - firstAxisPoint;
        if (axis.Length <= Epsilon) throw new ArgumentException("Mirror axis requires two distinct points.", nameof(secondAxisPoint));

        return entity switch
        {
            LineEntity line => new LineEntity(MirrorPoint(line.Start, firstAxisPoint, secondAxisPoint), MirrorPoint(line.End, firstAxisPoint, secondAxisPoint), Identity(line.Id, preserveIdentity)),
            PolylineEntity polyline => new PolylineEntity(polyline.Points.Select(point => MirrorPoint(point, firstAxisPoint, secondAxisPoint)), polyline.Closed, Identity(polyline.Id, preserveIdentity)),
            CircleEntity circle => new CircleEntity(MirrorPoint(circle.Center, firstAxisPoint, secondAxisPoint), circle.Radius, Identity(circle.Id, preserveIdentity)),
            ArcEntity arc => MirrorArc(arc, firstAxisPoint, secondAxisPoint, preserveIdentity),
            PointEntity point => new PointEntity(MirrorPoint(point.Position, firstAxisPoint, secondAxisPoint), Identity(point.Id, preserveIdentity)),
            EllipseEntity ellipse => new EllipseEntity(
                MirrorPoint(ellipse.Center, firstAxisPoint, secondAxisPoint),
                MirrorVector(ellipse.MajorAxis, firstAxisPoint, secondAxisPoint),
                ellipse.Ratio,
                ellipse.StartParameter,
                ellipse.EndParameter,
                Identity(ellipse.Id, preserveIdentity)),
            SplineEntity spline => new SplineEntity(spline.FitPoints.Select(point => MirrorPoint(point, firstAxisPoint, secondAxisPoint)), spline.Closed, Identity(spline.Id, preserveIdentity)),
            RayEntity ray => new RayEntity(MirrorPoint(ray.Origin, firstAxisPoint, secondAxisPoint), MirrorVector(ray.Direction, firstAxisPoint, secondAxisPoint), Identity(ray.Id, preserveIdentity)),
            XLineEntity xline => new XLineEntity(MirrorPoint(xline.Point, firstAxisPoint, secondAxisPoint), MirrorVector(xline.Direction, firstAxisPoint, secondAxisPoint), Identity(xline.Id, preserveIdentity)),
            TextEntity text => new TextEntity(
                MirrorPoint(text.Position, firstAxisPoint, secondAxisPoint),
                text.Text,
                text.Height,
                MirrorAngle(text.RotationRadians, firstAxisPoint, secondAxisPoint),
                Identity(text.Id, preserveIdentity)),
            LinearDimensionEntity dimension => new LinearDimensionEntity(
                MirrorPoint(dimension.FirstExtensionPoint, firstAxisPoint, secondAxisPoint),
                MirrorPoint(dimension.SecondExtensionPoint, firstAxisPoint, secondAxisPoint),
                MirrorPoint(dimension.DimensionLinePoint, firstAxisPoint, secondAxisPoint),
                dimension.TextOverride,
                Identity(dimension.Id, preserveIdentity)),
            HatchEntity hatch => new HatchEntity(
                hatch.Boundary.Select(point => MirrorPoint(point, firstAxisPoint, secondAxisPoint)),
                hatch.Pattern,
                hatch.PatternScale,
                MirrorAngle(hatch.PatternAngleRadians, firstAxisPoint, secondAxisPoint),
                Identity(hatch.Id, preserveIdentity)),
            BlockReferenceEntity block => new BlockReferenceEntity(
                block.DefinitionName,
                MirrorPoint(block.InsertionPoint, firstAxisPoint, secondAxisPoint),
                block.Contents.Select(child => Mirror(child, firstAxisPoint, secondAxisPoint, preserveIdentity)),
                block.Scale,
                MirrorAngle(block.RotationRadians, firstAxisPoint, secondAxisPoint),
                Identity(block.Id, preserveIdentity)),
            _ => throw Unsupported(entity)
        };
    }

    public static CadPoint RotatePoint(CadPoint point, CadPoint basePoint, double angleRadians)
    {
        var dx = point.X - basePoint.X;
        var dy = point.Y - basePoint.Y;
        var cosine = Math.Cos(angleRadians);
        var sine = Math.Sin(angleRadians);
        return new CadPoint(basePoint.X + (dx * cosine) - (dy * sine), basePoint.Y + (dx * sine) + (dy * cosine));
    }

    public static CadPoint ScalePoint(CadPoint point, CadPoint basePoint, double factor) =>
        new(basePoint.X + ((point.X - basePoint.X) * factor), basePoint.Y + ((point.Y - basePoint.Y) * factor));

    public static CadPoint MirrorPoint(CadPoint point, CadPoint firstAxisPoint, CadPoint secondAxisPoint)
    {
        var axis = secondAxisPoint - firstAxisPoint;
        var denominator = (axis.X * axis.X) + (axis.Y * axis.Y);
        if (denominator <= Epsilon) throw new ArgumentException("Mirror axis requires two distinct points.", nameof(secondAxisPoint));
        var fromAxis = point - firstAxisPoint;
        var projection = ((fromAxis.X * axis.X) + (fromAxis.Y * axis.Y)) / denominator;
        var projected = new CadPoint(firstAxisPoint.X + (axis.X * projection), firstAxisPoint.Y + (axis.Y * projection));
        return new CadPoint((2 * projected.X) - point.X, (2 * projected.Y) - point.Y);
    }

    private static CadVector RotateVector(CadVector vector, double angleRadians)
    {
        var cosine = Math.Cos(angleRadians);
        var sine = Math.Sin(angleRadians);
        return new CadVector((vector.X * cosine) - (vector.Y * sine), (vector.X * sine) + (vector.Y * cosine));
    }

    private static CadVector MirrorVector(CadVector vector, CadPoint firstAxisPoint, CadPoint secondAxisPoint)
    {
        var origin = new CadPoint(0, 0);
        var tip = new CadPoint(vector.X, vector.Y);
        var mirroredOrigin = MirrorPoint(origin, firstAxisPoint, secondAxisPoint);
        var mirroredTip = MirrorPoint(tip, firstAxisPoint, secondAxisPoint);
        return mirroredTip - mirroredOrigin;
    }

    private static double MirrorAngle(double angleRadians, CadPoint firstAxisPoint, CadPoint secondAxisPoint)
    {
        var origin = new CadPoint(0, 0);
        var direction = new CadPoint(Math.Cos(angleRadians), Math.Sin(angleRadians));
        var mirroredOrigin = MirrorPoint(origin, firstAxisPoint, secondAxisPoint);
        var mirroredDirection = MirrorPoint(direction, firstAxisPoint, secondAxisPoint);
        var vector = mirroredDirection - mirroredOrigin;
        return Math.Atan2(vector.Y, vector.X);
    }

    private static ArcEntity MirrorArc(ArcEntity arc, CadPoint firstAxisPoint, CadPoint secondAxisPoint, bool preserveIdentity)
    {
        var center = MirrorPoint(arc.Center, firstAxisPoint, secondAxisPoint);
        var start = MirrorPoint(arc.StartPoint, firstAxisPoint, secondAxisPoint);
        var startAngle = Math.Atan2(start.Y - center.Y, start.X - center.X);
        return ArcEntity.Create(center, arc.Radius, startAngle, -arc.SweepAngleRadians, Identity(arc.Id, preserveIdentity));
    }

    private static CadPoint TranslatePoint(CadPoint point, CadVector displacement) => new(point.X + displacement.X, point.Y + displacement.Y);
    private static Guid Identity(Guid existing, bool preserveIdentity) => preserveIdentity ? existing : Guid.NewGuid();
    private static NotSupportedException Unsupported(ICadEntity entity) => new($"Unsupported CAD entity type: {entity.GetType().Name}");
}
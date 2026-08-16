using UCAD.Core.Entities;
using UCAD.Core.Geometry;

namespace UCAD.Core.Modify;

/// <summary>
/// Shared immutable geometry transforms used by MOVE, COPY, ROTATE, SCALE and MIRROR.
/// Edit commands preserve entity identity; COPY-style previews/clones request fresh identities.
/// </summary>
public static class CadEntityTransform
{
    private const double Epsilon = 1e-9;

    public static ICadEntity Translate(ICadEntity entity, CadVector displacement, bool preserveIdentity = true)
    {
        ArgumentNullException.ThrowIfNull(entity);
        return entity switch
        {
            LineEntity line => new LineEntity(
                TranslatePoint(line.Start, displacement),
                TranslatePoint(line.End, displacement),
                Identity(line.Id, preserveIdentity)),
            PolylineEntity polyline => new PolylineEntity(
                polyline.Points.Select(point => TranslatePoint(point, displacement)),
                polyline.Closed,
                Identity(polyline.Id, preserveIdentity)),
            CircleEntity circle => new CircleEntity(
                TranslatePoint(circle.Center, displacement),
                circle.Radius,
                Identity(circle.Id, preserveIdentity)),
            ArcEntity arc => ArcEntity.Create(
                TranslatePoint(arc.Center, displacement),
                arc.Radius,
                arc.StartAngleRadians,
                arc.SweepAngleRadians,
                Identity(arc.Id, preserveIdentity)),
            _ => throw Unsupported(entity)
        };
    }

    public static ICadEntity Rotate(ICadEntity entity, CadPoint basePoint, double angleRadians, bool preserveIdentity = true)
    {
        ArgumentNullException.ThrowIfNull(entity);
        if (!double.IsFinite(angleRadians))
        {
            throw new ArgumentOutOfRangeException(nameof(angleRadians));
        }

        return entity switch
        {
            LineEntity line => new LineEntity(
                RotatePoint(line.Start, basePoint, angleRadians),
                RotatePoint(line.End, basePoint, angleRadians),
                Identity(line.Id, preserveIdentity)),
            PolylineEntity polyline => new PolylineEntity(
                polyline.Points.Select(point => RotatePoint(point, basePoint, angleRadians)),
                polyline.Closed,
                Identity(polyline.Id, preserveIdentity)),
            CircleEntity circle => new CircleEntity(
                RotatePoint(circle.Center, basePoint, angleRadians),
                circle.Radius,
                Identity(circle.Id, preserveIdentity)),
            ArcEntity arc => ArcEntity.Create(
                RotatePoint(arc.Center, basePoint, angleRadians),
                arc.Radius,
                arc.StartAngleRadians + angleRadians,
                arc.SweepAngleRadians,
                Identity(arc.Id, preserveIdentity)),
            _ => throw Unsupported(entity)
        };
    }

    public static ICadEntity Scale(ICadEntity entity, CadPoint basePoint, double factor, bool preserveIdentity = true)
    {
        ArgumentNullException.ThrowIfNull(entity);
        if (!double.IsFinite(factor) || factor <= Epsilon)
        {
            throw new ArgumentOutOfRangeException(nameof(factor), "Scale factor must be positive and finite.");
        }

        return entity switch
        {
            LineEntity line => new LineEntity(
                ScalePoint(line.Start, basePoint, factor),
                ScalePoint(line.End, basePoint, factor),
                Identity(line.Id, preserveIdentity)),
            PolylineEntity polyline => new PolylineEntity(
                polyline.Points.Select(point => ScalePoint(point, basePoint, factor)),
                polyline.Closed,
                Identity(polyline.Id, preserveIdentity)),
            CircleEntity circle => new CircleEntity(
                ScalePoint(circle.Center, basePoint, factor),
                circle.Radius * factor,
                Identity(circle.Id, preserveIdentity)),
            ArcEntity arc => ArcEntity.Create(
                ScalePoint(arc.Center, basePoint, factor),
                arc.Radius * factor,
                arc.StartAngleRadians,
                arc.SweepAngleRadians,
                Identity(arc.Id, preserveIdentity)),
            _ => throw Unsupported(entity)
        };
    }

    public static ICadEntity Mirror(ICadEntity entity, CadPoint firstAxisPoint, CadPoint secondAxisPoint, bool preserveIdentity = true)
    {
        ArgumentNullException.ThrowIfNull(entity);
        var axis = secondAxisPoint - firstAxisPoint;
        if (axis.Length <= Epsilon)
        {
            throw new ArgumentException("Mirror axis requires two distinct points.", nameof(secondAxisPoint));
        }

        return entity switch
        {
            LineEntity line => new LineEntity(
                MirrorPoint(line.Start, firstAxisPoint, secondAxisPoint),
                MirrorPoint(line.End, firstAxisPoint, secondAxisPoint),
                Identity(line.Id, preserveIdentity)),
            PolylineEntity polyline => new PolylineEntity(
                polyline.Points.Select(point => MirrorPoint(point, firstAxisPoint, secondAxisPoint)),
                polyline.Closed,
                Identity(polyline.Id, preserveIdentity)),
            CircleEntity circle => new CircleEntity(
                MirrorPoint(circle.Center, firstAxisPoint, secondAxisPoint),
                circle.Radius,
                Identity(circle.Id, preserveIdentity)),
            ArcEntity arc => MirrorArc(arc, firstAxisPoint, secondAxisPoint, preserveIdentity),
            _ => throw Unsupported(entity)
        };
    }

    public static CadPoint RotatePoint(CadPoint point, CadPoint basePoint, double angleRadians)
    {
        var dx = point.X - basePoint.X;
        var dy = point.Y - basePoint.Y;
        var cosine = Math.Cos(angleRadians);
        var sine = Math.Sin(angleRadians);
        return new CadPoint(
            basePoint.X + (dx * cosine) - (dy * sine),
            basePoint.Y + (dx * sine) + (dy * cosine));
    }

    public static CadPoint ScalePoint(CadPoint point, CadPoint basePoint, double factor) =>
        new(basePoint.X + ((point.X - basePoint.X) * factor),
            basePoint.Y + ((point.Y - basePoint.Y) * factor));

    public static CadPoint MirrorPoint(CadPoint point, CadPoint firstAxisPoint, CadPoint secondAxisPoint)
    {
        var axis = secondAxisPoint - firstAxisPoint;
        var denominator = (axis.X * axis.X) + (axis.Y * axis.Y);
        if (denominator <= Epsilon)
        {
            throw new ArgumentException("Mirror axis requires two distinct points.", nameof(secondAxisPoint));
        }

        var fromAxis = point - firstAxisPoint;
        var projection = ((fromAxis.X * axis.X) + (fromAxis.Y * axis.Y)) / denominator;
        var projected = new CadPoint(
            firstAxisPoint.X + (axis.X * projection),
            firstAxisPoint.Y + (axis.Y * projection));
        return new CadPoint((2 * projected.X) - point.X, (2 * projected.Y) - point.Y);
    }

    private static ArcEntity MirrorArc(ArcEntity arc, CadPoint firstAxisPoint, CadPoint secondAxisPoint, bool preserveIdentity)
    {
        var center = MirrorPoint(arc.Center, firstAxisPoint, secondAxisPoint);
        var start = MirrorPoint(arc.StartPoint, firstAxisPoint, secondAxisPoint);
        var startAngle = Math.Atan2(start.Y - center.Y, start.X - center.X);
        return ArcEntity.Create(
            center,
            arc.Radius,
            startAngle,
            -arc.SweepAngleRadians,
            Identity(arc.Id, preserveIdentity));
    }

    private static CadPoint TranslatePoint(CadPoint point, CadVector displacement) =>
        new(point.X + displacement.X, point.Y + displacement.Y);

    private static Guid Identity(Guid existing, bool preserveIdentity) =>
        preserveIdentity ? existing : Guid.NewGuid();

    private static NotSupportedException Unsupported(ICadEntity entity) =>
        new($"Unsupported CAD entity type: {entity.GetType().Name}");
}

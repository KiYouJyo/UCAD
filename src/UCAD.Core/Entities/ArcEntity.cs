using UCAD.Core.Geometry;

namespace UCAD.Core.Entities;

public sealed class ArcEntity : ICadEntity
{
    private const double Epsilon = 1e-9;

    private ArcEntity(CadPoint center, double radius, double startAngleRadians, double sweepAngleRadians)
    {
        Center = center;
        Radius = radius;
        StartAngleRadians = startAngleRadians;
        SweepAngleRadians = sweepAngleRadians;
    }

    public Guid Id { get; } = Guid.NewGuid();

    public CadPoint Center { get; }

    public double Radius { get; }

    public double StartAngleRadians { get; }

    public double SweepAngleRadians { get; }

    public double Length => Math.Abs(SweepAngleRadians) * Radius;

    public CadPoint StartPoint => PointAt(0);

    public CadPoint EndPoint => PointAt(1);

    public CadPoint PointAt(double fraction)
    {
        if (!double.IsFinite(fraction))
        {
            throw new ArgumentOutOfRangeException(nameof(fraction));
        }

        var angle = StartAngleRadians + (SweepAngleRadians * fraction);
        return new CadPoint(
            Center.X + (Math.Cos(angle) * Radius),
            Center.Y + (Math.Sin(angle) * Radius));
    }

    public IReadOnlyList<CadPoint> SamplePoints(int segmentCount = 48)
    {
        if (segmentCount < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(segmentCount));
        }

        var points = new CadPoint[segmentCount + 1];
        for (var i = 0; i <= segmentCount; i++)
        {
            points[i] = PointAt((double)i / segmentCount);
        }

        return points;
    }

    public static bool TryCreateFromThreePoints(CadPoint start, CadPoint pointOnArc, CadPoint end, out ArcEntity? arc)
    {
        arc = null;
        var x1 = start.X;
        var y1 = start.Y;
        var x2 = pointOnArc.X;
        var y2 = pointOnArc.Y;
        var x3 = end.X;
        var y3 = end.Y;

        var denominator = 2 * ((x1 * (y2 - y3)) + (x2 * (y3 - y1)) + (x3 * (y1 - y2)));
        if (!double.IsFinite(denominator) || Math.Abs(denominator) < Epsilon)
        {
            return false;
        }

        var p1Squared = (x1 * x1) + (y1 * y1);
        var p2Squared = (x2 * x2) + (y2 * y2);
        var p3Squared = (x3 * x3) + (y3 * y3);
        var center = new CadPoint(
            ((p1Squared * (y2 - y3)) + (p2Squared * (y3 - y1)) + (p3Squared * (y1 - y2))) / denominator,
            ((p1Squared * (x3 - x2)) + (p2Squared * (x1 - x3)) + (p3Squared * (x2 - x1))) / denominator);
        var radius = (start - center).Length;
        if (!double.IsFinite(radius) || radius < Epsilon)
        {
            return false;
        }

        var startAngle = Math.Atan2(start.Y - center.Y, start.X - center.X);
        var middleAngle = Math.Atan2(pointOnArc.Y - center.Y, pointOnArc.X - center.X);
        var endAngle = Math.Atan2(end.Y - center.Y, end.X - center.X);
        var counterClockwiseSweep = NormalizePositive(endAngle - startAngle);
        var middleFromStart = NormalizePositive(middleAngle - startAngle);
        var sweep = middleFromStart <= counterClockwiseSweep + Epsilon
            ? counterClockwiseSweep
            : counterClockwiseSweep - Math.Tau;

        if (Math.Abs(sweep) < Epsilon)
        {
            return false;
        }

        arc = new ArcEntity(center, radius, startAngle, sweep);
        return true;
    }

    private static double NormalizePositive(double angle)
    {
        var normalized = angle % Math.Tau;
        return normalized < 0 ? normalized + Math.Tau : normalized;
    }
}

using UCAD.Core.Geometry;

namespace UCAD.Core.Entities;

/// <summary>
/// 2D ellipse represented by center, major-axis vector and minor/major radius ratio.
/// Parameters follow the DXF ELLIPSE convention, making native/DXF exchange direct.
/// </summary>
public sealed record EllipseEntity : ICadEntity
{
    private const double Epsilon = 1e-9;

    public EllipseEntity(
        CadPoint center,
        CadVector majorAxis,
        double ratio,
        double startParameter = 0,
        double endParameter = Math.Tau)
        : this(center, majorAxis, ratio, startParameter, endParameter, Guid.NewGuid())
    {
    }

    internal EllipseEntity(
        CadPoint center,
        CadVector majorAxis,
        double ratio,
        double startParameter,
        double endParameter,
        Guid id)
    {
        if (!double.IsFinite(majorAxis.X) || !double.IsFinite(majorAxis.Y) || majorAxis.Length <= Epsilon)
            throw new ArgumentException("Ellipse major axis must be non-zero and finite.", nameof(majorAxis));
        if (!double.IsFinite(ratio) || ratio <= Epsilon || ratio > 1 + Epsilon)
            throw new ArgumentOutOfRangeException(nameof(ratio), "Ellipse radius ratio must be in (0, 1].");
        if (!double.IsFinite(startParameter) || !double.IsFinite(endParameter))
            throw new ArgumentOutOfRangeException(nameof(startParameter), "Ellipse parameters must be finite.");

        Center = center;
        MajorAxis = majorAxis;
        Ratio = Math.Min(1, ratio);
        StartParameter = startParameter;
        EndParameter = endParameter;
        Id = id;
    }

    public Guid Id { get; }
    public CadPoint Center { get; }
    public CadVector MajorAxis { get; }
    public double Ratio { get; }
    public double StartParameter { get; }
    public double EndParameter { get; }
    public double MajorRadius => MajorAxis.Length;
    public double MinorRadius => MajorRadius * Ratio;
    public bool IsFullEllipse => Math.Abs(SweepParameter - Math.Tau) <= 1e-8;

    public double SweepParameter
    {
        get
        {
            var sweep = (EndParameter - StartParameter) % Math.Tau;
            if (sweep < 0) sweep += Math.Tau;
            return Math.Abs(sweep) <= Epsilon ? Math.Tau : sweep;
        }
    }

    public CadPoint PointAtParameter(double parameter)
    {
        var majorLength = MajorRadius;
        var ux = MajorAxis.X / majorLength;
        var uy = MajorAxis.Y / majorLength;
        var minorX = -uy * MinorRadius;
        var minorY = ux * MinorRadius;
        return new CadPoint(
            Center.X + (MajorAxis.X * Math.Cos(parameter)) + (minorX * Math.Sin(parameter)),
            Center.Y + (MajorAxis.Y * Math.Cos(parameter)) + (minorY * Math.Sin(parameter)));
    }

    public IReadOnlyList<CadPoint> SamplePoints(int segmentCount = 96)
    {
        segmentCount = Math.Clamp(segmentCount, 12, 720);
        var sweep = SweepParameter;
        var points = new CadPoint[segmentCount + 1];
        for (var i = 0; i <= segmentCount; i++)
        {
            var t = StartParameter + (sweep * i / segmentCount);
            points[i] = PointAtParameter(t);
        }
        return points;
    }
}
using UCAD.Core.Geometry;

namespace UCAD.Core.Entities;

public sealed record RadialDimensionEntity : ICadEntity
{
    public RadialDimensionEntity(
        CadPoint center,
        CadPoint pointOnCircle,
        CadPoint textPoint,
        bool diameter = false,
        string? textOverride = null,
        string styleName = "Standard")
        : this(center, pointOnCircle, textPoint, diameter, textOverride, styleName, Guid.NewGuid()) { }

    internal RadialDimensionEntity(
        CadPoint center,
        CadPoint pointOnCircle,
        CadPoint textPoint,
        bool diameter,
        string? textOverride,
        string styleName,
        Guid id)
    {
        if ((pointOnCircle - center).Length <= 1e-9) throw new ArgumentException("Radial dimension requires a positive radius.", nameof(pointOnCircle));
        if (string.IsNullOrWhiteSpace(styleName)) throw new ArgumentException("Dimension style cannot be empty.", nameof(styleName));
        Center = center;
        PointOnCircle = pointOnCircle;
        TextPoint = textPoint;
        Diameter = diameter;
        TextOverride = string.IsNullOrWhiteSpace(textOverride) ? null : textOverride;
        StyleName = styleName.Trim();
        Id = id;
    }

    public Guid Id { get; }
    public CadPoint Center { get; }
    public CadPoint PointOnCircle { get; }
    public CadPoint TextPoint { get; }
    public bool Diameter { get; }
    public string? TextOverride { get; }
    public string StyleName { get; }
    public double Radius => (PointOnCircle - Center).Length;
    public double Measurement => Diameter ? Radius * 2 : Radius;
}
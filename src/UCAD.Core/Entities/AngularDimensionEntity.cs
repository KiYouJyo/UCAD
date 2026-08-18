using UCAD.Core.Geometry;

namespace UCAD.Core.Entities;

public sealed record AngularDimensionEntity : ICadEntity
{
    public AngularDimensionEntity(
        CadPoint vertex,
        CadPoint firstRayPoint,
        CadPoint secondRayPoint,
        CadPoint arcPoint,
        string? textOverride = null,
        string styleName = "Standard")
        : this(vertex, firstRayPoint, secondRayPoint, arcPoint, textOverride, styleName, Guid.NewGuid()) { }

    internal AngularDimensionEntity(
        CadPoint vertex,
        CadPoint firstRayPoint,
        CadPoint secondRayPoint,
        CadPoint arcPoint,
        string? textOverride,
        string styleName,
        Guid id)
    {
        if ((firstRayPoint - vertex).Length <= 1e-9 || (secondRayPoint - vertex).Length <= 1e-9)
            throw new ArgumentException("Angular dimension rays must have non-zero length.");
        if ((arcPoint - vertex).Length <= 1e-9) throw new ArgumentException("Angular dimension arc point must differ from the vertex.", nameof(arcPoint));
        if (string.IsNullOrWhiteSpace(styleName)) throw new ArgumentException("Dimension style cannot be empty.", nameof(styleName));
        Vertex = vertex;
        FirstRayPoint = firstRayPoint;
        SecondRayPoint = secondRayPoint;
        ArcPoint = arcPoint;
        TextOverride = string.IsNullOrWhiteSpace(textOverride) ? null : textOverride;
        StyleName = styleName.Trim();
        Id = id;
    }

    public Guid Id { get; }
    public CadPoint Vertex { get; }
    public CadPoint FirstRayPoint { get; }
    public CadPoint SecondRayPoint { get; }
    public CadPoint ArcPoint { get; }
    public string? TextOverride { get; }
    public string StyleName { get; }
    public double Radius => (ArcPoint - Vertex).Length;

    public double MeasurementRadians
    {
        get
        {
            var first = FirstRayPoint - Vertex;
            var second = SecondRayPoint - Vertex;
            var firstAngle = Math.Atan2(first.Y, first.X);
            var secondAngle = Math.Atan2(second.Y, second.X);
            var sweep = (secondAngle - firstAngle) % Math.Tau;
            if (sweep < 0) sweep += Math.Tau;
            return sweep > Math.PI ? Math.Tau - sweep : sweep;
        }
    }
}
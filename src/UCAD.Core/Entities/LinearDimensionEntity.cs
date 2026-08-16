using UCAD.Core.Geometry;

namespace UCAD.Core.Entities;

/// <summary>
/// Foundational aligned linear dimension. The dimension line is parallel to the
/// measured segment and offset through DimensionLinePoint.
/// </summary>
public sealed record LinearDimensionEntity : ICadEntity
{
    public LinearDimensionEntity(
        CadPoint firstExtensionPoint,
        CadPoint secondExtensionPoint,
        CadPoint dimensionLinePoint,
        string? textOverride = null)
        : this(firstExtensionPoint, secondExtensionPoint, dimensionLinePoint, textOverride, Guid.NewGuid())
    {
    }

    internal LinearDimensionEntity(
        CadPoint firstExtensionPoint,
        CadPoint secondExtensionPoint,
        CadPoint dimensionLinePoint,
        string? textOverride,
        Guid id)
    {
        if ((secondExtensionPoint - firstExtensionPoint).Length <= 1e-9)
            throw new ArgumentException("Dimension extension points must be distinct.", nameof(secondExtensionPoint));
        FirstExtensionPoint = firstExtensionPoint;
        SecondExtensionPoint = secondExtensionPoint;
        DimensionLinePoint = dimensionLinePoint;
        TextOverride = string.IsNullOrWhiteSpace(textOverride) ? null : textOverride;
        Id = id;
    }

    public Guid Id { get; }
    public CadPoint FirstExtensionPoint { get; }
    public CadPoint SecondExtensionPoint { get; }
    public CadPoint DimensionLinePoint { get; }
    public string? TextOverride { get; }
    public double Measurement => (SecondExtensionPoint - FirstExtensionPoint).Length;

    public (CadPoint First, CadPoint Second) GetDimensionLineEndpoints()
    {
        var direction = SecondExtensionPoint - FirstExtensionPoint;
        var length = direction.Length;
        var normalX = -direction.Y / length;
        var normalY = direction.X / length;
        var fromFirst = DimensionLinePoint - FirstExtensionPoint;
        var offset = (fromFirst.X * normalX) + (fromFirst.Y * normalY);
        var offsetVector = new CadVector(normalX * offset, normalY * offset);
        return (FirstExtensionPoint + offsetVector, SecondExtensionPoint + offsetVector);
    }
}

using UCAD.Core.Geometry;
using UCAD.Core.Styles;

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
        : this(firstExtensionPoint, secondExtensionPoint, dimensionLinePoint, textOverride, CadDimensionStyle.DefaultName, Guid.NewGuid()) { }

    public LinearDimensionEntity(
        CadPoint firstExtensionPoint,
        CadPoint secondExtensionPoint,
        CadPoint dimensionLinePoint,
        string? textOverride,
        string styleName)
        : this(firstExtensionPoint, secondExtensionPoint, dimensionLinePoint, textOverride, styleName, Guid.NewGuid()) { }

    internal LinearDimensionEntity(
        CadPoint firstExtensionPoint,
        CadPoint secondExtensionPoint,
        CadPoint dimensionLinePoint,
        string? textOverride,
        Guid id)
        : this(firstExtensionPoint, secondExtensionPoint, dimensionLinePoint, textOverride, CadDimensionStyle.DefaultName, id) { }

    internal LinearDimensionEntity(
        CadPoint firstExtensionPoint,
        CadPoint secondExtensionPoint,
        CadPoint dimensionLinePoint,
        string? textOverride,
        string styleName,
        Guid id)
    {
        if ((secondExtensionPoint - firstExtensionPoint).Length <= 1e-9)
            throw new ArgumentException("Dimension extension points must be distinct.", nameof(secondExtensionPoint));
        if (string.IsNullOrWhiteSpace(styleName)) throw new ArgumentException("Dimension style cannot be empty.", nameof(styleName));
        FirstExtensionPoint = firstExtensionPoint;
        SecondExtensionPoint = secondExtensionPoint;
        DimensionLinePoint = dimensionLinePoint;
        TextOverride = string.IsNullOrWhiteSpace(textOverride) ? null : textOverride;
        StyleName = styleName.Trim();
        Id = id;
    }

    public Guid Id { get; }
    public CadPoint FirstExtensionPoint { get; }
    public CadPoint SecondExtensionPoint { get; }
    public CadPoint DimensionLinePoint { get; }
    public string? TextOverride { get; }
    public string StyleName { get; }
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
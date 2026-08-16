namespace UCAD.Core.Layers;

/// <summary>
/// Per-entity display properties. Null color/lineweight mean ByLayer.
/// LineType defaults to ByLayer so later DXF mapping can distinguish inheritance.
/// </summary>
public sealed record CadEntityProperties
{
    public CadEntityProperties(
        string layerName = CadLayer.DefaultLayerName,
        string? colorHex = null,
        double? lineWeight = null,
        string lineType = "ByLayer")
    {
        if (string.IsNullOrWhiteSpace(layerName))
        {
            throw new ArgumentException("Entity layer cannot be empty.", nameof(layerName));
        }
        if (colorHex is not null && !CadLayer.IsValidColor(colorHex))
        {
            throw new ArgumentException("Entity color must use #RRGGBB format or be null for ByLayer.", nameof(colorHex));
        }
        if (lineWeight is not null && (!double.IsFinite(lineWeight.Value) || lineWeight.Value <= 0))
        {
            throw new ArgumentOutOfRangeException(nameof(lineWeight));
        }
        if (string.IsNullOrWhiteSpace(lineType))
        {
            throw new ArgumentException("Line type cannot be empty.", nameof(lineType));
        }

        LayerName = layerName.Trim();
        ColorHex = colorHex?.ToUpperInvariant();
        LineWeight = lineWeight;
        LineType = lineType.Trim();
    }

    public string LayerName { get; init; }
    public string? ColorHex { get; init; }
    public double? LineWeight { get; init; }
    public string LineType { get; init; }
}

namespace UCAD.Core.Layers;

/// <summary>
/// Per-entity display properties. Null color/lineweight/opacity mean ByLayer/default.
/// LineType defaults to ByLayer so later DXF mapping can distinguish inheritance.
/// SourceOrder/SourceHandle are import-only display metadata used to preserve AutoCAD
/// entity ordering across semantic/fallback repair passes; authored UCAD entities leave them null.
/// </summary>
public sealed record CadEntityProperties
{
    public CadEntityProperties(
        string layerName = CadLayer.DefaultLayerName,
        string? colorHex = null,
        double? lineWeight = null,
        string lineType = "ByLayer",
        int? sourceOrder = null,
        string? sourceHandle = null,
        double? opacity = null)
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
        if (sourceOrder is < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sourceOrder));
        }
        if (opacity is not null && (!double.IsFinite(opacity.Value) || opacity.Value < 0 || opacity.Value > 1))
        {
            throw new ArgumentOutOfRangeException(nameof(opacity), "Opacity must be between 0 and 1.");
        }

        LayerName = layerName.Trim();
        ColorHex = colorHex?.ToUpperInvariant();
        LineWeight = lineWeight;
        LineType = lineType.Trim();
        SourceOrder = sourceOrder;
        SourceHandle = string.IsNullOrWhiteSpace(sourceHandle) ? null : sourceHandle.Trim();
        Opacity = opacity;
    }

    public string LayerName { get; init; }
    public string? ColorHex { get; init; }
    public double? LineWeight { get; init; }
    public string LineType { get; init; }
    public int? SourceOrder { get; init; }
    public string? SourceHandle { get; init; }
    public double? Opacity { get; init; }
}

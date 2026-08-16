namespace UCAD.Core.Layers;

/// <summary>
/// Immutable layer definition. Entity color/lineweight may override the layer value;
/// otherwise the layer is the source of truth for display properties.
/// </summary>
public sealed record CadLayer
{
    public const string DefaultLayerName = "0";
    public const string DefaultColorHex = "#EDEDF2";

    public CadLayer(
        string name,
        string colorHex = DefaultColorHex,
        double lineWeight = 0.25,
        string lineType = "Continuous",
        bool isVisible = true,
        bool isLocked = false)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Layer name cannot be empty.", nameof(name));
        }
        if (!IsValidColor(colorHex))
        {
            throw new ArgumentException("Layer color must use #RRGGBB format.", nameof(colorHex));
        }
        if (!double.IsFinite(lineWeight) || lineWeight <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(lineWeight));
        }
        if (string.IsNullOrWhiteSpace(lineType))
        {
            throw new ArgumentException("Line type cannot be empty.", nameof(lineType));
        }

        Name = name.Trim();
        ColorHex = colorHex.ToUpperInvariant();
        LineWeight = lineWeight;
        LineType = lineType.Trim();
        IsVisible = isVisible;
        IsLocked = isLocked;
    }

    public string Name { get; init; }
    public string ColorHex { get; init; }
    public double LineWeight { get; init; }
    public string LineType { get; init; }
    public bool IsVisible { get; init; }
    public bool IsLocked { get; init; }

    public static CadLayer CreateDefault() => new(DefaultLayerName);

    public static bool IsValidColor(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length != 7 || value[0] != '#')
        {
            return false;
        }
        return value.AsSpan(1).ToString().All(Uri.IsHexDigit);
    }
}

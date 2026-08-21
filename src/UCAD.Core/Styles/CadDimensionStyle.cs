namespace UCAD.Core.Styles;

public sealed record CadDimensionStyle
{
    public const string DefaultName = "Standard";

    public CadDimensionStyle(
        string name,
        double textHeight = 2.5,
        double arrowSize = 2.5,
        int precision = 2,
        string prefix = "",
        string suffix = "")
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Dimension style name cannot be empty.", nameof(name));
        if (!double.IsFinite(textHeight) || textHeight <= 0) throw new ArgumentOutOfRangeException(nameof(textHeight));
        if (!double.IsFinite(arrowSize) || arrowSize <= 0) throw new ArgumentOutOfRangeException(nameof(arrowSize));
        if (precision is < 0 or > 8) throw new ArgumentOutOfRangeException(nameof(precision));
        Name = name.Trim();
        TextHeight = textHeight;
        ArrowSize = arrowSize;
        Precision = precision;
        Prefix = prefix ?? string.Empty;
        Suffix = suffix ?? string.Empty;
    }

    public string Name { get; }
    public double TextHeight { get; }
    public double ArrowSize { get; }
    public int Precision { get; }
    public string Prefix { get; }
    public string Suffix { get; }

    public string Format(double value) => $"{Prefix}{value.ToString($"F{Precision}", System.Globalization.CultureInfo.InvariantCulture)}{Suffix}";

    public static CadDimensionStyle CreateDefault() => new(DefaultName);
}
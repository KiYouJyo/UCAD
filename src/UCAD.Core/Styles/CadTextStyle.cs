namespace UCAD.Core.Styles;

public sealed record CadTextStyle
{
    public const string DefaultName = "Standard";

    public CadTextStyle(
        string name,
        string fontFamily = "Segoe UI",
        double widthFactor = 1,
        double obliqueAngleDegrees = 0)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Text style name cannot be empty.", nameof(name));
        if (string.IsNullOrWhiteSpace(fontFamily)) throw new ArgumentException("Font family cannot be empty.", nameof(fontFamily));
        if (!double.IsFinite(widthFactor) || widthFactor <= 0) throw new ArgumentOutOfRangeException(nameof(widthFactor));
        if (!double.IsFinite(obliqueAngleDegrees) || Math.Abs(obliqueAngleDegrees) >= 85) throw new ArgumentOutOfRangeException(nameof(obliqueAngleDegrees));
        Name = name.Trim();
        FontFamily = fontFamily.Trim();
        WidthFactor = widthFactor;
        ObliqueAngleDegrees = obliqueAngleDegrees;
    }

    public string Name { get; }
    public string FontFamily { get; }
    public double WidthFactor { get; }
    public double ObliqueAngleDegrees { get; }

    public static CadTextStyle CreateDefault() => new(DefaultName);
}
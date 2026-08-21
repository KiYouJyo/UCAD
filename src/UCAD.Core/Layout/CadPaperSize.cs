namespace UCAD.Core.Layout;

/// <summary>
/// Physical paper dimensions in millimetres. UCAD keeps page geometry in mm even when
/// model space later adopts another drawing unit, matching common CAD plot workflows.
/// </summary>
public sealed record CadPaperSize
{
    public CadPaperSize(string name, double widthMm, double heightMm)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Paper size name cannot be empty.", nameof(name));
        if (!double.IsFinite(widthMm) || widthMm <= 0) throw new ArgumentOutOfRangeException(nameof(widthMm));
        if (!double.IsFinite(heightMm) || heightMm <= 0) throw new ArgumentOutOfRangeException(nameof(heightMm));
        Name = name.Trim();
        WidthMm = widthMm;
        HeightMm = heightMm;
    }

    public string Name { get; }
    public double WidthMm { get; }
    public double HeightMm { get; }

    public static CadPaperSize A0 { get; } = new("ISO A0", 841, 1189);
    public static CadPaperSize A1 { get; } = new("ISO A1", 594, 841);
    public static CadPaperSize A2 { get; } = new("ISO A2", 420, 594);
    public static CadPaperSize A3 { get; } = new("ISO A3", 297, 420);
    public static CadPaperSize A4 { get; } = new("ISO A4", 210, 297);

    public static IReadOnlyList<CadPaperSize> IsoA { get; } = [A0, A1, A2, A3, A4];

    public (double WidthMm, double HeightMm) Oriented(bool landscape) => landscape
        ? (Math.Max(WidthMm, HeightMm), Math.Min(WidthMm, HeightMm))
        : (Math.Min(WidthMm, HeightMm), Math.Max(WidthMm, HeightMm));
}
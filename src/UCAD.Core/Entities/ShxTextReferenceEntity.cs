using UCAD.Core.Geometry;

namespace UCAD.Core.Entities;

/// <summary>
/// Display-only reference for TEXT/MTEXT whose AutoCAD text style is backed by SHX/SHP
/// resources. The file resolver replaces this entity with decoded vector polylines when
/// the support files are available, preserving source draw order and visual properties.
/// </summary>
public sealed class ShxTextReferenceEntity : ICadEntity
{
    public ShxTextReferenceEntity(
        string text,
        string primaryFontPath,
        string? bigFontPath,
        CadPoint insertionPoint,
        double height,
        double widthFactor,
        double obliqueRadians,
        double rotationRadians,
        string? sourceCodePage = null,
        bool multiline = false,
        double lineSpacingFactor = 1,
        int horizontalAlignment = 0,
        int verticalAlignment = 0,
        CadPoint? alignmentPoint = null,
        bool mirrorX = false,
        bool mirrorY = false)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentException.ThrowIfNullOrWhiteSpace(primaryFontPath);
        if (!double.IsFinite(height) || height <= 0) throw new ArgumentOutOfRangeException(nameof(height));
        if (!double.IsFinite(widthFactor) || Math.Abs(widthFactor) <= 1e-12) throw new ArgumentOutOfRangeException(nameof(widthFactor));
        if (!double.IsFinite(obliqueRadians) || Math.Abs(obliqueRadians) >= Math.PI / 2) throw new ArgumentOutOfRangeException(nameof(obliqueRadians));
        if (!double.IsFinite(rotationRadians)) throw new ArgumentOutOfRangeException(nameof(rotationRadians));
        if (!double.IsFinite(lineSpacingFactor) || lineSpacingFactor <= 0) throw new ArgumentOutOfRangeException(nameof(lineSpacingFactor));

        Id = Guid.NewGuid();
        Text = text;
        PrimaryFontPath = primaryFontPath.Trim();
        BigFontPath = string.IsNullOrWhiteSpace(bigFontPath) ? null : bigFontPath.Trim();
        InsertionPoint = insertionPoint;
        Height = height;
        WidthFactor = widthFactor;
        ObliqueRadians = obliqueRadians;
        RotationRadians = rotationRadians;
        SourceCodePage = sourceCodePage;
        Multiline = multiline;
        LineSpacingFactor = lineSpacingFactor;
        HorizontalAlignment = horizontalAlignment;
        VerticalAlignment = verticalAlignment;
        AlignmentPoint = alignmentPoint;
        MirrorX = mirrorX;
        MirrorY = mirrorY;
    }

    public Guid Id { get; }
    public string Text { get; }
    public string PrimaryFontPath { get; }
    public string? BigFontPath { get; }
    public CadPoint InsertionPoint { get; }
    public double Height { get; }
    public double WidthFactor { get; }
    public double ObliqueRadians { get; }
    public double RotationRadians { get; }
    public string? SourceCodePage { get; }
    public bool Multiline { get; }
    public double LineSpacingFactor { get; }
    public int HorizontalAlignment { get; }
    public int VerticalAlignment { get; }
    public CadPoint? AlignmentPoint { get; }
    public bool MirrorX { get; }
    public bool MirrorY { get; }
}

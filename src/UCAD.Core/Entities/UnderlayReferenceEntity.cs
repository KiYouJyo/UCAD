using UCAD.Core.Geometry;

namespace UCAD.Core.Entities;

public enum CadUnderlayKind
{
    Pdf,
    Dwf,
    Dgn,
    Unknown
}

/// <summary>
/// Display-only AutoCAD PDF/DWF/DGN underlay reference. The external document remains
/// outside the DWG/DXF container, so UCAD preserves its source file/page, placement,
/// clipping and display adjustments and lets the viewport choose the best available renderer.
/// </summary>
public sealed class UnderlayReferenceEntity : ICadEntity
{
    private readonly IReadOnlyList<CadPoint> _clipBoundary;

    public UnderlayReferenceEntity(
        CadUnderlayKind kind,
        string referencePath,
        string? page,
        CadPoint insertionPoint,
        double xScale,
        double yScale,
        double rotationRadians,
        IEnumerable<CadPoint>? clipBoundary = null,
        byte contrast = 100,
        byte fade = 0,
        bool monochrome = false,
        bool adjustForBackground = false,
        bool clipInside = true)
        : this(kind, referencePath, page, insertionPoint, xScale, yScale, rotationRadians,
            clipBoundary, contrast, fade, monochrome, adjustForBackground, clipInside, null, Guid.NewGuid())
    {
    }

    internal UnderlayReferenceEntity(
        CadUnderlayKind kind,
        string referencePath,
        string? page,
        CadPoint insertionPoint,
        double xScale,
        double yScale,
        double rotationRadians,
        IEnumerable<CadPoint>? clipBoundary,
        byte contrast,
        byte fade,
        bool monochrome,
        bool adjustForBackground,
        bool clipInside,
        string? resolvedPath,
        Guid id)
    {
        if (string.IsNullOrWhiteSpace(referencePath)) throw new ArgumentException("Underlay reference path cannot be empty.", nameof(referencePath));
        if (!double.IsFinite(xScale) || Math.Abs(xScale) <= 1e-12) throw new ArgumentOutOfRangeException(nameof(xScale));
        if (!double.IsFinite(yScale) || Math.Abs(yScale) <= 1e-12) throw new ArgumentOutOfRangeException(nameof(yScale));
        if (!double.IsFinite(rotationRadians)) throw new ArgumentOutOfRangeException(nameof(rotationRadians));

        Kind = kind;
        ReferencePath = referencePath.Trim();
        Page = string.IsNullOrWhiteSpace(page) ? null : page.Trim();
        InsertionPoint = insertionPoint;
        XScale = xScale;
        YScale = yScale;
        RotationRadians = rotationRadians;
        _clipBoundary = Array.AsReadOnly((clipBoundary ?? []).ToArray());
        Contrast = contrast;
        Fade = fade;
        Monochrome = monochrome;
        AdjustForBackground = adjustForBackground;
        ClipInside = clipInside;
        ResolvedPath = resolvedPath;
        Id = id;
    }

    public Guid Id { get; }
    public CadUnderlayKind Kind { get; }
    public string ReferencePath { get; }
    public string? Page { get; }
    public string? ResolvedPath { get; private set; }
    public bool IsResolved => !string.IsNullOrWhiteSpace(ResolvedPath);
    public CadPoint InsertionPoint { get; }
    public double XScale { get; }
    public double YScale { get; }
    public double RotationRadians { get; }
    public IReadOnlyList<CadPoint> ClipBoundary => _clipBoundary;
    public byte Contrast { get; }
    public byte Fade { get; }
    public bool Monochrome { get; }
    public bool AdjustForBackground { get; }
    public bool ClipInside { get; }

    internal void SetResolvedPath(string? path) => ResolvedPath = string.IsNullOrWhiteSpace(path) ? null : Path.GetFullPath(path);
}

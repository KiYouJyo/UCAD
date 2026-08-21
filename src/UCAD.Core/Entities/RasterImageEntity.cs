using UCAD.Core.Geometry;

namespace UCAD.Core.Entities;

/// <summary>
/// Display-only AutoCAD IMAGE reference. AutoCAD normally stores raster pixels outside
/// the DWG/DXF container; this entity preserves the external path, pixel-to-world basis,
/// clipping polygon and display adjustments so the app can resolve and paint the real image.
/// </summary>
public sealed class RasterImageEntity : ICadEntity
{
    private readonly IReadOnlyList<CadPoint> _clipBoundary;

    public RasterImageEntity(
        string referencePath,
        CadPoint insertionPoint,
        CadVector uVectorPerPixel,
        CadVector vVectorPerPixel,
        double pixelWidth,
        double pixelHeight,
        IEnumerable<CadPoint> clipBoundary,
        byte brightness = 50,
        byte contrast = 50,
        byte fade = 0,
        bool transparencyEnabled = false)
        : this(referencePath, insertionPoint, uVectorPerPixel, vVectorPerPixel, pixelWidth, pixelHeight,
            clipBoundary, brightness, contrast, fade, transparencyEnabled, null, Guid.NewGuid())
    {
    }

    internal RasterImageEntity(
        string referencePath,
        CadPoint insertionPoint,
        CadVector uVectorPerPixel,
        CadVector vVectorPerPixel,
        double pixelWidth,
        double pixelHeight,
        IEnumerable<CadPoint> clipBoundary,
        byte brightness,
        byte contrast,
        byte fade,
        bool transparencyEnabled,
        string? resolvedPath,
        Guid id)
    {
        if (string.IsNullOrWhiteSpace(referencePath)) throw new ArgumentException("Raster image reference path cannot be empty.", nameof(referencePath));
        if (!double.IsFinite(pixelWidth) || pixelWidth <= 0) throw new ArgumentOutOfRangeException(nameof(pixelWidth));
        if (!double.IsFinite(pixelHeight) || pixelHeight <= 0) throw new ArgumentOutOfRangeException(nameof(pixelHeight));
        ArgumentNullException.ThrowIfNull(clipBoundary);
        var clip = clipBoundary.ToArray();
        if (clip.Length < 3 || clip.Distinct().Count() < 3) throw new ArgumentException("Raster image clipping boundary requires at least three distinct points.", nameof(clipBoundary));

        ReferencePath = referencePath.Trim();
        InsertionPoint = insertionPoint;
        UVectorPerPixel = uVectorPerPixel;
        VVectorPerPixel = vVectorPerPixel;
        PixelWidth = pixelWidth;
        PixelHeight = pixelHeight;
        _clipBoundary = Array.AsReadOnly(clip);
        Brightness = brightness;
        Contrast = contrast;
        Fade = fade;
        TransparencyEnabled = transparencyEnabled;
        ResolvedPath = resolvedPath;
        Id = id;
    }

    public Guid Id { get; }
    public string ReferencePath { get; }
    public string? ResolvedPath { get; private set; }
    public bool IsResolved => !string.IsNullOrWhiteSpace(ResolvedPath);
    public CadPoint InsertionPoint { get; }
    public CadVector UVectorPerPixel { get; }
    public CadVector VVectorPerPixel { get; }
    public double PixelWidth { get; }
    public double PixelHeight { get; }
    public IReadOnlyList<CadPoint> ClipBoundary => _clipBoundary;
    public byte Brightness { get; }
    public byte Contrast { get; }
    public byte Fade { get; }
    public bool TransparencyEnabled { get; }

    internal void SetResolvedPath(string? path) => ResolvedPath = string.IsNullOrWhiteSpace(path) ? null : Path.GetFullPath(path);
}

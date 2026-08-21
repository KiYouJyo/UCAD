using UCAD.Core.Geometry;

namespace UCAD.Core.Entities;

/// <summary>
/// Display-only AutoCAD SHAPE reference retained until the external SHX/SHP resource can
/// be resolved. Once resolved, the reference is replaced in-place with ordinary UCAD
/// vector geometry so the renderer and plot paths do not need a special font engine.
/// </summary>
public sealed class ShapeReferenceEntity : ICadEntity
{
    public ShapeReferenceEntity(
        string shapeName,
        IEnumerable<string> referencePaths,
        CadPoint insertionPoint,
        double size = 1,
        double xScale = 1,
        double rotationRadians = 0,
        double obliqueRadians = 0)
        : this(shapeName, referencePaths, insertionPoint, size, xScale, rotationRadians, obliqueRadians, Guid.NewGuid())
    {
    }

    internal ShapeReferenceEntity(
        string shapeName,
        IEnumerable<string> referencePaths,
        CadPoint insertionPoint,
        double size,
        double xScale,
        double rotationRadians,
        double obliqueRadians,
        Guid id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(shapeName);
        ArgumentNullException.ThrowIfNull(referencePaths);
        if (!double.IsFinite(size) || size <= 0) throw new ArgumentOutOfRangeException(nameof(size));
        if (!double.IsFinite(xScale) || Math.Abs(xScale) <= 1e-12) throw new ArgumentOutOfRangeException(nameof(xScale));
        if (!double.IsFinite(rotationRadians)) throw new ArgumentOutOfRangeException(nameof(rotationRadians));
        if (!double.IsFinite(obliqueRadians) || Math.Abs(obliqueRadians) >= Math.PI / 2) throw new ArgumentOutOfRangeException(nameof(obliqueRadians));

        var paths = referencePaths
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Select(path => path.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (paths.Length == 0) throw new ArgumentException("A SHAPE reference requires at least one SHX/SHP candidate path.", nameof(referencePaths));

        Id = id;
        ShapeName = shapeName.Trim();
        ReferencePaths = paths;
        InsertionPoint = insertionPoint;
        Size = size;
        XScale = xScale;
        RotationRadians = rotationRadians;
        ObliqueRadians = obliqueRadians;
    }

    public Guid Id { get; }
    public string ShapeName { get; }
    public IReadOnlyList<string> ReferencePaths { get; }
    public CadPoint InsertionPoint { get; }
    public double Size { get; }
    public double XScale { get; }
    public double RotationRadians { get; }
    public double ObliqueRadians { get; }
}

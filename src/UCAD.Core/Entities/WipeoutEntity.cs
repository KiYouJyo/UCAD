using UCAD.Core.Geometry;

namespace UCAD.Core.Entities;

/// <summary>
/// Display-only AutoCAD WIPEOUT mask. The boundary is stored in world coordinates;
/// the viewport fills it with the current canvas background so the mask remains correct
/// in both light and dark canvas themes. Editing semantics intentionally remain deferred.
/// </summary>
public sealed class WipeoutEntity : ICadEntity
{
    private readonly IReadOnlyList<CadPoint> _boundary;

    public WipeoutEntity(IEnumerable<CadPoint> boundary)
        : this(boundary, Guid.NewGuid())
    {
    }

    internal WipeoutEntity(IEnumerable<CadPoint> boundary, Guid id)
    {
        ArgumentNullException.ThrowIfNull(boundary);
        var points = boundary.ToArray();
        if (points.Length < 3) throw new ArgumentException("A wipeout boundary requires at least three points.", nameof(boundary));
        if (points.Distinct().Count() < 3) throw new ArgumentException("A wipeout boundary requires at least three distinct points.", nameof(boundary));
        _boundary = Array.AsReadOnly(points);
        Id = id;
    }

    public Guid Id { get; }
    public IReadOnlyList<CadPoint> Boundary => _boundary;
}

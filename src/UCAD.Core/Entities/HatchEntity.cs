using UCAD.Core.Geometry;

namespace UCAD.Core.Entities;

/// <summary>
/// Foundational associative-free hatch entity. v0.7 stores an immutable closed polygon
/// boundary; Solid is the production pattern and ANSI31 is retained as metadata for the
/// renderer/export layer.
/// </summary>
public sealed class HatchEntity : ICadEntity
{
    private readonly IReadOnlyList<CadPoint> _boundary;

    public HatchEntity(IEnumerable<CadPoint> boundary, string pattern = "Solid", double patternScale = 1, double patternAngleRadians = 0)
        : this(boundary, pattern, patternScale, patternAngleRadians, Guid.NewGuid())
    {
    }

    internal HatchEntity(IEnumerable<CadPoint> boundary, string pattern, double patternScale, double patternAngleRadians, Guid id)
    {
        ArgumentNullException.ThrowIfNull(boundary);
        var points = boundary.ToArray();
        if (points.Length < 3) throw new ArgumentException("A hatch requires at least three boundary points.", nameof(boundary));
        if (string.IsNullOrWhiteSpace(pattern)) throw new ArgumentException("Hatch pattern cannot be empty.", nameof(pattern));
        if (!double.IsFinite(patternScale) || patternScale <= 0) throw new ArgumentOutOfRangeException(nameof(patternScale));
        if (!double.IsFinite(patternAngleRadians)) throw new ArgumentOutOfRangeException(nameof(patternAngleRadians));
        _boundary = Array.AsReadOnly(points);
        Pattern = pattern.Trim();
        PatternScale = patternScale;
        PatternAngleRadians = patternAngleRadians;
        Id = id;
    }

    public Guid Id { get; }
    public IReadOnlyList<CadPoint> Boundary => _boundary;
    public string Pattern { get; }
    public double PatternScale { get; }
    public double PatternAngleRadians { get; }
}

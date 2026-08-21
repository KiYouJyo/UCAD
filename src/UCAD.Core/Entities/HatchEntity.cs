using UCAD.Core.Geometry;

namespace UCAD.Core.Entities;

public enum HatchIslandDetection
{
    Normal,
    Outer,
    Ignore
}

/// <summary>
/// Immutable hatch entity with an outer polygon boundary, optional island loops and
/// optional associative source references. The original v0.7 constructor remains the
/// compatibility path and produces a non-associative single-loop hatch.
/// </summary>
public sealed class HatchEntity : ICadEntity
{
    private readonly IReadOnlyList<CadPoint> _boundary;
    private readonly IReadOnlyList<IReadOnlyList<CadPoint>> _islands;
    private readonly IReadOnlyList<Guid> _sourceEntityIds;

    public HatchEntity(
        IEnumerable<CadPoint> boundary,
        string pattern = "Solid",
        double patternScale = 1,
        double patternAngleRadians = 0)
        : this(
            boundary,
            pattern,
            patternScale,
            patternAngleRadians,
            islands: null,
            associative: false,
            sourceEntityIds: null,
            islandDetection: HatchIslandDetection.Normal,
            Guid.NewGuid())
    {
    }

    public HatchEntity(
        IEnumerable<CadPoint> boundary,
        string pattern,
        IEnumerable<IEnumerable<CadPoint>>? islands)
        : this(
            boundary,
            pattern,
            patternScale: 1,
            patternAngleRadians: 0,
            islands,
            associative: false,
            sourceEntityIds: null,
            islandDetection: HatchIslandDetection.Normal,
            Guid.NewGuid())
    {
    }

    public HatchEntity(
        IEnumerable<CadPoint> boundary,
        string pattern,
        double patternScale,
        double patternAngleRadians,
        IEnumerable<IEnumerable<CadPoint>>? islands,
        bool associative,
        IEnumerable<Guid>? sourceEntityIds,
        HatchIslandDetection islandDetection = HatchIslandDetection.Normal)
        : this(
            boundary,
            pattern,
            patternScale,
            patternAngleRadians,
            islands,
            associative,
            sourceEntityIds,
            islandDetection,
            Guid.NewGuid())
    {
    }

    internal HatchEntity(
        IEnumerable<CadPoint> boundary,
        string pattern,
        double patternScale,
        double patternAngleRadians,
        Guid id)
        : this(
            boundary,
            pattern,
            patternScale,
            patternAngleRadians,
            islands: null,
            associative: false,
            sourceEntityIds: null,
            islandDetection: HatchIslandDetection.Normal,
            id)
    {
    }

    internal HatchEntity(
        IEnumerable<CadPoint> boundary,
        string pattern,
        double patternScale,
        double patternAngleRadians,
        IEnumerable<IEnumerable<CadPoint>>? islands,
        bool associative,
        IEnumerable<Guid>? sourceEntityIds,
        HatchIslandDetection islandDetection,
        Guid id)
    {
        _boundary = FreezeLoop(boundary, nameof(boundary));
        if (string.IsNullOrWhiteSpace(pattern)) throw new ArgumentException("Hatch pattern cannot be empty.", nameof(pattern));
        if (!double.IsFinite(patternScale) || patternScale <= 0) throw new ArgumentOutOfRangeException(nameof(patternScale));
        if (!double.IsFinite(patternAngleRadians)) throw new ArgumentOutOfRangeException(nameof(patternAngleRadians));
        if (!Enum.IsDefined(islandDetection)) throw new ArgumentOutOfRangeException(nameof(islandDetection));

        var islandSnapshot = new List<IReadOnlyList<CadPoint>>();
        if (islands is not null)
        {
            var index = 0;
            foreach (var island in islands)
            {
                islandSnapshot.Add(FreezeLoop(island, $"islands[{index}]") );
                index++;
            }
        }
        _islands = islandSnapshot.AsReadOnly();

        var sources = sourceEntityIds?.Where(idValue => idValue != Guid.Empty).Distinct().ToArray() ?? [];
        if (associative && sources.Length == 0)
            throw new ArgumentException("Associative hatch requires at least one source entity id.", nameof(sourceEntityIds));
        _sourceEntityIds = Array.AsReadOnly(sources);

        Pattern = pattern.Trim();
        PatternScale = patternScale;
        PatternAngleRadians = patternAngleRadians;
        Associative = associative;
        IslandDetection = islandDetection;
        Id = id;
    }

    public Guid Id { get; }
    public IReadOnlyList<CadPoint> Boundary => _boundary;
    public IReadOnlyList<IReadOnlyList<CadPoint>> Islands => _islands;
    public string Pattern { get; }
    public double PatternScale { get; }
    public double PatternAngleRadians { get; }
    public bool Associative { get; }
    public IReadOnlyList<Guid> SourceEntityIds => _sourceEntityIds;
    public HatchIslandDetection IslandDetection { get; }

    public IEnumerable<IReadOnlyList<CadPoint>> EffectiveIslandLoops => IslandDetection switch
    {
        HatchIslandDetection.Ignore => [],
        HatchIslandDetection.Outer => _islands.Take(1),
        _ => _islands
    };

    private static IReadOnlyList<CadPoint> FreezeLoop(IEnumerable<CadPoint> loop, string parameterName)
    {
        ArgumentNullException.ThrowIfNull(loop);
        var points = loop.ToArray();
        if (points.Length < 3) throw new ArgumentException("A hatch loop requires at least three boundary points.", parameterName);
        if (points.Any(point => !double.IsFinite(point.X) || !double.IsFinite(point.Y)))
            throw new ArgumentException("Hatch boundary points must be finite.", parameterName);
        return Array.AsReadOnly(points);
    }
}

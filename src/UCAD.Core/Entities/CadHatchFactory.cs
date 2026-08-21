using UCAD.Core.Geometry;

namespace UCAD.Core.Entities;

public static class CadHatchFactory
{
    public static HatchEntity Update(
        HatchEntity source,
        IEnumerable<CadPoint>? boundary = null,
        string? pattern = null,
        double? patternScale = null,
        double? patternAngleRadians = null,
        IEnumerable<IEnumerable<CadPoint>>? islands = null,
        bool? associative = null,
        IEnumerable<Guid>? sourceEntityIds = null,
        HatchIslandDetection? islandDetection = null)
    {
        ArgumentNullException.ThrowIfNull(source);
        var nextAssociative = associative ?? source.Associative;
        var nextSources = sourceEntityIds?.ToArray() ?? source.SourceEntityIds.ToArray();
        if (!nextAssociative) nextSources = [];
        return new HatchEntity(
            boundary ?? source.Boundary,
            pattern ?? source.Pattern,
            patternScale ?? source.PatternScale,
            patternAngleRadians ?? source.PatternAngleRadians,
            islands ?? source.Islands,
            nextAssociative,
            nextSources,
            islandDetection ?? source.IslandDetection,
            source.Id);
    }

    public static HatchEntity CreateFromClosedPolyline(
        PolylineEntity boundary,
        string pattern,
        double patternScale,
        double patternAngleRadians,
        IEnumerable<PolylineEntity>? islandBoundaries = null,
        bool associative = false,
        HatchIslandDetection islandDetection = HatchIslandDetection.Normal)
    {
        ArgumentNullException.ThrowIfNull(boundary);
        if (!boundary.Closed) throw new ArgumentException("Hatch boundary polyline must be closed.", nameof(boundary));
        var islands = islandBoundaries?.ToArray() ?? [];
        if (islands.Any(island => !island.Closed)) throw new ArgumentException("Hatch island polylines must be closed.", nameof(islandBoundaries));
        var sources = associative
            ? new[] { boundary.Id }.Concat(islands.Select(island => island.Id)).ToArray()
            : [];
        return new HatchEntity(
            boundary.Points,
            pattern,
            patternScale,
            patternAngleRadians,
            islands.Select(island => island.Points),
            associative,
            sources,
            islandDetection);
    }
}
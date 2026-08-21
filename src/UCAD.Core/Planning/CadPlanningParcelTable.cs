using UCAD.Core.Entities;

namespace UCAD.Core.Planning;

public sealed record CadPlanningParcelRecord(
    Guid BoundaryEntityId,
    CadPlanningParcelData Data,
    CadParcelIndicatorInput IndicatorInput,
    CadPlanningControls Controls)
{
    public CadPlanningParcelRecord Validate()
    {
        if (BoundaryEntityId == Guid.Empty) throw new ArgumentException("Parcel boundary entity ID cannot be empty.", nameof(BoundaryEntityId));
        (Data ?? throw new ArgumentNullException(nameof(Data))).Validate();
        (IndicatorInput ?? throw new ArgumentNullException(nameof(IndicatorInput))).Validate();
        (Controls ?? throw new ArgumentNullException(nameof(Controls))).Validate();
        return this;
    }
}

public sealed record CadPlanningParcelResolved(
    CadPlanningParcelRecord Record,
    PolylineEntity Boundary,
    CadParcelIndicators Indicators,
    CadPlanningControlEvaluation Evaluation);

/// <summary>
/// Planning metadata keyed to ordinary closed PolylineEntity boundaries. Geometry remains
/// fully CAD-native; indicators are recomputed from the current boundary every time they
/// are resolved so edits cannot leave a stale cached parcel area/FAR behind.
/// </summary>
public sealed class CadPlanningParcelTable
{
    private readonly Dictionary<Guid, CadPlanningParcelRecord> _records = [];

    public IReadOnlyList<CadPlanningParcelRecord> Records =>
        _records.Values.OrderBy(record => record.Data.ParcelId, StringComparer.OrdinalIgnoreCase).ToArray();

    public int Count => _records.Count;

    public bool ContainsBoundary(Guid boundaryEntityId) => _records.ContainsKey(boundaryEntityId);

    public CadPlanningParcelRecord Get(Guid boundaryEntityId) =>
        _records.TryGetValue(boundaryEntityId, out var record)
            ? record
            : throw new KeyNotFoundException($"Planning parcel for boundary {boundaryEntityId} does not exist.");

    public void Set(CadPlanningParcelRecord record)
    {
        record = (record ?? throw new ArgumentNullException(nameof(record))).Validate();
        var duplicateParcelId = _records.Values.Any(existing =>
            existing.BoundaryEntityId != record.BoundaryEntityId &&
            string.Equals(existing.Data.ParcelId, record.Data.ParcelId, StringComparison.OrdinalIgnoreCase));
        if (duplicateParcelId) throw new InvalidOperationException($"Planning parcel ID '{record.Data.ParcelId}' already exists.");
        _records[record.BoundaryEntityId] = record;
    }

    public bool Remove(Guid boundaryEntityId) => _records.Remove(boundaryEntityId);

    public void Clear() => _records.Clear();

    public CadPlanningParcelResolved Resolve(CadDocument document, Guid boundaryEntityId)
    {
        ArgumentNullException.ThrowIfNull(document);
        var record = Get(boundaryEntityId);
        var boundary = document.Entities.FirstOrDefault(entity => entity.Id == boundaryEntityId) as PolylineEntity;
        if (boundary is null)
            throw new InvalidOperationException($"Parcel boundary {boundaryEntityId} no longer exists or is not a polyline.");
        if (!boundary.Closed)
            throw new InvalidOperationException($"Parcel boundary {boundaryEntityId} is no longer closed.");
        var indicators = CadParcelIndicators.Calculate(boundary, record.IndicatorInput);
        var evaluation = CadPlanningControlEvaluation.Evaluate(indicators, record.Controls);
        return new CadPlanningParcelResolved(record, boundary, indicators, evaluation);
    }

    public IReadOnlyList<CadPlanningParcelResolved> ResolveAll(CadDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        return Records.Select(record => Resolve(document, record.BoundaryEntityId)).ToArray();
    }

    public CadParcelSchedule BuildSchedule(CadDocument document)
    {
        var resolved = ResolveAll(document);
        return CadParcelSchedule.Build(resolved.Select(item =>
            new CadPlanningParcelSnapshot(item.Record.Data, item.Indicators, item.Record.Controls)));
    }

    public IReadOnlyList<CadPlanningParcelRecord> FindOrphans(CadDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        var existing = document.Entities.Select(entity => entity.Id).ToHashSet();
        return Records.Where(record => !existing.Contains(record.BoundaryEntityId)).ToArray();
    }

    public int RemoveOrphans(CadDocument document)
    {
        var orphans = FindOrphans(document);
        foreach (var orphan in orphans) _records.Remove(orphan.BoundaryEntityId);
        return orphans.Count;
    }
}

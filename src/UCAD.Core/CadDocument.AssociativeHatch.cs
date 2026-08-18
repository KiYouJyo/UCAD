using UCAD.Core.Entities;
using UCAD.Core.Geometry;

namespace UCAD.Core;

public sealed partial class CadDocument
{
    private bool _refreshingAssociativeHatches;

    public CadDocument()
    {
        Changed += CadDocument_AssociativeHatchRefresh;
    }

    private void CadDocument_AssociativeHatchRefresh(object? sender, CadDocumentChangedEventArgs e)
    {
        if (_refreshingAssociativeHatches) return;
        if (e.Kind is CadDocumentChangeKind.LayerTable
            or CadDocumentChangeKind.CurrentLayer
            or CadDocumentChangeKind.EntityProperties
            or CadDocumentChangeKind.BlockTable
            or CadDocumentChangeKind.StyleTable
            or CadDocumentChangeKind.LayoutTable)
        {
            return;
        }

        _refreshingAssociativeHatches = true;
        try
        {
            for (var index = 0; index < _entities.Count; index++)
            {
                if (_entities[index] is not HatchEntity { Associative: true } hatch) continue;
                if (!TryResolveAssociativeHatchSources(hatch, out var outer, out var islands))
                {
                    // Preserve the last valid hatch geometry but stop claiming an association
                    // once any referenced source disappears or stops being a closed polyline.
                    _entities[index] = CadHatchFactory.Update(
                        hatch,
                        associative: false,
                        sourceEntityIds: []);
                    continue;
                }

                if (HatchGeometryMatchesSources(hatch, outer!, islands!)) continue;
                _entities[index] = CadHatchFactory.Update(
                    hatch,
                    boundary: outer!.Points,
                    islands: islands!.Select(polyline => polyline.Points),
                    associative: true,
                    sourceEntityIds: hatch.SourceEntityIds);
            }
        }
        finally
        {
            _refreshingAssociativeHatches = false;
        }
    }

    private bool TryResolveAssociativeHatchSources(
        HatchEntity hatch,
        out PolylineEntity? outer,
        out IReadOnlyList<PolylineEntity>? islands)
    {
        outer = null;
        islands = null;
        if (hatch.SourceEntityIds.Count == 0) return false;

        var sourceEntities = new List<PolylineEntity>(hatch.SourceEntityIds.Count);
        foreach (var sourceId in hatch.SourceEntityIds)
        {
            var source = _entities.FirstOrDefault(entity => entity.Id == sourceId);
            if (source is not PolylineEntity { Closed: true } polyline) return false;
            sourceEntities.Add(polyline);
        }

        outer = sourceEntities[0];
        islands = sourceEntities.Skip(1).ToArray();
        return true;
    }

    private static bool HatchGeometryMatchesSources(
        HatchEntity hatch,
        PolylineEntity outer,
        IReadOnlyList<PolylineEntity> islands)
    {
        if (!PointsEqual(hatch.Boundary, outer.Points)) return false;
        if (hatch.Islands.Count != islands.Count) return false;
        for (var index = 0; index < islands.Count; index++)
            if (!PointsEqual(hatch.Islands[index], islands[index].Points)) return false;
        return true;
    }

    private static bool PointsEqual(IReadOnlyList<CadPoint> first, IReadOnlyList<CadPoint> second)
    {
        if (first.Count != second.Count) return false;
        for (var index = 0; index < first.Count; index++)
            if (first[index] != second[index]) return false;
        return true;
    }
}

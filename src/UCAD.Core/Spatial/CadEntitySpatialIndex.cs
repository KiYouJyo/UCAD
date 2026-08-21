using UCAD.Core.Entities;
using UCAD.Core.Geometry;
using UCAD.Core.Interaction;

namespace UCAD.Core.Spatial;

public sealed class CadEntitySpatialIndex
{
    private readonly CadSpatialIndex<ICadEntity> _index;
    private readonly IReadOnlyList<ICadEntity> _unbounded;

    private CadEntitySpatialIndex(CadSpatialIndex<ICadEntity> index, IReadOnlyList<ICadEntity> unbounded)
    {
        _index = index;
        _unbounded = unbounded;
    }

    public int Count => _index.Count + _unbounded.Count;

    public static CadEntitySpatialIndex Build(IEnumerable<ICadEntity> entities)
    {
        ArgumentNullException.ThrowIfNull(entities);
        var entries = new List<CadSpatialIndexEntry<ICadEntity>>();
        var unbounded = new List<ICadEntity>();
        foreach (var entity in entities)
        {
            if (TryBounds(entity, out var bounds)) entries.Add(new CadSpatialIndexEntry<ICadEntity>(entity, bounds));
            else unbounded.Add(entity);
        }
        return new CadEntitySpatialIndex(CadSpatialIndex<ICadEntity>.Build(entries), unbounded.AsReadOnly());
    }

    public IReadOnlyList<ICadEntity> Query(CadRect rectangle)
    {
        if (_unbounded.Count == 0) return _index.Query(rectangle);
        var result = _index.Query(rectangle).ToList();
        foreach (var entity in _unbounded)
            if (IntersectsRectangle(entity, rectangle)) result.Add(entity);
        return result;
    }

    public ICadEntity? FindNearest(CadPoint point, double maximumDistance)
    {
        var best = _index.FindNearest(point, maximumDistance, DistanceTo);
        var bestDistance = best is null ? maximumDistance : DistanceTo(best, point);
        foreach (var entity in _unbounded)
        {
            var distance = DistanceTo(entity, point);
            if (distance <= bestDistance)
            {
                best = entity;
                bestDistance = distance;
            }
        }
        return bestDistance <= maximumDistance ? best : null;
    }

    private static bool TryBounds(ICadEntity entity, out CadRect bounds)
    {
        if (CadAnnotationEntityGeometry.TryGetBounds(entity, out bounds)) return true;
        if (CadExtendedEntityGeometry.TryGetBounds(entity, out bounds)) return true;
        try
        {
            bounds = CadEntityGeometry.GetBounds(entity);
            return true;
        }
        catch (NotSupportedException)
        {
            bounds = default;
            return false;
        }
    }

    private static bool IntersectsRectangle(ICadEntity entity, CadRect rectangle)
    {
        if (CadAnnotationEntityGeometry.Supports(entity)) return CadAnnotationEntityGeometry.IntersectsRectangle(entity, rectangle);
        if (CadExtendedEntityGeometry.Supports(entity)) return CadExtendedEntityGeometry.IntersectsRectangle(entity, rectangle);
        return CadEntityGeometry.IntersectsRectangle(entity, rectangle);
    }

    private static double DistanceTo(ICadEntity entity, CadPoint point)
    {
        if (CadAnnotationEntityGeometry.Supports(entity)) return CadAnnotationEntityGeometry.DistanceTo(entity, point);
        if (CadExtendedEntityGeometry.Supports(entity)) return CadExtendedEntityGeometry.DistanceTo(entity, point);
        return CadEntityGeometry.DistanceTo(entity, point);
    }
}

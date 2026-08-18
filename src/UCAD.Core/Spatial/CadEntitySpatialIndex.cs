using UCAD.Core.Entities;
using UCAD.Core.Geometry;
using UCAD.Core.Interaction;

namespace UCAD.Core.Spatial;

public sealed class CadEntitySpatialIndex
{
    private readonly CadSpatialIndex<ICadEntity> _index;

    private CadEntitySpatialIndex(CadSpatialIndex<ICadEntity> index) => _index = index;

    public int Count => _index.Count;

    public static CadEntitySpatialIndex Build(IEnumerable<ICadEntity> entities)
    {
        ArgumentNullException.ThrowIfNull(entities);
        var entries = entities.Select(entity => new CadSpatialIndexEntry<ICadEntity>(entity, Bounds(entity)));
        return new CadEntitySpatialIndex(CadSpatialIndex<ICadEntity>.Build(entries));
    }

    public IReadOnlyList<ICadEntity> Query(CadRect rectangle) => _index.Query(rectangle);

    public ICadEntity? FindNearest(CadPoint point, double maximumDistance) =>
        _index.FindNearest(point, maximumDistance, DistanceTo);

    private static CadRect Bounds(ICadEntity entity)
    {
        if (CadAnnotationEntityGeometry.Supports(entity)) return CadAnnotationEntityGeometry.GetBounds(entity);
        if (CadExtendedEntityGeometry.Supports(entity)) return CadExtendedEntityGeometry.GetBounds(entity);
        return CadEntityGeometry.GetBounds(entity);
    }

    private static double DistanceTo(ICadEntity entity, CadPoint point)
    {
        if (CadAnnotationEntityGeometry.Supports(entity)) return CadAnnotationEntityGeometry.DistanceTo(entity, point);
        if (CadExtendedEntityGeometry.Supports(entity)) return CadExtendedEntityGeometry.DistanceTo(entity, point);
        return CadEntityGeometry.DistanceTo(entity, point);
    }
}

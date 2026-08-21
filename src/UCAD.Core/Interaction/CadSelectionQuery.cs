using UCAD.Core.Entities;
using UCAD.Core.Geometry;

namespace UCAD.Core.Interaction;

public static class CadSelectionQuery
{
    public static ICadEntity? HitTestNearest(
        IEnumerable<ICadEntity> entities,
        CadPoint point,
        double tolerance)
    {
        ArgumentNullException.ThrowIfNull(entities);
        if (!double.IsFinite(tolerance) || tolerance < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(tolerance));
        }

        ICadEntity? best = null;
        var bestDistance = tolerance;
        foreach (var entity in entities.Reverse())
        {
            var distance = DistanceTo(entity, point);
            if (distance <= bestDistance)
            {
                best = entity;
                bestDistance = distance;
            }
        }
        return best;
    }

    public static IReadOnlyList<Guid> QueryWindow(
        IEnumerable<ICadEntity> entities,
        CadRect rectangle,
        bool crossing)
    {
        ArgumentNullException.ThrowIfNull(entities);
        return entities
            .Where(entity => crossing
                ? IntersectsRectangle(entity, rectangle)
                : IsContainedBy(entity, rectangle))
            .Select(entity => entity.Id)
            .ToArray();
    }

    private static double DistanceTo(ICadEntity entity, CadPoint point)
    {
        if (CadExtendedEntityGeometry.Supports(entity)) return CadExtendedEntityGeometry.DistanceTo(entity, point);
        if (CadAnnotationEntityGeometry.Supports(entity)) return CadAnnotationEntityGeometry.DistanceTo(entity, point);
        return CadEntityGeometry.DistanceTo(entity, point);
    }

    private static bool IntersectsRectangle(ICadEntity entity, CadRect rectangle)
    {
        if (CadExtendedEntityGeometry.Supports(entity)) return CadExtendedEntityGeometry.IntersectsRectangle(entity, rectangle);
        if (CadAnnotationEntityGeometry.Supports(entity)) return CadAnnotationEntityGeometry.IntersectsRectangle(entity, rectangle);
        return CadEntityGeometry.IntersectsRectangle(entity, rectangle);
    }

    private static bool IsContainedBy(ICadEntity entity, CadRect rectangle)
    {
        if (CadExtendedEntityGeometry.Supports(entity)) return CadExtendedEntityGeometry.IsContainedBy(entity, rectangle);
        if (CadAnnotationEntityGeometry.Supports(entity)) return CadAnnotationEntityGeometry.IsContainedBy(entity, rectangle);
        return CadEntityGeometry.IsContainedBy(entity, rectangle);
    }
}
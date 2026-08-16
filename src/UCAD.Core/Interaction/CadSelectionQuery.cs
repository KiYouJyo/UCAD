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
            var distance = CadEntityGeometry.DistanceTo(entity, point);
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
                ? CadEntityGeometry.IntersectsRectangle(entity, rectangle)
                : CadEntityGeometry.IsContainedBy(entity, rectangle))
            .Select(entity => entity.Id)
            .ToArray();
    }
}

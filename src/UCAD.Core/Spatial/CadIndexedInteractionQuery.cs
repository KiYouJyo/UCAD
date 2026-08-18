using UCAD.Core.Entities;
using UCAD.Core.Geometry;
using UCAD.Core.Interaction;

namespace UCAD.Core.Spatial;

public static class CadIndexedInteractionQuery
{
    public static ICadEntity? HitTestNearest(
        CadEntitySpatialIndex index,
        CadPoint point,
        double tolerance)
    {
        ArgumentNullException.ThrowIfNull(index);
        return index.FindNearest(point, tolerance);
    }

    public static IReadOnlyList<Guid> QueryWindow(
        CadEntitySpatialIndex index,
        CadRect rectangle,
        bool crossing)
    {
        ArgumentNullException.ThrowIfNull(index);
        var candidates = index.Query(rectangle);
        return CadSelectionQuery.QueryWindow(candidates, rectangle, crossing);
    }

    public static ObjectSnapResult? ResolveObjectSnap(
        CadEntitySpatialIndex index,
        CadPoint cursor,
        double aperture,
        ObjectSnapMode modes)
    {
        ArgumentNullException.ThrowIfNull(index);
        if (!double.IsFinite(aperture) || aperture < 0) throw new ArgumentOutOfRangeException(nameof(aperture));
        if (modes == ObjectSnapMode.None) return null;
        var rectangle = new CadRect(
            cursor.X - aperture,
            cursor.Y - aperture,
            cursor.X + aperture,
            cursor.Y + aperture);
        var candidates = index.Query(rectangle);
        return ObjectSnapResolver.Resolve(candidates, cursor, aperture, modes);
    }
}

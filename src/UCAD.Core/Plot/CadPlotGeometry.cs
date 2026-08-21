using UCAD.Core.Entities;
using UCAD.Core.Geometry;
using UCAD.Core.Interaction;

namespace UCAD.Core.Plot;

public static class CadPlotGeometry
{
    public static bool TryGetDocumentExtents(CadDocument document, out CadRect extents)
    {
        ArgumentNullException.ThrowIfNull(document);
        var bounds = new List<CadRect>();
        foreach (var entity in document.VisibleEntities)
        {
            if (TryGetEntityBounds(entity, out var entityBounds)) bounds.Add(entityBounds);
        }
        if (bounds.Count == 0)
        {
            extents = default;
            return false;
        }
        extents = new CadRect(
            bounds.Min(rect => rect.Left),
            bounds.Min(rect => rect.Bottom),
            bounds.Max(rect => rect.Right),
            bounds.Max(rect => rect.Top));
        return true;
    }

    public static bool TryGetEntityBounds(ICadEntity entity, out CadRect bounds)
    {
        if (CadExtendedEntityGeometry.Supports(entity))
            return CadExtendedEntityGeometry.TryGetBounds(entity, out bounds);
        if (CadAnnotationEntityGeometry.Supports(entity))
            return CadAnnotationEntityGeometry.TryGetBounds(entity, out bounds);

        if (entity is RayEntity or XLineEntity)
        {
            bounds = default;
            return false;
        }

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
}
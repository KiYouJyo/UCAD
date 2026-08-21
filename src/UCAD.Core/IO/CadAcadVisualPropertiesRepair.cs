using AcadDocument = ACadSharp.CadDocument;
using AcadEntity = ACadSharp.Entities.Entity;
using UcadDocument = UCAD.Core.CadDocument;

namespace UCAD.Core.IO;

/// <summary>
/// Re-applies AutoCAD-native visual state after semantic/display fallback expansion.
/// SourceOrder is the stable join key: one source entity may expand into multiple UCAD
/// display entities, and all of them must inherit invisibility/transparency consistently.
/// SHAPE references and proxy graphics are consumed here only after normal semantic recovery
/// has had first refusal.
/// </summary>
internal static class CadAcadVisualPropertiesRepair
{
    public static void Apply(AcadDocument source, UcadDocument target)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(target);

        // At this stage SourceOrder has already been repaired. SHAPE references must be
        // registered before proxy graphics, otherwise a proxy outline would claim the source
        // slot and prevent the real SHX resource from being resolved after the drawing path is known.
        CadAcadShapeDisplayRepair.Apply(source, target, []);
        CadAcadProxyGraphicsDisplayRepair.Apply(source, target, []);

        var sourceEntities = source.Entities.ToArray();
        foreach (var entity in target.Entities.ToArray())
        {
            var properties = target.GetEntityProperties(entity.Id);
            if (properties.SourceOrder is not int sourceOrder || sourceOrder < 0 || sourceOrder >= sourceEntities.Length) continue;
            if (sourceEntities[sourceOrder] is not AcadEntity acadEntity) continue;

            var opacity = ResolveOpacity(acadEntity);
            if (properties.Opacity == opacity) continue;
            target.SetEntityProperties([entity.Id], current => current with { Opacity = opacity });
        }
    }

    private static double? ResolveOpacity(AcadEntity source)
    {
        if (source.IsInvisible) return 0d;
        var transparency = source.Transparency;
        if (transparency.IsByLayer || transparency.IsByBlock) return null;
        return Math.Clamp((100d - transparency.Value) / 100d, 0d, 1d);
    }
}

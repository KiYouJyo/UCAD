using ACadSharp.Entities;
using UCAD.Core.Entities;
using UCAD.Core.Geometry;
using UCAD.Core.Layers;
using AcadDocument = ACadSharp.CadDocument;
using UcadDocument = UCAD.Core.CadDocument;

namespace UCAD.Core.IO;

/// <summary>
/// Display-first recovery for AutoCAD WIPEOUT. A tail-position wipeout can be restored
/// as a real background mask without changing source draw order. When later source
/// entities still exist, the exact boundary is retained as an outline until the full
/// draw-order metadata path can place the mask between those entities safely.
/// </summary>
internal static class CadAcadWipeoutDisplayRepair
{
    private const double Tolerance = 1e-9;

    public static void Apply(AcadDocument source, UcadDocument target, List<string> warnings)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(warnings);

        var sourceEntities = source.Entities.ToArray();
        for (var index = 0; index < sourceEntities.Length; index++)
        {
            if (sourceEntities[index] is not Wipeout wipeout || !wipeout.ShowImage) continue;
            try
            {
                var boundary = GetWorldBoundary(wipeout);
                if (boundary.Count < 3) continue;
                var properties = ToProperties(wipeout, target);
                var safeTailMask = !sourceEntities.Skip(index + 1).Any(entity => entity is not Wipeout);

                if (safeTailMask)
                {
                    target.Add(new WipeoutEntity(boundary), properties);
                    warnings.RemoveAll(warning => warning.Contains("WIPEOUT", StringComparison.OrdinalIgnoreCase) && warning.Contains("could not be imported", StringComparison.OrdinalIgnoreCase));
                }
                else
                {
                    target.Add(new PolylineEntity(boundary, closed: true), properties);
                    const string warning = "AutoCAD WIPEOUT boundary was recovered, but the mask occurs before later source entities; only its exact boundary is shown until source draw-order metadata can place the opaque mask safely.";
                    if (!warnings.Contains(warning, StringComparer.Ordinal)) warnings.Add(warning);
                }
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or OverflowException)
            {
                warnings.Add($"AutoCAD WIPEOUT display recovery failed; the normalized import was retained. {ex.Message}");
            }
        }
    }

    private static IReadOnlyList<CadPoint> GetWorldBoundary(Wipeout source)
    {
        IReadOnlyList<CSMath.XY> local;
        var useClip = source.ClippingState && source.Flags.HasFlag(ImageDisplayFlags.UseClippingBoundary) && source.ClipBoundaryVertices.Count >= 2;
        if (useClip)
        {
            if (source.ClipBoundaryVertices.Count == 2)
            {
                var a = source.ClipBoundaryVertices[0];
                var b = source.ClipBoundaryVertices[1];
                local =
                [
                    new CSMath.XY(a.X, a.Y),
                    new CSMath.XY(b.X, a.Y),
                    new CSMath.XY(b.X, b.Y),
                    new CSMath.XY(a.X, b.Y)
                ];
            }
            else
            {
                local = source.ClipBoundaryVertices.ToArray();
            }
        }
        else
        {
            var width = Math.Max(source.Size.X, 1d);
            var height = Math.Max(source.Size.Y, 1d);
            local =
            [
                new CSMath.XY(-0.5, -0.5),
                new CSMath.XY(width - 0.5, -0.5),
                new CSMath.XY(width - 0.5, height - 0.5),
                new CSMath.XY(-0.5, height - 0.5)
            ];
        }

        var result = local.Select(point => ToWorld(source, point)).ToList();
        while (result.Count > 1 && PointsNear(result[0], result[^1])) result.RemoveAt(result.Count - 1);
        return result;
    }

    private static CadPoint ToWorld(Wipeout source, CSMath.XY local)
    {
        // IMAGE/WIPEOUT clip coordinates use pixel-edge coordinates whose default lower
        // corner is (-0.5,-0.5). Shift by +0.5 before applying the per-pixel U/V vectors.
        var u = local.X + 0.5;
        var v = local.Y + 0.5;
        return new CadPoint(
            source.InsertPoint.X + source.UVector.X * u + source.VVector.X * v,
            source.InsertPoint.Y + source.UVector.Y * u + source.VVector.Y * v);
    }

    private static CadEntityProperties ToProperties(ACadSharp.Entities.Entity source, UcadDocument target)
    {
        var layer = string.IsNullOrWhiteSpace(source.Layer?.Name) ? CadLayer.DefaultLayerName : source.Layer.Name;
        if (!target.TryGetLayer(layer, out _)) target.CreateLayer(new CadLayer(layer));
        var lineType = string.IsNullOrWhiteSpace(source.LineType?.Name) ? "ByLayer" : source.LineType.Name;
        return new CadEntityProperties(layer, lineType: lineType);
    }

    private static bool PointsNear(CadPoint first, CadPoint second) =>
        Math.Abs(first.X - second.X) <= Tolerance && Math.Abs(first.Y - second.Y) <= Tolerance;
}

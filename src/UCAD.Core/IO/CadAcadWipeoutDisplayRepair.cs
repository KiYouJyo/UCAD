using ACadSharp.Entities;
using UCAD.Core.Entities;
using UCAD.Core.Geometry;
using UCAD.Core.Layers;
using AcadDocument = ACadSharp.CadDocument;
using UcadDocument = UCAD.Core.CadDocument;

namespace UCAD.Core.IO;

/// <summary>
/// Display-first recovery for AutoCAD WIPEOUT and the shared external visual-resource
/// stage. Source-order metadata keeps recovered masks and references at their original
/// ENTITIES position; raster images and underlays reuse this stage for placement/clipping.
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
                var properties = ToProperties(wipeout, target, index);
                target.Add(new WipeoutEntity(boundary), properties);
                warnings.RemoveAll(warning =>
                    warning.Contains("WIPEOUT", StringComparison.OrdinalIgnoreCase) &&
                    (warning.Contains("could not be imported", StringComparison.OrdinalIgnoreCase) ||
                     warning.Contains("only its exact boundary", StringComparison.OrdinalIgnoreCase)));
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or OverflowException)
            {
                warnings.Add($"AutoCAD WIPEOUT display recovery failed; the normalized import was retained. {ex.Message}");
            }
        }

        CadAcadRasterImageDisplayRepair.Apply(source, target, warnings);
        CadAcadUnderlayDisplayRepair.Apply(source, target, warnings);
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
        var u = local.X + 0.5;
        var v = local.Y + 0.5;
        return new CadPoint(
            source.InsertPoint.X + source.UVector.X * u + source.VVector.X * v,
            source.InsertPoint.Y + source.UVector.Y * u + source.VVector.Y * v);
    }

    private static CadEntityProperties ToProperties(ACadSharp.Entities.Entity source, UcadDocument target, int sourceOrder)
    {
        var layer = string.IsNullOrWhiteSpace(source.Layer?.Name) ? CadLayer.DefaultLayerName : source.Layer.Name;
        if (!target.TryGetLayer(layer, out _)) target.CreateLayer(new CadLayer(layer));
        var lineType = string.IsNullOrWhiteSpace(source.LineType?.Name) ? "ByLayer" : source.LineType.Name;
        return new CadEntityProperties(layer, lineType: lineType, sourceOrder: sourceOrder);
    }

    private static bool PointsNear(CadPoint first, CadPoint second) =>
        Math.Abs(first.X - second.X) <= Tolerance && Math.Abs(first.Y - second.Y) <= Tolerance;
}

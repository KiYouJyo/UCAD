using ACadSharp.Entities;
using UCAD.Core.Entities;
using UCAD.Core.Geometry;
using UCAD.Core.Layers;
using AcadDocument = ACadSharp.CadDocument;
using AcadRasterImage = ACadSharp.Entities.RasterImage;
using UcadDocument = UCAD.Core.CadDocument;

namespace UCAD.Core.IO;

/// <summary>
/// Recovers AutoCAD IMAGE placement and clipping semantics. Raster bytes are usually external
/// to DWG/DXF, so this pass preserves the reference and exact world transform; a later resolver
/// uses the source drawing path to locate the actual file without guessing inside Core import.
/// </summary>
internal static class CadAcadRasterImageDisplayRepair
{
    private const double Tolerance = 1e-9;

    public static void Apply(AcadDocument source, UcadDocument target, List<string> warnings)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(warnings);

        var sourceEntities = source.Entities.ToArray();
        for (var sourceOrder = 0; sourceOrder < sourceEntities.Length; sourceOrder++)
        {
            if (sourceEntities[sourceOrder] is not AcadRasterImage image || !image.ShowImage) continue;
            try
            {
                var width = image.Size.X;
                var height = image.Size.Y;
                if (!double.IsFinite(width) || width <= Tolerance || !double.IsFinite(height) || height <= Tolerance)
                {
                    warnings.Add("AutoCAD IMAGE has invalid pixel dimensions; its original source payload remains preserved.");
                    continue;
                }

                var referencePath = image.Definition?.FileName;
                if (string.IsNullOrWhiteSpace(referencePath)) referencePath = "<missing-image-definition>";
                var clip = GetWorldBoundary(image);
                if (clip.Count < 3) continue;

                var entity = new RasterImageEntity(
                    referencePath,
                    new CadPoint(image.InsertPoint.X, image.InsertPoint.Y),
                    new CadVector(image.UVector.X, image.UVector.Y),
                    new CadVector(image.VVector.X, image.VVector.Y),
                    width,
                    height,
                    clip,
                    image.Brightness,
                    image.Contrast,
                    image.Fade,
                    image.Flags.HasFlag(ImageDisplayFlags.TransparencyIsOn));

                var layer = string.IsNullOrWhiteSpace(image.Layer?.Name) ? CadLayer.DefaultLayerName : image.Layer.Name;
                if (!target.TryGetLayer(layer, out _)) target.CreateLayer(new CadLayer(layer));
                var lineType = string.IsNullOrWhiteSpace(image.LineType?.Name) ? "ByLayer" : image.LineType.Name;
                target.Add(entity, new CadEntityProperties(layer, lineType: lineType, sourceOrder: sourceOrder));

                warnings.RemoveAll(warning =>
                    warning.Contains("IMAGE", StringComparison.OrdinalIgnoreCase) &&
                    (warning.Contains("not supported", StringComparison.OrdinalIgnoreCase) ||
                     warning.Contains("could not be imported", StringComparison.OrdinalIgnoreCase) ||
                     warning.Contains("skipped", StringComparison.OrdinalIgnoreCase)));
                if (string.Equals(referencePath, "<missing-image-definition>", StringComparison.Ordinal))
                {
                    const string notice = "AutoCAD IMAGE placement was recovered, but its external image definition path is missing; the original container remains preserved.";
                    if (!warnings.Contains(notice, StringComparer.Ordinal)) warnings.Add(notice);
                }
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or OverflowException)
            {
                warnings.Add($"AutoCAD IMAGE display recovery failed; the original source payload remains preserved. {ex.Message}");
            }
        }
    }

    private static IReadOnlyList<CadPoint> GetWorldBoundary(AcadRasterImage source)
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
                    new CSMath.XY(a.X, a.Y), new CSMath.XY(b.X, a.Y),
                    new CSMath.XY(b.X, b.Y), new CSMath.XY(a.X, b.Y)
                ];
            }
            else
            {
                local = source.ClipBoundaryVertices.ToArray();
            }
        }
        else
        {
            local =
            [
                new CSMath.XY(-0.5, -0.5),
                new CSMath.XY(source.Size.X - 0.5, -0.5),
                new CSMath.XY(source.Size.X - 0.5, source.Size.Y - 0.5),
                new CSMath.XY(-0.5, source.Size.Y - 0.5)
            ];
        }

        var result = local.Select(point => ToWorld(source, point)).ToList();
        while (result.Count > 1 && PointsNear(result[0], result[^1])) result.RemoveAt(result.Count - 1);
        return result;
    }

    private static CadPoint ToWorld(AcadRasterImage source, CSMath.XY local)
    {
        var u = local.X + 0.5;
        var v = local.Y + 0.5;
        return new CadPoint(
            source.InsertPoint.X + source.UVector.X * u + source.VVector.X * v,
            source.InsertPoint.Y + source.UVector.Y * u + source.VVector.Y * v);
    }

    private static bool PointsNear(CadPoint first, CadPoint second) =>
        Math.Abs(first.X - second.X) <= Tolerance && Math.Abs(first.Y - second.Y) <= Tolerance;
}

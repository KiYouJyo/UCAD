using ACadSharp.Entities;
using UCAD.Core.Entities;
using UCAD.Core.Geometry;
using UCAD.Core.Layers;
using AcadDocument = ACadSharp.CadDocument;
using UcadDocument = UCAD.Core.CadDocument;

namespace UCAD.Core.IO;

/// <summary>
/// Display-first recovery for AutoCAD underlays. PDF underlays are modeled explicitly;
/// DWF/DGN instances remain preserved by the source envelope until their native entity
/// classes are exposed consistently by the transport library.
/// </summary>
internal static class CadAcadUnderlayDisplayRepair
{
    private const double Epsilon = 1e-12;

    public static void Apply(AcadDocument source, UcadDocument target, List<string> warnings)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(warnings);

        var sourceEntities = source.Entities.ToArray();
        for (var sourceOrder = 0; sourceOrder < sourceEntities.Length; sourceOrder++)
        {
            if (sourceEntities[sourceOrder] is not PdfUnderlay underlay ||
                !underlay.Flags.HasFlag(UnderlayDisplayFlags.ShowUnderlay)) continue;
            try
            {
                var referencePath = underlay.Definition?.File;
                if (string.IsNullOrWhiteSpace(referencePath)) referencePath = "<missing-pdf-underlay-definition>";
                var clip = ConvertClipBoundary(underlay);
                var entity = new UnderlayReferenceEntity(
                    CadUnderlayKind.Pdf,
                    referencePath,
                    underlay.Definition?.Page,
                    new CadPoint(underlay.InsertPoint.X, underlay.InsertPoint.Y),
                    underlay.XScale,
                    underlay.YScale,
                    underlay.Rotation,
                    clip,
                    underlay.Contrast,
                    underlay.Fade,
                    underlay.Flags.HasFlag(UnderlayDisplayFlags.Monochrome),
                    underlay.Flags.HasFlag(UnderlayDisplayFlags.AdjustForBackground),
                    underlay.Flags.HasFlag(UnderlayDisplayFlags.ClipInsideMode));

                var layer = string.IsNullOrWhiteSpace(underlay.Layer?.Name) ? CadLayer.DefaultLayerName : underlay.Layer.Name;
                if (!target.TryGetLayer(layer, out _)) target.CreateLayer(new CadLayer(layer));
                var lineType = string.IsNullOrWhiteSpace(underlay.LineType?.Name) ? "ByLayer" : underlay.LineType.Name;
                target.Add(entity, new CadEntityProperties(layer, lineType: lineType, sourceOrder: sourceOrder));

                warnings.RemoveAll(warning =>
                    (warning.Contains("PDFUNDERLAY", StringComparison.OrdinalIgnoreCase) || warning.Contains("PDF underlay", StringComparison.OrdinalIgnoreCase)) &&
                    (warning.Contains("not supported", StringComparison.OrdinalIgnoreCase) ||
                     warning.Contains("skipped", StringComparison.OrdinalIgnoreCase) ||
                     warning.Contains("could not be imported", StringComparison.OrdinalIgnoreCase)));
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or OverflowException)
            {
                warnings.Add($"AutoCAD PDF underlay display recovery failed; the original source payload remains preserved. {ex.Message}");
            }
        }
    }

    private static IReadOnlyList<CadPoint> ConvertClipBoundary(PdfUnderlay source)
    {
        if (!source.Flags.HasFlag(UnderlayDisplayFlags.ClippingOn) || source.ClipBoundaryVertices.Count < 2)
            return [];

        var local = new List<CSMath.XY>();
        if (source.ClipBoundaryVertices.Count == 2)
        {
            var a = source.ClipBoundaryVertices[0];
            var b = source.ClipBoundaryVertices[1];
            local.Add(new CSMath.XY(a.X, a.Y));
            local.Add(new CSMath.XY(b.X, a.Y));
            local.Add(new CSMath.XY(b.X, b.Y));
            local.Add(new CSMath.XY(a.X, b.Y));
        }
        else
        {
            local.AddRange(source.ClipBoundaryVertices);
        }

        var cos = Math.Cos(source.Rotation);
        var sin = Math.Sin(source.Rotation);
        var result = new List<CadPoint>(local.Count);
        foreach (var point in local)
        {
            var x = point.X * source.XScale;
            var y = point.Y * source.YScale;
            result.Add(new CadPoint(
                source.InsertPoint.X + (x * cos) - (y * sin),
                source.InsertPoint.Y + (x * sin) + (y * cos)));
        }

        while (result.Count > 1 && PointsNear(result[0], result[^1])) result.RemoveAt(result.Count - 1);
        return result;
    }

    private static bool PointsNear(CadPoint first, CadPoint second) =>
        Math.Abs(first.X - second.X) <= Epsilon && Math.Abs(first.Y - second.Y) <= Epsilon;
}

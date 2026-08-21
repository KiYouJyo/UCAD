using UCAD.Core.Entities;
using UCAD.Core.Geometry;
using UCAD.Core.Layers;
using AcadDocument = ACadSharp.CadDocument;
using AcadShape = ACadSharp.Entities.Shape;
using UcadDocument = UCAD.Core.CadDocument;

namespace UCAD.Core.IO;

/// <summary>
/// Retains AutoCAD SHAPE entities as resolvable display references. The external-reference
/// resolver later locates the associated SHX/SHP file and replaces each reference with
/// decoded world-coordinate vector strokes. A tiny source-ordered diamond remains visible
/// when the external resource is unavailable, so SHAPE records never disappear silently.
/// </summary>
internal static class CadAcadShapeDisplayRepair
{
    internal const string MarkerHandlePrefix = "$UCAD-SHAPE-MARKER:";

    public static void Apply(AcadDocument source, UcadDocument target, List<string> warnings)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(warnings);

        var sourceEntities = source.Entities.ToArray();
        for (var sourceOrder = 0; sourceOrder < sourceEntities.Length; sourceOrder++)
        {
            if (sourceEntities[sourceOrder] is not AcadShape shape) continue;
            try
            {
                var candidates = new List<string>();
                if (!string.IsNullOrWhiteSpace(shape.ShapeStyle?.Filename)) candidates.Add(shape.ShapeStyle.Filename);
                foreach (var style in source.TextStyles)
                {
                    if (!style.IsShapeFile || string.IsNullOrWhiteSpace(style.Filename)) continue;
                    if (!candidates.Contains(style.Filename, StringComparer.OrdinalIgnoreCase)) candidates.Add(style.Filename);
                }

                if (candidates.Count == 0)
                {
                    warnings.Add($"AutoCAD SHAPE '{shape.ShapeStyle?.Name ?? "<unnamed>"}' has no SHX/SHP file reference; source payload remains preserved.");
                    continue;
                }

                var shapeName = shape.ShapeStyle?.Name;
                if (string.IsNullOrWhiteSpace(shapeName)) shapeName = "<shape>";
                var insertion = new CadPoint(shape.InsertionPoint.X, shape.InsertionPoint.Y);
                var size = Math.Max(Math.Abs(shape.Size), 1e-9);
                var entity = new ShapeReferenceEntity(
                    shapeName,
                    candidates,
                    insertion,
                    size,
                    Math.Abs(shape.RelativeXScale) <= 1e-12 ? 1 : shape.RelativeXScale,
                    shape.Rotation,
                    shape.ObliqueAngle);

                var layer = string.IsNullOrWhiteSpace(shape.Layer?.Name) ? CadLayer.DefaultLayerName : shape.Layer.Name;
                if (!target.TryGetLayer(layer, out _)) target.CreateLayer(new CadLayer(layer));
                var lineType = string.IsNullOrWhiteSpace(shape.LineType?.Name) ? "ByLayer" : shape.LineType.Name;
                var properties = new CadEntityProperties(layer, lineType: lineType, sourceOrder: sourceOrder);
                target.Add(entity, properties);

                var markerRadius = Math.Max(size * 0.18, 0.2);
                var marker = new PolylineEntity(
                    [
                        new CadPoint(insertion.X, insertion.Y + markerRadius),
                        new CadPoint(insertion.X + markerRadius, insertion.Y),
                        new CadPoint(insertion.X, insertion.Y - markerRadius),
                        new CadPoint(insertion.X - markerRadius, insertion.Y)
                    ],
                    closed: true);
                target.Add(marker, properties with { SourceHandle = MarkerHandlePrefix + entity.Id.ToString("N") });

                warnings.RemoveAll(warning => warning.Contains("SHAPE", StringComparison.OrdinalIgnoreCase) &&
                    (warning.Contains("not supported", StringComparison.OrdinalIgnoreCase) || warning.Contains("skipped", StringComparison.OrdinalIgnoreCase)));
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or OverflowException)
            {
                warnings.Add($"AutoCAD SHAPE display recovery failed; source payload remains preserved. {ex.Message}");
            }
        }
    }
}

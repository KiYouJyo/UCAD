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
/// decoded world-coordinate vector strokes.
/// </summary>
internal static class CadAcadShapeDisplayRepair
{
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
                // ACadSharp resolves group 2 through the shape style; the style name is therefore
                // the best native name hint. For DXF bridge cases the fallback list lets the SHX
                // resolver search all shape-file styles and match the actual symbol name.
                if (string.IsNullOrWhiteSpace(shapeName)) shapeName = "<shape>";

                var entity = new ShapeReferenceEntity(
                    shapeName,
                    candidates,
                    new CadPoint(shape.InsertionPoint.X, shape.InsertionPoint.Y),
                    Math.Max(Math.Abs(shape.Size), 1e-9),
                    Math.Abs(shape.RelativeXScale) <= 1e-12 ? 1 : shape.RelativeXScale,
                    shape.Rotation,
                    shape.ObliqueAngle);

                var layer = string.IsNullOrWhiteSpace(shape.Layer?.Name) ? CadLayer.DefaultLayerName : shape.Layer.Name;
                if (!target.TryGetLayer(layer, out _)) target.CreateLayer(new CadLayer(layer));
                var lineType = string.IsNullOrWhiteSpace(shape.LineType?.Name) ? "ByLayer" : shape.LineType.Name;
                target.Add(entity, new CadEntityProperties(layer, lineType: lineType, sourceOrder: sourceOrder));

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

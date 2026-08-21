using ACadSharp.Entities;
using UCAD.Core.Entities;
using UCAD.Core.Geometry;
using UCAD.Core.Layers;
using UCAD.Core.Styles;
using AcadDocument = ACadSharp.CadDocument;
using UcadDocument = UCAD.Core.CadDocument;
using AcadTolerance = ACadSharp.Entities.Tolerance;

namespace UCAD.Core.IO;

/// <summary>
/// Display-first recovery for AutoCAD TOLERANCE entities. UCAD does not yet expose an
/// editable geometric-tolerance model, but dropping the entity would remove visible
/// annotation from engineering drawings. Preserve its rendered text, insertion point,
/// direction and layer as an MTEXT snapshot instead.
/// </summary>
internal static class CadAcadToleranceDisplayRepair
{
    private const double Epsilon = 1e-9;

    public static void Apply(AcadDocument source, UcadDocument target, List<string> warnings)
    {
        foreach (var tolerance in source.Entities.OfType<AcadTolerance>())
        {
            if (string.IsNullOrWhiteSpace(tolerance.Text)) continue;
            try
            {
                var height = tolerance.Style?.TextHeight ?? 2.5;
                if (!double.IsFinite(height) || height <= Epsilon) height = 2.5;
                var styleName = tolerance.Style?.TextStyle?.Name;
                if (string.IsNullOrWhiteSpace(styleName)) styleName = CadTextStyle.DefaultName;
                if (!target.TryGetTextStyle(styleName, out _)) target.DefineTextStyle(new CadTextStyle(styleName));

                var dx = tolerance.Direction.X;
                var dy = tolerance.Direction.Y;
                var rotation = Math.Abs(dx) <= Epsilon && Math.Abs(dy) <= Epsilon ? 0.0 : Math.Atan2(dy, dx);
                var width = Math.Max(height * 4.0, Math.Max(1, tolerance.Text.Length) * height * 0.75);
                var entity = new MTextEntity(
                    new CadPoint(tolerance.InsertionPoint.X, tolerance.InsertionPoint.Y),
                    tolerance.Text,
                    height,
                    width,
                    rotation,
                    styleName);

                var layer = string.IsNullOrWhiteSpace(tolerance.Layer?.Name) ? CadLayer.DefaultLayerName : tolerance.Layer.Name;
                if (!target.TryGetLayer(layer, out _)) target.CreateLayer(new CadLayer(layer));
                var lineType = string.IsNullOrWhiteSpace(tolerance.LineType?.Name) ? "ByLayer" : tolerance.LineType.Name;
                target.Add(entity, new CadEntityProperties(layer, lineType: lineType));

                warnings.RemoveAll(warning =>
                    warning.Contains("TOLERANCE", StringComparison.OrdinalIgnoreCase) &&
                    (warning.Contains("not supported", StringComparison.OrdinalIgnoreCase) ||
                     warning.Contains("skipped", StringComparison.OrdinalIgnoreCase) ||
                     warning.Contains("could not be imported", StringComparison.OrdinalIgnoreCase)));
                const string notice = "AutoCAD TOLERANCE was restored as visible annotation text; editable geometric-tolerance semantics are deferred.";
                if (!warnings.Contains(notice, StringComparer.Ordinal)) warnings.Add(notice);
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or NotSupportedException)
            {
                warnings.Add($"AutoCAD TOLERANCE display recovery failed; source preservation remains active. {ex.Message}");
            }
        }
    }
}

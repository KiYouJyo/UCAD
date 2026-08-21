using System.Text;
using ACadSharp.Entities;
using ACadSharp.IO;
using UCAD.Core.Entities;
using UCAD.Core.Geometry;
using UCAD.Core.Layers;
using AcadDimension = ACadSharp.Entities.Dimension;
using AcadDocument = ACadSharp.CadDocument;
using UcadDocument = UCAD.Core.CadDocument;

namespace UCAD.Core.IO;

/// <summary>
/// Display-first recovery for AutoCAD DIMENSION variants that do not yet have a
/// lossless editable UCAD entity. ACadSharp is allowed to build the native anonymous
/// dimension block; UCAD then imports that block as ordinary visible 2D geometry.
/// This keeps rotated/ordinate and future dimension variants visible without pretending
/// that their editing semantics are already implemented.
/// </summary>
internal static class CadAcadDimensionDisplayRepair
{
    private const double Tolerance = 1e-7;

    public static void Apply(AcadDocument source, UcadDocument target, List<string> warnings)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(warnings);

        foreach (var dimension in source.Entities.OfType<AcadDimension>())
        {
            if (!NeedsDisplayFallback(dimension)) continue;

            try
            {
                var recovered = BuildDisplayGeometry(dimension, warnings);
                if (recovered is null || recovered.Document.Entities.Count == 0)
                {
                    warnings.Add($"AutoCAD DIMENSION {dimension.GetType().Name} requires display fallback but produced no visible 2D geometry.");
                    continue;
                }

                CopyStyles(recovered.Document, target);
                var ownerLayer = ResolveLayer(dimension);
                EnsureLayer(target, ownerLayer);
                var additions = new List<(ICadEntity Entity, CadEntityProperties Properties)>();

                foreach (var entity in recovered.Document.Entities)
                {
                    var properties = recovered.Document.GetEntityProperties(entity.Id);
                    if (string.Equals(properties.LayerName, "Defpoints", StringComparison.OrdinalIgnoreCase)) continue;
                    var layerName = string.Equals(properties.LayerName, CadLayer.DefaultLayerName, StringComparison.OrdinalIgnoreCase)
                        ? ownerLayer
                        : properties.LayerName;
                    EnsureLayer(target, layerName);
                    additions.Add((entity, properties with { LayerName = layerName }));
                }

                if (additions.Count == 0)
                {
                    warnings.Add($"AutoCAD DIMENSION {dimension.GetType().Name} display block contained only non-display/reference geometry.");
                    continue;
                }

                RemoveIncorrectSemanticPlaceholder(dimension, target);
                target.AddRange(additions);

                warnings.RemoveAll(warning =>
                    warning.Contains("DIMENSION", StringComparison.OrdinalIgnoreCase) &&
                    warning.Contains(dimension.GetType().Name, StringComparison.OrdinalIgnoreCase) &&
                    warning.Contains("no lossless", StringComparison.OrdinalIgnoreCase));

                var notice = $"AutoCAD DIMENSION {dimension.GetType().Name} was recovered as display geometry; editable native semantics are deferred.";
                if (!warnings.Contains(notice, StringComparer.Ordinal)) warnings.Add(notice);
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or NotSupportedException or IOException or FormatException or OverflowException)
            {
                warnings.Add($"AutoCAD DIMENSION {dimension.GetType().Name} display fallback failed; existing normalized display was retained. {ex.Message}");
            }
        }
    }

    private static bool NeedsDisplayFallback(AcadDimension dimension)
    {
        // DimensionLinear derives from DimensionAligned, but its independent rotation axis
        // cannot be represented losslessly by UCAD's current aligned-dimension entity.
        if (dimension is DimensionLinear or DimensionOrdinate) return true;

        return dimension is not DimensionAligned
            and not DimensionAngular3Pt
            and not DimensionAngular2Line
            and not DimensionRadius
            and not DimensionDiameter;
    }

    private static DxfImportResult? BuildDisplayGeometry(AcadDimension source, List<string> warnings)
    {
        var clone = (AcadDimension)source.Clone();
        clone.UpdateBlock();
        var block = clone.Block;
        if (block is null || block.Entities.Count == 0) return null;

        var snapshot = new AcadDocument();
        foreach (var entity in block.Entities)
        {
            if (string.Equals(entity.Layer?.Name, "Defpoints", StringComparison.OrdinalIgnoreCase)) continue;
            snapshot.Entities.Add((ACadSharp.Entities.Entity)entity.Clone());
        }
        if (snapshot.Entities.Count == 0) return null;

        using var output = new MemoryStream();
        using (var writer = new DxfWriter(output, snapshot, binary: false)) writer.Write();
        var text = Encoding.UTF8.GetString(output.ToArray());
        var imported = CadDxfFullInteropCodec.Import(text);
        var localWarnings = imported.Warnings.ToList();

        // Anonymous dimension blocks can contain INSERT-based arrowheads and annotation
        // entities. Reuse the same native semantic/display recovery pipeline on the snapshot.
        CadAcadDwgSemanticRepair.Apply(snapshot, imported.Document, localWarnings);
        CadAcadInsertDisplayRepair.Apply(snapshot, imported.Document, localWarnings);

        foreach (var warning in localWarnings)
        {
            var message = $"DIMENSION display snapshot: {warning}";
            if (!warnings.Contains(message, StringComparer.Ordinal)) warnings.Add(message);
        }
        return imported;
    }

    private static void RemoveIncorrectSemanticPlaceholder(AcadDimension source, UcadDocument target)
    {
        if (source is not DimensionLinear linear) return;
        var first = ToCadPoint(linear.FirstPoint);
        var second = ToCadPoint(linear.SecondPoint);
        var candidate = target.Entities.OfType<LinearDimensionEntity>().FirstOrDefault(entity =>
            (PointsNear(entity.FirstExtensionPoint, first) && PointsNear(entity.SecondExtensionPoint, second)) ||
            (PointsNear(entity.FirstExtensionPoint, second) && PointsNear(entity.SecondExtensionPoint, first)));
        if (candidate is not null) target.Remove(candidate.Id);
    }

    private static void CopyStyles(UcadDocument source, UcadDocument target)
    {
        foreach (var style in source.TextStyles)
            if (!target.TryGetTextStyle(style.Name, out _)) target.DefineTextStyle(style);
        foreach (var style in source.DimensionStyles)
            if (!target.TryGetDimensionStyle(style.Name, out _)) target.DefineDimensionStyle(style);
    }

    private static string ResolveLayer(ACadSharp.Entities.Entity source) =>
        string.IsNullOrWhiteSpace(source.Layer?.Name) ? CadLayer.DefaultLayerName : source.Layer.Name;

    private static void EnsureLayer(UcadDocument document, string name)
    {
        if (!document.TryGetLayer(name, out _)) document.CreateLayer(new CadLayer(name));
    }

    private static CadPoint ToCadPoint(CSMath.XYZ point) => new(point.X, point.Y);
    private static bool PointsNear(CadPoint first, CadPoint second) =>
        Math.Abs(first.X - second.X) <= Tolerance && Math.Abs(first.Y - second.Y) <= Tolerance;
}

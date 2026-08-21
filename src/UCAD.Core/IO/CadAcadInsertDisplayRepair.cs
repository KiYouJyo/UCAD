using System.Text;
using ACadSharp.Entities;
using ACadSharp.IO;
using UCAD.Core.Entities;
using UCAD.Core.Geometry;
using UCAD.Core.Layers;
using UCAD.Core.Styles;
using AcadDocument = ACadSharp.CadDocument;
using AcadInsert = ACadSharp.Entities.Insert;
using UcadDocument = UCAD.Core.CadDocument;
using UcadText = UCAD.Core.Entities.TextEntity;

namespace UCAD.Core.IO;

/// <summary>
/// Display-first recovery for AutoCAD INSERT variants that UCAD's editable block-reference
/// model cannot represent yet (mirrored/non-uniform scale and MINSERT arrays). ACadSharp owns
/// the affine explosion so the fallback does not reimplement DWG/DXF block mathematics.
/// </summary>
internal static class CadAcadInsertDisplayRepair
{
    private const double Tolerance = 1e-8;
    private const int MaxNestedDepth = 24;

    public static void Apply(AcadDocument source, UcadDocument target, List<string> warnings)
    {
        foreach (var insert in source.Entities.OfType<AcadInsert>())
        {
            if (!NeedsDisplayFallback(insert)) continue;
            try
            {
                if (insert.IsMultiple) RemoveSingleArrayPlaceholder(insert, target);

                var recovered = 0;
                foreach (var instance in EnumerateInstances(insert))
                {
                    var exploded = Flatten(instance, 0, warnings).ToArray();
                    if (exploded.Length > 0) recovered += ImportSnapshot(exploded, instance, target, warnings);
                    recovered += RecoverAttributes(instance, target, warnings);
                }

                if (recovered == 0)
                {
                    warnings.Add($"AutoCAD INSERT '{insert.Block?.Name ?? "<unknown>"}' required display fallback but produced no recoverable 2D geometry.");
                    continue;
                }

                warnings.RemoveAll(warning =>
                    warning.Contains("INSERT", StringComparison.OrdinalIgnoreCase) &&
                    warning.Contains(insert.Block?.Name ?? string.Empty, StringComparison.OrdinalIgnoreCase) &&
                    (warning.Contains("mirrored", StringComparison.OrdinalIgnoreCase) ||
                     warning.Contains("non-uniform", StringComparison.OrdinalIgnoreCase) ||
                     warning.Contains("positive uniform scale", StringComparison.OrdinalIgnoreCase)));
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or NotSupportedException or IOException)
            {
                warnings.Add($"AutoCAD INSERT '{insert.Block?.Name ?? "<unknown>"}' display fallback failed: {ex.Message}");
            }
        }
    }

    private static bool NeedsDisplayFallback(AcadInsert insert)
    {
        var nonUniform = Math.Abs(insert.XScale - insert.YScale) > Math.Max(Tolerance, Math.Max(Math.Abs(insert.XScale), Math.Abs(insert.YScale)) * Tolerance);
        return insert.XScale <= Tolerance || insert.YScale <= Tolerance || nonUniform || insert.IsMultiple;
    }

    private static IEnumerable<AcadInsert> EnumerateInstances(AcadInsert source)
    {
        var rows = Math.Max((int)source.RowCount, 1);
        var columns = Math.Max((int)source.ColumnCount, 1);
        var cos = Math.Cos(source.Rotation);
        var sin = Math.Sin(source.Rotation);

        for (var row = 0; row < rows; row++)
        {
            for (var column = 0; column < columns; column++)
            {
                var instance = (AcadInsert)source.Clone();
                instance.RowCount = 1;
                instance.ColumnCount = 1;
                if (row != 0 || column != 0)
                {
                    var localX = column * source.ColumnSpacing;
                    var localY = row * source.RowSpacing;
                    instance.InsertPoint = new CSMath.XYZ(
                        source.InsertPoint.X + localX * cos - localY * sin,
                        source.InsertPoint.Y + localX * sin + localY * cos,
                        source.InsertPoint.Z);
                }
                yield return instance;
            }
        }
    }

    private static IEnumerable<ACadSharp.Entities.Entity> Flatten(AcadInsert insert, int depth, List<string> warnings)
    {
        if (depth >= MaxNestedDepth)
        {
            warnings.Add($"AutoCAD INSERT '{insert.Block?.Name ?? "<unknown>"}' exceeded nested display-fallback depth {MaxNestedDepth}; deeper content was skipped.");
            yield break;
        }

        foreach (var entity in insert.Explode())
        {
            if (entity is AcadInsert nested)
            {
                foreach (var child in Flatten(nested, depth + 1, warnings)) yield return child;
            }
            else
            {
                yield return entity;
            }
        }
    }

    private static int ImportSnapshot(
        IReadOnlyList<ACadSharp.Entities.Entity> exploded,
        AcadInsert sourceInsert,
        UcadDocument target,
        List<string> warnings)
    {
        var snapshot = new AcadDocument();
        foreach (var entity in exploded) snapshot.Entities.Add(entity);

        using var output = new MemoryStream();
        using (var writer = new DxfWriter(output, snapshot, binary: false)) writer.Write();
        var text = Encoding.UTF8.GetString(output.ToArray());
        var imported = CadDxfFullInteropCodec.Import(text);
        var localWarnings = imported.Warnings.ToList();
        CadAcadDwgSemanticRepair.Apply(snapshot, imported.Document, localWarnings);
        foreach (var warning in localWarnings)
        {
            if (!warnings.Contains(warning, StringComparer.Ordinal)) warnings.Add($"INSERT display snapshot: {warning}");
        }

        CopyStyles(imported.Document, target);
        var additions = new List<(ICadEntity Entity, CadEntityProperties Properties)>();
        foreach (var entity in imported.Document.Entities)
        {
            var properties = imported.Document.GetEntityProperties(entity.Id);
            var layerName = string.Equals(properties.LayerName, CadLayer.DefaultLayerName, StringComparison.OrdinalIgnoreCase)
                ? ResolveInsertLayer(sourceInsert)
                : properties.LayerName;
            EnsureLayer(target, layerName);
            additions.Add((entity, properties with { LayerName = layerName }));
        }
        if (additions.Count > 0) target.AddRange(additions);
        return additions.Count;
    }

    private static int RecoverAttributes(AcadInsert instance, UcadDocument target, List<string> warnings)
    {
        if (instance.Block is null || !instance.Block.AttributeDefinitions.Any()) return 0;
        var values = instance.Attributes
            .Where(attribute => !string.IsNullOrWhiteSpace(attribute.Tag))
            .GroupBy(attribute => attribute.Tag, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First().Value ?? string.Empty, StringComparer.OrdinalIgnoreCase);
        var transform = instance.GetTransform();
        var properties = ToInsertProperties(instance, target);
        var added = 0;

        foreach (var definition in instance.Block.AttributeDefinitions)
        {
            var value = values.TryGetValue(definition.Tag, out var overrideValue) ? overrideValue : definition.Value;
            if (string.IsNullOrEmpty(value)) continue;
            var attribute = new AttributeEntity(definition) { Value = value };
            attribute.ApplyTransform(transform);
            var height = double.IsFinite(attribute.Height) && attribute.Height > Tolerance ? attribute.Height : 2.5;
            var styleName = string.IsNullOrWhiteSpace(attribute.Style?.Name) ? CadTextStyle.DefaultName : attribute.Style.Name;
            if (!target.TryGetTextStyle(styleName, out _)) target.DefineTextStyle(new CadTextStyle(styleName));
            target.Add(new UcadText(ToCadPoint(attribute.InsertPoint), value, height, attribute.Rotation, styleName), properties);
            added++;
        }
        return added;
    }

    private static void RemoveSingleArrayPlaceholder(AcadInsert insert, UcadDocument target)
    {
        var name = insert.Block?.Name;
        if (string.IsNullOrWhiteSpace(name)) return;
        var point = ToCadPoint(insert.InsertPoint);
        var placeholder = target.Entities.OfType<BlockReferenceEntity>().FirstOrDefault(reference =>
            string.Equals(reference.DefinitionName, name, StringComparison.OrdinalIgnoreCase) &&
            PointsNear(reference.InsertionPoint, point));
        if (placeholder is not null) target.Remove(placeholder.Id);
    }

    private static void CopyStyles(UcadDocument source, UcadDocument target)
    {
        foreach (var style in source.TextStyles)
            if (!target.TryGetTextStyle(style.Name, out _)) target.DefineTextStyle(style);
        foreach (var style in source.DimensionStyles)
            if (!target.TryGetDimensionStyle(style.Name, out _)) target.DefineDimensionStyle(style);
    }

    private static CadEntityProperties ToInsertProperties(AcadInsert source, UcadDocument target)
    {
        var layer = ResolveInsertLayer(source);
        EnsureLayer(target, layer);
        var lineType = string.IsNullOrWhiteSpace(source.LineType?.Name) ? "ByLayer" : source.LineType.Name;
        return new CadEntityProperties(layer, lineType: lineType);
    }

    private static string ResolveInsertLayer(AcadInsert source) =>
        string.IsNullOrWhiteSpace(source.Layer?.Name) ? CadLayer.DefaultLayerName : source.Layer.Name;

    private static void EnsureLayer(UcadDocument document, string name)
    {
        if (!document.TryGetLayer(name, out _)) document.CreateLayer(new CadLayer(name));
    }

    private static CadPoint ToCadPoint(CSMath.XYZ point) => new(point.X, point.Y);
    private static bool PointsNear(CadPoint first, CadPoint second) =>
        Math.Abs(first.X - second.X) <= Tolerance && Math.Abs(first.Y - second.Y) <= Tolerance;
}

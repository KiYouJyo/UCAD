using UCAD.Core.Entities;
using UCAD.Core.Geometry;
using UCAD.Core.Layers;
using AcadDocument = ACadSharp.CadDocument;
using AcadMText = ACadSharp.Entities.MText;
using AcadText = ACadSharp.Entities.TextEntity;
using AcadTextMirror = ACadSharp.Entities.TextMirrorFlag;
using AcadEntity = ACadSharp.Entities.Entity;
using UcadDocument = UCAD.Core.CadDocument;
using UcadMText = UCAD.Core.Entities.MTextEntity;
using UcadText = UCAD.Core.Entities.TextEntity;

namespace UCAD.Core.IO;

/// <summary>
/// Replaces generic TrueType-style display text with a resolvable reference whenever the
/// AutoCAD text style points at SHX/SHP resources. This prevents old engineering drawings
/// from silently substituting Segoe UI for single-stroke SHX glyphs and lets BigFont take
/// over DBCS glyphs after the source drawing directory is known.
/// </summary>
internal static class CadAcadShxTextDisplayRepair
{
    public static void Apply(AcadDocument source, UcadDocument target, List<string> warnings)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(warnings);

        var sourceEntities = source.Entities.ToArray();
        for (var sourceOrder = 0; sourceOrder < sourceEntities.Length; sourceOrder++)
        {
            var sourceEntity = sourceEntities[sourceOrder];
            try
            {
                ShxTextReferenceEntity? replacement = sourceEntity switch
                {
                    AcadText text when IsShxStyle(text.Style?.Filename) => CreateTextReference(text, source.Header.CodePage),
                    AcadMText text when IsShxStyle(text.Style?.Filename) => CreateMTextReference(text, source.Header.CodePage),
                    _ => null
                };
                if (replacement is null) continue;

                var existing = target.Entities
                    .Where(entity => target.GetEntityProperties(entity.Id).SourceOrder == sourceOrder)
                    .ToArray();
                var properties = existing.Length > 0
                    ? target.GetEntityProperties(existing[0].Id)
                    : CreateProperties(sourceEntity, sourceOrder, target);

                var replaceableIds = existing
                    .Where(entity => entity is UcadText or UcadMText)
                    .Select(entity => entity.Id)
                    .ToArray();
                if (replaceableIds.Length > 0) target.RemoveRange(replaceableIds);
                target.Add(replacement, properties with { SourceOrder = sourceOrder });

                warnings.RemoveAll(warning =>
                    warning.Contains("SHX", StringComparison.OrdinalIgnoreCase) &&
                    warning.Contains("font", StringComparison.OrdinalIgnoreCase) &&
                    warning.Contains("substitut", StringComparison.OrdinalIgnoreCase));
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or OverflowException)
            {
                warnings.Add($"AutoCAD SHX text display recovery failed; generic text fallback remains available. {ex.Message}");
            }
        }
    }

    private static ShxTextReferenceEntity? CreateTextReference(AcadText text, string? codePage)
    {
        if (string.IsNullOrEmpty(text.Value) || string.IsNullOrWhiteSpace(text.Style?.Filename)) return null;
        var alignment = text.AlignmentPoint;
        var hasAlignment = Convert.ToInt32(text.HorizontalAlignment) != 0 || Convert.ToInt32(text.VerticalAlignment) != 0;
        return new ShxTextReferenceEntity(
            text.Value,
            text.Style.Filename,
            text.Style.BigFontFilename,
            new CadPoint(text.InsertPoint.X, text.InsertPoint.Y),
            Math.Max(text.Height, 1e-9),
            NormalizeScale(text.WidthFactor * NormalizeStyleWidth(text.Style.Width)),
            NormalizeOblique(text.ObliqueAngle + text.Style.ObliqueAngle),
            text.Rotation,
            codePage,
            multiline: false,
            lineSpacingFactor: 1,
            horizontalAlignment: Convert.ToInt32(text.HorizontalAlignment),
            verticalAlignment: Convert.ToInt32(text.VerticalAlignment),
            alignmentPoint: hasAlignment ? new CadPoint(alignment.X, alignment.Y) : null,
            mirrorX: text.Mirror.HasFlag(AcadTextMirror.Backward),
            mirrorY: text.Mirror.HasFlag(AcadTextMirror.UpsideDown));
    }

    private static ShxTextReferenceEntity? CreateMTextReference(AcadMText text, string? codePage)
    {
        if (string.IsNullOrEmpty(text.PlainText) || string.IsNullOrWhiteSpace(text.Style?.Filename)) return null;
        return new ShxTextReferenceEntity(
            text.PlainText,
            text.Style.Filename,
            text.Style.BigFontFilename,
            new CadPoint(text.InsertPoint.X, text.InsertPoint.Y),
            Math.Max(text.Height, 1e-9),
            NormalizeScale(NormalizeStyleWidth(text.Style.Width)),
            NormalizeOblique(text.Style.ObliqueAngle),
            text.Rotation,
            codePage,
            multiline: true,
            lineSpacingFactor: Math.Clamp(text.LineSpacing, 0.25, 4),
            horizontalAlignment: 0,
            verticalAlignment: Convert.ToInt32(text.AttachmentPoint));
    }

    private static CadEntityProperties CreateProperties(AcadEntity entity, int sourceOrder, UcadDocument target)
    {
        var layer = string.IsNullOrWhiteSpace(entity.Layer?.Name) ? CadLayer.DefaultLayerName : entity.Layer.Name;
        if (!target.TryGetLayer(layer, out _)) target.CreateLayer(new CadLayer(layer));
        var lineType = string.IsNullOrWhiteSpace(entity.LineType?.Name) ? "ByLayer" : entity.LineType.Name;
        return new CadEntityProperties(layer, lineType: lineType, sourceOrder: sourceOrder);
    }

    private static bool IsShxStyle(string? path) =>
        !string.IsNullOrWhiteSpace(path) &&
        (path.EndsWith(".shx", StringComparison.OrdinalIgnoreCase) || path.EndsWith(".shp", StringComparison.OrdinalIgnoreCase));

    private static double NormalizeStyleWidth(double width) => double.IsFinite(width) && width > 0 ? width : 1;
    private static double NormalizeScale(double width) => double.IsFinite(width) && Math.Abs(width) > 1e-12 ? width : 1;

    private static double NormalizeOblique(double radians)
    {
        if (!double.IsFinite(radians)) return 0;
        var limit = 84.999 * Math.PI / 180.0;
        return Math.Clamp(radians, -limit, limit);
    }
}

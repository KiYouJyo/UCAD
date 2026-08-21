using System.Globalization;
using System.Text;
using UCAD.Core.Entities;
using UCAD.Core.Geometry;
using UCAD.Core.Layers;

namespace UCAD.Core.IO;

/// <summary>
/// Final DXF semantic boundary layered over the advanced bridge. It supplies dimension
/// kinds that the original advanced bridge intentionally did not yet map without
/// destabilizing its established block/hatch/leader path.
/// </summary>
public static class CadDxfFullInteropCodec
{
    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

    public static DxfImportResult Import(string content)
    {
        ArgumentNullException.ThrowIfNull(content);
        var imported = CadDxfAdvancedInteropCodec.Import(content);
        var warnings = imported.Warnings
            .Where(warning => !IsRadialDimensionSkipWarning(warning))
            .ToList();

        foreach (var record in EnumerateEntityRecords(content).Where(record => string.Equals(record.Type, "DIMENSION", StringComparison.OrdinalIgnoreCase)))
        {
            if (!record.TryGetInt(70, out var rawType)) continue;
            var type = rawType & 0x0F;
            if (type is not 3 and not 4) continue;
            try
            {
                var first = record.RequirePoint(10, 20);
                var second = record.RequirePoint(15, 25);
                var textPoint = record.TryGetPoint(11, 21, out var explicitText) ? explicitText : first;
                var textOverride = record.GetString(1);
                if (string.Equals(textOverride, "<>", StringComparison.Ordinal)) textOverride = null;
                var styleName = record.GetString(3);
                if (string.IsNullOrWhiteSpace(styleName)) styleName = imported.Document.CurrentDimensionStyleName;
                if (!imported.Document.TryGetDimensionStyle(styleName, out _))
                    imported.Document.DefineDimensionStyle(new UCAD.Core.Styles.CadDimensionStyle(styleName));

                RadialDimensionEntity radial;
                if (type == 4)
                {
                    radial = new RadialDimensionEntity(first, second, textPoint, false, textOverride, styleName);
                }
                else
                {
                    var center = new CadPoint((first.X + second.X) * 0.5, (first.Y + second.Y) * 0.5);
                    radial = new RadialDimensionEntity(center, second, textPoint, true, textOverride, styleName);
                }

                var layerName = record.GetString(8);
                if (string.IsNullOrWhiteSpace(layerName)) layerName = CadLayer.DefaultLayerName;
                if (!imported.Document.TryGetLayer(layerName, out _)) imported.Document.CreateLayer(new CadLayer(layerName));
                imported.Document.Add(radial, new CadEntityProperties(layerName));
            }
            catch (Exception ex) when (ex is FormatException or ArgumentException or InvalidOperationException)
            {
                warnings.Add($"DXF DIMENSION radial/diametric record was skipped: {ex.Message}");
            }
        }

        CadDxfDisplayFallbackCodec.Apply(content, imported.Document, warnings);
        CadDxfHatchDisplayFallback.Apply(content, imported.Document, warnings);
        CadDxfUnderlayDisplayFallback.Apply(content, imported.Document, warnings);
        imported.Document.ResetHistory();
        return new DxfImportResult(imported.Document, warnings);
    }

    public static DxfExportResult Export(CadDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        var baseline = CadDxfAdvancedInteropCodec.Export(document);
        var warnings = baseline.Warnings
            .Where(warning => !warning.Contains(nameof(RadialDimensionEntity), StringComparison.OrdinalIgnoreCase))
            .ToList();
        var radial = document.Entities.OfType<RadialDimensionEntity>().ToArray();
        if (radial.Length == 0) return new DxfExportResult(baseline.Content, warnings);

        var insertion = FindEntitiesEndSectionOffset(baseline.Content);
        if (insertion < 0)
        {
            warnings.Add("DXF radial dimensions could not be injected because the ENTITIES section terminator was not found.");
            return new DxfExportResult(baseline.Content, warnings);
        }

        var builder = new StringBuilder();
        foreach (var entity in radial) WriteRadialDimension(builder, entity, document.GetEntityProperties(entity.Id));
        var content = baseline.Content.Insert(insertion, builder.ToString());
        return new DxfExportResult(content, warnings);
    }

    private static void WriteRadialDimension(StringBuilder sb, RadialDimensionEntity entity, CadEntityProperties properties)
    {
        Pair(sb, 0, "DIMENSION");
        Pair(sb, 8, properties.LayerName);
        Pair(sb, 2, string.Empty);
        Pair(sb, 3, entity.StyleName);
        Pair(sb, 70, entity.Diameter ? 3 : 4);
        if (!string.IsNullOrEmpty(entity.TextOverride)) Pair(sb, 1, entity.TextOverride);

        if (entity.Diameter)
        {
            var dx = entity.PointOnCircle.X - entity.Center.X;
            var dy = entity.PointOnCircle.Y - entity.Center.Y;
            Point(sb, 10, 20, new CadPoint(entity.Center.X - dx, entity.Center.Y - dy));
            Point(sb, 15, 25, entity.PointOnCircle);
            Pair(sb, 40, Math.Sqrt(dx * dx + dy * dy) * 2.0);
        }
        else
        {
            Point(sb, 10, 20, entity.Center);
            Point(sb, 15, 25, entity.PointOnCircle);
            Pair(sb, 40, (entity.PointOnCircle - entity.Center).Length);
        }
        Point(sb, 11, 21, entity.TextPoint);
    }

    private static bool IsRadialDimensionSkipWarning(string warning) =>
        warning.Contains("DIMENSION type 3", StringComparison.OrdinalIgnoreCase) ||
        warning.Contains("DIMENSION type 4", StringComparison.OrdinalIgnoreCase) ||
        warning.Contains(nameof(RadialDimensionEntity), StringComparison.OrdinalIgnoreCase);

    private static int FindEntitiesEndSectionOffset(string content)
    {
        var marker = "0\nSECTION\n2\nENTITIES\n";
        var start = content.IndexOf(marker, StringComparison.Ordinal);
        if (start < 0)
        {
            marker = "0\r\nSECTION\r\n2\r\nENTITIES\r\n";
            start = content.IndexOf(marker, StringComparison.Ordinal);
        }
        if (start < 0) return -1;
        var lfEnd = content.IndexOf("0\nENDSEC\n", start + marker.Length, StringComparison.Ordinal);
        if (lfEnd >= 0) return lfEnd;
        return content.IndexOf("0\r\nENDSEC\r\n", start + marker.Length, StringComparison.Ordinal);
    }

    private static IEnumerable<DxfRecord> EnumerateEntityRecords(string content)
    {
        var pairs = ReadPairs(content);
        var inEntities = false;
        for (var i = 0; i < pairs.Count; i++)
        {
            var pair = pairs[i];
            if (pair.Code == 0 && pair.Value.Equals("SECTION", StringComparison.OrdinalIgnoreCase) && i + 1 < pairs.Count && pairs[i + 1].Code == 2)
            {
                inEntities = pairs[i + 1].Value.Equals("ENTITIES", StringComparison.OrdinalIgnoreCase);
                i++;
                continue;
            }
            if (inEntities && pair.Code == 0 && pair.Value.Equals("ENDSEC", StringComparison.OrdinalIgnoreCase)) { inEntities = false; continue; }
            if (!inEntities || pair.Code != 0) continue;
            var type = pair.Value;
            var values = new List<DxfPair>();
            var j = i + 1;
            while (j < pairs.Count && pairs[j].Code != 0) values.Add(pairs[j++]);
            yield return new DxfRecord(type, values);
            i = j - 1;
        }
    }

    private static IReadOnlyList<DxfPair> ReadPairs(string content)
    {
        var lines = content.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n').Split('\n');
        var pairs = new List<DxfPair>();
        for (var i = 0; i + 1 < lines.Length; i += 2)
        {
            if (!int.TryParse(lines[i].Trim(), NumberStyles.Integer, Invariant, out var code)) continue;
            pairs.Add(new DxfPair(code, lines[i + 1].TrimEnd()));
        }
        return pairs;
    }

    private static void Pair(StringBuilder sb, int code, string value) => sb.Append(code.ToString(Invariant)).Append('\n').Append(value).Append('\n');
    private static void Pair(StringBuilder sb, int code, int value) => Pair(sb, code, value.ToString(Invariant));
    private static void Pair(StringBuilder sb, int code, double value) => Pair(sb, code, value.ToString("0.###############", Invariant));
    private static void Point(StringBuilder sb, int xCode, int yCode, CadPoint point) { Pair(sb, xCode, point.X); Pair(sb, yCode, point.Y); }

    private readonly record struct DxfPair(int Code, string Value);

    private sealed class DxfRecord(string type, IReadOnlyList<DxfPair> values)
    {
        public string Type { get; } = type;
        private IReadOnlyList<DxfPair> Values { get; } = values;
        public string? GetString(int code) => Values.FirstOrDefault(pair => pair.Code == code).Value;
        public bool TryGetInt(int code, out int value) => int.TryParse(GetString(code), NumberStyles.Integer, Invariant, out value);
        public CadPoint RequirePoint(int xCode, int yCode) => TryGetPoint(xCode, yCode, out var point) ? point : throw new FormatException($"DXF point {xCode}/{yCode} is missing.");
        public bool TryGetPoint(int xCode, int yCode, out CadPoint point)
        {
            point = default;
            if (!double.TryParse(GetString(xCode), NumberStyles.Float, Invariant, out var x) || !double.TryParse(GetString(yCode), NumberStyles.Float, Invariant, out var y)) return false;
            point = new CadPoint(x, y);
            return true;
        }
    }
}

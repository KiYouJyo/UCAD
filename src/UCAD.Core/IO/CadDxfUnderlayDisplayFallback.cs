using System.Globalization;
using UCAD.Core.Entities;
using UCAD.Core.Geometry;
using UCAD.Core.Layers;

namespace UCAD.Core.IO;

/// <summary>
/// Raw DXF recovery for DWF/DGN underlays. ACadSharp currently exposes PDF underlays as
/// typed entities but not all DWF/DGN variants, while the DXF representation is stable and
/// shares AcDbUnderlayReference group codes. Definitions live in OBJECTS and references in
/// ENTITIES, linked by group 340 -> definition handle.
/// </summary>
internal static class CadDxfUnderlayDisplayFallback
{
    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;
    private const double Epsilon = 1e-12;

    public static void Apply(string content, CadDocument document, List<string> warnings)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(warnings);

        var records = ReadRecords(content);
        var definitions = records
            .Where(record => record.Section == "OBJECTS" && IsDefinition(record.Type))
            .Select(ParseDefinition)
            .Where(definition => definition is not null)
            .Cast<UnderlayDefinition>()
            .ToDictionary(definition => definition.Handle, StringComparer.OrdinalIgnoreCase);

        var sourceOrder = -1;
        foreach (var record in records.Where(record => record.Section == "ENTITIES"))
        {
            sourceOrder++;
            var kind = record.Type.ToUpperInvariant() switch
            {
                "DWFUNDERLAY" or "DWFREFERENCE" => CadUnderlayKind.Dwf,
                "DGNUNDERLAY" or "DGNREFERENCE" => CadUnderlayKind.Dgn,
                _ => CadUnderlayKind.Unknown
            };
            if (kind == CadUnderlayKind.Unknown) continue;

            try
            {
                var flags = record.GetInt(280, 10);
                if ((flags & 2) == 0) continue; // underlay display switched off in source drawing

                var definitionHandle = record.GetString(340);
                definitions.TryGetValue(definitionHandle ?? string.Empty, out var definition);
                var referencePath = definition?.FileName;
                if (string.IsNullOrWhiteSpace(referencePath)) referencePath = $"<missing-{kind.ToString().ToLowerInvariant()}-underlay-definition>";

                var insertion = new CadPoint(record.GetDouble(10, 0), record.GetDouble(20, 0));
                var xScale = NonZeroScale(record.GetDouble(41, 1));
                var yScale = NonZeroScale(record.GetDouble(42, 1));
                var rotation = DegreesToRadians(record.GetDouble(50, 0));
                var clip = (flags & 1) != 0
                    ? ConvertClipBoundary(record.GetPointList(11, 21), insertion, xScale, yScale, rotation)
                    : [];

                var underlay = new UnderlayReferenceEntity(
                    kind,
                    referencePath,
                    definition?.Name,
                    insertion,
                    xScale,
                    yScale,
                    rotation,
                    clip,
                    (byte)Math.Clamp(record.GetInt(281, 100), 0, 100),
                    (byte)Math.Clamp(record.GetInt(282, 0), 0, 100),
                    monochrome: (flags & 4) != 0,
                    adjustForBackground: (flags & 8) != 0,
                    clipInside: (flags & 16) != 0);

                var layer = record.GetString(8);
                if (string.IsNullOrWhiteSpace(layer)) layer = CadLayer.DefaultLayerName;
                if (!document.TryGetLayer(layer, out _)) document.CreateLayer(new CadLayer(layer));
                var lineType = record.GetString(6);
                if (string.IsNullOrWhiteSpace(lineType)) lineType = "ByLayer";
                document.Add(underlay, new CadEntityProperties(layer, lineType: lineType, sourceOrder: sourceOrder, sourceHandle: record.GetString(5)));

                warnings.RemoveAll(warning =>
                    warning.Contains(kind == CadUnderlayKind.Dwf ? "DWF" : "DGN", StringComparison.OrdinalIgnoreCase) &&
                    warning.Contains("UNDERLAY", StringComparison.OrdinalIgnoreCase) &&
                    (warning.Contains("not supported", StringComparison.OrdinalIgnoreCase) ||
                     warning.Contains("skipped", StringComparison.OrdinalIgnoreCase) ||
                     warning.Contains("could not be imported", StringComparison.OrdinalIgnoreCase)));
            }
            catch (Exception ex) when (ex is ArgumentException or FormatException or InvalidOperationException or OverflowException)
            {
                warnings.Add($"DXF {kind} underlay display recovery failed; the original source payload remains preserved. {ex.Message}");
            }
        }
    }

    private static UnderlayDefinition? ParseDefinition(DxfRecord record)
    {
        var handle = record.GetString(5);
        var fileName = record.GetString(1);
        if (string.IsNullOrWhiteSpace(handle) || string.IsNullOrWhiteSpace(fileName)) return null;
        return new UnderlayDefinition(handle.Trim(), fileName.Trim(), record.GetString(2));
    }

    private static bool IsDefinition(string type) =>
        type.Equals("DWFDEFINITION", StringComparison.OrdinalIgnoreCase) ||
        type.Equals("DGNDEFINITION", StringComparison.OrdinalIgnoreCase);

    private static IReadOnlyList<CadPoint> ConvertClipBoundary(
        IReadOnlyList<CadPoint> local,
        CadPoint insertion,
        double xScale,
        double yScale,
        double rotation)
    {
        if (local.Count < 2) return [];
        IReadOnlyList<CadPoint> boundary = local;
        if (local.Count == 2)
        {
            var a = local[0];
            var b = local[1];
            boundary =
            [
                new CadPoint(a.X, a.Y),
                new CadPoint(b.X, a.Y),
                new CadPoint(b.X, b.Y),
                new CadPoint(a.X, b.Y)
            ];
        }

        var cos = Math.Cos(rotation);
        var sin = Math.Sin(rotation);
        var result = new List<CadPoint>(boundary.Count);
        foreach (var point in boundary)
        {
            var x = point.X * xScale;
            var y = point.Y * yScale;
            result.Add(new CadPoint(
                insertion.X + x * cos - y * sin,
                insertion.Y + x * sin + y * cos));
        }
        while (result.Count > 1 && (result[0] - result[^1]).Length <= Epsilon) result.RemoveAt(result.Count - 1);
        return result;
    }

    private static double NonZeroScale(double value) =>
        double.IsFinite(value) && Math.Abs(value) > Epsilon ? value : 1d;

    private static double DegreesToRadians(double value) => value * Math.PI / 180d;

    private static IReadOnlyList<DxfRecord> ReadRecords(string content)
    {
        var lines = content.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n').Split('\n');
        var pairs = new List<DxfPair>();
        for (var i = 0; i + 1 < lines.Length; i += 2)
        {
            if (!int.TryParse(lines[i].Trim(), NumberStyles.Integer, Invariant, out var code)) continue;
            pairs.Add(new DxfPair(code, lines[i + 1].TrimEnd()));
        }

        var records = new List<DxfRecord>();
        var section = string.Empty;
        for (var i = 0; i < pairs.Count; i++)
        {
            if (pairs[i].Code == 0 && pairs[i].Value.Equals("SECTION", StringComparison.OrdinalIgnoreCase) &&
                i + 1 < pairs.Count && pairs[i + 1].Code == 2)
            {
                section = pairs[++i].Value.ToUpperInvariant();
                continue;
            }
            if (pairs[i].Code == 0 && pairs[i].Value.Equals("ENDSEC", StringComparison.OrdinalIgnoreCase))
            {
                section = string.Empty;
                continue;
            }
            if (string.IsNullOrEmpty(section) || pairs[i].Code != 0) continue;

            var type = pairs[i].Value;
            var values = new List<DxfPair>();
            var cursor = i + 1;
            while (cursor < pairs.Count && pairs[cursor].Code != 0) values.Add(pairs[cursor++]);
            records.Add(new DxfRecord(section, type, values));
            i = cursor - 1;
        }
        return records;
    }

    private readonly record struct DxfPair(int Code, string Value);
    private sealed record UnderlayDefinition(string Handle, string FileName, string? Name);

    private sealed class DxfRecord(string section, string type, IReadOnlyList<DxfPair> values)
    {
        public string Section { get; } = section;
        public string Type { get; } = type;
        private IReadOnlyList<DxfPair> Values { get; } = values;

        public string? GetString(int code) => Values.FirstOrDefault(pair => pair.Code == code).Value;
        public int GetInt(int code, int fallback) => int.TryParse(GetString(code), NumberStyles.Integer, Invariant, out var value) ? value : fallback;
        public double GetDouble(int code, double fallback) => double.TryParse(GetString(code), NumberStyles.Float, Invariant, out var value) ? value : fallback;

        public IReadOnlyList<CadPoint> GetPointList(int xCode, int yCode)
        {
            var result = new List<CadPoint>();
            double? x = null;
            foreach (var pair in Values)
            {
                if (pair.Code == xCode)
                {
                    if (double.TryParse(pair.Value, NumberStyles.Float, Invariant, out var parsed)) x = parsed;
                }
                else if (pair.Code == yCode && x is not null && double.TryParse(pair.Value, NumberStyles.Float, Invariant, out var y))
                {
                    result.Add(new CadPoint(x.Value, y));
                    x = null;
                }
            }
            return result;
        }
    }
}

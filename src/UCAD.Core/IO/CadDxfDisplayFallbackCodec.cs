using System.Globalization;
using UCAD.Core.Entities;
using UCAD.Core.Geometry;
using UCAD.Core.Layers;

namespace UCAD.Core.IO;

/// <summary>
/// Display-first recovery for common AutoCAD 2D records that the editable DXF bridge
/// intentionally does not model yet. The fallback prefers visually faithful UCAD geometry
/// over dropping an entity completely; it does not claim round-trip semantic fidelity.
/// </summary>
internal static class CadDxfDisplayFallbackCodec
{
    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;
    private const double Epsilon = 1e-10;
    private const double MaxArcStepRadians = Math.PI / 18.0; // 10 degrees

    public static void Apply(string content, CadDocument document, List<string> warnings)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(warnings);

        var pairs = ParsePairs(content);
        var inEntities = false;
        for (var i = 0; i < pairs.Count; i++)
        {
            var pair = pairs[i];
            if (pair.Code == 0 && EqualsToken(pair.Value, "SECTION") && i + 1 < pairs.Count && pairs[i + 1].Code == 2)
            {
                inEntities = EqualsToken(pairs[++i].Value, "ENTITIES");
                continue;
            }
            if (pair.Code == 0 && EqualsToken(pair.Value, "ENDSEC"))
            {
                inEntities = false;
                continue;
            }
            if (!inEntities || pair.Code != 0) continue;

            var type = pair.Value.ToUpperInvariant();
            if (type == "POLYLINE")
            {
                var header = ReadRecord(pairs, i + 1, out var cursor);
                var vertices = new List<IReadOnlyList<DxfPair>>();
                while (cursor < pairs.Count && pairs[cursor].Code == 0 && EqualsToken(pairs[cursor].Value, "VERTEX"))
                {
                    vertices.Add(ReadRecord(pairs, cursor + 1, out cursor));
                }
                if (cursor < pairs.Count && pairs[cursor].Code == 0 && EqualsToken(pairs[cursor].Value, "SEQEND"))
                {
                    ReadRecord(pairs, cursor + 1, out cursor);
                }
                i = Math.Max(i, cursor - 1);
                RecoverLegacyPolyline(header, vertices, document, warnings);
                continue;
            }

            var record = ReadRecord(pairs, i + 1, out var nextIndex);
            i = nextIndex - 1;
            switch (type)
            {
                case "LWPOLYLINE":
                    RecoverBulgedLightweightPolyline(record, document, warnings);
                    break;
                case "SOLID":
                case "TRACE":
                    RecoverFilledFace(type, record, document, warnings);
                    break;
                case "3DFACE":
                    RecoverFaceOutline(record, document, warnings);
                    break;
            }
        }
    }

    private static void RecoverBulgedLightweightPolyline(IReadOnlyList<DxfPair> record, CadDocument document, List<string> warnings)
    {
        var vertices = ReadLightweightVertices(record);
        if (vertices.Count < 2 || vertices.All(vertex => Math.Abs(vertex.Bulge) <= Epsilon)) return;

        try
        {
            var closed = (GetInt(record, 70, 0) & 1) != 0;
            var points = FlattenBulgedPolyline(vertices, closed);
            document.Add(new PolylineEntity(points, closed), ParseEntityProperties(record, document));
            RemoveWarnings(warnings, "LWPOLYLINE", "bulge");
        }
        catch (Exception ex) when (ex is ArgumentException or FormatException or InvalidOperationException)
        {
            warnings.Add($"DXF display fallback could not flatten LWPOLYLINE bulge geometry: {ex.Message}");
        }
    }

    private static void RecoverLegacyPolyline(
        IReadOnlyList<DxfPair> header,
        IReadOnlyList<IReadOnlyList<DxfPair>> vertexRecords,
        CadDocument document,
        List<string> warnings)
    {
        var flags = GetInt(header, 70, 0);
        if ((flags & (16 | 64)) != 0) return; // polygon mesh / polyface mesh are outside the 2D display fallback.

        var vertices = new List<BulgeVertex>();
        foreach (var vertex in vertexRecords)
        {
            if (!TryGetDouble(vertex, 10, out var x) || !TryGetDouble(vertex, 20, out var y)) continue;
            vertices.Add(new BulgeVertex(new CadPoint(x, y), GetDouble(vertex, 42, 0.0)));
        }
        if (vertices.Count < 2) return;

        try
        {
            var closed = (flags & 1) != 0;
            var points = FlattenBulgedPolyline(vertices, closed);
            document.Add(new PolylineEntity(points, closed), ParseEntityProperties(header, document));
            RemoveWarnings(warnings, "POLYLINE");
            RemoveWarnings(warnings, "VERTEX");
            RemoveWarnings(warnings, "SEQEND");
        }
        catch (Exception ex) when (ex is ArgumentException or FormatException or InvalidOperationException)
        {
            warnings.Add($"DXF display fallback could not recover legacy POLYLINE geometry: {ex.Message}");
        }
    }

    private static void RecoverFilledFace(string type, IReadOnlyList<DxfPair> record, CadDocument document, List<string> warnings)
    {
        try
        {
            var vertices = ReadFaceVertices(record);
            if (vertices.Count < 3) return;

            // AutoCAD SOLID/TRACE stores the fourth vertex in a crossed ordering; reorder it
            // into a simple boundary so the UCAD solid hatch does not self-intersect.
            if (vertices.Count == 4)
                vertices = [vertices[0], vertices[1], vertices[3], vertices[2]];

            document.Add(new HatchEntity(vertices, "Solid"), ParseEntityProperties(record, document));
            RemoveWarnings(warnings, type);
        }
        catch (Exception ex) when (ex is ArgumentException or FormatException or InvalidOperationException)
        {
            warnings.Add($"DXF display fallback could not recover {type}: {ex.Message}");
        }
    }

    private static void RecoverFaceOutline(IReadOnlyList<DxfPair> record, CadDocument document, List<string> warnings)
    {
        try
        {
            var vertices = ReadFaceVertices(record);
            if (vertices.Count < 3) return;
            document.Add(new PolylineEntity(vertices, closed: true), ParseEntityProperties(record, document));
            RemoveWarnings(warnings, "3DFACE");
        }
        catch (Exception ex) when (ex is ArgumentException or FormatException or InvalidOperationException)
        {
            warnings.Add($"DXF display fallback could not recover 3DFACE outline: {ex.Message}");
        }
    }

    private static List<BulgeVertex> ReadLightweightVertices(IReadOnlyList<DxfPair> record)
    {
        var result = new List<BulgeVertex>();
        double? x = null;
        double? y = null;
        double bulge = 0;

        void Flush()
        {
            if (x is null || y is null) return;
            result.Add(new BulgeVertex(new CadPoint(x.Value, y.Value), bulge));
            x = null;
            y = null;
            bulge = 0;
        }

        foreach (var pair in record)
        {
            if (pair.Code == 10)
            {
                Flush();
                x = ParseDouble(pair.Value, 10);
            }
            else if (pair.Code == 20)
            {
                y = ParseDouble(pair.Value, 20);
            }
            else if (pair.Code == 42)
            {
                bulge = ParseDouble(pair.Value, 42);
            }
        }
        Flush();
        return result;
    }

    private static IReadOnlyList<CadPoint> FlattenBulgedPolyline(IReadOnlyList<BulgeVertex> vertices, bool closed)
    {
        var output = new List<CadPoint>(vertices.Count * 4) { vertices[0].Point };
        var segmentCount = closed ? vertices.Count : vertices.Count - 1;
        for (var i = 0; i < segmentCount; i++)
        {
            var start = vertices[i];
            var end = vertices[(i + 1) % vertices.Count].Point;
            AppendBulgeSegment(output, start.Point, end, start.Bulge);
        }

        if (closed && output.Count > 1 && PointsNear(output[0], output[^1])) output.RemoveAt(output.Count - 1);
        return output;
    }

    private static void AppendBulgeSegment(List<CadPoint> output, CadPoint start, CadPoint end, double bulge)
    {
        if (Math.Abs(bulge) <= Epsilon)
        {
            AppendDistinct(output, end);
            return;
        }

        var dx = end.X - start.X;
        var dy = end.Y - start.Y;
        var chord = Math.Sqrt(dx * dx + dy * dy);
        if (chord <= Epsilon)
        {
            AppendDistinct(output, end);
            return;
        }

        var sweep = 4.0 * Math.Atan(bulge);
        var halfSweep = sweep * 0.5;
        var tanHalf = Math.Tan(halfSweep);
        if (Math.Abs(tanHalf) <= Epsilon)
        {
            AppendDistinct(output, end);
            return;
        }

        var midpoint = new CadPoint((start.X + end.X) * 0.5, (start.Y + end.Y) * 0.5);
        var centerOffset = chord / (2.0 * tanHalf);
        var nx = -dy / chord;
        var ny = dx / chord;
        var center = new CadPoint(midpoint.X + nx * centerOffset, midpoint.Y + ny * centerOffset);
        var radius = Math.Sqrt(Math.Pow(start.X - center.X, 2) + Math.Pow(start.Y - center.Y, 2));
        var startAngle = Math.Atan2(start.Y - center.Y, start.X - center.X);
        var steps = Math.Max(2, (int)Math.Ceiling(Math.Abs(sweep) / MaxArcStepRadians));

        for (var step = 1; step <= steps; step++)
        {
            if (step == steps)
            {
                AppendDistinct(output, end);
                continue;
            }
            var angle = startAngle + sweep * step / steps;
            AppendDistinct(output, new CadPoint(center.X + radius * Math.Cos(angle), center.Y + radius * Math.Sin(angle)));
        }
    }

    private static List<CadPoint> ReadFaceVertices(IReadOnlyList<DxfPair> record)
    {
        var result = new List<CadPoint>(4);
        foreach (var xCode in new[] { 10, 11, 12, 13 })
        {
            var yCode = xCode + 10;
            if (!TryGetDouble(record, xCode, out var x) || !TryGetDouble(record, yCode, out var y)) continue;
            var point = new CadPoint(x, y);
            if (result.Count == 0 || !PointsNear(result[^1], point)) result.Add(point);
        }
        if (result.Count > 2 && PointsNear(result[0], result[^1])) result.RemoveAt(result.Count - 1);
        return result;
    }

    private static CadEntityProperties ParseEntityProperties(IReadOnlyList<DxfPair> record, CadDocument document)
    {
        var layerName = GetString(record, 8, CadLayer.DefaultLayerName);
        if (string.IsNullOrWhiteSpace(layerName)) layerName = CadLayer.DefaultLayerName;
        if (!document.TryGetLayer(layerName, out _)) document.CreateLayer(new CadLayer(layerName));
        var lineType = GetString(record, 6, "ByLayer");
        if (string.IsNullOrWhiteSpace(lineType)) lineType = "ByLayer";
        return new CadEntityProperties(layerName, lineType: lineType);
    }

    private static void RemoveWarnings(List<string> warnings, params string[] fragments)
    {
        warnings.RemoveAll(warning => fragments.All(fragment => warning.Contains(fragment, StringComparison.OrdinalIgnoreCase)));
    }

    private static void AppendDistinct(List<CadPoint> points, CadPoint point)
    {
        if (points.Count == 0 || !PointsNear(points[^1], point)) points.Add(point);
    }

    private static bool PointsNear(CadPoint first, CadPoint second) =>
        Math.Abs(first.X - second.X) <= 1e-8 && Math.Abs(first.Y - second.Y) <= 1e-8;

    private static IReadOnlyList<DxfPair> ParsePairs(string content)
    {
        var result = new List<DxfPair>();
        using var reader = new StringReader(content);
        while (true)
        {
            var codeLine = reader.ReadLine();
            if (codeLine is null) break;
            var valueLine = reader.ReadLine();
            if (valueLine is null) break;
            if (!int.TryParse(codeLine.Trim().TrimStart('\uFEFF'), NumberStyles.Integer, Invariant, out var code)) continue;
            result.Add(new DxfPair(code, valueLine.TrimEnd()));
        }
        return result;
    }

    private static IReadOnlyList<DxfPair> ReadRecord(IReadOnlyList<DxfPair> pairs, int start, out int nextIndex)
    {
        var record = new List<DxfPair>();
        var i = start;
        while (i < pairs.Count && pairs[i].Code != 0) record.Add(pairs[i++]);
        nextIndex = i;
        return record;
    }

    private static string GetString(IReadOnlyList<DxfPair> record, int code, string fallback) =>
        record.FirstOrDefault(pair => pair.Code == code).Value is { Length: > 0 } value ? value : fallback;

    private static int GetInt(IReadOnlyList<DxfPair> record, int code, int fallback) =>
        int.TryParse(GetString(record, code, string.Empty), NumberStyles.Integer, Invariant, out var value) ? value : fallback;

    private static double GetDouble(IReadOnlyList<DxfPair> record, int code, double fallback) =>
        TryGetDouble(record, code, out var value) ? value : fallback;

    private static bool TryGetDouble(IReadOnlyList<DxfPair> record, int code, out double value) =>
        double.TryParse(GetString(record, code, string.Empty), NumberStyles.Float, Invariant, out value) && double.IsFinite(value);

    private static double ParseDouble(string value, int code)
    {
        if (!double.TryParse(value, NumberStyles.Float, Invariant, out var parsed) || !double.IsFinite(parsed))
            throw new FormatException($"DXF group {code} value '{value}' is not a finite number.");
        return parsed;
    }

    private static bool EqualsToken(string? value, string expected) => string.Equals(value, expected, StringComparison.OrdinalIgnoreCase);

    private readonly record struct DxfPair(int Code, string Value);
    private readonly record struct BulgeVertex(CadPoint Point, double Bulge);
}

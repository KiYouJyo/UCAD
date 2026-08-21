using System.Globalization;
using UCAD.Core.Entities;
using UCAD.Core.Geometry;
using UCAD.Core.Layers;

namespace UCAD.Core.IO;

/// <summary>
/// Recovers AutoCAD HATCH boundary-path variants that the editable hatch model does not
/// fully preserve yet. Edge paths are tessellated into display polygons so imported drawings
/// remain visually complete without pretending that the original edge semantics are editable.
/// </summary>
internal static class CadDxfHatchDisplayFallback
{
    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;
    private const double Epsilon = 1e-10;
    private const double MaxArcStepRadians = Math.PI / 36.0; // 5 degrees

    public static void Apply(string content, CadDocument document, List<string> warnings)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(warnings);

        foreach (var record in EnumerateEntities(content).Where(record => EqualsToken(record.Type, "HATCH")))
        {
            if (!NeedsFallback(record.Data)) continue;
            try
            {
                var parsed = ParseBoundaryPaths(record.Data);
                if (parsed.Loops.Count == 0) continue;

                var pattern = GetString(record.Data, 2, "SOLID");
                var scale = Math.Max(GetDouble(record.Data, 41, 1.0), 1e-9);
                var angle = DegreesToRadians(GetDouble(record.Data, 52, 0.0));
                var islandDetection = GetInt(record.Data, 75, 0) switch
                {
                    1 => HatchIslandDetection.Outer,
                    2 => HatchIslandDetection.Ignore,
                    _ => HatchIslandDetection.Normal
                };

                document.Add(
                    new HatchEntity(
                        parsed.Loops[0],
                        pattern,
                        scale,
                        angle,
                        parsed.Loops.Skip(1),
                        associative: false,
                        sourceEntityIds: null,
                        islandDetection),
                    ParseEntityProperties(record.Data, document));

                warnings.RemoveAll(warning =>
                    warning.Contains("HATCH", StringComparison.OrdinalIgnoreCase) &&
                    (warning.Contains("edge-based", StringComparison.OrdinalIgnoreCase) ||
                     warning.Contains("bulge", StringComparison.OrdinalIgnoreCase) ||
                     warning.Contains("could not be imported", StringComparison.OrdinalIgnoreCase) ||
                     warning.Contains("supported closed polyline boundary", StringComparison.OrdinalIgnoreCase)));

                if (parsed.ApproximatedSpline)
                    warnings.Add("DXF display fallback approximated a HATCH spline edge with its fit/control polygon; the original source remains preserved for lossless untouched re-export.");
            }
            catch (Exception ex) when (ex is ArgumentException or FormatException or InvalidOperationException)
            {
                warnings.Add($"DXF display fallback could not recover HATCH edge geometry: {ex.Message}");
            }
        }
    }

    private static bool NeedsFallback(IReadOnlyList<DxfPair> record)
    {
        for (var i = 0; i < record.Count; i++)
        {
            if (record[i].Code != 92) continue;
            var flags = ParseInt(record[i].Value, 92);
            if ((flags & 2) == 0) return true;

            var cursor = i + 1;
            while (cursor < record.Count && record[cursor].Code != 92 && record[cursor].Code != 75)
            {
                if (record[cursor].Code == 42 && Math.Abs(ParseDouble(record[cursor].Value, 42)) > Epsilon) return true;
                cursor++;
            }
        }
        return false;
    }

    private static HatchPathResult ParseBoundaryPaths(IReadOnlyList<DxfPair> record)
    {
        var expectedPaths = Math.Max(GetInt(record, 91, 0), 0);
        var loops = new List<IReadOnlyList<CadPoint>>();
        var approximatedSpline = false;
        var cursor = 0;

        for (var pathIndex = 0; pathIndex < expectedPaths; pathIndex++)
        {
            cursor = FindNext(record, cursor, 92);
            if (cursor < 0) break;
            var flags = ParseInt(record[cursor].Value, 92);
            cursor++;

            IReadOnlyList<CadPoint> loop;
            if ((flags & 2) != 0)
            {
                loop = ParsePolylinePath(record, ref cursor);
            }
            else
            {
                loop = ParseEdgePath(record, ref cursor, ref approximatedSpline);
            }

            var normalized = NormalizeLoop(loop);
            if (normalized.Count >= 3) loops.Add(normalized);
        }

        return new HatchPathResult(loops, approximatedSpline);
    }

    private static IReadOnlyList<CadPoint> ParsePolylinePath(IReadOnlyList<DxfPair> record, ref int cursor)
    {
        var hasBulge = ReadNextInt(record, ref cursor, 72) != 0;
        var closed = ReadNextInt(record, ref cursor, 73) != 0;
        var vertexCount = ReadNextInt(record, ref cursor, 93);
        if (vertexCount < 3) throw new FormatException("HATCH polyline path has fewer than three vertices.");

        var vertices = new List<BulgeVertex>(vertexCount);
        for (var vertexIndex = 0; vertexIndex < vertexCount; vertexIndex++)
        {
            cursor = FindNext(record, cursor, 10);
            if (cursor < 0) throw new FormatException("HATCH polyline path is missing a vertex X coordinate.");
            var x = ParseDouble(record[cursor++].Value, 10);
            cursor = FindNextBefore(record, cursor, 20, 10, 92);
            if (cursor < 0) throw new FormatException("HATCH polyline path is missing a vertex Y coordinate.");
            var y = ParseDouble(record[cursor++].Value, 20);
            var bulge = 0.0;
            if (hasBulge && cursor < record.Count && record[cursor].Code == 42)
                bulge = ParseDouble(record[cursor++].Value, 42);
            vertices.Add(new BulgeVertex(new CadPoint(x, y), bulge));
        }

        SkipSourceHandles(record, ref cursor);
        return FlattenBulgedPolyline(vertices, closed: closed || vertices.Count >= 3);
    }

    private static IReadOnlyList<CadPoint> ParseEdgePath(IReadOnlyList<DxfPair> record, ref int cursor, ref bool approximatedSpline)
    {
        cursor = FindNext(record, cursor, 93);
        if (cursor < 0) throw new FormatException("HATCH edge path is missing edge count group 93.");
        var edgeCount = ParseInt(record[cursor++].Value, 93);
        if (edgeCount <= 0) throw new FormatException("HATCH edge path contains no edges.");

        var loop = new List<CadPoint>();
        for (var edgeIndex = 0; edgeIndex < edgeCount; edgeIndex++)
        {
            cursor = FindNext(record, cursor, 72);
            if (cursor < 0) throw new FormatException("HATCH edge path is missing edge type group 72.");
            var type = ParseInt(record[cursor++].Value, 72);
            var edgeEnd = FindEdgeEnd(record, cursor);
            var edge = Slice(record, cursor, edgeEnd);
            cursor = edgeEnd;

            switch (type)
            {
                case 1:
                    AppendLineEdge(loop, edge);
                    break;
                case 2:
                    AppendCircularArcEdge(loop, edge);
                    break;
                case 3:
                    AppendEllipticalArcEdge(loop, edge);
                    break;
                case 4:
                    AppendSplineEdge(loop, edge);
                    approximatedSpline = true;
                    break;
                default:
                    throw new FormatException($"HATCH edge type {type} is not recognized by the display fallback.");
            }
        }

        SkipSourceHandles(record, ref cursor);
        return loop;
    }

    private static void AppendLineEdge(List<CadPoint> loop, IReadOnlyList<DxfPair> edge)
    {
        var start = ReadPoint(edge, 10, 20);
        var end = ReadPoint(edge, 11, 21);
        AppendDistinct(loop, start);
        AppendDistinct(loop, end);
    }

    private static void AppendCircularArcEdge(List<CadPoint> loop, IReadOnlyList<DxfPair> edge)
    {
        var center = ReadPoint(edge, 10, 20);
        var radius = RequiredDouble(edge, 40);
        if (radius <= Epsilon) throw new FormatException("HATCH circular arc edge has a non-positive radius.");
        var start = DegreesToRadians(RequiredDouble(edge, 50));
        var end = DegreesToRadians(RequiredDouble(edge, 51));
        var ccw = GetInt(edge, 73, 1) != 0;
        AppendArc(loop, center, radius, start, end, ccw);
    }

    private static void AppendEllipticalArcEdge(List<CadPoint> loop, IReadOnlyList<DxfPair> edge)
    {
        var center = ReadPoint(edge, 10, 20);
        var major = new CadVector(RequiredDouble(edge, 11), RequiredDouble(edge, 21));
        var majorLength = major.Length;
        if (majorLength <= Epsilon) throw new FormatException("HATCH elliptical arc edge has a zero major axis.");
        var ratio = RequiredDouble(edge, 40);
        if (!double.IsFinite(ratio) || ratio <= Epsilon) throw new FormatException("HATCH elliptical arc edge has an invalid minor/major ratio.");
        var start = DegreesToRadians(RequiredDouble(edge, 50));
        var end = DegreesToRadians(RequiredDouble(edge, 51));
        var ccw = GetInt(edge, 73, 1) != 0;
        AppendEllipseArc(loop, center, major, ratio, start, end, ccw);
    }

    private static void AppendSplineEdge(List<CadPoint> loop, IReadOnlyList<DxfPair> edge)
    {
        var fitPoints = ReadRepeatedPoints(edge, 11, 21);
        var points = fitPoints.Count >= 2 ? fitPoints : ReadRepeatedPoints(edge, 10, 20);
        if (points.Count < 2) throw new FormatException("HATCH spline edge has no usable fit/control points.");
        foreach (var point in points) AppendDistinct(loop, point);
    }

    private static void AppendArc(List<CadPoint> loop, CadPoint center, double radius, double start, double end, bool ccw)
    {
        var sweep = ccw ? NormalizePositive(end - start) : -NormalizePositive(start - end);
        if (Math.Abs(sweep) <= Epsilon) sweep = ccw ? Math.Tau : -Math.Tau;
        var steps = Math.Max(2, (int)Math.Ceiling(Math.Abs(sweep) / MaxArcStepRadians));
        for (var i = 0; i <= steps; i++)
        {
            var angle = start + sweep * i / steps;
            AppendDistinct(loop, new CadPoint(center.X + radius * Math.Cos(angle), center.Y + radius * Math.Sin(angle)));
        }
    }

    private static void AppendEllipseArc(List<CadPoint> loop, CadPoint center, CadVector major, double ratio, double start, double end, bool ccw)
    {
        var sweep = ccw ? NormalizePositive(end - start) : -NormalizePositive(start - end);
        if (Math.Abs(sweep) <= Epsilon) sweep = ccw ? Math.Tau : -Math.Tau;
        var minor = new CadVector(-major.Y * ratio, major.X * ratio);
        var steps = Math.Max(2, (int)Math.Ceiling(Math.Abs(sweep) / MaxArcStepRadians));
        for (var i = 0; i <= steps; i++)
        {
            var angle = start + sweep * i / steps;
            AppendDistinct(loop, new CadPoint(
                center.X + major.X * Math.Cos(angle) + minor.X * Math.Sin(angle),
                center.Y + major.Y * Math.Cos(angle) + minor.Y * Math.Sin(angle)));
        }
    }

    private static IReadOnlyList<CadPoint> FlattenBulgedPolyline(IReadOnlyList<BulgeVertex> vertices, bool closed)
    {
        if (vertices.Count < 2) return [];
        var result = new List<CadPoint> { vertices[0].Point };
        var count = closed ? vertices.Count : vertices.Count - 1;
        for (var i = 0; i < count; i++)
        {
            var start = vertices[i];
            var end = vertices[(i + 1) % vertices.Count].Point;
            if (Math.Abs(start.Bulge) <= Epsilon)
            {
                AppendDistinct(result, end);
                continue;
            }

            var dx = end.X - start.Point.X;
            var dy = end.Y - start.Point.Y;
            var chord = Math.Sqrt(dx * dx + dy * dy);
            if (chord <= Epsilon) continue;
            var sweep = 4.0 * Math.Atan(start.Bulge);
            var tanHalf = Math.Tan(sweep * 0.5);
            if (Math.Abs(tanHalf) <= Epsilon)
            {
                AppendDistinct(result, end);
                continue;
            }
            var midpoint = new CadPoint((start.Point.X + end.X) * 0.5, (start.Point.Y + end.Y) * 0.5);
            var centerOffset = chord / (2.0 * tanHalf);
            var center = new CadPoint(midpoint.X - dy / chord * centerOffset, midpoint.Y + dx / chord * centerOffset);
            var radius = Math.Sqrt(Math.Pow(start.Point.X - center.X, 2) + Math.Pow(start.Point.Y - center.Y, 2));
            var startAngle = Math.Atan2(start.Point.Y - center.Y, start.Point.X - center.X);
            var steps = Math.Max(2, (int)Math.Ceiling(Math.Abs(sweep) / MaxArcStepRadians));
            for (var step = 1; step <= steps; step++)
            {
                if (step == steps) AppendDistinct(result, end);
                else
                {
                    var angle = startAngle + sweep * step / steps;
                    AppendDistinct(result, new CadPoint(center.X + radius * Math.Cos(angle), center.Y + radius * Math.Sin(angle)));
                }
            }
        }
        return result;
    }

    private static IReadOnlyList<CadPoint> NormalizeLoop(IReadOnlyList<CadPoint> source)
    {
        var result = new List<CadPoint>(source.Count);
        foreach (var point in source) AppendDistinct(result, point);
        if (result.Count > 2 && PointsNear(result[0], result[^1])) result.RemoveAt(result.Count - 1);
        return result;
    }

    private static void SkipSourceHandles(IReadOnlyList<DxfPair> record, ref int cursor)
    {
        if (cursor >= record.Count || record[cursor].Code != 97) return;
        var count = ParseInt(record[cursor++].Value, 97);
        for (var i = 0; i < count && cursor < record.Count; i++)
        {
            if (record[cursor].Code is 330 or 331 or 340) cursor++;
            else break;
        }
    }

    private static int FindEdgeEnd(IReadOnlyList<DxfPair> record, int start)
    {
        for (var i = start; i < record.Count; i++)
        {
            if (record[i].Code is 72 or 92 or 97) return i;
        }
        return record.Count;
    }

    private static int ReadNextInt(IReadOnlyList<DxfPair> record, ref int cursor, int code)
    {
        cursor = FindNext(record, cursor, code);
        if (cursor < 0) throw new FormatException($"HATCH path is missing group {code}.");
        return ParseInt(record[cursor++].Value, code);
    }

    private static int FindNext(IReadOnlyList<DxfPair> record, int start, int code)
    {
        for (var i = Math.Max(start, 0); i < record.Count; i++) if (record[i].Code == code) return i;
        return -1;
    }

    private static int FindNextBefore(IReadOnlyList<DxfPair> record, int start, int code, params int[] stopCodes)
    {
        for (var i = Math.Max(start, 0); i < record.Count; i++)
        {
            if (record[i].Code == code) return i;
            if (stopCodes.Contains(record[i].Code)) return -1;
        }
        return -1;
    }

    private static IReadOnlyList<DxfPair> Slice(IReadOnlyList<DxfPair> record, int start, int end)
    {
        var result = new List<DxfPair>(Math.Max(end - start, 0));
        for (var i = start; i < end && i < record.Count; i++) result.Add(record[i]);
        return result;
    }

    private static List<CadPoint> ReadRepeatedPoints(IReadOnlyList<DxfPair> record, int xCode, int yCode)
    {
        var points = new List<CadPoint>();
        for (var i = 0; i < record.Count; i++)
        {
            if (record[i].Code != xCode) continue;
            var x = ParseDouble(record[i].Value, xCode);
            var yIndex = i + 1;
            while (yIndex < record.Count && record[yIndex].Code != yCode && record[yIndex].Code != xCode) yIndex++;
            if (yIndex < record.Count && record[yIndex].Code == yCode)
                points.Add(new CadPoint(x, ParseDouble(record[yIndex].Value, yCode)));
        }
        return points;
    }

    private static CadPoint ReadPoint(IReadOnlyList<DxfPair> record, int xCode, int yCode) =>
        new(RequiredDouble(record, xCode), RequiredDouble(record, yCode));

    private static CadEntityProperties ParseEntityProperties(IReadOnlyList<DxfPair> record, CadDocument document)
    {
        var layer = GetString(record, 8, CadLayer.DefaultLayerName);
        if (string.IsNullOrWhiteSpace(layer)) layer = CadLayer.DefaultLayerName;
        if (!document.TryGetLayer(layer, out _)) document.CreateLayer(new CadLayer(layer));
        var lineType = GetString(record, 6, "ByLayer");
        if (string.IsNullOrWhiteSpace(lineType)) lineType = "ByLayer";
        return new CadEntityProperties(layer, lineType: lineType);
    }

    private static IEnumerable<DxfEntityRecord> EnumerateEntities(string content)
    {
        var pairs = ParsePairs(content);
        var inEntities = false;
        for (var i = 0; i < pairs.Count; i++)
        {
            if (pairs[i].Code == 0 && EqualsToken(pairs[i].Value, "SECTION") && i + 1 < pairs.Count && pairs[i + 1].Code == 2)
            {
                inEntities = EqualsToken(pairs[++i].Value, "ENTITIES");
                continue;
            }
            if (pairs[i].Code == 0 && EqualsToken(pairs[i].Value, "ENDSEC"))
            {
                inEntities = false;
                continue;
            }
            if (!inEntities || pairs[i].Code != 0) continue;
            var type = pairs[i].Value;
            var data = new List<DxfPair>();
            var j = i + 1;
            while (j < pairs.Count && pairs[j].Code != 0) data.Add(pairs[j++]);
            yield return new DxfEntityRecord(type, data);
            i = j - 1;
        }
    }

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

    private static string GetString(IReadOnlyList<DxfPair> record, int code, string fallback) =>
        record.FirstOrDefault(pair => pair.Code == code).Value is { Length: > 0 } value ? value : fallback;

    private static int GetInt(IReadOnlyList<DxfPair> record, int code, int fallback) =>
        int.TryParse(GetString(record, code, string.Empty), NumberStyles.Integer, Invariant, out var value) ? value : fallback;

    private static double GetDouble(IReadOnlyList<DxfPair> record, int code, double fallback) =>
        double.TryParse(GetString(record, code, string.Empty), NumberStyles.Float, Invariant, out var value) && double.IsFinite(value) ? value : fallback;

    private static double RequiredDouble(IReadOnlyList<DxfPair> record, int code)
    {
        var value = GetString(record, code, string.Empty);
        return ParseDouble(value, code);
    }

    private static int ParseInt(string value, int code)
    {
        if (!int.TryParse(value, NumberStyles.Integer, Invariant, out var parsed)) throw new FormatException($"DXF group {code} value '{value}' is not an integer.");
        return parsed;
    }

    private static double ParseDouble(string value, int code)
    {
        if (!double.TryParse(value, NumberStyles.Float, Invariant, out var parsed) || !double.IsFinite(parsed))
            throw new FormatException($"DXF group {code} value '{value}' is not a finite number.");
        return parsed;
    }

    private static double DegreesToRadians(double value) => value * Math.PI / 180.0;
    private static double NormalizePositive(double value)
    {
        value %= Math.Tau;
        if (value < 0) value += Math.Tau;
        return value;
    }

    private static void AppendDistinct(List<CadPoint> points, CadPoint point)
    {
        if (points.Count == 0 || !PointsNear(points[^1], point)) points.Add(point);
    }

    private static bool PointsNear(CadPoint first, CadPoint second) =>
        Math.Abs(first.X - second.X) <= 1e-8 && Math.Abs(first.Y - second.Y) <= 1e-8;

    private static bool EqualsToken(string? value, string expected) => string.Equals(value, expected, StringComparison.OrdinalIgnoreCase);

    private readonly record struct DxfPair(int Code, string Value);
    private readonly record struct DxfEntityRecord(string Type, IReadOnlyList<DxfPair> Data);
    private readonly record struct BulgeVertex(CadPoint Point, double Bulge);
    private sealed record HatchPathResult(IReadOnlyList<IReadOnlyList<CadPoint>> Loops, bool ApproximatedSpline);
}

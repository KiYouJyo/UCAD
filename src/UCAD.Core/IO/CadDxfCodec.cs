using System.Globalization;
using System.Text;
using UCAD.Core.Entities;
using UCAD.Core.Geometry;
using UCAD.Core.Layers;

namespace UCAD.Core.IO;

/// <summary>
/// Lightweight ASCII DXF codec for UCAD's 2D-first exchange path. The codec preserves
/// supported geometry as native DXF entities and reports unsupported records instead of
/// silently approximating them.
/// </summary>
public static class CadDxfCodec
{
    private const string AcadVersion = "AC1032"; // AutoCAD 2018
    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

    public static DxfImportResult Import(string text)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        var pairs = ParsePairs(text);
        var document = new CadDocument();
        var warnings = new List<string>();

        ImportLayerTable(pairs, document, warnings);
        ImportEntities(pairs, document, warnings);
        ApplyCurrentLayer(pairs, document, warnings);

        return new DxfImportResult(document, warnings);
    }

    public static DxfExportResult Export(CadDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        var warnings = new List<string>();
        var sb = new StringBuilder(capacity: Math.Max(4096, document.Entities.Count * 220));

        WritePair(sb, 0, "SECTION");
        WritePair(sb, 2, "HEADER");
        WritePair(sb, 9, "$ACADVER");
        WritePair(sb, 1, AcadVersion);
        WritePair(sb, 9, "$DWGCODEPAGE");
        WritePair(sb, 3, "UTF-8");
        WritePair(sb, 9, "$INSUNITS");
        WritePair(sb, 70, 4); // millimeters
        WritePair(sb, 9, "$CLAYER");
        WritePair(sb, 8, document.CurrentLayerName);
        WritePair(sb, 0, "ENDSEC");

        WriteLayerTable(sb, document);

        WritePair(sb, 0, "SECTION");
        WritePair(sb, 2, "ENTITIES");
        foreach (var entity in document.Entities)
        {
            var properties = document.GetEntityProperties(entity.Id);
            if (!TryWriteEntity(sb, entity, properties, warnings))
            {
                warnings.Add($"DXF export skipped unsupported entity {entity.GetType().Name} ({entity.Id}).");
            }
        }
        WritePair(sb, 0, "ENDSEC");
        WritePair(sb, 0, "EOF");

        return new DxfExportResult(sb.ToString(), warnings);
    }

    private static IReadOnlyList<DxfPair> ParsePairs(string text)
    {
        var result = new List<DxfPair>();
        using var reader = new StringReader(text);
        var lineNumber = 0;
        while (true)
        {
            var codeLine = reader.ReadLine();
            if (codeLine is null) break;
            lineNumber++;
            var valueLine = reader.ReadLine();
            if (valueLine is null)
                throw new FormatException($"DXF group code at line {lineNumber} has no value line.");
            lineNumber++;

            if (!int.TryParse(codeLine.Trim().TrimStart('\uFEFF'), NumberStyles.Integer, Invariant, out var code))
                throw new FormatException($"Invalid DXF group code '{codeLine}' at line {lineNumber - 1}.");
            result.Add(new DxfPair(code, valueLine.Trim()));
        }
        return result;
    }

    private static void ImportLayerTable(IReadOnlyList<DxfPair> pairs, CadDocument document, List<string> warnings)
    {
        for (var i = 0; i < pairs.Count; i++)
        {
            if (pairs[i].Code != 0 || !EqualsToken(pairs[i].Value, "LAYER")) continue;
            var record = ReadRecord(pairs, i + 1, out var nextIndex);
            i = nextIndex - 1;

            var name = GetString(record, 2, CadLayer.DefaultLayerName);
            if (string.IsNullOrWhiteSpace(name) || string.Equals(name, CadLayer.DefaultLayerName, StringComparison.OrdinalIgnoreCase)) continue;
            if (document.TryGetLayer(name, out _)) continue;

            try
            {
                var color = GetTrueColor(record, 420) ?? CadLayer.DefaultColorHex;
                var lineWeight = GetLineWeight(record, 370) ?? 0.25;
                var lineType = GetString(record, 6, "Continuous");
                var flags = GetInt(record, 70, 0);
                var aci = GetInt(record, 62, 7);
                document.CreateLayer(new CadLayer(
                    name,
                    color,
                    lineWeight,
                    lineType,
                    isVisible: aci >= 0,
                    isLocked: (flags & 4) != 0));
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
            {
                warnings.Add($"DXF layer '{name}' could not be imported: {ex.Message}");
            }
        }
    }

    private static void ImportEntities(IReadOnlyList<DxfPair> pairs, CadDocument document, List<string> warnings)
    {
        string? section = null;
        for (var i = 0; i < pairs.Count; i++)
        {
            var pair = pairs[i];
            if (pair.Code == 0 && EqualsToken(pair.Value, "SECTION"))
            {
                if (i + 1 < pairs.Count && pairs[i + 1].Code == 2) section = pairs[++i].Value;
                continue;
            }
            if (pair.Code == 0 && EqualsToken(pair.Value, "ENDSEC"))
            {
                section = null;
                continue;
            }
            if (!EqualsToken(section, "ENTITIES") || pair.Code != 0) continue;

            var type = pair.Value.ToUpperInvariant();
            var record = ReadRecord(pairs, i + 1, out var nextIndex);
            i = nextIndex - 1;

            try
            {
                var entity = type switch
                {
                    "LINE" => ParseLine(record),
                    "LWPOLYLINE" => ParseLightweightPolyline(record),
                    "CIRCLE" => ParseCircle(record),
                    "ARC" => ParseArc(record),
                    "POINT" => ParsePoint(record),
                    "ELLIPSE" => ParseEllipse(record),
                    "SPLINE" => ParseSpline(record),
                    "RAY" => ParseRay(record),
                    "XLINE" => ParseXLine(record),
                    "TEXT" => ParseText(record),
                    "MTEXT" => ParseMText(record),
                    "HATCH" => ParseHatch(record),
                    _ => null
                };

                if (entity is null)
                {
                    warnings.Add($"DXF entity '{type}' is not supported by the exchange foundation and was skipped.");
                    continue;
                }

                if (entity is HatchEntity && GetInt(record, 71, 0) != 0)
                    warnings.Add("DXF HATCH association handles cannot yet be mapped to UCAD entity ids; the hatch was imported as non-associative.");

                var properties = ParseEntityProperties(record, document);
                document.Add(entity, properties);
            }
            catch (Exception ex) when (ex is ArgumentException or FormatException or InvalidOperationException)
            {
                warnings.Add($"DXF entity '{type}' could not be imported: {ex.Message}");
            }
        }
    }

    private static void ApplyCurrentLayer(IReadOnlyList<DxfPair> pairs, CadDocument document, List<string> warnings)
    {
        for (var i = 0; i < pairs.Count - 1; i++)
        {
            if (pairs[i].Code != 9 || !EqualsToken(pairs[i].Value, "$CLAYER")) continue;
            var value = pairs[i + 1].Value;
            if (string.IsNullOrWhiteSpace(value)) return;
            try
            {
                EnsureLayer(document, value);
                document.SetCurrentLayer(value);
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
            {
                warnings.Add($"DXF current layer '{value}' could not be restored: {ex.Message}");
            }
            return;
        }
    }

    private static ICadEntity ParseLine(IReadOnlyList<DxfPair> record) =>
        new LineEntity(
            new CadPoint(RequiredDouble(record, 10), RequiredDouble(record, 20)),
            new CadPoint(RequiredDouble(record, 11), RequiredDouble(record, 21)));

    private static ICadEntity ParseCircle(IReadOnlyList<DxfPair> record) =>
        new CircleEntity(
            new CadPoint(RequiredDouble(record, 10), RequiredDouble(record, 20)),
            RequiredDouble(record, 40));

    private static ICadEntity ParseArc(IReadOnlyList<DxfPair> record)
    {
        var center = new CadPoint(RequiredDouble(record, 10), RequiredDouble(record, 20));
        var radius = RequiredDouble(record, 40);
        var start = DegreesToRadians(RequiredDouble(record, 50));
        var end = DegreesToRadians(RequiredDouble(record, 51));
        var sweep = NormalizePositiveRadians(end - start);
        if (sweep <= 1e-12) sweep = Math.Tau;
        return ArcEntity.Create(center, radius, start, sweep);
    }

    private static ICadEntity ParseLightweightPolyline(IReadOnlyList<DxfPair> record)
    {
        if (record.Any(pair => pair.Code == 42 && Math.Abs(ParseDouble(pair.Value, 42)) > 1e-12))
            throw new FormatException("LWPOLYLINE bulge arcs are not yet supported; the entity was not flattened.");

        var points = new List<CadPoint>();
        double? x = null;
        foreach (var pair in record)
        {
            if (pair.Code == 10) x = ParseDouble(pair.Value, 10);
            else if (pair.Code == 20 && x is not null)
            {
                points.Add(new CadPoint(x.Value, ParseDouble(pair.Value, 20)));
                x = null;
            }
        }
        if (points.Count < 2) throw new FormatException("LWPOLYLINE requires at least two vertices.");
        var closed = (GetInt(record, 70, 0) & 1) != 0;
        return new PolylineEntity(points, closed);
    }

    private static ICadEntity ParsePoint(IReadOnlyList<DxfPair> record) =>
        new PointEntity(new CadPoint(RequiredDouble(record, 10), RequiredDouble(record, 20)));

    private static ICadEntity ParseEllipse(IReadOnlyList<DxfPair> record)
    {
        var center = new CadPoint(RequiredDouble(record, 10), RequiredDouble(record, 20));
        var majorAxis = new CadVector(RequiredDouble(record, 11), RequiredDouble(record, 21));
        var ratio = RequiredDouble(record, 40);
        var start = GetDouble(record, 41, 0);
        var end = GetDouble(record, 42, Math.Tau);
        return new EllipseEntity(center, majorAxis, ratio, start, end);
    }

    private static ICadEntity ParseSpline(IReadOnlyList<DxfPair> record)
    {
        var fitPoints = ReadRepeatedPoints(record, 11, 21);
        if (fitPoints.Count < 2)
        {
            var controlPoints = ReadRepeatedPoints(record, 10, 20);
            if (controlPoints.Count < 2) throw new FormatException("SPLINE requires at least two fit or control points.");
            fitPoints = controlPoints;
        }
        var closed = (GetInt(record, 70, 0) & 1) != 0;
        return new SplineEntity(fitPoints, closed);
    }

    private static ICadEntity ParseRay(IReadOnlyList<DxfPair> record) =>
        new RayEntity(
            new CadPoint(RequiredDouble(record, 10), RequiredDouble(record, 20)),
            new CadVector(RequiredDouble(record, 11), RequiredDouble(record, 21)));

    private static ICadEntity ParseXLine(IReadOnlyList<DxfPair> record) =>
        new XLineEntity(
            new CadPoint(RequiredDouble(record, 10), RequiredDouble(record, 20)),
            new CadVector(RequiredDouble(record, 11), RequiredDouble(record, 21)));

    private static ICadEntity ParseText(IReadOnlyList<DxfPair> record)
    {
        var position = new CadPoint(RequiredDouble(record, 10), RequiredDouble(record, 20));
        var text = GetString(record, 1, string.Empty);
        var height = GetDouble(record, 40, 2.5);
        var rotation = DegreesToRadians(GetDouble(record, 50, 0));
        var style = GetString(record, 7, "Standard");
        return new TextEntity(position, text, height, rotation, style);
    }

    private static ICadEntity ParseMText(IReadOnlyList<DxfPair> record)
    {
        var position = new CadPoint(RequiredDouble(record, 10), RequiredDouble(record, 20));
        var chunks = record.Where(pair => pair.Code is 1 or 3).Select(pair => pair.Value).ToArray();
        if (chunks.Length == 0) throw new FormatException("MTEXT requires text content in group 1/3.");
        var text = DecodeMText(string.Concat(chunks));
        var height = GetDouble(record, 40, 2.5);
        var width = Math.Max(GetDouble(record, 41, 40), 1e-9);
        var rotation = DegreesToRadians(GetDouble(record, 50, 0));
        var style = GetString(record, 7, "Standard");
        return new MTextEntity(position, text, height, width, rotation, style);
    }

    private static ICadEntity ParseHatch(IReadOnlyList<DxfPair> record)
    {
        var loops = ReadHatchPolylineLoops(record);
        if (loops.Count == 0) throw new FormatException("HATCH does not contain a supported closed polyline boundary path.");

        var pattern = GetString(record, 2, "SOLID");
        var scale = Math.Max(GetDouble(record, 41, 1), 1e-9);
        var angle = DegreesToRadians(GetDouble(record, 52, 0));
        var islandDetection = GetInt(record, 75, 0) switch
        {
            1 => HatchIslandDetection.Outer,
            2 => HatchIslandDetection.Ignore,
            _ => HatchIslandDetection.Normal
        };

        return new HatchEntity(
            loops[0],
            pattern,
            scale,
            angle,
            loops.Skip(1),
            associative: false,
            sourceEntityIds: null,
            islandDetection);
    }

    private static List<IReadOnlyList<CadPoint>> ReadHatchPolylineLoops(IReadOnlyList<DxfPair> record)
    {
        var loops = new List<IReadOnlyList<CadPoint>>();
        for (var i = 0; i < record.Count; i++)
        {
            if (record[i].Code != 92) continue;
            var flags = ParseInt(record[i].Value, 92);
            if ((flags & 2) == 0)
                throw new FormatException("HATCH edge-based boundary paths are not yet supported by the UCAD DXF bridge.");

            var cursor = i + 1;
            var hasBulge = false;
            var closed = true;
            while (cursor < record.Count && record[cursor].Code != 93 && record[cursor].Code != 92)
            {
                if (record[cursor].Code == 72) hasBulge = ParseInt(record[cursor].Value, 72) != 0;
                else if (record[cursor].Code == 73) closed = ParseInt(record[cursor].Value, 73) != 0;
                cursor++;
            }
            if (cursor >= record.Count || record[cursor].Code != 93)
                throw new FormatException("HATCH polyline boundary is missing vertex count group 93.");
            if (!closed) throw new FormatException("HATCH polyline boundary is not closed.");

            var vertexCount = ParseInt(record[cursor].Value, 93);
            if (vertexCount < 3) throw new FormatException("HATCH polyline boundary requires at least three vertices.");
            cursor++;

            var points = new List<CadPoint>(vertexCount);
            for (var vertex = 0; vertex < vertexCount; vertex++)
            {
                while (cursor < record.Count && record[cursor].Code != 10 && record[cursor].Code != 92) cursor++;
                if (cursor >= record.Count || record[cursor].Code != 10)
                    throw new FormatException("HATCH polyline boundary is missing a vertex X coordinate.");
                var x = ParseDouble(record[cursor].Value, 10);
                cursor++;

                while (cursor < record.Count && record[cursor].Code != 20 && record[cursor].Code != 92) cursor++;
                if (cursor >= record.Count || record[cursor].Code != 20)
                    throw new FormatException("HATCH polyline boundary is missing a vertex Y coordinate.");
                var y = ParseDouble(record[cursor].Value, 20);
                cursor++;

                if (hasBulge && cursor < record.Count && record[cursor].Code == 42 && Math.Abs(ParseDouble(record[cursor].Value, 42)) > 1e-12)
                    throw new FormatException("HATCH polyline bulge arcs are not yet supported.");
                if (hasBulge && cursor < record.Count && record[cursor].Code == 42) cursor++;
                points.Add(new CadPoint(x, y));
            }

            loops.Add(points.AsReadOnly());
            i = cursor - 1;
        }
        return loops;
    }

    private static List<CadPoint> ReadRepeatedPoints(IReadOnlyList<DxfPair> record, int xCode, int yCode)
    {
        var points = new List<CadPoint>();
        double? x = null;
        foreach (var pair in record)
        {
            if (pair.Code == xCode) x = ParseDouble(pair.Value, xCode);
            else if (pair.Code == yCode && x is not null)
            {
                points.Add(new CadPoint(x.Value, ParseDouble(pair.Value, yCode)));
                x = null;
            }
        }
        return points;
    }

    private static CadEntityProperties ParseEntityProperties(IReadOnlyList<DxfPair> record, CadDocument document)
    {
        var layerName = GetString(record, 8, CadLayer.DefaultLayerName);
        EnsureLayer(document, layerName);
        var color = GetTrueColor(record, 420);
        var lineWeight = GetLineWeight(record, 370);
        var lineType = GetString(record, 6, "ByLayer");
        return new CadEntityProperties(layerName, color, lineWeight, lineType);
    }

    private static void EnsureLayer(CadDocument document, string layerName)
    {
        if (document.TryGetLayer(layerName, out _)) return;
        document.CreateLayer(new CadLayer(layerName));
    }

    private static void WriteLayerTable(StringBuilder sb, CadDocument document)
    {
        WritePair(sb, 0, "SECTION");
        WritePair(sb, 2, "TABLES");
        WritePair(sb, 0, "TABLE");
        WritePair(sb, 2, "LAYER");
        WritePair(sb, 70, document.Layers.Count);
        foreach (var layer in document.Layers)
        {
            WritePair(sb, 0, "LAYER");
            WritePair(sb, 2, layer.Name);
            WritePair(sb, 70, layer.IsLocked ? 4 : 0);
            WritePair(sb, 62, layer.IsVisible ? 7 : -7);
            WritePair(sb, 420, HexToTrueColor(layer.ColorHex));
            WritePair(sb, 6, layer.LineType);
            WritePair(sb, 370, ToDxfLineWeight(layer.LineWeight));
        }
        WritePair(sb, 0, "ENDTAB");
        WritePair(sb, 0, "ENDSEC");
    }

    private static bool TryWriteEntity(StringBuilder sb, ICadEntity entity, CadEntityProperties properties, List<string> warnings)
    {
        switch (entity)
        {
            case LineEntity line:
                WritePair(sb, 0, "LINE");
                WriteEntityProperties(sb, properties);
                WritePoint(sb, 10, 20, line.Start);
                WritePoint(sb, 11, 21, line.End);
                return true;

            case PolylineEntity polyline:
                WritePair(sb, 0, "LWPOLYLINE");
                WriteEntityProperties(sb, properties);
                WritePair(sb, 90, polyline.Points.Count);
                WritePair(sb, 70, polyline.Closed ? 1 : 0);
                foreach (var point in polyline.Points) WritePoint(sb, 10, 20, point);
                return true;

            case CircleEntity circle:
                WritePair(sb, 0, "CIRCLE");
                WriteEntityProperties(sb, properties);
                WritePoint(sb, 10, 20, circle.Center);
                WritePair(sb, 40, circle.Radius);
                return true;

            case ArcEntity arc:
                WritePair(sb, 0, "ARC");
                WriteEntityProperties(sb, properties);
                WritePoint(sb, 10, 20, arc.Center);
                WritePair(sb, 40, arc.Radius);
                if (arc.SweepAngleRadians >= 0)
                {
                    WritePair(sb, 50, NormalizeDegrees(RadiansToDegrees(arc.StartAngleRadians)));
                    WritePair(sb, 51, NormalizeDegrees(RadiansToDegrees(arc.StartAngleRadians + arc.SweepAngleRadians)));
                }
                else
                {
                    WritePair(sb, 50, NormalizeDegrees(RadiansToDegrees(arc.StartAngleRadians + arc.SweepAngleRadians)));
                    WritePair(sb, 51, NormalizeDegrees(RadiansToDegrees(arc.StartAngleRadians)));
                }
                return true;

            case PointEntity point:
                WritePair(sb, 0, "POINT");
                WriteEntityProperties(sb, properties);
                WritePoint(sb, 10, 20, point.Position);
                return true;

            case EllipseEntity ellipse:
                WritePair(sb, 0, "ELLIPSE");
                WriteEntityProperties(sb, properties);
                WritePoint(sb, 10, 20, ellipse.Center);
                WriteVector(sb, 11, 21, ellipse.MajorAxis);
                WritePair(sb, 40, ellipse.Ratio);
                WritePair(sb, 41, ellipse.StartParameter);
                WritePair(sb, 42, ellipse.StartParameter + ellipse.SweepParameter);
                return true;

            case SplineEntity spline:
                WritePair(sb, 0, "SPLINE");
                WriteEntityProperties(sb, properties);
                WritePair(sb, 70, spline.Closed ? 1 : 0);
                WritePair(sb, 71, Math.Min(3, spline.FitPoints.Count - 1));
                WritePair(sb, 72, 0);
                WritePair(sb, 73, 0);
                WritePair(sb, 74, spline.FitPoints.Count);
                foreach (var fit in spline.FitPoints) WritePoint(sb, 11, 21, fit);
                return true;

            case RayEntity ray:
                WritePair(sb, 0, "RAY");
                WriteEntityProperties(sb, properties);
                WritePoint(sb, 10, 20, ray.Origin);
                WriteVector(sb, 11, 21, ray.Direction);
                return true;

            case XLineEntity xline:
                WritePair(sb, 0, "XLINE");
                WriteEntityProperties(sb, properties);
                WritePoint(sb, 10, 20, xline.Point);
                WriteVector(sb, 11, 21, xline.Direction);
                return true;

            case TextEntity text:
                WritePair(sb, 0, "TEXT");
                WriteEntityProperties(sb, properties);
                WritePoint(sb, 10, 20, text.Position);
                WritePair(sb, 40, text.Height);
                WritePair(sb, 1, text.Text);
                WritePair(sb, 7, text.StyleName);
                WritePair(sb, 50, NormalizeDegrees(RadiansToDegrees(text.RotationRadians)));
                return true;

            case MTextEntity mtext:
                WritePair(sb, 0, "MTEXT");
                WriteEntityProperties(sb, properties);
                WritePoint(sb, 10, 20, mtext.Position);
                WritePair(sb, 40, mtext.TextHeight);
                WritePair(sb, 41, mtext.Width);
                WritePair(sb, 71, 1); // top-left attachment
                WritePair(sb, 7, mtext.StyleName);
                WritePair(sb, 50, NormalizeDegrees(RadiansToDegrees(mtext.RotationRadians)));
                WriteMTextContent(sb, mtext.Text);
                return true;

            case HatchEntity hatch:
                WritePair(sb, 0, "HATCH");
                WriteEntityProperties(sb, properties);
                WritePoint(sb, 10, 20, new CadPoint(0, 0));
                WritePair(sb, 30, 0);
                WritePair(sb, 210, 0);
                WritePair(sb, 220, 0);
                WritePair(sb, 230, 1);
                WritePair(sb, 2, hatch.Pattern);
                var isSolid = string.Equals(hatch.Pattern, "SOLID", StringComparison.OrdinalIgnoreCase);
                WritePair(sb, 70, isSolid ? 1 : 0);
                WritePair(sb, 71, 0); // source GUIDs cannot map to DXF handles yet
                var loops = new List<IReadOnlyList<CadPoint>> { hatch.Boundary };
                loops.AddRange(hatch.EffectiveIslandLoops);
                WritePair(sb, 91, loops.Count);
                foreach (var loop in loops) WriteHatchPolylineLoop(sb, loop);
                WritePair(sb, 75, hatch.IslandDetection switch
                {
                    HatchIslandDetection.Outer => 1,
                    HatchIslandDetection.Ignore => 2,
                    _ => 0
                });
                WritePair(sb, 76, 1); // predefined pattern type
                if (!isSolid)
                {
                    WritePair(sb, 52, NormalizeDegrees(RadiansToDegrees(hatch.PatternAngleRadians)));
                    WritePair(sb, 41, hatch.PatternScale);
                    WritePair(sb, 77, 0);
                    WritePair(sb, 78, 0);
                }
                WritePair(sb, 98, 0);
                if (hatch.Associative)
                    warnings.Add($"DXF HATCH {hatch.Id} was exported non-associative because UCAD GUIDs cannot yet be mapped to stable DXF handles.");
                return true;

            default:
                return false;
        }
    }

    private static void WriteMTextContent(StringBuilder sb, string text)
    {
        var encoded = text
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace("\r", "\n", StringComparison.Ordinal)
            .Replace("\n", "\\P", StringComparison.Ordinal);
        const int chunkSize = 250;
        var offset = 0;
        while (encoded.Length - offset > chunkSize)
        {
            WritePair(sb, 3, encoded.Substring(offset, chunkSize));
            offset += chunkSize;
        }
        WritePair(sb, 1, encoded[offset..]);
    }

    private static string DecodeMText(string text) =>
        text.Replace("\\P", "\n", StringComparison.OrdinalIgnoreCase);

    private static void WriteHatchPolylineLoop(StringBuilder sb, IReadOnlyList<CadPoint> loop)
    {
        WritePair(sb, 92, 2); // polyline boundary path
        WritePair(sb, 72, 0); // no bulges
        WritePair(sb, 73, 1); // closed
        WritePair(sb, 93, loop.Count);
        foreach (var point in loop) WritePoint(sb, 10, 20, point);
        WritePair(sb, 97, 0); // no source boundary handles
    }

    private static void WriteEntityProperties(StringBuilder sb, CadEntityProperties properties)
    {
        WritePair(sb, 8, properties.LayerName);
        if (properties.ColorHex is not null) WritePair(sb, 420, HexToTrueColor(properties.ColorHex));
        if (properties.LineWeight is not null) WritePair(sb, 370, ToDxfLineWeight(properties.LineWeight.Value));
        if (!string.Equals(properties.LineType, "ByLayer", StringComparison.OrdinalIgnoreCase)) WritePair(sb, 6, properties.LineType);
    }

    private static void WritePoint(StringBuilder sb, int xCode, int yCode, CadPoint point)
    {
        WritePair(sb, xCode, point.X);
        WritePair(sb, yCode, point.Y);
    }

    private static void WriteVector(StringBuilder sb, int xCode, int yCode, CadVector vector)
    {
        WritePair(sb, xCode, vector.X);
        WritePair(sb, yCode, vector.Y);
    }

    private static IReadOnlyList<DxfPair> ReadRecord(IReadOnlyList<DxfPair> pairs, int start, out int nextIndex)
    {
        var record = new List<DxfPair>();
        var i = start;
        while (i < pairs.Count && pairs[i].Code != 0)
        {
            record.Add(pairs[i]);
            i++;
        }
        nextIndex = i;
        return record;
    }

    private static double RequiredDouble(IReadOnlyList<DxfPair> record, int code)
    {
        var pair = record.FirstOrDefault(candidate => candidate.Code == code);
        if (pair is null) throw new FormatException($"Required DXF group {code} is missing.");
        return ParseDouble(pair.Value, code);
    }

    private static double GetDouble(IReadOnlyList<DxfPair> record, int code, double fallback)
    {
        var pair = record.FirstOrDefault(candidate => candidate.Code == code);
        return pair is null ? fallback : ParseDouble(pair.Value, code);
    }

    private static int GetInt(IReadOnlyList<DxfPair> record, int code, int fallback)
    {
        var pair = record.FirstOrDefault(candidate => candidate.Code == code);
        if (pair is null) return fallback;
        return int.TryParse(pair.Value, NumberStyles.Integer, Invariant, out var value) ? value : fallback;
    }

    private static int ParseInt(string value, int code)
    {
        if (!int.TryParse(value, NumberStyles.Integer, Invariant, out var parsed))
            throw new FormatException($"DXF group {code} has invalid integer value '{value}'.");
        return parsed;
    }

    private static string GetString(IReadOnlyList<DxfPair> record, int code, string fallback) =>
        record.FirstOrDefault(candidate => candidate.Code == code)?.Value ?? fallback;

    private static double ParseDouble(string value, int code)
    {
        if (!double.TryParse(value, NumberStyles.Float, Invariant, out var parsed) || !double.IsFinite(parsed))
            throw new FormatException($"DXF group {code} has invalid numeric value '{value}'.");
        return parsed;
    }

    private static string? GetTrueColor(IReadOnlyList<DxfPair> record, int code)
    {
        var value = GetInt(record, code, -1);
        if (value < 0) return null;
        return $"#{(value & 0xFFFFFF):X6}";
    }

    private static double? GetLineWeight(IReadOnlyList<DxfPair> record, int code)
    {
        var value = GetInt(record, code, -1);
        return value > 0 ? value / 100.0 : null;
    }

    private static int HexToTrueColor(string colorHex) =>
        int.Parse(colorHex.AsSpan(1), NumberStyles.HexNumber, Invariant);

    private static int ToDxfLineWeight(double millimeters) =>
        Math.Max(1, (int)Math.Round(millimeters * 100, MidpointRounding.AwayFromZero));

    private static double DegreesToRadians(double degrees) => degrees * Math.PI / 180.0;
    private static double RadiansToDegrees(double radians) => radians * 180.0 / Math.PI;

    private static double NormalizePositiveRadians(double radians)
    {
        var normalized = radians % Math.Tau;
        return normalized < 0 ? normalized + Math.Tau : normalized;
    }

    private static double NormalizeDegrees(double degrees)
    {
        var normalized = degrees % 360.0;
        return normalized < 0 ? normalized + 360.0 : normalized;
    }

    private static bool EqualsToken(string? left, string right) =>
        string.Equals(left, right, StringComparison.OrdinalIgnoreCase);

    private static void WritePair(StringBuilder sb, int code, object value)
    {
        sb.AppendLine(code.ToString(Invariant));
        sb.AppendLine(value switch
        {
            double number => number.ToString("0.###############", Invariant),
            float number => number.ToString("0.###############", Invariant),
            IFormattable formattable => formattable.ToString(null, Invariant),
            _ => value.ToString() ?? string.Empty
        });
    }

    private sealed record DxfPair(int Code, string Value);
}

public sealed record DxfImportResult(CadDocument Document, IReadOnlyList<string> Warnings)
{
    public bool HasWarnings => Warnings.Count > 0;
}

public sealed record DxfExportResult(string Content, IReadOnlyList<string> Warnings)
{
    public bool HasWarnings => Warnings.Count > 0;
}

using System.Globalization;
using UCAD.Core.Entities;
using UCAD.Core.Layers;

namespace UCAD.Core.IO;

/// <summary>
/// Re-attaches original DXF ENTITIES ordering to the display entities created by the
/// baseline, advanced and display-fallback passes. The import architecture intentionally
/// performs semantic repair in several passes; without this metadata a later-created
/// WIPEOUT or annotation could be painted at the wrong z-position even though its geometry is correct.
/// </summary>
internal static class CadDxfSourceOrderRepair
{
    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;
    private static readonly HashSet<string> AdvancedTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "DIMENSION", "LEADER", "INSERT", "ATTRIB", "SEQEND"
    };

    public static void Apply(string content, CadDocument document)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(document);

        var records = ReadEntityRecords(content);
        if (records.Count == 0 || document.Entities.Count == 0) return;

        var linkedAnnotationHandles = records
            .Where(record => EqualsToken(record.Type, "LEADER"))
            .Select(record => GetString(record.Data, 340, string.Empty))
            .Where(handle => !string.IsNullOrWhiteSpace(handle))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        AssignBaselineRecords(records, document, linkedAnnotationHandles);
        AssignAdvancedRecords(records, document);
        AssignPolylineFallbacks(records, document);
        AssignFaceFallbacks(records, document);
        AssignHatchFallbacks(records, document);
    }

    private static void AssignBaselineRecords(
        IReadOnlyList<DxfRecord> records,
        CadDocument document,
        IReadOnlySet<string> linkedAnnotationHandles)
    {
        var searchStart = 0;
        foreach (var record in records)
        {
            if (!TryGetBaselineTargetType(record, linkedAnnotationHandles, out var targetType)) continue;
            var match = FindNextUnordered(document, targetType, searchStart);
            if (match < 0) continue;
            SetOrder(document, document.Entities[match], record);
            searchStart = match + 1;
        }
    }

    private static void AssignAdvancedRecords(IReadOnlyList<DxfRecord> records, CadDocument document)
    {
        foreach (var record in records.Where(record => EqualsToken(record.Type, "DIMENSION")))
        {
            var rawType = GetInt(record.Data, 70, 0) & 0x0F;
            Type? targetType = rawType switch
            {
                0 or 1 => typeof(LinearDimensionEntity),
                2 or 5 => typeof(AngularDimensionEntity),
                3 or 4 => typeof(RadialDimensionEntity),
                _ => null
            };
            if (targetType is null) continue;
            var match = FindNextUnordered(document, targetType, 0);
            if (match >= 0) SetOrder(document, document.Entities[match], record);
        }

        foreach (var record in records.Where(record => EqualsToken(record.Type, "LEADER")))
        {
            var layer = GetString(record.Data, 8, CadLayer.DefaultLayerName);
            var leader = document.Entities
                .OfType<LeaderEntity>()
                .FirstOrDefault(entity =>
                    document.GetEntityProperties(entity.Id).SourceOrder is null &&
                    string.Equals(document.GetEntityProperties(entity.Id).LayerName, layer, StringComparison.OrdinalIgnoreCase));
            if (leader is not null)
            {
                SetOrder(document, leader, record);
                continue;
            }

            // Annotation-less LEADER is intentionally downgraded to an open polyline.
            var polyline = document.Entities
                .OfType<PolylineEntity>()
                .FirstOrDefault(entity =>
                    !entity.Closed &&
                    document.GetEntityProperties(entity.Id).SourceOrder is null &&
                    string.Equals(document.GetEntityProperties(entity.Id).LayerName, layer, StringComparison.OrdinalIgnoreCase));
            if (polyline is not null) SetOrder(document, polyline, record);
        }

        foreach (var record in records.Where(record => EqualsToken(record.Type, "INSERT")))
        {
            var name = GetString(record.Data, 2, string.Empty);
            var hasPoint = TryGetPoint(record.Data, 10, 20, out var insertion);
            var reference = document.Entities
                .OfType<BlockReferenceEntity>()
                .Where(entity => document.GetEntityProperties(entity.Id).SourceOrder is null)
                .Where(entity => string.IsNullOrWhiteSpace(name) || string.Equals(entity.DefinitionName, name, StringComparison.OrdinalIgnoreCase))
                .OrderBy(entity => hasPoint
                    ? DistanceSquared(entity.InsertionPoint.X, entity.InsertionPoint.Y, insertion.X, insertion.Y)
                    : 0.0)
                .FirstOrDefault();
            if (reference is not null) SetOrder(document, reference, record);
        }
    }

    private static void AssignPolylineFallbacks(IReadOnlyList<DxfRecord> records, CadDocument document)
    {
        foreach (var record in records.Where(IsPolylineFallbackRecord))
        {
            var candidate = document.Entities
                .OfType<PolylineEntity>()
                .FirstOrDefault(entity => document.GetEntityProperties(entity.Id).SourceOrder is null);
            if (candidate is not null) SetOrder(document, candidate, record);
        }
    }

    private static void AssignFaceFallbacks(IReadOnlyList<DxfRecord> records, CadDocument document)
    {
        // SOLID/TRACE are recovered by the first display-fallback pass before edge HATCH
        // recovery runs. Match them first so the remaining unordered HatchEntity queue can
        // be paired safely with source HATCH records in source order.
        foreach (var record in records.Where(record => EqualsToken(record.Type, "SOLID") || EqualsToken(record.Type, "TRACE")))
        {
            var layer = GetString(record.Data, 8, CadLayer.DefaultLayerName);
            var sourcePoint = TryGetPoint(record.Data, 10, 20, out var point) ? point : (double X, double Y)?null;
            var candidate = document.Entities
                .OfType<HatchEntity>()
                .Where(entity => document.GetEntityProperties(entity.Id).SourceOrder is null)
                .Where(entity => string.Equals(document.GetEntityProperties(entity.Id).LayerName, layer, StringComparison.OrdinalIgnoreCase))
                .OrderBy(entity => sourcePoint is null || entity.Boundary.Count == 0
                    ? 0.0
                    : DistanceSquared(entity.Boundary[0].X, entity.Boundary[0].Y, sourcePoint.Value.X, sourcePoint.Value.Y))
                .FirstOrDefault();
            if (candidate is not null) SetOrder(document, candidate, record);
        }

        foreach (var record in records.Where(record => EqualsToken(record.Type, "3DFACE")))
        {
            var candidate = document.Entities
                .OfType<PolylineEntity>()
                .FirstOrDefault(entity => document.GetEntityProperties(entity.Id).SourceOrder is null);
            if (candidate is not null) SetOrder(document, candidate, record);
        }
    }

    private static void AssignHatchFallbacks(IReadOnlyList<DxfRecord> records, CadDocument document)
    {
        foreach (var record in records.Where(IsHatchFallbackRecord))
        {
            var layer = GetString(record.Data, 8, CadLayer.DefaultLayerName);
            var candidate = document.Entities
                .OfType<HatchEntity>()
                .FirstOrDefault(entity =>
                    document.GetEntityProperties(entity.Id).SourceOrder is null &&
                    string.Equals(document.GetEntityProperties(entity.Id).LayerName, layer, StringComparison.OrdinalIgnoreCase));
            if (candidate is not null) SetOrder(document, candidate, record);
        }
    }

    private static bool TryGetBaselineTargetType(
        DxfRecord record,
        IReadOnlySet<string> linkedAnnotationHandles,
        out Type targetType)
    {
        targetType = typeof(ICadEntity);
        if (AdvancedTypes.Contains(record.Type)) return false;
        var handle = GetString(record.Data, 5, string.Empty);
        if ((EqualsToken(record.Type, "TEXT") || EqualsToken(record.Type, "MTEXT")) && linkedAnnotationHandles.Contains(handle)) return false;

        if (EqualsToken(record.Type, "LINE")) targetType = typeof(LineEntity);
        else if (EqualsToken(record.Type, "LWPOLYLINE"))
        {
            if (record.Data.Any(pair => pair.Code == 42 && Math.Abs(ParseDoubleOrZero(pair.Value)) > 1e-12)) return false;
            targetType = typeof(PolylineEntity);
        }
        else if (EqualsToken(record.Type, "CIRCLE")) targetType = typeof(CircleEntity);
        else if (EqualsToken(record.Type, "ARC")) targetType = typeof(ArcEntity);
        else if (EqualsToken(record.Type, "POINT")) targetType = typeof(PointEntity);
        else if (EqualsToken(record.Type, "ELLIPSE")) targetType = typeof(EllipseEntity);
        else if (EqualsToken(record.Type, "SPLINE")) targetType = typeof(SplineEntity);
        else if (EqualsToken(record.Type, "RAY")) targetType = typeof(RayEntity);
        else if (EqualsToken(record.Type, "XLINE")) targetType = typeof(XLineEntity);
        else if (EqualsToken(record.Type, "TEXT")) targetType = typeof(TextEntity);
        else if (EqualsToken(record.Type, "MTEXT")) targetType = typeof(MTextEntity);
        else if (EqualsToken(record.Type, "HATCH"))
        {
            if (IsHatchFallbackRecord(record)) return false;
            targetType = typeof(HatchEntity);
        }
        else return false;
        return true;
    }

    private static bool IsPolylineFallbackRecord(DxfRecord record) =>
        EqualsToken(record.Type, "POLYLINE") ||
        (EqualsToken(record.Type, "LWPOLYLINE") && record.Data.Any(pair => pair.Code == 42 && Math.Abs(ParseDoubleOrZero(pair.Value)) > 1e-12));

    private static bool IsHatchFallbackRecord(DxfRecord record)
    {
        if (!EqualsToken(record.Type, "HATCH")) return false;
        for (var i = 0; i < record.Data.Count; i++)
        {
            if (record.Data[i].Code != 92) continue;
            if (!int.TryParse(record.Data[i].Value, NumberStyles.Integer, Invariant, out var flags)) return true;
            if ((flags & 2) == 0) return true;
            var cursor = i + 1;
            while (cursor < record.Data.Count && record.Data[cursor].Code != 92 && record.Data[cursor].Code != 75)
            {
                if (record.Data[cursor].Code == 42 && Math.Abs(ParseDoubleOrZero(record.Data[cursor].Value)) > 1e-12) return true;
                cursor++;
            }
        }
        return false;
    }

    private static int FindNextUnordered(CadDocument document, Type targetType, int start)
    {
        for (var index = Math.Max(0, start); index < document.Entities.Count; index++)
        {
            var entity = document.Entities[index];
            if (document.GetEntityProperties(entity.Id).SourceOrder is not null) continue;
            if (targetType.IsInstanceOfType(entity)) return index;
        }
        return -1;
    }

    private static void SetOrder(CadDocument document, ICadEntity entity, DxfRecord source)
    {
        var handle = GetString(source.Data, 5, string.Empty);
        document.SetEntityProperties([entity.Id], properties => properties with
        {
            SourceOrder = source.Order,
            SourceHandle = string.IsNullOrWhiteSpace(handle) ? properties.SourceHandle : handle
        });
    }

    private static IReadOnlyList<DxfRecord> ReadEntityRecords(string content)
    {
        var pairs = ParsePairs(content);
        var records = new List<DxfRecord>();
        var inEntities = false;
        var order = 0;
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

            var data = new List<DxfPair>();
            var cursor = i + 1;
            while (cursor < pairs.Count && pairs[cursor].Code != 0) data.Add(pairs[cursor++]);
            records.Add(new DxfRecord(pair.Value, data, order++));
            i = cursor - 1;
        }
        return records;
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
        int.TryParse(GetString(record, code, string.Empty), NumberStyles.Integer, Invariant, out var parsed) ? parsed : fallback;

    private static bool TryGetPoint(IReadOnlyList<DxfPair> record, int xCode, int yCode, out (double X, double Y) point)
    {
        point = default;
        var xText = GetString(record, xCode, string.Empty);
        var yText = GetString(record, yCode, string.Empty);
        if (!double.TryParse(xText, NumberStyles.Float, Invariant, out var x) || !double.TryParse(yText, NumberStyles.Float, Invariant, out var y)) return false;
        if (!double.IsFinite(x) || !double.IsFinite(y)) return false;
        point = (x, y);
        return true;
    }

    private static double ParseDoubleOrZero(string value) =>
        double.TryParse(value, NumberStyles.Float, Invariant, out var parsed) && double.IsFinite(parsed) ? parsed : 0.0;

    private static double DistanceSquared(double ax, double ay, double bx, double by)
    {
        var dx = ax - bx;
        var dy = ay - by;
        return dx * dx + dy * dy;
    }

    private static bool EqualsToken(string? value, string expected) => string.Equals(value, expected, StringComparison.OrdinalIgnoreCase);

    private readonly record struct DxfPair(int Code, string Value);
    private sealed record DxfRecord(string Type, IReadOnlyList<DxfPair> Data, int Order);
}

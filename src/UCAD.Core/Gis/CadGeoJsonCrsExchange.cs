using System.Text.Json;
using System.Text.Json.Nodes;

namespace UCAD.Core.Gis;

public static class CadGeoJsonCrsExchange
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public static CadGeoJsonExportResult ExportWgs84(
        CadDocument document,
        CadCoordinateReferenceSystem sourceCrs,
        CadGeoJsonExportOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(document);
        if (sourceCrs == CadCoordinateReferenceSystem.LocalPlanar)
        {
            throw new NotSupportedException(
                "Standards-safe GeoJSON export requires a known geographic/projected CRS. " +
                "LocalPlanar coordinates cannot be labeled as WGS84 without an explicit projection transform.");
        }

        var raw = CadGeoJsonCodec.Export(document, options);
        if (sourceCrs == CadCoordinateReferenceSystem.Wgs84LongitudeLatitude) return raw;
        var root = JsonNode.Parse(raw.Json) ?? throw new FormatException("GeoJSON export produced an empty document.");
        TransformFeatureCollectionCoordinates(
            root,
            point => CadCoordinateTransform.Transform(
                point,
                CadCoordinateReferenceSystem.WebMercator,
                CadCoordinateReferenceSystem.Wgs84LongitudeLatitude));
        return new CadGeoJsonExportResult(root.ToJsonString(JsonOptions), raw.Warnings);
    }

    public static CadGeoJsonImportResult ImportWgs84(
        string json,
        CadCoordinateReferenceSystem targetCrs)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);
        if (targetCrs == CadCoordinateReferenceSystem.LocalPlanar)
        {
            throw new NotSupportedException(
                "GeoJSON coordinates are interpreted as WGS84. Import into LocalPlanar requires an explicit project CRS transform.");
        }
        if (targetCrs == CadCoordinateReferenceSystem.Wgs84LongitudeLatitude)
            return CadGeoJsonCodec.Import(json);

        var root = JsonNode.Parse(json) ?? throw new FormatException("GeoJSON document is empty.");
        TransformFeatureCollectionCoordinates(
            root,
            point => CadCoordinateTransform.Transform(
                point,
                CadCoordinateReferenceSystem.Wgs84LongitudeLatitude,
                CadCoordinateReferenceSystem.WebMercator));
        return CadGeoJsonCodec.Import(root.ToJsonString(JsonOptions));
    }

    private static void TransformFeatureCollectionCoordinates(JsonNode root, Func<Core.Geometry.CadPoint, Core.Geometry.CadPoint> transform)
    {
        if (root is not JsonObject objectRoot ||
            objectRoot["type"]?.GetValue<string>() != "FeatureCollection" ||
            objectRoot["features"] is not JsonArray features)
        {
            throw new FormatException("GeoJSON root must be a FeatureCollection.");
        }

        foreach (var featureNode in features)
        {
            if (featureNode is not JsonObject feature || feature["geometry"] is not JsonObject geometry) continue;
            var geometryType = geometry["type"]?.GetValue<string>();
            if (geometry["coordinates"] is not JsonNode coordinates || string.IsNullOrWhiteSpace(geometryType)) continue;
            geometry["coordinates"] = TransformCoordinates(geometryType, coordinates, transform);
        }
    }

    private static JsonNode TransformCoordinates(
        string geometryType,
        JsonNode coordinates,
        Func<Core.Geometry.CadPoint, Core.Geometry.CadPoint> transform) => geometryType switch
    {
        "Point" => TransformPoint(coordinates, transform),
        "MultiPoint" or "LineString" => TransformPointArray(coordinates, transform),
        "MultiLineString" or "Polygon" => TransformNestedPointArray(coordinates, transform),
        "MultiPolygon" => TransformTripleNestedPointArray(coordinates, transform),
        _ => throw new NotSupportedException($"GeoJSON geometry type '{geometryType}' is not supported by CRS transformation.")
    };

    private static JsonNode TransformPoint(JsonNode node, Func<Core.Geometry.CadPoint, Core.Geometry.CadPoint> transform)
    {
        if (node is not JsonArray array || array.Count < 2) throw new FormatException("GeoJSON coordinate requires X and Y values.");
        var x = array[0]?.GetValue<double>() ?? throw new FormatException("GeoJSON coordinate X is missing.");
        var y = array[1]?.GetValue<double>() ?? throw new FormatException("GeoJSON coordinate Y is missing.");
        var transformed = transform(new Core.Geometry.CadPoint(x, y));
        var result = new JsonArray(transformed.X, transformed.Y);
        for (var index = 2; index < array.Count; index++) result.Add(array[index]?.DeepClone());
        return result;
    }

    private static JsonNode TransformPointArray(JsonNode node, Func<Core.Geometry.CadPoint, Core.Geometry.CadPoint> transform)
    {
        if (node is not JsonArray array) throw new FormatException("GeoJSON coordinates must be an array.");
        return new JsonArray(array.Select(item => item is null ? null : TransformPoint(item, transform)).ToArray());
    }

    private static JsonNode TransformNestedPointArray(JsonNode node, Func<Core.Geometry.CadPoint, Core.Geometry.CadPoint> transform)
    {
        if (node is not JsonArray array) throw new FormatException("GeoJSON coordinates must be a nested array.");
        return new JsonArray(array.Select(item => item is null ? null : TransformPointArray(item, transform)).ToArray());
    }

    private static JsonNode TransformTripleNestedPointArray(JsonNode node, Func<Core.Geometry.CadPoint, Core.Geometry.CadPoint> transform)
    {
        if (node is not JsonArray array) throw new FormatException("GeoJSON coordinates must be a triple-nested array.");
        return new JsonArray(array.Select(item => item is null ? null : TransformNestedPointArray(item, transform)).ToArray());
    }
}

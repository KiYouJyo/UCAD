using System.Text.Json;
using System.Text.Json.Nodes;
using UCAD.Core.Entities;
using UCAD.Core.Geometry;

namespace UCAD.Core.Gis;

public sealed record CadGeoJsonExportOptions(
    int CurveSegments = 96,
    bool IncludeHatches = true)
{
    public CadGeoJsonExportOptions Validate()
    {
        if (CurveSegments is < 8 or > 4096) throw new ArgumentOutOfRangeException(nameof(CurveSegments));
        return this;
    }
}

public sealed record CadGeoJsonExportResult(string Json, IReadOnlyList<string> Warnings);

public sealed record CadGeoJsonImportedEntity(
    ICadEntity Entity,
    IReadOnlyDictionary<string, string?> Properties,
    string? SuggestedLayerName);

public sealed record CadGeoJsonImportResult(
    IReadOnlyList<CadGeoJsonImportedEntity> Entities,
    IReadOnlyList<string> Warnings);

/// <summary>
/// Coordinate-neutral GeoJSON exchange. Coordinates are passed through as UCAD planar
/// drawing coordinates; CRS transformation is intentionally a separate adapter layer.
/// </summary>
public static class CadGeoJsonCodec
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public static CadGeoJsonExportResult Export(
        CadDocument document,
        CadGeoJsonExportOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(document);
        options = (options ?? new CadGeoJsonExportOptions()).Validate();
        var warnings = new List<string>();
        var features = new JsonArray();

        foreach (var entity in document.VisibleEntities)
        {
            var properties = document.GetEntityProperties(entity.Id);
            var geometry = ToGeometry(entity, options, warnings);
            if (geometry is null) continue;
            var layer = document.GetLayer(properties.LayerName);
            var featureProperties = new JsonObject
            {
                ["ucadEntityType"] = entity.GetType().Name,
                ["layer"] = properties.LayerName,
                ["color"] = properties.ColorHex ?? layer.ColorHex,
                ["lineWeight"] = properties.LineWeight ?? layer.LineWeight,
                ["lineType"] = properties.LineType
            };
            features.Add(new JsonObject
            {
                ["type"] = "Feature",
                ["geometry"] = geometry,
                ["properties"] = featureProperties
            });
        }

        var root = new JsonObject
        {
            ["type"] = "FeatureCollection",
            ["features"] = features
        };
        return new CadGeoJsonExportResult(root.ToJsonString(JsonOptions), warnings.AsReadOnly());
    }

    public static CadGeoJsonImportResult Import(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);
        var warnings = new List<string>();
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        if (!root.TryGetProperty("type", out var type) || !string.Equals(type.GetString(), "FeatureCollection", StringComparison.Ordinal))
            throw new FormatException("GeoJSON root must be a FeatureCollection.");
        if (!root.TryGetProperty("features", out var features) || features.ValueKind != JsonValueKind.Array)
            throw new FormatException("GeoJSON FeatureCollection requires a features array.");

        var imported = new List<CadGeoJsonImportedEntity>();
        var featureIndex = 0;
        foreach (var feature in features.EnumerateArray())
        {
            try
            {
                ImportFeature(feature, imported, warnings, featureIndex);
            }
            catch (Exception ex) when (ex is FormatException or ArgumentException or InvalidOperationException)
            {
                warnings.Add($"Feature {featureIndex} was skipped: {ex.Message}");
            }
            featureIndex++;
        }
        return new CadGeoJsonImportResult(imported.AsReadOnly(), warnings.AsReadOnly());
    }

    private static JsonObject? ToGeometry(
        ICadEntity entity,
        CadGeoJsonExportOptions options,
        List<string> warnings)
    {
        switch (entity)
        {
            case PointEntity point:
                return PointGeometry(point.Position);
            case LineEntity line:
                return LineStringGeometry([line.Start, line.End]);
            case PolylineEntity polyline when polyline.Closed:
                return PolygonGeometry(polyline.Points, []);
            case PolylineEntity polyline:
                return LineStringGeometry(polyline.Points);
            case CircleEntity circle:
                return PolygonGeometry(
                    SampleCircle(circle.Center, circle.Radius, options.CurveSegments),
                    []);
            case ArcEntity arc:
                return LineStringGeometry(arc.SamplePoints(options.CurveSegments));
            case EllipseEntity ellipse:
                return LineStringGeometry(ellipse.SamplePoints(options.CurveSegments));
            case SplineEntity spline:
                return spline.Closed
                    ? PolygonGeometry(spline.SamplePoints(options.CurveSegments), [])
                    : LineStringGeometry(spline.SamplePoints(options.CurveSegments));
            case HatchEntity hatch when options.IncludeHatches:
                return PolygonGeometry(hatch.Boundary, hatch.EffectiveIslandLoops.ToArray());
            case HatchEntity:
                return null;
            case BlockReferenceEntity block:
                warnings.Add($"Block reference '{block.DefinitionName}' ({block.Id}) was skipped by GeoJSON export; explode or flatten blocks before GIS exchange.");
                return null;
            case RayEntity or XLineEntity:
                warnings.Add($"Infinite entity {entity.GetType().Name} ({entity.Id}) was skipped by GeoJSON export.");
                return null;
            case TextEntity or MTextEntity or LinearDimensionEntity or AngularDimensionEntity or RadialDimensionEntity or LeaderEntity:
                warnings.Add($"Annotation entity {entity.GetType().Name} ({entity.Id}) was skipped by GeoJSON geometry export.");
                return null;
            default:
                warnings.Add($"Unsupported entity {entity.GetType().Name} ({entity.Id}) was skipped by GeoJSON export.");
                return null;
        }
    }

    private static void ImportFeature(
        JsonElement feature,
        List<CadGeoJsonImportedEntity> output,
        List<string> warnings,
        int featureIndex)
    {
        if (!feature.TryGetProperty("type", out var featureType) || !string.Equals(featureType.GetString(), "Feature", StringComparison.Ordinal))
            throw new FormatException("Item is not a GeoJSON Feature.");
        var properties = ReadProperties(feature);
        var suggestedLayer = properties.TryGetValue("layer", out var layer) ? layer : null;
        if (!feature.TryGetProperty("geometry", out var geometry) || geometry.ValueKind == JsonValueKind.Null)
            throw new FormatException("Feature has no geometry.");
        if (!geometry.TryGetProperty("type", out var geometryTypeElement)) throw new FormatException("Geometry has no type.");
        var geometryType = geometryTypeElement.GetString();
        if (!geometry.TryGetProperty("coordinates", out var coordinates)) throw new FormatException("Geometry has no coordinates.");

        switch (geometryType)
        {
            case "Point":
                output.Add(Wrap(new PointEntity(ReadPoint(coordinates)), properties, suggestedLayer));
                break;
            case "LineString":
            {
                var points = ReadPointArray(coordinates, 2);
                output.Add(Wrap(points.Count == 2 ? new LineEntity(points[0], points[1]) : new PolylineEntity(points), properties, suggestedLayer));
                break;
            }
            case "Polygon":
            {
                if (coordinates.ValueKind != JsonValueKind.Array) throw new FormatException("Polygon coordinates must be an array of rings.");
                var rings = coordinates.EnumerateArray().Select(ReadRing).ToArray();
                if (rings.Length == 0) throw new FormatException("Polygon requires an exterior ring.");
                output.Add(Wrap(new PolylineEntity(rings[0], closed: true), properties, suggestedLayer));
                for (var index = 1; index < rings.Length; index++)
                {
                    var holeProperties = new Dictionary<string, string?>(properties, StringComparer.OrdinalIgnoreCase)
                    {
                        ["ucadGeoJsonRole"] = "interior-ring",
                        ["ucadGeoJsonParentFeature"] = featureIndex.ToString(System.Globalization.CultureInfo.InvariantCulture)
                    };
                    output.Add(Wrap(new PolylineEntity(rings[index], closed: true), holeProperties, suggestedLayer));
                }
                if (rings.Length > 1)
                    warnings.Add($"Feature {featureIndex} polygon holes were imported as separate closed polylines because UCAD polyline geometry has no hole topology.");
                break;
            }
            case "MultiLineString":
                foreach (var line in coordinates.EnumerateArray())
                {
                    var points = ReadPointArray(line, 2);
                    output.Add(Wrap(points.Count == 2 ? new LineEntity(points[0], points[1]) : new PolylineEntity(points), properties, suggestedLayer));
                }
                break;
            case "MultiPolygon":
                foreach (var polygon in coordinates.EnumerateArray())
                {
                    var rings = polygon.EnumerateArray().Select(ReadRing).ToArray();
                    if (rings.Length == 0) continue;
                    output.Add(Wrap(new PolylineEntity(rings[0], closed: true), properties, suggestedLayer));
                    for (var index = 1; index < rings.Length; index++)
                    {
                        var holeProperties = new Dictionary<string, string?>(properties, StringComparer.OrdinalIgnoreCase)
                        {
                            ["ucadGeoJsonRole"] = "interior-ring"
                        };
                        output.Add(Wrap(new PolylineEntity(rings[index], closed: true), holeProperties, suggestedLayer));
                    }
                }
                break;
            default:
                throw new FormatException($"Geometry type '{geometryType}' is not supported.");
        }
    }

    private static CadGeoJsonImportedEntity Wrap(
        ICadEntity entity,
        IReadOnlyDictionary<string, string?> properties,
        string? suggestedLayer) =>
        new(entity, properties, suggestedLayer);

    private static IReadOnlyDictionary<string, string?> ReadProperties(JsonElement feature)
    {
        var result = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        if (!feature.TryGetProperty("properties", out var properties) || properties.ValueKind != JsonValueKind.Object)
            return result;
        foreach (var property in properties.EnumerateObject())
        {
            result[property.Name] = property.Value.ValueKind switch
            {
                JsonValueKind.Null => null,
                JsonValueKind.String => property.Value.GetString(),
                _ => property.Value.GetRawText()
            };
        }
        return result;
    }

    private static CadPoint ReadPoint(JsonElement coordinate)
    {
        if (coordinate.ValueKind != JsonValueKind.Array) throw new FormatException("Coordinate must be an array.");
        var values = coordinate.EnumerateArray().Take(2).Select(value => value.GetDouble()).ToArray();
        if (values.Length < 2 || values.Any(value => !double.IsFinite(value))) throw new FormatException("Coordinate requires finite X and Y values.");
        return new CadPoint(values[0], values[1]);
    }

    private static IReadOnlyList<CadPoint> ReadPointArray(JsonElement coordinates, int minimumCount)
    {
        if (coordinates.ValueKind != JsonValueKind.Array) throw new FormatException("Coordinates must be an array.");
        var points = coordinates.EnumerateArray().Select(ReadPoint).ToArray();
        if (points.Length < minimumCount) throw new FormatException($"Geometry requires at least {minimumCount} coordinates.");
        return points;
    }

    private static IReadOnlyList<CadPoint> ReadRing(JsonElement coordinates)
    {
        var points = ReadPointArray(coordinates, 4).ToList();
        if ((points[0] - points[^1]).Length <= 1e-9) points.RemoveAt(points.Count - 1);
        if (points.Count < 3) throw new FormatException("Polygon ring requires at least three distinct vertices.");
        return points;
    }

    private static JsonObject PointGeometry(CadPoint point) => new()
    {
        ["type"] = "Point",
        ["coordinates"] = Coordinate(point)
    };

    private static JsonObject LineStringGeometry(IReadOnlyList<CadPoint> points) => new()
    {
        ["type"] = "LineString",
        ["coordinates"] = new JsonArray(points.Select(point => (JsonNode?)Coordinate(point)).ToArray())
    };

    private static JsonObject PolygonGeometry(
        IReadOnlyList<CadPoint> exterior,
        IReadOnlyList<IReadOnlyList<CadPoint>> holes)
    {
        var rings = new JsonArray { Ring(exterior) };
        foreach (var hole in holes) rings.Add(Ring(hole));
        return new JsonObject
        {
            ["type"] = "Polygon",
            ["coordinates"] = rings
        };
    }

    private static JsonArray Ring(IReadOnlyList<CadPoint> points)
    {
        if (points.Count < 3) throw new ArgumentException("Polygon ring requires at least three points.", nameof(points));
        var values = new List<JsonNode?>(points.Count + 1);
        values.AddRange(points.Select(point => (JsonNode?)Coordinate(point)));
        if ((points[0] - points[^1]).Length > 1e-9) values.Add(Coordinate(points[0]));
        return new JsonArray(values.ToArray());
    }

    private static JsonArray Coordinate(CadPoint point) => new(point.X, point.Y);

    private static IReadOnlyList<CadPoint> SampleCircle(CadPoint center, double radius, int segments) =>
        Enumerable.Range(0, segments)
            .Select(index =>
            {
                var angle = Math.Tau * index / segments;
                return new CadPoint(center.X + (Math.Cos(angle) * radius), center.Y + (Math.Sin(angle) * radius));
            })
            .ToArray();
}

using System.Text.Json;
using System.Text.Json.Serialization;
using UCAD.Core.Blocks;
using UCAD.Core.Entities;
using UCAD.Core.Geometry;
using UCAD.Core.Layers;

namespace UCAD.Core.IO;

/// <summary>
/// Lossless UCAD-native JSON persistence. DXF remains the exchange format; .ucad is the
/// authoring format that must preserve every entity type shipped by the current Core.
/// </summary>
public static class CadNativeDocumentCodec
{
    public const string FileExtension = ".ucad";
    private const string Schema = "ucad-document";
    private const int FormatVersion = 1;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
    };

    public static string Serialize(CadDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        var model = new DocumentDto
        {
            Schema = Schema,
            FormatVersion = FormatVersion,
            CurrentLayer = document.CurrentLayerName,
            Layers = document.Layers.Select(ToDto).ToList(),
            Blocks = document.Blocks.Select(ToDto).ToList(),
            Entities = document.Entities.Select(entity => ToDto(entity, document.GetEntityProperties(entity.Id))).ToList()
        };
        return JsonSerializer.Serialize(model, JsonOptions);
    }

    public static CadDocument Deserialize(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);
        var model = JsonSerializer.Deserialize<DocumentDto>(json, JsonOptions)
                    ?? throw new FormatException("UCAD document JSON is empty.");
        if (!string.Equals(model.Schema, Schema, StringComparison.Ordinal))
            throw new FormatException($"Unsupported UCAD document schema '{model.Schema}'.");
        if (model.FormatVersion != FormatVersion)
            throw new FormatException($"Unsupported UCAD document format version {model.FormatVersion}.");

        var document = new CadDocument();
        foreach (var layer in model.Layers ?? [])
        {
            if (string.Equals(layer.Name, CadLayer.DefaultLayerName, StringComparison.OrdinalIgnoreCase))
            {
                var currentDefault = document.GetLayer(CadLayer.DefaultLayerName);
                if (layer.ColorHex != currentDefault.ColorHex ||
                    layer.LineWeight != currentDefault.LineWeight ||
                    layer.LineType != currentDefault.LineType ||
                    layer.IsVisible != currentDefault.IsVisible ||
                    layer.IsLocked != currentDefault.IsLocked)
                {
                    document.UpdateLayer(
                        CadLayer.DefaultLayerName,
                        layer.ColorHex,
                        layer.LineWeight,
                        layer.LineType,
                        layer.IsVisible,
                        layer.IsLocked);
                }
                continue;
            }
            document.CreateLayer(new CadLayer(
                Require(layer.Name, "layer.name"),
                Require(layer.ColorHex, "layer.colorHex"),
                layer.LineWeight,
                Require(layer.LineType, "layer.lineType"),
                layer.IsVisible,
                layer.IsLocked));
        }

        foreach (var block in model.Blocks ?? [])
        {
            var entities = (block.Entities ?? []).Select(FromDto).ToArray();
            document.DefineBlock(new CadBlockDefinition(
                Require(block.Name, "block.name"),
                FromDto(block.BasePoint, "block.basePoint"),
                entities));
        }

        foreach (var item in model.Entities ?? [])
        {
            var entity = FromDto(item);
            var properties = item.Properties is null
                ? new CadEntityProperties(document.CurrentLayerName)
                : new CadEntityProperties(
                    Require(item.Properties.LayerName, "entity.properties.layerName"),
                    item.Properties.ColorHex,
                    item.Properties.LineWeight,
                    item.Properties.LineType ?? "ByLayer");
            document.Add(entity, properties);
        }

        if (!string.IsNullOrWhiteSpace(model.CurrentLayer))
        {
            if (!document.TryGetLayer(model.CurrentLayer, out _))
                throw new FormatException($"Current layer '{model.CurrentLayer}' does not exist in the document layer table.");
            document.SetCurrentLayer(model.CurrentLayer);
        }

        return document;
    }

    private static LayerDto ToDto(CadLayer layer) => new()
    {
        Name = layer.Name,
        ColorHex = layer.ColorHex,
        LineWeight = layer.LineWeight,
        LineType = layer.LineType,
        IsVisible = layer.IsVisible,
        IsLocked = layer.IsLocked
    };

    private static BlockDto ToDto(CadBlockDefinition block) => new()
    {
        Name = block.Name,
        BasePoint = ToDto(block.BasePoint),
        Entities = block.Entities.Select(entity => ToDto(entity, properties: null)).ToList()
    };

    private static EntityDto ToDto(ICadEntity entity, CadEntityProperties? properties)
    {
        var dto = entity switch
        {
            LineEntity line => new EntityDto
            {
                Type = "line",
                Start = ToDto(line.Start),
                End = ToDto(line.End)
            },
            PolylineEntity polyline => new EntityDto
            {
                Type = "polyline",
                Points = polyline.Points.Select(ToDto).ToList(),
                Closed = polyline.Closed
            },
            CircleEntity circle => new EntityDto
            {
                Type = "circle",
                Center = ToDto(circle.Center),
                Radius = circle.Radius
            },
            ArcEntity arc => new EntityDto
            {
                Type = "arc",
                Center = ToDto(arc.Center),
                Radius = arc.Radius,
                StartAngleRadians = arc.StartAngleRadians,
                SweepAngleRadians = arc.SweepAngleRadians
            },
            PointEntity point => new EntityDto
            {
                Type = "point",
                Position = ToDto(point.Position)
            },
            EllipseEntity ellipse => new EntityDto
            {
                Type = "ellipse",
                Center = ToDto(ellipse.Center),
                MajorAxis = ToDto(ellipse.MajorAxis),
                Ratio = ellipse.Ratio,
                StartParameter = ellipse.StartParameter,
                EndParameter = ellipse.EndParameter
            },
            SplineEntity spline => new EntityDto
            {
                Type = "spline",
                FitPoints = spline.FitPoints.Select(ToDto).ToList(),
                Closed = spline.Closed
            },
            RayEntity ray => new EntityDto
            {
                Type = "ray",
                Origin = ToDto(ray.Origin),
                Direction = ToDto(ray.Direction)
            },
            XLineEntity xline => new EntityDto
            {
                Type = "xline",
                Position = ToDto(xline.Point),
                Direction = ToDto(xline.Direction)
            },
            TextEntity text => new EntityDto
            {
                Type = "text",
                Position = ToDto(text.Position),
                Text = text.Text,
                Height = text.Height,
                RotationRadians = text.RotationRadians
            },
            LinearDimensionEntity dimension => new EntityDto
            {
                Type = "linearDimension",
                FirstExtensionPoint = ToDto(dimension.FirstExtensionPoint),
                SecondExtensionPoint = ToDto(dimension.SecondExtensionPoint),
                DimensionLinePoint = ToDto(dimension.DimensionLinePoint),
                TextOverride = dimension.TextOverride
            },
            HatchEntity hatch => new EntityDto
            {
                Type = "hatch",
                Boundary = hatch.Boundary.Select(ToDto).ToList(),
                Pattern = hatch.Pattern,
                PatternScale = hatch.PatternScale,
                PatternAngleRadians = hatch.PatternAngleRadians
            },
            BlockReferenceEntity reference => new EntityDto
            {
                Type = "blockReference",
                DefinitionName = reference.DefinitionName,
                InsertionPoint = ToDto(reference.InsertionPoint),
                Scale = reference.Scale,
                RotationRadians = reference.RotationRadians,
                Contents = reference.Contents.Select(child => ToDto(child, properties: null)).ToList()
            },
            _ => throw new NotSupportedException($"Native UCAD persistence does not support {entity.GetType().FullName}.")
        };

        if (properties is not null)
        {
            dto.Properties = new EntityPropertiesDto
            {
                LayerName = properties.LayerName,
                ColorHex = properties.ColorHex,
                LineWeight = properties.LineWeight,
                LineType = properties.LineType
            };
        }
        return dto;
    }

    private static ICadEntity FromDto(EntityDto dto)
    {
        return dto.Type switch
        {
            "line" => new LineEntity(FromDto(dto.Start, "line.start"), FromDto(dto.End, "line.end")),
            "polyline" => new PolylineEntity(
                RequirePoints(dto.Points, "polyline.points", minimum: 2),
                dto.Closed),
            "circle" => new CircleEntity(FromDto(dto.Center, "circle.center"), Positive(dto.Radius, "circle.radius")),
            "arc" => ArcEntity.Create(
                FromDto(dto.Center, "arc.center"),
                Positive(dto.Radius, "arc.radius"),
                Finite(dto.StartAngleRadians, "arc.startAngleRadians"),
                NonZeroFinite(dto.SweepAngleRadians, "arc.sweepAngleRadians")),
            "point" => new PointEntity(FromDto(dto.Position, "point.position")),
            "ellipse" => new EllipseEntity(
                FromDto(dto.Center, "ellipse.center"),
                FromDto(dto.MajorAxis, "ellipse.majorAxis"),
                PositiveRatio(dto.Ratio, "ellipse.ratio"),
                Finite(dto.StartParameter, "ellipse.startParameter"),
                Finite(dto.EndParameter, "ellipse.endParameter")),
            "spline" => new SplineEntity(
                RequirePoints(dto.FitPoints, "spline.fitPoints", minimum: 2),
                dto.Closed),
            "ray" => new RayEntity(
                FromDto(dto.Origin, "ray.origin"),
                FromDto(dto.Direction, "ray.direction")),
            "xline" => new XLineEntity(
                FromDto(dto.Position, "xline.point"),
                FromDto(dto.Direction, "xline.direction")),
            "text" => new TextEntity(
                FromDto(dto.Position, "text.position"),
                dto.Text ?? string.Empty,
                Positive(dto.Height, "text.height"),
                Finite(dto.RotationRadians, "text.rotationRadians")),
            "linearDimension" => new LinearDimensionEntity(
                FromDto(dto.FirstExtensionPoint, "linearDimension.firstExtensionPoint"),
                FromDto(dto.SecondExtensionPoint, "linearDimension.secondExtensionPoint"),
                FromDto(dto.DimensionLinePoint, "linearDimension.dimensionLinePoint"),
                dto.TextOverride),
            "hatch" => new HatchEntity(
                RequirePoints(dto.Boundary, "hatch.boundary", minimum: 3),
                Require(dto.Pattern, "hatch.pattern"),
                Positive(dto.PatternScale, "hatch.patternScale"),
                Finite(dto.PatternAngleRadians, "hatch.patternAngleRadians")),
            "blockReference" => new BlockReferenceEntity(
                Require(dto.DefinitionName, "blockReference.definitionName"),
                FromDto(dto.InsertionPoint, "blockReference.insertionPoint"),
                RequireEntities(dto.Contents, "blockReference.contents"),
                Positive(dto.Scale, "blockReference.scale"),
                Finite(dto.RotationRadians, "blockReference.rotationRadians")),
            _ => throw new FormatException($"Unsupported UCAD native entity type '{dto.Type}'.")
        };
    }

    private static PointDto ToDto(CadPoint point) => new() { X = point.X, Y = point.Y };
    private static VectorDto ToDto(CadVector vector) => new() { X = vector.X, Y = vector.Y };

    private static CadPoint FromDto(PointDto? point, string path)
    {
        if (point is null) throw new FormatException($"Missing {path}.");
        return new CadPoint(Finite(point.X, path + ".x"), Finite(point.Y, path + ".y"));
    }

    private static CadVector FromDto(VectorDto? vector, string path)
    {
        if (vector is null) throw new FormatException($"Missing {path}.");
        return new CadVector(Finite(vector.X, path + ".x"), Finite(vector.Y, path + ".y"));
    }

    private static IReadOnlyList<CadPoint> RequirePoints(List<PointDto>? points, string path, int minimum)
    {
        if (points is null || points.Count < minimum) throw new FormatException($"{path} requires at least {minimum} points.");
        return points.Select((point, index) => FromDto(point, $"{path}[{index}]")).ToArray();
    }

    private static IReadOnlyList<ICadEntity> RequireEntities(List<EntityDto>? entities, string path)
    {
        if (entities is null || entities.Count == 0) throw new FormatException($"{path} requires at least one entity.");
        return entities.Select(FromDto).ToArray();
    }

    private static string Require(string? value, string path)
    {
        if (string.IsNullOrWhiteSpace(value)) throw new FormatException($"Missing {path}.");
        return value;
    }

    private static double Finite(double value, string path)
    {
        if (!double.IsFinite(value)) throw new FormatException($"{path} must be finite.");
        return value;
    }

    private static double Positive(double value, string path)
    {
        if (!double.IsFinite(value) || value <= 0) throw new FormatException($"{path} must be positive and finite.");
        return value;
    }

    private static double PositiveRatio(double value, string path)
    {
        if (!double.IsFinite(value) || value <= 0 || value > 1 + 1e-9) throw new FormatException($"{path} must be in (0, 1].");
        return Math.Min(1, value);
    }

    private static double NonZeroFinite(double value, string path)
    {
        if (!double.IsFinite(value) || Math.Abs(value) <= 1e-12) throw new FormatException($"{path} must be non-zero and finite.");
        return value;
    }

    private sealed class DocumentDto
    {
        public string? Schema { get; set; }
        public int FormatVersion { get; set; }
        public string? CurrentLayer { get; set; }
        public List<LayerDto>? Layers { get; set; }
        public List<BlockDto>? Blocks { get; set; }
        public List<EntityDto>? Entities { get; set; }
    }

    private sealed class LayerDto
    {
        public string? Name { get; set; }
        public string? ColorHex { get; set; }
        public double LineWeight { get; set; } = 0.25;
        public string? LineType { get; set; } = "Continuous";
        public bool IsVisible { get; set; } = true;
        public bool IsLocked { get; set; }
    }

    private sealed class BlockDto
    {
        public string? Name { get; set; }
        public PointDto? BasePoint { get; set; }
        public List<EntityDto>? Entities { get; set; }
    }

    private sealed class EntityDto
    {
        public string? Type { get; set; }
        public EntityPropertiesDto? Properties { get; set; }
        public PointDto? Start { get; set; }
        public PointDto? End { get; set; }
        public List<PointDto>? Points { get; set; }
        public bool Closed { get; set; }
        public PointDto? Center { get; set; }
        public double Radius { get; set; }
        public double StartAngleRadians { get; set; }
        public double SweepAngleRadians { get; set; }
        public PointDto? Position { get; set; }
        public PointDto? Origin { get; set; }
        public VectorDto? MajorAxis { get; set; }
        public VectorDto? Direction { get; set; }
        public double Ratio { get; set; }
        public double StartParameter { get; set; }
        public double EndParameter { get; set; }
        public List<PointDto>? FitPoints { get; set; }
        public string? Text { get; set; }
        public double Height { get; set; }
        public double RotationRadians { get; set; }
        public PointDto? FirstExtensionPoint { get; set; }
        public PointDto? SecondExtensionPoint { get; set; }
        public PointDto? DimensionLinePoint { get; set; }
        public string? TextOverride { get; set; }
        public List<PointDto>? Boundary { get; set; }
        public string? Pattern { get; set; }
        public double PatternScale { get; set; }
        public double PatternAngleRadians { get; set; }
        public string? DefinitionName { get; set; }
        public PointDto? InsertionPoint { get; set; }
        public double Scale { get; set; }
        public List<EntityDto>? Contents { get; set; }
    }

    private sealed class EntityPropertiesDto
    {
        public string? LayerName { get; set; }
        public string? ColorHex { get; set; }
        public double? LineWeight { get; set; }
        public string? LineType { get; set; }
    }

    private sealed class PointDto
    {
        public double X { get; set; }
        public double Y { get; set; }
    }

    private sealed class VectorDto
    {
        public double X { get; set; }
        public double Y { get; set; }
    }
}
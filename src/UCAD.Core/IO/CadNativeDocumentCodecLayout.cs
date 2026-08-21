using System.Text.Json;
using System.Text.Json.Nodes;
using UCAD.Core.Geometry;
using UCAD.Core.Layout;

namespace UCAD.Core.IO;

/// <summary>
/// Current UCAD native codec plus paper-space layout metadata. The base/current payload
/// stays backward compatible; layout state is stored in its own namespaced extension.
/// </summary>
public static class CadNativeDocumentCodecLayout
{
    private const string ExtensionsProperty = "extensions";
    private const string ExtensionName = "ucad.layout";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    public static string Serialize(CadDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        var root = JsonNode.Parse(CadNativeDocumentCodecCurrent.Serialize(document))?.AsObject()
            ?? throw new InvalidOperationException("Current UCAD native codec returned invalid JSON.");
        var extensions = root[ExtensionsProperty] as JsonObject ?? new JsonObject();
        extensions[ExtensionName] = JsonSerializer.SerializeToNode(ToDto(document), JsonOptions);
        root[ExtensionsProperty] = extensions;
        return root.ToJsonString(JsonOptions);
    }

    public static CadDocument Deserialize(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);
        var root = JsonNode.Parse(json)?.AsObject() ?? throw new FormatException("UCAD document JSON is empty.");
        LayoutExtensionDto? extension = null;
        if (root[ExtensionsProperty] is JsonObject extensions && extensions[ExtensionName] is JsonNode node)
        {
            extension = node.Deserialize<LayoutExtensionDto>(JsonOptions);
            extensions.Remove(ExtensionName);
            if (extensions.Count == 0) root.Remove(ExtensionsProperty);
        }

        var document = CadNativeDocumentCodecCurrent.Deserialize(root.ToJsonString(JsonOptions));
        if (extension is not null) Apply(document, extension);
        return document;
    }

    public static bool HasLayoutExtension(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) return false;
        try
        {
            var root = JsonNode.Parse(json)?.AsObject();
            return root?[ExtensionsProperty]?[ExtensionName] is not null;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static LayoutExtensionDto ToDto(CadDocument document) => new()
    {
        ActiveLayoutName = document.ActiveLayoutName,
        Layouts = document.Layouts.Select(layout => new LayoutDto
        {
            Name = layout.Name,
            ModelLayout = layout.ModelLayout,
            PageSetup = ToDto(layout.PageSetup),
            Viewports = layout.Viewports.Select(viewport => new ViewportDto
            {
                Id = viewport.Id,
                Name = viewport.Name,
                PaperRect = ToDto(viewport.PaperRectMm),
                ModelCenter = ToDto(viewport.ModelCenter),
                ScaleDenominator = viewport.ScaleDenominator,
                TwistAngleRadians = viewport.TwistAngleRadians,
                Locked = viewport.Locked
            }).ToList()
        }).ToList()
    };

    private static PageSetupDto ToDto(CadPageSetup setup) => new()
    {
        PaperName = setup.PaperSize.Name,
        PaperWidthMm = setup.PaperSize.WidthMm,
        PaperHeightMm = setup.PaperSize.HeightMm,
        Landscape = setup.Landscape,
        MarginLeftMm = setup.MarginLeftMm,
        MarginTopMm = setup.MarginTopMm,
        MarginRightMm = setup.MarginRightMm,
        MarginBottomMm = setup.MarginBottomMm,
        PlotScaleDenominator = setup.PlotScaleDenominator,
        PlotArea = setup.PlotArea.ToString(),
        PlotStyle = setup.PlotStyle.ToString(),
        ModelWindow = setup.ModelWindow is CadRect window ? ToDto(window) : null
    };

    private static void Apply(CadDocument document, LayoutExtensionDto extension)
    {
        var layouts = (extension.Layouts ?? []).Select((layout, index) =>
        {
            var name = Require(layout.Name, $"layouts[{index}].name");
            var setup = FromDto(layout.PageSetup, $"layouts[{index}].pageSetup");
            var viewports = (layout.Viewports ?? []).Select((viewport, viewportIndex) => new CadLayoutViewport(
                Require(viewport.Name, $"layouts[{index}].viewports[{viewportIndex}].name"),
                FromDto(viewport.PaperRect, $"layouts[{index}].viewports[{viewportIndex}].paperRect"),
                FromDto(viewport.ModelCenter, $"layouts[{index}].viewports[{viewportIndex}].modelCenter"),
                Positive(viewport.ScaleDenominator, $"layouts[{index}].viewports[{viewportIndex}].scaleDenominator"),
                Finite(viewport.TwistAngleRadians, $"layouts[{index}].viewports[{viewportIndex}].twistAngleRadians"),
                viewport.Locked,
                viewport.Id == Guid.Empty ? null : viewport.Id)).ToArray();
            return new CadLayoutDefinition(name, setup, viewports, layout.ModelLayout);
        }).ToArray();

        if (layouts.Length == 0) throw new FormatException("Layout extension contains no layouts.");
        var active = string.IsNullOrWhiteSpace(extension.ActiveLayoutName) ? layouts[0].Name : extension.ActiveLayoutName;
        document.SetLayoutTable(layouts, active!);
    }

    private static CadPageSetup FromDto(PageSetupDto? dto, string path)
    {
        if (dto is null) throw new FormatException($"Missing {path}.");
        var paper = new CadPaperSize(
            Require(dto.PaperName, path + ".paperName"),
            Positive(dto.PaperWidthMm, path + ".paperWidthMm"),
            Positive(dto.PaperHeightMm, path + ".paperHeightMm"));
        var area = Enum.TryParse<CadPlotArea>(dto.PlotArea, true, out var parsedArea)
            ? parsedArea
            : throw new FormatException($"Invalid {path}.plotArea '{dto.PlotArea}'.");
        var style = Enum.TryParse<CadPlotStyleMode>(dto.PlotStyle, true, out var parsedStyle)
            ? parsedStyle
            : throw new FormatException($"Invalid {path}.plotStyle '{dto.PlotStyle}'.");
        return new CadPageSetup(
            paper,
            dto.Landscape,
            NonNegative(dto.MarginLeftMm, path + ".marginLeftMm"),
            NonNegative(dto.MarginTopMm, path + ".marginTopMm"),
            NonNegative(dto.MarginRightMm, path + ".marginRightMm"),
            NonNegative(dto.MarginBottomMm, path + ".marginBottomMm"),
            Positive(dto.PlotScaleDenominator, path + ".plotScaleDenominator"),
            area,
            style,
            dto.ModelWindow is null ? null : FromDto(dto.ModelWindow, path + ".modelWindow"));
    }

    private static PointDto ToDto(CadPoint point) => new() { X = point.X, Y = point.Y };
    private static RectDto ToDto(CadRect rect) => new() { Left = rect.Left, Bottom = rect.Bottom, Right = rect.Right, Top = rect.Top };

    private static CadPoint FromDto(PointDto? point, string path)
    {
        if (point is null) throw new FormatException($"Missing {path}.");
        return new CadPoint(Finite(point.X, path + ".x"), Finite(point.Y, path + ".y"));
    }

    private static CadRect FromDto(RectDto? rect, string path)
    {
        if (rect is null) throw new FormatException($"Missing {path}.");
        var value = new CadRect(
            Finite(rect.Left, path + ".left"),
            Finite(rect.Bottom, path + ".bottom"),
            Finite(rect.Right, path + ".right"),
            Finite(rect.Top, path + ".top"));
        if (value.Width <= 0 || value.Height <= 0) throw new FormatException($"{path} must have positive area.");
        return value;
    }

    private static string Require(string? value, string path)
    {
        if (string.IsNullOrWhiteSpace(value)) throw new FormatException($"Missing {path}.");
        return value.Trim();
    }

    private static double Positive(double value, string path)
    {
        if (!double.IsFinite(value) || value <= 0) throw new FormatException($"{path} must be positive and finite.");
        return value;
    }

    private static double NonNegative(double value, string path)
    {
        if (!double.IsFinite(value) || value < 0) throw new FormatException($"{path} must be non-negative and finite.");
        return value;
    }

    private static double Finite(double value, string path)
    {
        if (!double.IsFinite(value)) throw new FormatException($"{path} must be finite.");
        return value;
    }

    private sealed class LayoutExtensionDto
    {
        public string? ActiveLayoutName { get; set; }
        public List<LayoutDto>? Layouts { get; set; }
    }

    private sealed class LayoutDto
    {
        public string? Name { get; set; }
        public bool ModelLayout { get; set; }
        public PageSetupDto? PageSetup { get; set; }
        public List<ViewportDto>? Viewports { get; set; }
    }

    private sealed class PageSetupDto
    {
        public string? PaperName { get; set; }
        public double PaperWidthMm { get; set; }
        public double PaperHeightMm { get; set; }
        public bool Landscape { get; set; }
        public double MarginLeftMm { get; set; }
        public double MarginTopMm { get; set; }
        public double MarginRightMm { get; set; }
        public double MarginBottomMm { get; set; }
        public double PlotScaleDenominator { get; set; }
        public string? PlotArea { get; set; }
        public string? PlotStyle { get; set; }
        public RectDto? ModelWindow { get; set; }
    }

    private sealed class ViewportDto
    {
        public Guid Id { get; set; }
        public string? Name { get; set; }
        public RectDto? PaperRect { get; set; }
        public PointDto? ModelCenter { get; set; }
        public double ScaleDenominator { get; set; }
        public double TwistAngleRadians { get; set; }
        public bool Locked { get; set; }
    }

    private sealed class PointDto { public double X { get; set; } public double Y { get; set; } }
    private sealed class RectDto { public double Left { get; set; } public double Bottom { get; set; } public double Right { get; set; } public double Top { get; set; } }
}

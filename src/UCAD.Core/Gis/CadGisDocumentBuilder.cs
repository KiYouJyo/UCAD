using UCAD.Core.Gis;
using UCAD.Core.Layers;

namespace UCAD.Core.Gis;

public sealed record CadGisDocumentBuildResult(
    CadDocument Document,
    IReadOnlyList<string> Warnings);

public static class CadGisDocumentBuilder
{
    private const string DefaultGisLayer = "GIS";

    public static CadGisDocumentBuildResult FromGeoJson(CadGeoJsonImportResult import)
    {
        ArgumentNullException.ThrowIfNull(import);
        var document = new CadDocument();
        var warnings = new List<string>(import.Warnings);
        foreach (var item in import.Entities)
        {
            var layerName = EnsureLayer(document, item.SuggestedLayerName, warnings);
            document.Add(item.Entity, new CadEntityProperties(layerName));
        }
        document.ResetHistory();
        return new CadGisDocumentBuildResult(document, warnings.AsReadOnly());
    }

    public static CadGisDocumentBuildResult FromCsvPoints(CadCsvPointImportResult import)
    {
        ArgumentNullException.ThrowIfNull(import);
        var document = new CadDocument();
        var warnings = new List<string>(import.Warnings);
        foreach (var record in import.Records)
        {
            var layerName = EnsureLayer(document, record.SuggestedLayerName, warnings);
            document.Add(record.Point, new CadEntityProperties(layerName));
        }
        document.ResetHistory();
        return new CadGisDocumentBuildResult(document, warnings.AsReadOnly());
    }

    private static string EnsureLayer(CadDocument document, string? suggestedLayerName, List<string> warnings)
    {
        var requested = string.IsNullOrWhiteSpace(suggestedLayerName) ? DefaultGisLayer : suggestedLayerName.Trim();
        if (document.TryGetLayer(requested, out _)) return requested;
        try
        {
            document.CreateLayer(new CadLayer(requested));
            return requested;
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            warnings.Add($"GIS layer '{requested}' could not be created and was mapped to '{DefaultGisLayer}': {ex.Message}");
            if (!document.TryGetLayer(DefaultGisLayer, out _)) document.CreateLayer(new CadLayer(DefaultGisLayer));
            return DefaultGisLayer;
        }
    }
}

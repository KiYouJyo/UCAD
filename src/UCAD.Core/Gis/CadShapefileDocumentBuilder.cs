using System.Globalization;
using UCAD.Core.Layers;

namespace UCAD.Core.Gis;

public sealed record CadShapefileDocumentBuildResult(
    CadDocument Document,
    IReadOnlyList<string> Warnings);

public static class CadShapefileDocumentBuilder
{
    private const string FallbackLayer = "GIS";

    public static CadShapefileDocumentBuildResult Build(CadShapefilePackageImportResult import)
    {
        ArgumentNullException.ThrowIfNull(import);
        var document = new CadDocument();
        var warnings = new List<string>(import.Warnings);
        var entities = import.Bundle.Geometry.Entities;
        var records = import.Bundle.Attributes?.Records;
        var canMap = import.Bundle.CanMapRecordsOneToOne && records is not null;

        for (var index = 0; index < entities.Count; index++)
        {
            CadEntityProperties properties;
            if (canMap)
            {
                properties = PropertiesFromRecord(document, records![index], warnings);
            }
            else
            {
                var layer = EnsureLayer(document, FallbackLayer, warnings);
                properties = new CadEntityProperties(layer);
            }
            document.Add(entities[index], properties);
        }

        document.ResetHistory();
        return new CadShapefileDocumentBuildResult(document, warnings.AsReadOnly());
    }

    private static CadEntityProperties PropertiesFromRecord(
        CadDocument document,
        CadDbfRecord record,
        List<string> warnings)
    {
        var layerName = Value(record, "LAYER") ?? FallbackLayer;
        layerName = EnsureLayer(document, layerName, warnings);
        var color = NormalizeColor(Value(record, "COLOR"), warnings);
        var lineType = Value(record, "LTYPE") ?? "ByLayer";
        double? lineWeight = null;
        var rawWeight = Value(record, "LWEIGHT");
        if (!string.IsNullOrWhiteSpace(rawWeight))
        {
            if (double.TryParse(rawWeight, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) &&
                double.IsFinite(parsed) && parsed > 0)
            {
                lineWeight = parsed;
            }
            else
            {
                warnings.Add($"DBF lineweight '{rawWeight}' is invalid and was restored as ByLayer.");
            }
        }
        return new CadEntityProperties(layerName, color, lineWeight, lineType);
    }

    private static string EnsureLayer(CadDocument document, string requestedName, List<string> warnings)
    {
        var requested = string.IsNullOrWhiteSpace(requestedName) ? FallbackLayer : requestedName.Trim();
        if (document.TryGetLayer(requested, out _)) return requested;
        try
        {
            document.CreateLayer(new CadLayer(requested));
            return requested;
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            warnings.Add($"Shapefile layer '{requested}' could not be created and was mapped to '{FallbackLayer}': {ex.Message}");
            if (!document.TryGetLayer(FallbackLayer, out _)) document.CreateLayer(new CadLayer(FallbackLayer));
            return FallbackLayer;
        }
    }

    private static string? NormalizeColor(string? value, List<string> warnings)
    {
        if (string.IsNullOrWhiteSpace(value) || string.Equals(value, "ByLayer", StringComparison.OrdinalIgnoreCase)) return null;
        var text = value.Trim();
        if (text.Length == 7 && text[0] == '#' && text.Skip(1).All(Uri.IsHexDigit)) return text.ToUpperInvariant();
        warnings.Add($"DBF color '{value}' is invalid and was restored as ByLayer.");
        return null;
    }

    private static string? Value(CadDbfRecord record, string field) =>
        record.Values.TryGetValue(field, out var value) && !string.IsNullOrWhiteSpace(value) ? value.Trim() : null;
}

using System.Globalization;
using UCAD.Core.Layers;

namespace UCAD.Core.Gis;

public static class CadShapefileMappedDocumentBuilder
{
    private const string FallbackLayer = "GIS";

    public static CadShapefileDocumentBuildResult Build(
        CadShapefilePackageImportResult import,
        ReadOnlySpan<byte> shpContent,
        ReadOnlySpan<byte> dbfContent)
    {
        ArgumentNullException.ThrowIfNull(import);
        if (import.Bundle.Attributes is null || dbfContent.IsEmpty)
            return CadShapefileDocumentBuilder.Build(import);

        var binding = CadShapefileAttributeBinding.Bind(
            shpContent,
            dbfContent,
            import.Bundle.Geometry,
            import.Bundle.Attributes);
        if (!binding.CanBind)
        {
            var fallback = CadShapefileDocumentBuilder.Build(import);
            return new CadShapefileDocumentBuildResult(
                fallback.Document,
                fallback.Warnings.Concat(binding.Warnings).Distinct(StringComparer.Ordinal).ToArray());
        }

        var document = new CadDocument();
        var warnings = new List<string>(import.Warnings);
        warnings.AddRange(binding.Warnings);
        for (var index = 0; index < import.Bundle.Geometry.Entities.Count; index++)
        {
            var record = binding.AttributesByEntity[index]
                         ?? throw new InvalidOperationException("Successful Shapefile binding produced a null DBF record.");
            document.Add(import.Bundle.Geometry.Entities[index], PropertiesFromRecord(document, record, warnings));
        }
        document.ResetHistory();
        return new CadShapefileDocumentBuildResult(document, warnings.Distinct(StringComparer.Ordinal).ToArray());
    }

    private static CadEntityProperties PropertiesFromRecord(
        CadDocument document,
        CadDbfRecord record,
        List<string> warnings)
    {
        var layerName = EnsureLayer(document, Value(record, "LAYER") ?? FallbackLayer, warnings);
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

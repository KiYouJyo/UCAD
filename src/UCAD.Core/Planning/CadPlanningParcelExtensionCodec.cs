using System.Text.Json;
using System.Text.Json.Nodes;

namespace UCAD.Core.Planning;

public sealed record CadPlanningParcelExtensionReadResult(
    string BaseDocumentJson,
    CadPlanningParcelTable ParcelTable);

/// <summary>
/// Adds/removes a top-level ucad.planning extension without changing the strict base
/// UCAD document schema. Callers detach this extension before handing JSON to the native
/// document codec, preserving backward-compatible base parsing.
/// </summary>
public static class CadPlanningParcelExtensionCodec
{
    public const string ExtensionPropertyName = "ucad.planning";
    private const int Version = 1;
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public static string Attach(string baseDocumentJson, CadPlanningParcelTable table)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(baseDocumentJson);
        ArgumentNullException.ThrowIfNull(table);
        var root = JsonNode.Parse(baseDocumentJson) as JsonObject
                   ?? throw new FormatException("UCAD base document JSON root must be an object.");
        root[ExtensionPropertyName] = new JsonObject
        {
            ["version"] = Version,
            ["parcels"] = new JsonArray(table.Records.Select(ToNode).ToArray())
        };
        return root.ToJsonString(JsonOptions);
    }

    public static CadPlanningParcelExtensionReadResult Detach(string documentJson)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(documentJson);
        var root = JsonNode.Parse(documentJson) as JsonObject
                   ?? throw new FormatException("UCAD document JSON root must be an object.");
        var table = new CadPlanningParcelTable();
        if (root[ExtensionPropertyName] is JsonObject extension)
        {
            var version = extension["version"]?.GetValue<int>()
                          ?? throw new FormatException("ucad.planning extension is missing its version.");
            if (version != Version) throw new FormatException($"Unsupported ucad.planning extension version {version}.");
            if (extension["parcels"] is JsonArray parcels)
            {
                foreach (var parcelNode in parcels)
                {
                    if (parcelNode is null) throw new FormatException("ucad.planning contains a null parcel record.");
                    table.Set(FromNode(parcelNode));
                }
            }
            root.Remove(ExtensionPropertyName);
        }
        return new CadPlanningParcelExtensionReadResult(root.ToJsonString(JsonOptions), table);
    }

    private static JsonNode ToNode(CadPlanningParcelRecord record)
    {
        record.Validate();
        return new JsonObject
        {
            ["boundaryEntityId"] = record.BoundaryEntityId.ToString("D"),
            ["parcelId"] = record.Data.ParcelId,
            ["landUseCode"] = record.Data.LandUseCode,
            ["landUseName"] = record.Data.LandUseName,
            ["notes"] = record.Data.Notes,
            ["areaScale"] = record.IndicatorInput.AreaScale,
            ["grossFloorArea"] = record.IndicatorInput.GrossFloorArea,
            ["buildingFootprintArea"] = record.IndicatorInput.BuildingFootprintArea,
            ["greenArea"] = record.IndicatorInput.GreenArea,
            ["proposedHeight"] = record.IndicatorInput.ProposedHeight,
            ["maximumFloorAreaRatio"] = record.Controls.MaximumFloorAreaRatio,
            ["maximumBuildingDensityPercent"] = record.Controls.MaximumBuildingDensityPercent,
            ["minimumGreenRatioPercent"] = record.Controls.MinimumGreenRatioPercent,
            ["maximumHeight"] = record.Controls.MaximumHeight
        };
    }

    private static CadPlanningParcelRecord FromNode(JsonNode node)
    {
        var obj = node as JsonObject ?? throw new FormatException("Planning parcel record must be an object.");
        var boundaryText = RequiredString(obj, "boundaryEntityId");
        if (!Guid.TryParse(boundaryText, out var boundaryId) || boundaryId == Guid.Empty)
            throw new FormatException($"Planning parcel boundaryEntityId '{boundaryText}' is invalid.");
        var data = new CadPlanningParcelData(
            RequiredString(obj, "parcelId"),
            RequiredString(obj, "landUseCode"),
            OptionalString(obj, "landUseName"),
            OptionalString(obj, "notes"));
        var input = new CadParcelIndicatorInput(
            RequiredDouble(obj, "areaScale"),
            RequiredDouble(obj, "grossFloorArea"),
            RequiredDouble(obj, "buildingFootprintArea"),
            RequiredDouble(obj, "greenArea"),
            OptionalDouble(obj, "proposedHeight"));
        var controls = new CadPlanningControls(
            OptionalDouble(obj, "maximumFloorAreaRatio"),
            OptionalDouble(obj, "maximumBuildingDensityPercent"),
            OptionalDouble(obj, "minimumGreenRatioPercent"),
            OptionalDouble(obj, "maximumHeight"));
        return new CadPlanningParcelRecord(boundaryId, data, input, controls).Validate();
    }

    private static string RequiredString(JsonObject obj, string property) =>
        obj[property]?.GetValue<string>() is { Length: > 0 } value
            ? value
            : throw new FormatException($"Planning parcel property '{property}' is required.");

    private static string? OptionalString(JsonObject obj, string property) =>
        obj[property] is null ? null : obj[property]!.GetValue<string?>();

    private static double RequiredDouble(JsonObject obj, string property)
    {
        if (obj[property] is null) throw new FormatException($"Planning parcel property '{property}' is required.");
        var value = obj[property]!.GetValue<double>();
        if (!double.IsFinite(value)) throw new FormatException($"Planning parcel property '{property}' must be finite.");
        return value;
    }

    private static double? OptionalDouble(JsonObject obj, string property)
    {
        if (obj[property] is null) return null;
        var value = obj[property]!.GetValue<double>();
        if (!double.IsFinite(value)) throw new FormatException($"Planning parcel property '{property}' must be finite.");
        return value;
    }
}

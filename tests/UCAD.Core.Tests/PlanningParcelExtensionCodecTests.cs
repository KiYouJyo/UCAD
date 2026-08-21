using System.Text.Json;
using System.Text.Json.Nodes;
using UCAD.Core.Planning;
using Xunit;

namespace UCAD.Core.Tests;

public sealed class PlanningParcelExtensionCodecTests
{
    [Fact]
    public void AttachAndDetachRoundTripPlanningTableWithoutChangingBaseProperties()
    {
        const string baseJson = """
        {
          "schema": "ucad-document",
          "formatVersion": 1,
          "sentinel": "keep-me",
          "nested": { "value": 42 }
        }
        """;
        var boundaryId = Guid.NewGuid();
        var table = new CadPlanningParcelTable();
        table.Set(new CadPlanningParcelRecord(
            boundaryId,
            new CadPlanningParcelData("A-01", "R2", "居住用地", "TOD parcel"),
            new CadParcelIndicatorInput(
                AreaScale: 0.000001,
                GrossFloorArea: 12500,
                BuildingFootprintArea: 1750,
                GreenArea: 1500,
                ProposedHeight: 36),
            new CadPlanningControls(
                MaximumFloorAreaRatio: 2.8,
                MaximumBuildingDensityPercent: 40,
                MinimumGreenRatioPercent: 30,
                MaximumHeight: 45)));

        var attached = CadPlanningParcelExtensionCodec.Attach(baseJson, table);
        using (var attachedJson = JsonDocument.Parse(attached))
        {
            Assert.True(attachedJson.RootElement.TryGetProperty(CadPlanningParcelExtensionCodec.ExtensionPropertyName, out _));
            Assert.Equal("keep-me", attachedJson.RootElement.GetProperty("sentinel").GetString());
            Assert.Equal(42, attachedJson.RootElement.GetProperty("nested").GetProperty("value").GetInt32());
        }

        var detached = CadPlanningParcelExtensionCodec.Detach(attached);
        var restored = Assert.Single(detached.ParcelTable.Records);
        Assert.Equal(boundaryId, restored.BoundaryEntityId);
        Assert.Equal("A-01", restored.Data.ParcelId);
        Assert.Equal("R2", restored.Data.LandUseCode);
        Assert.Equal("居住用地", restored.Data.LandUseName);
        Assert.Equal("TOD parcel", restored.Data.Notes);
        Assert.Equal(0.000001, restored.IndicatorInput.AreaScale, 12);
        Assert.Equal(12500, restored.IndicatorInput.GrossFloorArea, 8);
        Assert.Equal(36, restored.IndicatorInput.ProposedHeight);
        Assert.Equal(2.8, restored.Controls.MaximumFloorAreaRatio);
        Assert.Equal(30, restored.Controls.MinimumGreenRatioPercent);

        var baseNode = JsonNode.Parse(detached.BaseDocumentJson)!.AsObject();
        Assert.False(baseNode.ContainsKey(CadPlanningParcelExtensionCodec.ExtensionPropertyName));
        Assert.Equal("keep-me", baseNode["sentinel"]!.GetValue<string>());
        Assert.Equal(42, baseNode["nested"]!["value"]!.GetValue<int>());
    }

    [Fact]
    public void DetachWithoutPlanningExtensionReturnsEmptyTableAndPreservesBaseSemantics()
    {
        const string json = "{\"schema\":\"ucad-document\",\"formatVersion\":1,\"value\":7}";

        var result = CadPlanningParcelExtensionCodec.Detach(json);

        Assert.Equal(0, result.ParcelTable.Count);
        using var restored = JsonDocument.Parse(result.BaseDocumentJson);
        Assert.Equal("ucad-document", restored.RootElement.GetProperty("schema").GetString());
        Assert.Equal(7, restored.RootElement.GetProperty("value").GetInt32());
    }

    [Fact]
    public void UnsupportedPlanningExtensionVersionIsRejected()
    {
        const string json = """
        {
          "schema": "ucad-document",
          "formatVersion": 1,
          "ucad.planning": { "version": 99, "parcels": [] }
        }
        """;

        Assert.Throws<FormatException>(() => CadPlanningParcelExtensionCodec.Detach(json));
    }

    [Fact]
    public void AttachReplacesExistingPlanningExtensionInsteadOfNestingOrDuplicatingIt()
    {
        const string json = """
        {
          "schema": "ucad-document",
          "formatVersion": 1,
          "ucad.planning": { "version": 1, "parcels": [] }
        }
        """;
        var table = new CadPlanningParcelTable();
        table.Set(new CadPlanningParcelRecord(
            Guid.NewGuid(),
            new CadPlanningParcelData("B-01", "B1"),
            new CadParcelIndicatorInput(1, 0, 0, 0),
            new CadPlanningControls()));

        var attached = CadPlanningParcelExtensionCodec.Attach(json, table);
        var detached = CadPlanningParcelExtensionCodec.Detach(attached);

        Assert.Single(detached.ParcelTable.Records);
        Assert.Equal("B-01", detached.ParcelTable.Records[0].Data.ParcelId);
    }
}

using UCAD.Core.Entities;
using UCAD.Core.Geometry;
using UCAD.Core.Gis;
using Xunit;

namespace UCAD.Core.Tests;

public sealed class CsvPointCodecTests
{
    [Fact]
    public void CsvPointRoundTripPreservesCoordinatesNamesLayersAndExtraProperties()
    {
        var records = new[]
        {
            new CadCsvPointRecord(
                new PointEntity(new CadPoint(120.123456789, 30.987654321)),
                "POI, East",
                "SURVEY",
                new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
                {
                    ["Category"] = "Transit",
                    ["Note"] = "Line 1\nExit \"A\""
                }),
            new CadCsvPointRecord(
                new PointEntity(new CadPoint(-10.5, 5.25)),
                "Sample B",
                null,
                new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
                {
                    ["Category"] = "Field"
                })
        };

        var csv = CadCsvPointCodec.Export(records);
        var imported = CadCsvPointCodec.Import(csv);

        Assert.Empty(imported.Warnings);
        Assert.Equal(2, imported.Records.Count);
        Assert.Equal(120.123456789, imported.Records[0].Point.Position.X, 9);
        Assert.Equal(30.987654321, imported.Records[0].Point.Position.Y, 9);
        Assert.Equal("POI, East", imported.Records[0].Name);
        Assert.Equal("SURVEY", imported.Records[0].SuggestedLayerName);
        Assert.Equal("Transit", imported.Records[0].Properties["Category"]);
        Assert.Equal("Line 1\nExit \"A\"", imported.Records[0].Properties["Note"]);
        Assert.Equal("Sample B", imported.Records[1].Name);
        Assert.Null(imported.Records[1].SuggestedLayerName);
    }

    [Fact]
    public void CustomHeadersAndDelimiterAreSupported()
    {
        const string csv = "E;N;Label;Group;Kind\r\n100.5;200.25;Tree 1;GREEN;survey\r\n";
        var schema = new CadCsvPointSchema("E", "N", "Label", "Group");

        var result = CadCsvPointCodec.Import(csv, schema, ';');

        var record = Assert.Single(result.Records);
        Assert.Equal(new CadPoint(100.5, 200.25), record.Point.Position);
        Assert.Equal("Tree 1", record.Name);
        Assert.Equal("GREEN", record.SuggestedLayerName);
        Assert.Equal("survey", record.Properties["Kind"]);
    }

    [Fact]
    public void InvalidRowsAreSkippedWithWarningsWithoutDroppingValidRows()
    {
        const string csv = "X,Y,Name,Layer\r\n1,2,Good,A\r\nbad,3,Broken,A\r\n4,5,Good2,B\r\n";

        var result = CadCsvPointCodec.Import(csv);

        Assert.Equal(2, result.Records.Count);
        Assert.Single(result.Warnings);
        Assert.True(result.Warnings[0].Contains("row 3", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void MissingCoordinateHeaderIsRejected()
    {
        const string csv = "Longitude,Latitude\r\n1,2\r\n";

        Assert.Throws<FormatException>(() => CadCsvPointCodec.Import(csv));
    }
}

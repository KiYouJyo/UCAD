using System.Text;
using UCAD.Core.Gis;
using Xunit;

namespace UCAD.Core.Tests;

public sealed class DbfAndPrjCodecTests
{
    [Fact]
    public void DbfRoundTripsUtf8CharacterNumericAndLogicalValues()
    {
        var fields = new[]
        {
            new CadDbfFieldDefinition("NAME", CadDbfFieldType.Character, 40),
            new CadDbfFieldDefinition("FAR", CadDbfFieldType.Numeric, 10, 2),
            new CadDbfFieldDefinition("ACTIVE", CadDbfFieldType.Logical, 1)
        };
        var table = new CadDbfTable(
            fields,
            [
                new CadDbfRecord(new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
                {
                    ["NAME"] = "北部ソフトウェア園区",
                    ["FAR"] = "2.5",
                    ["ACTIVE"] = "true"
                }),
                new CadDbfRecord(new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
                {
                    ["NAME"] = "规划地块A",
                    ["FAR"] = "1.75",
                    ["ACTIVE"] = "false"
                })
            ]);

        var bytes = CadDbfCodec.Export(table, new DateTime(2026, 8, 18));
        var restored = CadDbfCodec.Import(bytes);

        Assert.Equal(3, restored.Fields.Count);
        Assert.Equal(2, restored.Records.Count);
        Assert.Equal("北部ソフトウェア園区", restored.Records[0].Values["NAME"]);
        Assert.Equal("2.50", restored.Records[0].Values["FAR"]);
        Assert.Equal("true", restored.Records[0].Values["ACTIVE"]);
        Assert.Equal("规划地块A", restored.Records[1].Values["NAME"]);
        Assert.Equal("1.75", restored.Records[1].Values["FAR"]);
        Assert.Equal("false", restored.Records[1].Values["ACTIVE"]);
        Assert.Equal("UTF-8\r\n", Encoding.ASCII.GetString(CadDbfCodec.CreateCpgUtf8()));
    }

    [Fact]
    public void DbfNumericOverflowIsRejectedInsteadOfTruncated()
    {
        var table = new CadDbfTable(
            [new CadDbfFieldDefinition("VALUE", CadDbfFieldType.Numeric, 4, 0)],
            [new CadDbfRecord(new Dictionary<string, string?> { ["VALUE"] = "12345" })]);

        Assert.Throws<FormatException>(() => CadDbfCodec.Export(table));
    }

    [Theory]
    [InlineData(CadCoordinateReferenceSystem.Wgs84LongitudeLatitude)]
    [InlineData(CadCoordinateReferenceSystem.WebMercator)]
    public void KnownPrjRoundTripsIdentification(CadCoordinateReferenceSystem crs)
    {
        var wkt = CadPrjCodec.GetWkt(crs);

        var identified = CadPrjCodec.IdentifyKnown(wkt);

        Assert.Equal(crs, identified);
    }

    [Fact]
    public void LocalPlanarDoesNotEmitFabricatedPrj()
    {
        Assert.Throws<NotSupportedException>(() => CadPrjCodec.GetWkt(CadCoordinateReferenceSystem.LocalPlanar));
        Assert.Null(CadPrjCodec.IdentifyKnown("LOCAL_CS[\"Unknown project grid\"]"));
    }
}

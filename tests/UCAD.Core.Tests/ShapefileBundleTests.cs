using System.Text;
using UCAD.Core.Entities;
using UCAD.Core.Geometry;
using UCAD.Core.Gis;
using Xunit;

namespace UCAD.Core.Tests;

public sealed class ShapefileBundleTests
{
    private static readonly CadDbfFieldDefinition[] Fields =
    [
        new("ID", CadDbfFieldType.Character, 16),
        new("NAME", CadDbfFieldType.Character, 64),
        new("FAR", CadDbfFieldType.Numeric, 10, 2)
    ];

    [Fact]
    public void BundleRoundTripsGeometryUtf8AttributesAndKnownPrj()
    {
        var features = new[]
        {
            Feature(new CadPoint(139.6917, 35.6895), "A-01", "東京都心", "2.50"),
            Feature(new CadPoint(139.70, 35.70), "A-02", "計画地块", "3.25")
        };

        var exported = CadShapefileBundle.Export(
            features,
            Fields,
            CadCoordinateReferenceSystem.Wgs84LongitudeLatitude,
            new DateTime(2026, 8, 18));
        var imported = CadShapefileBundle.Import(
            exported.ShpContent,
            exported.DbfContent,
            exported.CpgContent,
            exported.PrjContent ?? []);

        Assert.Equal(CadShapefileShapeType.Point, exported.ShapeType);
        Assert.Equal("UTF-8\r\n", Encoding.ASCII.GetString(exported.CpgContent));
        Assert.NotNull(exported.PrjContent);
        Assert.True(imported.CanMapRecordsOneToOne);
        Assert.Equal(CadCoordinateReferenceSystem.Wgs84LongitudeLatitude, imported.IdentifiedCrs);
        Assert.Equal(2, imported.Geometry.Entities.Count);
        Assert.NotNull(imported.Attributes);
        Assert.Equal("東京都心", imported.Attributes!.Records[0].Values["NAME"]);
        Assert.Equal("計画地块", imported.Attributes.Records[1].Values["NAME"]);
        Assert.Equal("3.25", imported.Attributes.Records[1].Values["FAR"]);
        Assert.Empty(imported.Warnings);
    }

    [Fact]
    public void LocalPlanarBundleDoesNotFabricatePrj()
    {
        var exported = CadShapefileBundle.Export(
            [Feature(new CadPoint(500000, 3500000), "P1", "Local", "1.00")],
            Fields,
            CadCoordinateReferenceSystem.LocalPlanar);

        Assert.Null(exported.PrjContent);
        Assert.True(exported.Warnings.Any(warning => warning.Contains("No PRJ", StringComparison.OrdinalIgnoreCase)));
    }

    [Fact]
    public void RecordCountMismatchDisablesAutomaticAttributeMapping()
    {
        var shp = CadShapefileGeometryCodec.Export([
            new PointEntity(new CadPoint(0, 0)),
            new PointEntity(new CadPoint(1, 1))
        ]).ShpContent;
        var dbf = CadDbfCodec.Export(new CadDbfTable(
            Fields,
            [new CadDbfRecord(new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
            {
                ["ID"] = "ONLY-ONE",
                ["NAME"] = "Single record",
                ["FAR"] = "1.00"
            })]));

        var imported = CadShapefileBundle.Import(shp, dbf, CadDbfCodec.CreateCpgUtf8());

        Assert.False(imported.CanMapRecordsOneToOne);
        Assert.Equal(2, imported.Geometry.Entities.Count);
        Assert.Single(imported.Attributes!.Records);
        Assert.True(imported.Warnings.Any(warning => warning.Contains("were not guessed", StringComparison.OrdinalIgnoreCase)));
    }

    private static CadShapefileFeature Feature(CadPoint point, string id, string name, string far) =>
        new(
            new PointEntity(point),
            new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
            {
                ["ID"] = id,
                ["NAME"] = name,
                ["FAR"] = far
            });
}

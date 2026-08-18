using UCAD.Core.Entities;
using UCAD.Core.Geometry;
using UCAD.Core.Gis;
using Xunit;

namespace UCAD.Core.Tests;

public sealed class ShapefileDocumentBuilderTests
{
    private static readonly CadDbfFieldDefinition[] Fields =
    [
        new("LAYER", CadDbfFieldType.Character, 32),
        new("COLOR", CadDbfFieldType.Character, 12),
        new("LWEIGHT", CadDbfFieldType.Numeric, 10, 2),
        new("LTYPE", CadDbfFieldType.Character, 16)
    ];

    [Fact]
    public void OneToOneRecordsRestoreCadLayerAndEntityProperties()
    {
        var package = CadShapefilePackage.Export(
        [
            Feature(new CadPoint(0, 0), "PARCEL", "#336699", "0.35", "Dashed"),
            Feature(new CadPoint(10, 20), "ROAD", "ByLayer", "", "Center")
        ],
        Fields,
        CadCoordinateReferenceSystem.LocalPlanar);
        var imported = CadShapefilePackage.Import(
            package.ShpContent,
            package.ShxContent,
            package.DbfContent,
            package.CpgContent);

        var built = CadShapefileDocumentBuilder.Build(imported);

        Assert.Equal(2, built.Document.Entities.Count);
        Assert.True(built.Document.TryGetLayer("PARCEL", out _));
        Assert.True(built.Document.TryGetLayer("ROAD", out _));
        var first = built.Document.GetEntityProperties(built.Document.Entities[0].Id);
        Assert.Equal("PARCEL", first.LayerName);
        Assert.Equal("#336699", first.ColorHex);
        Assert.Equal(0.35, first.LineWeight);
        Assert.Equal("Dashed", first.LineType);
        var second = built.Document.GetEntityProperties(built.Document.Entities[1].Id);
        Assert.Equal("ROAD", second.LayerName);
        Assert.Null(second.ColorHex);
        Assert.Null(second.LineWeight);
        Assert.Equal("Center", second.LineType);
        Assert.False(built.Document.CanUndo);
        Assert.False(built.Document.CanRedo);
    }

    [Fact]
    public void RecordMismatchUsesSafeGisLayerInsteadOfGuessingAttributes()
    {
        var shp = CadShapefileGeometryCodec.Export([
            new PointEntity(new CadPoint(0, 0)),
            new PointEntity(new CadPoint(1, 1))
        ]).ShpContent;
        var dbf = CadDbfCodec.Export(new CadDbfTable(
            Fields,
            [new CadDbfRecord(new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
            {
                ["LAYER"] = "SHOULD_NOT_GUESS",
                ["COLOR"] = "#FF0000",
                ["LWEIGHT"] = "0.5",
                ["LTYPE"] = "Dashed"
            })]));
        var imported = CadShapefilePackage.Import(shp, default, dbf, CadDbfCodec.CreateCpgUtf8());

        var built = CadShapefileDocumentBuilder.Build(imported);

        Assert.Equal(2, built.Document.Entities.Count);
        Assert.All(built.Document.Entities, entity =>
        {
            var properties = built.Document.GetEntityProperties(entity.Id);
            Assert.Equal("GIS", properties.LayerName);
            Assert.Null(properties.ColorHex);
            Assert.Null(properties.LineWeight);
        });
        Assert.True(built.Warnings.Any(warning => warning.Contains("not guessed", StringComparison.OrdinalIgnoreCase)));
        Assert.False(built.Document.CanUndo);
    }

    private static CadShapefileFeature Feature(CadPoint point, string layer, string color, string weight, string lineType) =>
        new(
            new PointEntity(point),
            new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
            {
                ["LAYER"] = layer,
                ["COLOR"] = color,
                ["LWEIGHT"] = weight,
                ["LTYPE"] = lineType
            });
}

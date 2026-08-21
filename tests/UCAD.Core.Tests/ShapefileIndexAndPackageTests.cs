using System.Buffers.Binary;
using UCAD.Core.Entities;
using UCAD.Core.Geometry;
using UCAD.Core.Gis;
using Xunit;

namespace UCAD.Core.Tests;

public sealed class ShapefileIndexAndPackageTests
{
    private static readonly CadDbfFieldDefinition[] Fields =
    [
        new("ID", CadDbfFieldType.Character, 16)
    ];

    [Fact]
    public void PackageExportsConsistentShxIndex()
    {
        var package = CadShapefilePackage.Export(
        [
            Feature(new CadPoint(0, 0), "P1"),
            Feature(new CadPoint(10, 20), "P2"),
            Feature(new CadPoint(30, 40), "P3")
        ],
        Fields,
        CadCoordinateReferenceSystem.WebMercator);

        var validation = CadShapefileIndexCodec.Validate(package.ShpContent, package.ShxContent);
        var imported = CadShapefilePackage.Import(
            package.ShpContent,
            package.ShxContent,
            package.DbfContent,
            package.CpgContent,
            package.PrjContent ?? []);

        Assert.True(validation.IsConsistent);
        Assert.Equal(3, validation.RecordCount);
        Assert.NotNull(imported.IndexValidation);
        Assert.True(imported.IndexValidation!.IsConsistent);
        Assert.True(imported.Bundle.CanMapRecordsOneToOne);
        Assert.Equal(CadCoordinateReferenceSystem.WebMercator, imported.Bundle.IdentifiedCrs);
        Assert.Empty(imported.Warnings);
    }

    [Fact]
    public void TamperedShxIsReportedAndDoesNotControlGeometryReading()
    {
        var package = CadShapefilePackage.Export(
        [
            Feature(new CadPoint(1, 2), "P1"),
            Feature(new CadPoint(3, 4), "P2")
        ],
        Fields);
        var tampered = package.ShxContent.ToArray();
        BinaryPrimitives.WriteInt32BigEndian(tampered.AsSpan(100, 4), 999999);

        var imported = CadShapefilePackage.Import(
            package.ShpContent,
            tampered,
            package.DbfContent,
            package.CpgContent);

        Assert.NotNull(imported.IndexValidation);
        Assert.False(imported.IndexValidation!.IsConsistent);
        Assert.Equal(2, imported.Bundle.Geometry.Entities.Count);
        Assert.True(imported.Warnings.Any(warning => warning.Contains("ignored the index", StringComparison.OrdinalIgnoreCase)));
    }

    [Fact]
    public void MissingShxFallsBackToSequentialShpWithWarning()
    {
        var package = CadShapefilePackage.Export(
            [Feature(new CadPoint(1, 2), "P1")],
            Fields);

        var imported = CadShapefilePackage.Import(
            package.ShpContent,
            shxContent: default,
            dbfContent: package.DbfContent,
            cpgContent: package.CpgContent);

        Assert.Null(imported.IndexValidation);
        Assert.Single(imported.Bundle.Geometry.Entities);
        Assert.True(imported.Warnings.Any(warning => warning.Contains("No SHX", StringComparison.OrdinalIgnoreCase)));
    }

    private static CadShapefileFeature Feature(CadPoint point, string id) =>
        new(
            new PointEntity(point),
            new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
            {
                ["ID"] = id
            });
}

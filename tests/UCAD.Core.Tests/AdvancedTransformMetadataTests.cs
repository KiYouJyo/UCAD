using UCAD.Core.Entities;
using UCAD.Core.Geometry;
using UCAD.Core.Modify;
using Xunit;

namespace UCAD.Core.Tests;

public sealed class AdvancedTransformMetadataTests
{
    [Fact]
    public void InPlaceHatchTransformPreservesAssociationAndIslands()
    {
        var outer = new PolylineEntity([
            new CadPoint(0, 0), new CadPoint(100, 0), new CadPoint(100, 100), new CadPoint(0, 100)
        ], closed: true);
        var island = new PolylineEntity([
            new CadPoint(25, 25), new CadPoint(75, 25), new CadPoint(75, 75), new CadPoint(25, 75)
        ], closed: true);
        var hatch = CadHatchFactory.CreateFromClosedPolyline(
            outer,
            "ANSI31",
            2,
            Math.PI / 6,
            [island],
            associative: true,
            islandDetection: HatchIslandDetection.Outer);

        var moved = Assert.IsType<HatchEntity>(CadEntityTransform.Translate(hatch, new CadVector(10, 20)));

        Assert.Equal(hatch.Id, moved.Id);
        Assert.True(moved.Associative);
        Assert.Equal(hatch.SourceEntityIds, moved.SourceEntityIds);
        Assert.Equal(HatchIslandDetection.Outer, moved.IslandDetection);
        Assert.Single(moved.Islands);
        Assert.Equal(new CadPoint(10, 20), moved.Boundary[0]);
        Assert.Equal(new CadPoint(35, 45), moved.Islands[0][0]);
    }

    [Fact]
    public void CopyHatchKeepsGeometryMetadataButDropsUnsafeAssociation()
    {
        var outer = new PolylineEntity([
            new CadPoint(0, 0), new CadPoint(40, 0), new CadPoint(40, 40), new CadPoint(0, 40)
        ], closed: true);
        var island = new PolylineEntity([
            new CadPoint(10, 10), new CadPoint(20, 10), new CadPoint(20, 20), new CadPoint(10, 20)
        ], closed: true);
        var hatch = CadHatchFactory.CreateFromClosedPolyline(
            outer,
            "Solid",
            1,
            0,
            [island],
            associative: true);

        var copied = Assert.IsType<HatchEntity>(CadEntityTransform.Translate(hatch, new CadVector(100, 0), preserveIdentity: false));

        Assert.NotEqual(hatch.Id, copied.Id);
        Assert.False(copied.Associative);
        Assert.Empty(copied.SourceEntityIds);
        Assert.Single(copied.Islands);
        Assert.Equal(hatch.Pattern, copied.Pattern);
        Assert.Equal(new CadPoint(100, 0), copied.Boundary[0]);
    }

    [Fact]
    public void BlockReferenceCopyPreservesInstanceAttributes()
    {
        var child = new LineEntity(new CadPoint(0, 0), new CadPoint(10, 0));
        var reference = new BlockReferenceEntity(
            "ParcelLabel",
            new CadPoint(5, 5),
            [child],
            1,
            0,
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["PARCEL_ID"] = "A-01",
                ["FAR"] = "2.50"
            });

        var copied = Assert.IsType<BlockReferenceEntity>(
            CadEntityTransform.Translate(reference, new CadVector(25, 0), preserveIdentity: false));

        Assert.NotEqual(reference.Id, copied.Id);
        Assert.Equal("A-01", copied.AttributeValues["PARCEL_ID"]);
        Assert.Equal("2.50", copied.AttributeValues["FAR"]);
        Assert.Equal(new CadPoint(30, 5), copied.InsertionPoint);
    }
}

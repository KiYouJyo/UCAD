using UCAD.Core.Entities;
using UCAD.Core.Geometry;
using UCAD.Core.IO;
using Xunit;

namespace UCAD.Core.Tests;

public sealed class CurrentNativeMetadataTests
{
    [Fact]
    public void AssociativeHatchSourcesAreReboundToRestoredEntityIds()
    {
        var document = new CadDocument();
        var boundary = new PolylineEntity([
            new CadPoint(0, 0),
            new CadPoint(10, 0),
            new CadPoint(10, 10),
            new CadPoint(0, 10)
        ], closed: true);
        document.Add(boundary);
        var hatch = CadHatchFactory.CreateFromClosedPolyline(
            boundary,
            "Solid",
            1,
            0,
            associative: true);
        document.Add(hatch);

        var originalBoundaryId = boundary.Id;
        var json = CadNativeDocumentCodecCurrent.Serialize(document);
        var restored = CadNativeDocumentCodecCurrent.Deserialize(json);

        Assert.True(CadNativeDocumentCodecCurrent.HasCurrentExtension(json));
        var restoredBoundary = Assert.IsType<PolylineEntity>(restored.Entities[0]);
        var restoredHatch = Assert.IsType<HatchEntity>(restored.Entities[1]);
        Assert.NotEqual(originalBoundaryId, restoredBoundary.Id);
        Assert.Single(restoredHatch.SourceEntityIds);
        Assert.Equal(restoredBoundary.Id, restoredHatch.SourceEntityIds[0]);
        Assert.True(restoredHatch.Associative);
    }

    [Fact]
    public void MissingAssociativeSourceCannotBeSerializedSilently()
    {
        var document = new CadDocument();
        var hatch = new HatchEntity(
            [new CadPoint(0, 0), new CadPoint(10, 0), new CadPoint(10, 10), new CadPoint(0, 10)],
            "Solid",
            1,
            0,
            islands: null,
            associative: true,
            sourceEntityIds: [Guid.NewGuid()]);
        document.Add(hatch);

        Assert.Throws<InvalidOperationException>(() => CadNativeDocumentCodecCurrent.Serialize(document));
    }
}
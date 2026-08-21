using UCAD.Core.Entities;
using UCAD.Core.Geometry;
using UCAD.Core.Modify;
using Xunit;

namespace UCAD.Core.Tests;

public sealed class AssociativeHatchBehaviorTests
{
    [Fact]
    public void ReplacingSourcePolylineRefreshesAssociativeHatchBoundary()
    {
        var document = new CadDocument();
        var boundary = new PolylineEntity([
            new CadPoint(0, 0),
            new CadPoint(10, 0),
            new CadPoint(10, 10),
            new CadPoint(0, 10)
        ], closed: true);
        document.Add(boundary);
        var hatch = CadHatchFactory.CreateFromClosedPolyline(boundary, "Solid", 1, 0, associative: true);
        document.Add(hatch);

        var movedBoundary = Assert.IsType<PolylineEntity>(CadEntityTransform.Translate(boundary, new CadVector(5, 7)));
        document.ReplaceRange([movedBoundary]);

        var refreshed = Assert.IsType<HatchEntity>(document.Entities.Single(entity => entity.Id == hatch.Id));
        Assert.True(refreshed.Associative);
        Assert.Equal(movedBoundary.Points, refreshed.Boundary);
        Assert.Single(refreshed.SourceEntityIds);
        Assert.Equal(boundary.Id, refreshed.SourceEntityIds[0]);
    }

    [Fact]
    public void RemovingSourceDisassociatesButKeepsLastValidGeometry()
    {
        var document = new CadDocument();
        var boundary = new PolylineEntity([
            new CadPoint(0, 0),
            new CadPoint(20, 0),
            new CadPoint(20, 20),
            new CadPoint(0, 20)
        ], closed: true);
        document.Add(boundary);
        var hatch = CadHatchFactory.CreateFromClosedPolyline(boundary, "Solid", 1, 0, associative: true);
        document.Add(hatch);
        var lastBoundary = hatch.Boundary.ToArray();

        Assert.True(document.Remove(boundary.Id));

        var detached = Assert.IsType<HatchEntity>(document.Entities.Single(entity => entity.Id == hatch.Id));
        Assert.False(detached.Associative);
        Assert.Empty(detached.SourceEntityIds);
        Assert.Equal(lastBoundary, detached.Boundary);
    }

    [Fact]
    public void UndoRestoresAssociationAndSourceGeometryTogether()
    {
        var document = new CadDocument();
        var boundary = new PolylineEntity([
            new CadPoint(0, 0),
            new CadPoint(12, 0),
            new CadPoint(12, 8),
            new CadPoint(0, 8)
        ], closed: true);
        document.Add(boundary);
        var hatch = CadHatchFactory.CreateFromClosedPolyline(boundary, "Solid", 1, 0, associative: true);
        document.Add(hatch);
        document.ResetHistory();

        Assert.True(document.Remove(boundary.Id));
        Assert.False(Assert.IsType<HatchEntity>(document.Entities.Single()).Associative);

        Assert.True(document.Undo());
        var restoredBoundary = Assert.IsType<PolylineEntity>(document.Entities.First(entity => entity.Id == boundary.Id));
        var restoredHatch = Assert.IsType<HatchEntity>(document.Entities.First(entity => entity.Id == hatch.Id));
        Assert.True(restoredHatch.Associative);
        Assert.Equal(restoredBoundary.Points, restoredHatch.Boundary);
        Assert.Equal(restoredBoundary.Id, Assert.Single(restoredHatch.SourceEntityIds));
    }
}

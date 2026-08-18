using UCAD.Core.Entities;
using UCAD.Core.Geometry;
using UCAD.Core.Modify;
using UCAD.Core.Planning;
using Xunit;

namespace UCAD.Core.Tests;

public sealed class PlanningParcelTableTests
{
    [Fact]
    public void ResolveRecomputesIndicatorsFromCurrentBoundaryGeometry()
    {
        var document = new CadDocument();
        var boundary = new PolylineEntity([
            new CadPoint(0, 0),
            new CadPoint(10, 0),
            new CadPoint(10, 10),
            new CadPoint(0, 10)
        ], closed: true);
        document.Add(boundary);
        var table = new CadPlanningParcelTable();
        table.Set(new CadPlanningParcelRecord(
            boundary.Id,
            new CadPlanningParcelData("A-01", "R2", "Residential"),
            new CadParcelIndicatorInput(
                AreaScale: 1,
                GrossFloorArea: 200,
                BuildingFootprintArea: 25,
                GreenArea: 35,
                ProposedHeight: 24),
            new CadPlanningControls(
                MaximumFloorAreaRatio: 3,
                MaximumBuildingDensityPercent: 40,
                MinimumGreenRatioPercent: 30,
                MaximumHeight: 30)));

        var first = table.Resolve(document, boundary.Id);
        Assert.Equal(100, first.Indicators.ParcelArea, 8);
        Assert.Equal(2, first.Indicators.FloorAreaRatio, 8);
        Assert.True(first.Evaluation.Passes);

        var scaled = Assert.IsType<PolylineEntity>(
            CadEntityTransform.Scale(boundary, new CadPoint(0, 0), 2));
        document.Replace(boundary.Id, [scaled]);

        var second = table.Resolve(document, boundary.Id);
        Assert.Equal(boundary.Id, second.Boundary.Id);
        Assert.Equal(400, second.Indicators.ParcelArea, 8);
        Assert.Equal(0.5, second.Indicators.FloorAreaRatio, 8);
        Assert.Equal(6.25, second.Indicators.BuildingDensityPercent, 8);
        Assert.Equal(8.75, second.Indicators.GreenRatioPercent, 8);
        Assert.False(second.Evaluation.Passes);
        Assert.Single(second.Evaluation.Failures);
        Assert.Equal(CadPlanningControlKind.GreenRatio, second.Evaluation.Failures[0].Kind);
    }

    [Fact]
    public void DuplicateParcelIdIsRejectedAcrossDifferentBoundaries()
    {
        var table = new CadPlanningParcelTable();
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        var input = new CadParcelIndicatorInput(1, 0, 0, 0);
        var controls = new CadPlanningControls();
        table.Set(new CadPlanningParcelRecord(first, new CadPlanningParcelData("A-01", "R2"), input, controls));

        Assert.Throws<InvalidOperationException>(() =>
            table.Set(new CadPlanningParcelRecord(second, new CadPlanningParcelData("a-01", "B1"), input, controls)));
    }

    [Fact]
    public void OrphansAreDetectedAndCanBeRemovedWithoutTouchingCadGeometry()
    {
        var document = new CadDocument();
        var boundary = new PolylineEntity([
            new CadPoint(0, 0), new CadPoint(10, 0), new CadPoint(10, 10), new CadPoint(0, 10)
        ], closed: true);
        document.Add(boundary);
        var table = new CadPlanningParcelTable();
        table.Set(new CadPlanningParcelRecord(
            boundary.Id,
            new CadPlanningParcelData("A-01", "R2"),
            new CadParcelIndicatorInput(1, 0, 0, 0),
            new CadPlanningControls()));

        document.Remove(boundary.Id);

        var orphan = Assert.Single(table.FindOrphans(document));
        Assert.Equal(boundary.Id, orphan.BoundaryEntityId);
        Assert.Equal(1, table.RemoveOrphans(document));
        Assert.Equal(0, table.Count);
        Assert.Empty(document.Entities);
    }
}

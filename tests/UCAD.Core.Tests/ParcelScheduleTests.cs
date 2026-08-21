using UCAD.Core.Planning;
using Xunit;

namespace UCAD.Core.Tests;

public sealed class ParcelScheduleTests
{
    [Fact]
    public void ScheduleAggregatesOverallAndLandUseIndicators()
    {
        var parcels = new[]
        {
            Snapshot("A-01", "R2", "Residential", 1000, 2500, 300, 350),
            Snapshot("A-02", "R2", "Residential", 2000, 4000, 500, 700),
            Snapshot("B-01", "B1", "Commercial", 1000, 3000, 450, 200)
        };

        var schedule = CadParcelSchedule.Build(parcels);

        Assert.Equal(3, schedule.Parcels.Count);
        Assert.Equal(2, schedule.Groups.Count);
        Assert.Equal(4000, schedule.TotalParcelArea, 8);
        Assert.Equal(9500, schedule.TotalGrossFloorArea, 8);
        Assert.Equal(2.375, schedule.OverallFloorAreaRatio, 8);
        Assert.Equal(31.25, schedule.OverallBuildingDensityPercent, 8);
        Assert.Equal(31.25, schedule.OverallGreenRatioPercent, 8);

        var residential = schedule.Groups.Single(group => group.LandUseCode == "R2");
        Assert.Equal(2, residential.ParcelCount);
        Assert.Equal(3000, residential.ParcelArea, 8);
        Assert.Equal(6500, residential.GrossFloorArea, 8);
        Assert.Equal("Residential", residential.LandUseName);
    }

    [Fact]
    public void EmptyScheduleReturnsZeroTotals()
    {
        var schedule = CadParcelSchedule.Build([]);

        Assert.Empty(schedule.Parcels);
        Assert.Empty(schedule.Groups);
        Assert.Equal(0, schedule.TotalParcelArea);
        Assert.Equal(0, schedule.OverallFloorAreaRatio);
    }

    private static CadPlanningParcelSnapshot Snapshot(
        string id,
        string code,
        string name,
        double area,
        double gfa,
        double footprint,
        double green) =>
        new(
            new CadPlanningParcelData(id, code, name),
            new CadParcelIndicators(
                area,
                gfa,
                footprint,
                green,
                gfa / area,
                footprint / area * 100,
                green / area * 100,
                null));
}

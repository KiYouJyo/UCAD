using UCAD.Core.Planning;
using Xunit;

namespace UCAD.Core.Tests;

public sealed class ParcelScheduleCsvTests
{
    [Fact]
    public void CsvUsesLocalizedHeadersInvariantNumbersAndEscaping()
    {
        var parcel = new CadPlanningParcelSnapshot(
            new CadPlanningParcelData("A,01", "R2", "居住\"混合"),
            new CadParcelIndicators(1000.5, 2500.25, 300, 350, 2.498, 29.985, 34.982, 36),
            new CadPlanningControls(MaximumFloorAreaRatio: 3));
        var schedule = CadParcelSchedule.Build([parcel]);
        var headers = new CadParcelScheduleCsvHeaders(
            ParcelId: "地块",
            LandUseCode: "用地代码",
            LandUseName: "用地名称",
            ParcelArea: "面积",
            GrossFloorArea: "建筑面积",
            FloorAreaRatio: "容积率",
            BuildingDensity: "建筑密度",
            GreenRatio: "绿地率",
            ProposedHeight: "高度",
            Compliance: "符合性");

        var csv = CadParcelScheduleCsv.Export(schedule, headers);

        Assert.True(csv.StartsWith("地块,用地代码,用地名称,面积", StringComparison.Ordinal));
        Assert.True(csv.Contains("\"A,01\"", StringComparison.Ordinal));
        Assert.True(csv.Contains("\"居住\"\"混合\"", StringComparison.Ordinal));
        Assert.True(csv.Contains("1000.5", StringComparison.Ordinal));
        Assert.True(csv.Contains(",PASS", StringComparison.Ordinal));
        Assert.True(csv.EndsWith("\r\n", StringComparison.Ordinal));
    }
}

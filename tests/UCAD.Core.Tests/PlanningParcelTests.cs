using UCAD.Core.Entities;
using UCAD.Core.Geometry;
using UCAD.Core.Planning;
using Xunit;

namespace UCAD.Core.Tests;

public sealed class PlanningParcelTests
{
    [Fact]
    public void ParcelIndicatorsCalculateFarDensityAndGreenRatio()
    {
        var boundary = new PolylineEntity([
            new CadPoint(0, 0),
            new CadPoint(100000, 0),
            new CadPoint(100000, 50000),
            new CadPoint(0, 50000)
        ], closed: true);
        var input = new CadParcelIndicatorInput(
            AreaScale: 1.0 / 1_000_000.0,
            GrossFloorArea: 12500,
            BuildingFootprintArea: 1750,
            GreenArea: 1500,
            ProposedHeight: 36);

        var indicators = CadParcelIndicators.Calculate(boundary, input);

        Assert.Equal(5000, indicators.ParcelArea, 8);
        Assert.Equal(2.5, indicators.FloorAreaRatio, 8);
        Assert.Equal(35, indicators.BuildingDensityPercent, 8);
        Assert.Equal(30, indicators.GreenRatioPercent, 8);
        Assert.Equal(36, indicators.ProposedHeight);
    }

    [Fact]
    public void ControlEvaluationReportsOnlyFailedLimits()
    {
        var indicators = new CadParcelIndicators(
            ParcelArea: 5000,
            GrossFloorArea: 12500,
            BuildingFootprintArea: 1750,
            GreenArea: 1500,
            FloorAreaRatio: 2.5,
            BuildingDensityPercent: 35,
            GreenRatioPercent: 30,
            ProposedHeight: 36);
        var controls = new CadPlanningControls(
            MaximumFloorAreaRatio: 2.4,
            MaximumBuildingDensityPercent: 40,
            MinimumGreenRatioPercent: 35,
            MaximumHeight: 40);

        var evaluation = CadPlanningControlEvaluation.Evaluate(indicators, controls);

        Assert.False(evaluation.Passes);
        Assert.Equal(2, evaluation.Failures.Count);
        Assert.True(evaluation.Failures.Any(check => check.Kind == CadPlanningControlKind.FloorAreaRatio));
        Assert.True(evaluation.Failures.Any(check => check.Kind == CadPlanningControlKind.GreenRatio));
    }

    [Fact]
    public void PlanningLabelUsesParcelCentroidAndLocalizedLabelsFromOptions()
    {
        var boundary = new PolylineEntity([
            new CadPoint(0, 0),
            new CadPoint(10000, 0),
            new CadPoint(10000, 10000),
            new CadPoint(0, 10000)
        ], closed: true);
        var indicators = CadParcelIndicators.Calculate(
            boundary,
            new CadParcelIndicatorInput(1.0 / 1_000_000.0, 250, 30, 35, 24));
        var parcel = new CadPlanningParcelData("A-01", "R2", "居住用地");
        var options = new CadPlanningParcelLabelOptions(
            ParcelIdLabel: "地块",
            LandUseLabel: "用地",
            AreaLabel: "面积",
            FarLabel: "容积率",
            DensityLabel: "建筑密度",
            GreenLabel: "绿地率");

        var label = CadPlanningParcelLabelFactory.Create(boundary, parcel, indicators, options);

        Assert.Equal(new CadPoint(5000, 5000), label.Position);
        Assert.True(label.Text.Contains("地块: A-01", StringComparison.Ordinal));
        Assert.True(label.Text.Contains("用地: R2 · 居住用地", StringComparison.Ordinal));
        Assert.True(label.Text.Contains("容积率: 2.50", StringComparison.Ordinal));
        Assert.True(label.Text.Contains("绿地率: 35.00%", StringComparison.Ordinal));
    }

    [Fact]
    public void ParcelRejectsFootprintGreaterThanParcelArea()
    {
        var boundary = new PolylineEntity([
            new CadPoint(0, 0), new CadPoint(10, 0), new CadPoint(10, 10), new CadPoint(0, 10)
        ], closed: true);
        var input = new CadParcelIndicatorInput(1, 0, 101, 0);

        Assert.Throws<ArgumentException>(() => CadParcelIndicators.Calculate(boundary, input));
    }
}

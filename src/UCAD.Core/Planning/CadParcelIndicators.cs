using UCAD.Core.Architecture;
using UCAD.Core.Entities;

namespace UCAD.Core.Planning;

public sealed record CadParcelIndicatorInput(
    double AreaScale,
    double GrossFloorArea,
    double BuildingFootprintArea,
    double GreenArea,
    double? ProposedHeight = null)
{
    public CadParcelIndicatorInput Validate()
    {
        if (!double.IsFinite(AreaScale) || AreaScale <= 0) throw new ArgumentOutOfRangeException(nameof(AreaScale));
        ValidateNonNegative(GrossFloorArea, nameof(GrossFloorArea));
        ValidateNonNegative(BuildingFootprintArea, nameof(BuildingFootprintArea));
        ValidateNonNegative(GreenArea, nameof(GreenArea));
        if (ProposedHeight is double height) ValidateNonNegative(height, nameof(ProposedHeight));
        return this;
    }

    private static void ValidateNonNegative(double value, string name)
    {
        if (!double.IsFinite(value) || value < 0) throw new ArgumentOutOfRangeException(name);
    }
}

public sealed record CadParcelIndicators(
    double ParcelArea,
    double GrossFloorArea,
    double BuildingFootprintArea,
    double GreenArea,
    double FloorAreaRatio,
    double BuildingDensityPercent,
    double GreenRatioPercent,
    double? ProposedHeight)
{
    public static CadParcelIndicators Calculate(PolylineEntity parcelBoundary, CadParcelIndicatorInput input)
    {
        ArgumentNullException.ThrowIfNull(parcelBoundary);
        input = (input ?? throw new ArgumentNullException(nameof(input))).Validate();
        var parcelArea = CadClosedPolylineMetrics.Measure(parcelBoundary).Area * input.AreaScale;
        if (parcelArea <= 1e-12) throw new ArgumentException("Parcel area must be positive.", nameof(parcelBoundary));
        if (input.BuildingFootprintArea > parcelArea + 1e-9)
            throw new ArgumentException("Building footprint area cannot exceed parcel area.", nameof(input));
        if (input.GreenArea > parcelArea + 1e-9)
            throw new ArgumentException("Green area cannot exceed parcel area.", nameof(input));

        return new CadParcelIndicators(
            parcelArea,
            input.GrossFloorArea,
            input.BuildingFootprintArea,
            input.GreenArea,
            input.GrossFloorArea / parcelArea,
            (input.BuildingFootprintArea / parcelArea) * 100.0,
            (input.GreenArea / parcelArea) * 100.0,
            input.ProposedHeight);
    }
}

public sealed record CadPlanningControls(
    double? MaximumFloorAreaRatio = null,
    double? MaximumBuildingDensityPercent = null,
    double? MinimumGreenRatioPercent = null,
    double? MaximumHeight = null)
{
    public CadPlanningControls Validate()
    {
        ValidateOptionalNonNegative(MaximumFloorAreaRatio, nameof(MaximumFloorAreaRatio));
        ValidateOptionalPercentage(MaximumBuildingDensityPercent, nameof(MaximumBuildingDensityPercent));
        ValidateOptionalPercentage(MinimumGreenRatioPercent, nameof(MinimumGreenRatioPercent));
        ValidateOptionalNonNegative(MaximumHeight, nameof(MaximumHeight));
        return this;
    }

    private static void ValidateOptionalNonNegative(double? value, string name)
    {
        if (value is double number && (!double.IsFinite(number) || number < 0)) throw new ArgumentOutOfRangeException(name);
    }

    private static void ValidateOptionalPercentage(double? value, string name)
    {
        if (value is double number && (!double.IsFinite(number) || number is < 0 or > 100)) throw new ArgumentOutOfRangeException(name);
    }
}

public enum CadPlanningControlKind
{
    FloorAreaRatio,
    BuildingDensity,
    GreenRatio,
    Height
}

public sealed record CadPlanningControlCheck(
    CadPlanningControlKind Kind,
    bool Passes,
    double Actual,
    double Limit,
    bool MinimumRequirement);

public sealed record CadPlanningControlEvaluation(IReadOnlyList<CadPlanningControlCheck> Checks)
{
    public bool Passes => Checks.All(check => check.Passes);
    public IReadOnlyList<CadPlanningControlCheck> Failures => Checks.Where(check => !check.Passes).ToArray();

    public static CadPlanningControlEvaluation Evaluate(CadParcelIndicators indicators, CadPlanningControls controls)
    {
        ArgumentNullException.ThrowIfNull(indicators);
        controls = (controls ?? throw new ArgumentNullException(nameof(controls))).Validate();
        var checks = new List<CadPlanningControlCheck>();
        if (controls.MaximumFloorAreaRatio is double far)
            checks.Add(new CadPlanningControlCheck(CadPlanningControlKind.FloorAreaRatio, indicators.FloorAreaRatio <= far + 1e-9, indicators.FloorAreaRatio, far, false));
        if (controls.MaximumBuildingDensityPercent is double density)
            checks.Add(new CadPlanningControlCheck(CadPlanningControlKind.BuildingDensity, indicators.BuildingDensityPercent <= density + 1e-9, indicators.BuildingDensityPercent, density, false));
        if (controls.MinimumGreenRatioPercent is double green)
            checks.Add(new CadPlanningControlCheck(CadPlanningControlKind.GreenRatio, indicators.GreenRatioPercent + 1e-9 >= green, indicators.GreenRatioPercent, green, true));
        if (controls.MaximumHeight is double height && indicators.ProposedHeight is double actualHeight)
            checks.Add(new CadPlanningControlCheck(CadPlanningControlKind.Height, actualHeight <= height + 1e-9, actualHeight, height, false));
        return new CadPlanningControlEvaluation(checks.AsReadOnly());
    }
}

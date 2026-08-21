namespace UCAD.Core.Planning;

public sealed record CadPlanningParcelSnapshot(
    CadPlanningParcelData Parcel,
    CadParcelIndicators Indicators,
    CadPlanningControls? Controls = null)
{
    public CadPlanningParcelSnapshot Validate()
    {
        (Parcel ?? throw new ArgumentNullException(nameof(Parcel))).Validate();
        ArgumentNullException.ThrowIfNull(Indicators);
        Controls?.Validate();
        return this;
    }
}

public sealed record CadParcelScheduleGroup(
    string LandUseCode,
    string? LandUseName,
    int ParcelCount,
    double ParcelArea,
    double GrossFloorArea,
    double BuildingFootprintArea,
    double GreenArea,
    double FloorAreaRatio,
    double BuildingDensityPercent,
    double GreenRatioPercent);

public sealed record CadParcelSchedule(
    IReadOnlyList<CadPlanningParcelSnapshot> Parcels,
    IReadOnlyList<CadParcelScheduleGroup> Groups,
    double TotalParcelArea,
    double TotalGrossFloorArea,
    double TotalBuildingFootprintArea,
    double TotalGreenArea,
    double OverallFloorAreaRatio,
    double OverallBuildingDensityPercent,
    double OverallGreenRatioPercent)
{
    public static CadParcelSchedule Build(IEnumerable<CadPlanningParcelSnapshot> parcels)
    {
        ArgumentNullException.ThrowIfNull(parcels);
        var snapshot = parcels.Select(parcel => parcel.Validate()).ToArray();
        if (snapshot.Length == 0)
            return new CadParcelSchedule([], [], 0, 0, 0, 0, 0, 0, 0);

        var totalArea = snapshot.Sum(parcel => parcel.Indicators.ParcelArea);
        var totalGfa = snapshot.Sum(parcel => parcel.Indicators.GrossFloorArea);
        var totalFootprint = snapshot.Sum(parcel => parcel.Indicators.BuildingFootprintArea);
        var totalGreen = snapshot.Sum(parcel => parcel.Indicators.GreenArea);
        if (totalArea <= 1e-12) throw new InvalidOperationException("Parcel schedule total area must be positive.");

        var groups = snapshot
            .GroupBy(parcel => parcel.Parcel.LandUseCode.Trim(), StringComparer.OrdinalIgnoreCase)
            .Select(group => BuildGroup(group.Key, group.ToArray()))
            .OrderBy(group => group.LandUseCode, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return new CadParcelSchedule(
            Array.AsReadOnly(snapshot),
            Array.AsReadOnly(groups),
            totalArea,
            totalGfa,
            totalFootprint,
            totalGreen,
            totalGfa / totalArea,
            (totalFootprint / totalArea) * 100.0,
            (totalGreen / totalArea) * 100.0);
    }

    private static CadParcelScheduleGroup BuildGroup(string code, IReadOnlyList<CadPlanningParcelSnapshot> parcels)
    {
        var area = parcels.Sum(parcel => parcel.Indicators.ParcelArea);
        var gfa = parcels.Sum(parcel => parcel.Indicators.GrossFloorArea);
        var footprint = parcels.Sum(parcel => parcel.Indicators.BuildingFootprintArea);
        var green = parcels.Sum(parcel => parcel.Indicators.GreenArea);
        var names = parcels
            .Select(parcel => parcel.Parcel.LandUseName?.Trim())
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        return new CadParcelScheduleGroup(
            code,
            names.Length == 1 ? names[0] : null,
            parcels.Count,
            area,
            gfa,
            footprint,
            green,
            area <= 1e-12 ? 0 : gfa / area,
            area <= 1e-12 ? 0 : (footprint / area) * 100.0,
            area <= 1e-12 ? 0 : (green / area) * 100.0);
    }
}

using System.Globalization;
using UCAD.Core.Architecture;
using UCAD.Core.Entities;

namespace UCAD.Core.Planning;

public sealed record CadPlanningParcelData(
    string ParcelId,
    string LandUseCode,
    string? LandUseName = null,
    string? Notes = null)
{
    public CadPlanningParcelData Validate()
    {
        if (string.IsNullOrWhiteSpace(ParcelId)) throw new ArgumentException("Parcel ID cannot be empty.", nameof(ParcelId));
        if (string.IsNullOrWhiteSpace(LandUseCode)) throw new ArgumentException("Land-use code cannot be empty.", nameof(LandUseCode));
        return this;
    }
}

public sealed record CadPlanningParcelLabelOptions(
    double TextHeight = 250,
    double Width = 3000,
    int AreaPrecision = 1,
    int RatioPrecision = 2,
    string AreaSuffix = "m²",
    string TextStyleName = "Standard",
    string ParcelIdLabel = "Parcel",
    string LandUseLabel = "Land use",
    string AreaLabel = "Area",
    string FarLabel = "FAR",
    string DensityLabel = "Density",
    string GreenLabel = "Green")
{
    public CadPlanningParcelLabelOptions Validate()
    {
        if (!double.IsFinite(TextHeight) || TextHeight <= 0) throw new ArgumentOutOfRangeException(nameof(TextHeight));
        if (!double.IsFinite(Width) || Width <= 0) throw new ArgumentOutOfRangeException(nameof(Width));
        if (AreaPrecision is < 0 or > 12) throw new ArgumentOutOfRangeException(nameof(AreaPrecision));
        if (RatioPrecision is < 0 or > 12) throw new ArgumentOutOfRangeException(nameof(RatioPrecision));
        if (string.IsNullOrWhiteSpace(TextStyleName)) throw new ArgumentException("Text style cannot be empty.", nameof(TextStyleName));
        return this;
    }
}

public static class CadPlanningParcelLabelFactory
{
    public static MTextEntity Create(
        PolylineEntity parcelBoundary,
        CadPlanningParcelData parcel,
        CadParcelIndicators indicators,
        CadPlanningParcelLabelOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(parcelBoundary);
        parcel = (parcel ?? throw new ArgumentNullException(nameof(parcel))).Validate();
        ArgumentNullException.ThrowIfNull(indicators);
        options = (options ?? new CadPlanningParcelLabelOptions()).Validate();
        var centroid = CadClosedPolylineMetrics.Measure(parcelBoundary).Centroid;
        var areaFormat = "F" + options.AreaPrecision.ToString(CultureInfo.InvariantCulture);
        var ratioFormat = "F" + options.RatioPrecision.ToString(CultureInfo.InvariantCulture);
        var landUse = string.IsNullOrWhiteSpace(parcel.LandUseName)
            ? parcel.LandUseCode.Trim()
            : parcel.LandUseCode.Trim() + " · " + parcel.LandUseName.Trim();
        var areaSuffix = string.IsNullOrWhiteSpace(options.AreaSuffix) ? string.Empty : " " + options.AreaSuffix.Trim();
        var lines = new List<string>
        {
            $"{options.ParcelIdLabel}: {parcel.ParcelId.Trim()}",
            $"{options.LandUseLabel}: {landUse}",
            $"{options.AreaLabel}: {indicators.ParcelArea.ToString(areaFormat, CultureInfo.InvariantCulture)}{areaSuffix}",
            $"{options.FarLabel}: {indicators.FloorAreaRatio.ToString(ratioFormat, CultureInfo.InvariantCulture)}",
            $"{options.DensityLabel}: {indicators.BuildingDensityPercent.ToString(ratioFormat, CultureInfo.InvariantCulture)}%",
            $"{options.GreenLabel}: {indicators.GreenRatioPercent.ToString(ratioFormat, CultureInfo.InvariantCulture)}%"
        };
        if (!string.IsNullOrWhiteSpace(parcel.Notes)) lines.Add(parcel.Notes.Trim());
        return new MTextEntity(
            centroid,
            string.Join("\n", lines),
            options.TextHeight,
            options.Width,
            rotationRadians: 0,
            styleName: options.TextStyleName);
    }
}

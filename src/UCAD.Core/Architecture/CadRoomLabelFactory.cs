using System.Globalization;
using UCAD.Core.Entities;

namespace UCAD.Core.Architecture;

public sealed record CadRoomLabelOptions(
    double AreaScale = 1,
    string AreaSuffix = "",
    int Precision = 2,
    double TextHeight = 250,
    string TextStyleName = "Standard")
{
    public CadRoomLabelOptions Validate()
    {
        if (!double.IsFinite(AreaScale) || AreaScale <= 0) throw new ArgumentOutOfRangeException(nameof(AreaScale));
        if (Precision is < 0 or > 12) throw new ArgumentOutOfRangeException(nameof(Precision));
        if (!double.IsFinite(TextHeight) || TextHeight <= 0) throw new ArgumentOutOfRangeException(nameof(TextHeight));
        if (string.IsNullOrWhiteSpace(TextStyleName)) throw new ArgumentException("Text style cannot be empty.", nameof(TextStyleName));
        return this;
    }
}

public static class CadRoomLabelFactory
{
    public static TextEntity Create(
        PolylineEntity boundary,
        string roomName,
        CadRoomLabelOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(boundary);
        if (string.IsNullOrWhiteSpace(roomName)) throw new ArgumentException("Room name cannot be empty.", nameof(roomName));
        options = (options ?? new CadRoomLabelOptions()).Validate();
        var measurement = CadClosedPolylineMetrics.Measure(boundary);
        var displayedArea = measurement.Area * options.AreaScale;
        var format = "F" + options.Precision.ToString(CultureInfo.InvariantCulture);
        var suffix = string.IsNullOrWhiteSpace(options.AreaSuffix) ? string.Empty : " " + options.AreaSuffix.Trim();
        var label = roomName.Trim() + "  " + displayedArea.ToString(format, CultureInfo.InvariantCulture) + suffix;
        return new TextEntity(
            measurement.Centroid,
            label,
            options.TextHeight,
            rotationRadians: 0,
            styleName: options.TextStyleName);
    }
}

using System.Globalization;
using System.Text;

namespace UCAD.Core.Planning;

public sealed record CadParcelScheduleCsvHeaders(
    string ParcelId = "Parcel ID",
    string LandUseCode = "Land-use code",
    string LandUseName = "Land-use name",
    string ParcelArea = "Parcel area",
    string GrossFloorArea = "Gross floor area",
    string FloorAreaRatio = "FAR",
    string BuildingDensity = "Building density (%)",
    string GreenRatio = "Green ratio (%)",
    string ProposedHeight = "Height",
    string Compliance = "Compliance");

public static class CadParcelScheduleCsv
{
    public static string Export(
        CadParcelSchedule schedule,
        CadParcelScheduleCsvHeaders? headers = null,
        char delimiter = ',')
    {
        ArgumentNullException.ThrowIfNull(schedule);
        headers ??= new CadParcelScheduleCsvHeaders();
        if (delimiter is '\r' or '\n' or '"') throw new ArgumentOutOfRangeException(nameof(delimiter));

        var builder = new StringBuilder();
        AppendRow(builder, delimiter,
        [
            headers.ParcelId,
            headers.LandUseCode,
            headers.LandUseName,
            headers.ParcelArea,
            headers.GrossFloorArea,
            headers.FloorAreaRatio,
            headers.BuildingDensity,
            headers.GreenRatio,
            headers.ProposedHeight,
            headers.Compliance
        ]);

        foreach (var item in schedule.Parcels)
        {
            var indicators = item.Indicators;
            var compliance = item.Controls is null
                ? string.Empty
                : CadPlanningControlEvaluation.Evaluate(indicators, item.Controls).Passes ? "PASS" : "FAIL";
            AppendRow(builder, delimiter,
            [
                item.Parcel.ParcelId,
                item.Parcel.LandUseCode,
                item.Parcel.LandUseName ?? string.Empty,
                F(indicators.ParcelArea),
                F(indicators.GrossFloorArea),
                F(indicators.FloorAreaRatio),
                F(indicators.BuildingDensityPercent),
                F(indicators.GreenRatioPercent),
                indicators.ProposedHeight is double height ? F(height) : string.Empty,
                compliance
            ]);
        }
        return builder.ToString();
    }

    private static void AppendRow(StringBuilder builder, char delimiter, IReadOnlyList<string> values)
    {
        for (var index = 0; index < values.Count; index++)
        {
            if (index > 0) builder.Append(delimiter);
            builder.Append(Escape(values[index], delimiter));
        }
        builder.Append("\r\n");
    }

    private static string Escape(string value, char delimiter)
    {
        value ??= string.Empty;
        var mustQuote = value.Contains(delimiter) || value.Contains('"') || value.Contains('\r') || value.Contains('\n');
        if (!mustQuote) return value;
        return '"' + value.Replace("\"", "\"\"", StringComparison.Ordinal) + '"';
    }

    private static string F(double value) => value.ToString("0.########", CultureInfo.InvariantCulture);
}

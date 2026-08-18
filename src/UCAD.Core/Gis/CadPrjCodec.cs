using System.Text;

namespace UCAD.Core.Gis;

public static class CadPrjCodec
{
    public const string Wgs84Wkt = "GEOGCS[\"GCS_WGS_1984\",DATUM[\"D_WGS_1984\",SPHEROID[\"WGS_1984\",6378137.0,298.257223563]],PRIMEM[\"Greenwich\",0.0],UNIT[\"Degree\",0.0174532925199433]]";
    public const string WebMercatorWkt = "PROJCS[\"WGS_1984_Web_Mercator_Auxiliary_Sphere\",GEOGCS[\"GCS_WGS_1984\",DATUM[\"D_WGS_1984\",SPHEROID[\"WGS_1984\",6378137.0,298.257223563]],PRIMEM[\"Greenwich\",0.0],UNIT[\"Degree\",0.0174532925199433]],PROJECTION[\"Mercator_Auxiliary_Sphere\"],PARAMETER[\"False_Easting\",0.0],PARAMETER[\"False_Northing\",0.0],PARAMETER[\"Central_Meridian\",0.0],PARAMETER[\"Standard_Parallel_1\",0.0],PARAMETER[\"Auxiliary_Sphere_Type\",0.0],UNIT[\"Meter\",1.0]]";

    public static byte[] Export(CadCoordinateReferenceSystem crs) =>
        Encoding.UTF8.GetBytes(GetWkt(crs));

    public static string GetWkt(CadCoordinateReferenceSystem crs) => crs switch
    {
        CadCoordinateReferenceSystem.Wgs84LongitudeLatitude => Wgs84Wkt,
        CadCoordinateReferenceSystem.WebMercator => WebMercatorWkt,
        CadCoordinateReferenceSystem.LocalPlanar => throw new NotSupportedException(
            "LocalPlanar has no implied CRS. Supply an explicit projection definition instead of emitting a fabricated PRJ."),
        _ => throw new NotSupportedException($"CRS {crs} has no PRJ definition in UCAD Core.")
    };

    public static CadCoordinateReferenceSystem? IdentifyKnown(string wkt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(wkt);
        var normalized = Normalize(wkt);
        if (normalized.Contains("MERCATOR_AUXILIARY_SPHERE", StringComparison.Ordinal) ||
            normalized.Contains("WEB_MERCATOR", StringComparison.Ordinal))
            return CadCoordinateReferenceSystem.WebMercator;
        if (normalized.Contains("GCS_WGS_1984", StringComparison.Ordinal) ||
            normalized.Contains("WGS_1984", StringComparison.Ordinal) && normalized.StartsWith("GEOGCS", StringComparison.Ordinal))
            return CadCoordinateReferenceSystem.Wgs84LongitudeLatitude;
        return null;
    }

    private static string Normalize(string value) =>
        string.Concat(value.Where(character => !char.IsWhiteSpace(character))).ToUpperInvariant();
}

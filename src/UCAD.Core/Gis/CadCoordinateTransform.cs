using UCAD.Core.Geometry;

namespace UCAD.Core.Gis;

public enum CadCoordinateReferenceSystem
{
    LocalPlanar,
    Wgs84LongitudeLatitude,
    WebMercator
}

public static class CadCoordinateTransform
{
    public const double EarthRadiusMeters = 6378137.0;
    public const double MaximumWebMercatorLatitudeDegrees = 85.0511287798066;
    private const double DegreesToRadians = Math.PI / 180.0;
    private const double RadiansToDegrees = 180.0 / Math.PI;

    public static CadPoint Transform(
        CadPoint point,
        CadCoordinateReferenceSystem source,
        CadCoordinateReferenceSystem target)
    {
        if (!double.IsFinite(point.X) || !double.IsFinite(point.Y))
            throw new ArgumentException("Coordinate must be finite.", nameof(point));
        if (source == target) return point;
        return (source, target) switch
        {
            (CadCoordinateReferenceSystem.Wgs84LongitudeLatitude, CadCoordinateReferenceSystem.WebMercator) =>
                Wgs84ToWebMercator(point),
            (CadCoordinateReferenceSystem.WebMercator, CadCoordinateReferenceSystem.Wgs84LongitudeLatitude) =>
                WebMercatorToWgs84(point),
            _ => throw new NotSupportedException(
                $"Coordinate transformation {source} -> {target} is not implemented. " +
                "Local/projected CRS transformations require an explicit projection adapter rather than an assumed EPSG mapping.")
        };
    }

    public static IReadOnlyList<CadPoint> Transform(
        IEnumerable<CadPoint> points,
        CadCoordinateReferenceSystem source,
        CadCoordinateReferenceSystem target)
    {
        ArgumentNullException.ThrowIfNull(points);
        return points.Select(point => Transform(point, source, target)).ToArray();
    }

    public static CadPoint Wgs84ToWebMercator(CadPoint longitudeLatitude)
    {
        var longitude = longitudeLatitude.X;
        var latitude = longitudeLatitude.Y;
        if (!double.IsFinite(longitude) || !double.IsFinite(latitude))
            throw new ArgumentException("WGS84 longitude/latitude must be finite.", nameof(longitudeLatitude));
        if (longitude is < -180 or > 180)
            throw new ArgumentOutOfRangeException(nameof(longitudeLatitude), "Longitude must be between -180 and 180 degrees.");
        if (latitude is < -90 or > 90)
            throw new ArgumentOutOfRangeException(nameof(longitudeLatitude), "Latitude must be between -90 and 90 degrees.");

        var clampedLatitude = Math.Clamp(latitude, -MaximumWebMercatorLatitudeDegrees, MaximumWebMercatorLatitudeDegrees);
        var x = EarthRadiusMeters * longitude * DegreesToRadians;
        var latitudeRadians = clampedLatitude * DegreesToRadians;
        var y = EarthRadiusMeters * Math.Log(Math.Tan((Math.PI / 4.0) + (latitudeRadians / 2.0)));
        return new CadPoint(x, y);
    }

    public static CadPoint WebMercatorToWgs84(CadPoint webMercator)
    {
        if (!double.IsFinite(webMercator.X) || !double.IsFinite(webMercator.Y))
            throw new ArgumentException("Web Mercator coordinate must be finite.", nameof(webMercator));
        var longitude = (webMercator.X / EarthRadiusMeters) * RadiansToDegrees;
        var latitude = (2.0 * Math.Atan(Math.Exp(webMercator.Y / EarthRadiusMeters)) - (Math.PI / 2.0)) * RadiansToDegrees;
        if (!double.IsFinite(longitude) || !double.IsFinite(latitude))
            throw new ArgumentOutOfRangeException(nameof(webMercator), "Web Mercator coordinate is outside the finite transform domain.");
        return new CadPoint(longitude, latitude);
    }
}

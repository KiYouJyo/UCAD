using UCAD.Core.Geometry;
using UCAD.Core.Gis;
using Xunit;

namespace UCAD.Core.Tests;

public sealed class CoordinateTransformTests
{
    [Theory]
    [InlineData(0, 0)]
    [InlineData(116.391, 39.907)]
    [InlineData(139.6917, 35.6895)]
    [InlineData(-73.9857, 40.7484)]
    public void Wgs84AndWebMercatorRoundTrip(double longitude, double latitude)
    {
        var source = new CadPoint(longitude, latitude);

        var projected = CadCoordinateTransform.Transform(
            source,
            CadCoordinateReferenceSystem.Wgs84LongitudeLatitude,
            CadCoordinateReferenceSystem.WebMercator);
        var restored = CadCoordinateTransform.Transform(
            projected,
            CadCoordinateReferenceSystem.WebMercator,
            CadCoordinateReferenceSystem.Wgs84LongitudeLatitude);

        Assert.Equal(longitude, restored.X, 8);
        Assert.Equal(latitude, restored.Y, 8);
    }

    [Fact]
    public void WebMercatorClampsPolarLatitudeToFiniteDomain()
    {
        var northPole = CadCoordinateTransform.Wgs84ToWebMercator(new CadPoint(0, 90));
        var edge = CadCoordinateTransform.Wgs84ToWebMercator(
            new CadPoint(0, CadCoordinateTransform.MaximumWebMercatorLatitudeDegrees));

        Assert.True(double.IsFinite(northPole.Y));
        Assert.Equal(edge.Y, northPole.Y, 6);
    }

    [Fact]
    public void LocalPlanarDoesNotPretendToKnowAnArbitraryProjection()
    {
        var point = new CadPoint(500000, 3500000);

        Assert.Throws<NotSupportedException>(() => CadCoordinateTransform.Transform(
            point,
            CadCoordinateReferenceSystem.LocalPlanar,
            CadCoordinateReferenceSystem.Wgs84LongitudeLatitude));
        Assert.Equal(point, CadCoordinateTransform.Transform(
            point,
            CadCoordinateReferenceSystem.LocalPlanar,
            CadCoordinateReferenceSystem.LocalPlanar));
    }

    [Fact]
    public void InvalidLongitudeIsRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            CadCoordinateTransform.Wgs84ToWebMercator(new CadPoint(181, 0)));
    }
}

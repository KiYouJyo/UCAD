using UCAD.Core.Architecture;
using UCAD.Core.Entities;
using UCAD.Core.Geometry;
using Xunit;

namespace UCAD.Core.Tests;

public sealed class ClosedPolylineMetricsTests
{
    [Fact]
    public void RectangleMeasurementReturnsAreaPerimeterAndCentroid()
    {
        var rectangle = new PolylineEntity([
            new CadPoint(0, 0),
            new CadPoint(10, 0),
            new CadPoint(10, 20),
            new CadPoint(0, 20)
        ], closed: true);

        var measurement = CadClosedPolylineMetrics.Measure(rectangle);

        Assert.Equal(200, measurement.Area, 10);
        Assert.Equal(60, measurement.Perimeter, 10);
        Assert.Equal(5, measurement.Centroid.X, 10);
        Assert.Equal(10, measurement.Centroid.Y, 10);
        Assert.False(measurement.Clockwise);
    }

    [Fact]
    public void ReversedWindingKeepsAbsoluteAreaAndCentroid()
    {
        var polygon = new PolylineEntity([
            new CadPoint(0, 0),
            new CadPoint(0, 20),
            new CadPoint(10, 20),
            new CadPoint(10, 0)
        ], closed: true);

        var measurement = CadClosedPolylineMetrics.Measure(polygon);

        Assert.Equal(200, measurement.Area, 10);
        Assert.Equal(-200, measurement.SignedArea, 10);
        Assert.True(measurement.Clockwise);
        Assert.Equal(5, measurement.Centroid.X, 10);
        Assert.Equal(10, measurement.Centroid.Y, 10);
    }

    [Fact]
    public void OpenPolylineIsRejected()
    {
        var open = new PolylineEntity([
            new CadPoint(0, 0),
            new CadPoint(10, 0),
            new CadPoint(10, 10)
        ], closed: false);

        Assert.Throws<ArgumentException>(() => CadClosedPolylineMetrics.Measure(open));
    }
}

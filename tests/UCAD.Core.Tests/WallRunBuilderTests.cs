using UCAD.Core.Architecture;
using UCAD.Core.Geometry;
using Xunit;

namespace UCAD.Core.Tests;

public sealed class WallRunBuilderTests
{
    [Fact]
    public void RightAngleWallRunProducesSingleClosedOutline()
    {
        var wall = CadWallRunBuilder.Create(
        [
            new CadPoint(0, 0),
            new CadPoint(1000, 0),
            new CadPoint(1000, 1000)
        ],
        thickness: 200);

        Assert.True(wall.Closed);
        Assert.Equal(6, wall.Points.Count);
        Assert.Equal(new CadPoint(0, 100), wall.Points[0]);
        Assert.Equal(new CadPoint(900, 100), wall.Points[1]);
        Assert.Equal(new CadPoint(900, 1000), wall.Points[2]);
        Assert.Equal(new CadPoint(1100, 1000), wall.Points[3]);
        Assert.Equal(new CadPoint(1100, -100), wall.Points[4]);
        Assert.Equal(new CadPoint(0, -100), wall.Points[5]);
    }

    [Fact]
    public void ConsecutiveDuplicateCenterlinePointsAreIgnored()
    {
        var wall = CadWallRunBuilder.Create(
        [
            new CadPoint(0, 0),
            new CadPoint(0, 0),
            new CadPoint(500, 0),
            new CadPoint(500, 0),
            new CadPoint(1000, 0)
        ],
        thickness: 100);

        Assert.True(wall.Closed);
        Assert.Equal(6, wall.Points.Count);
    }

    [Fact]
    public void SharpCornerMiterRemainsBounded()
    {
        var vertex = new CadPoint(1000, 0);
        var wall = CadWallRunBuilder.Create(
        [
            new CadPoint(0, 0),
            vertex,
            new CadPoint(5, 50)
        ],
        thickness: 200,
        miterLimit: 4);

        Assert.All(wall.Points, point =>
        {
            Assert.True(double.IsFinite(point.X));
            Assert.True(double.IsFinite(point.Y));
        });
        var maximumDistanceFromCorner = wall.Points.Max(point => (point - vertex).Length);
        Assert.True(maximumDistanceFromCorner < 1500);
    }
}

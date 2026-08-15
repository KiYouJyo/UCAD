using UCAD.Core.Entities;
using UCAD.Core.Geometry;
using Xunit;

namespace UCAD.Core.Tests;

public sealed class DrawingEntityTests
{
    [Fact]
    public void ClosedPolylineIncludesClosingSegmentInLength()
    {
        var polyline = new PolylineEntity(
            [new CadPoint(0, 0), new CadPoint(1, 0), new CadPoint(1, 1), new CadPoint(0, 1)],
            closed: true);

        Assert.Equal(4, polyline.Length, 10);
    }

    [Fact]
    public void PolylineRequiresTwoPoints()
    {
        Assert.Throws<ArgumentException>(() => new PolylineEntity([new CadPoint(0, 0)]));
    }

    [Fact]
    public void CircleRequiresPositiveRadius()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new CircleEntity(new CadPoint(0, 0), 0));
        Assert.Equal(Math.Tau * 5, new CircleEntity(new CadPoint(0, 0), 5).Circumference, 10);
    }

    [Fact]
    public void ThreePointArcPassesThroughExpectedSemicircle()
    {
        Assert.True(ArcEntity.TryCreateFromThreePoints(
            new CadPoint(1, 0),
            new CadPoint(0, 1),
            new CadPoint(-1, 0),
            out var arc));

        Assert.NotNull(arc);
        Assert.Equal(0, arc!.Center.X, 10);
        Assert.Equal(0, arc.Center.Y, 10);
        Assert.Equal(1, arc.Radius, 10);
        Assert.Equal(Math.PI, arc.SweepAngleRadians, 10);
        Assert.Equal(new CadPoint(-1, 0).X, arc.EndPoint.X, 10);
        Assert.Equal(new CadPoint(-1, 0).Y, arc.EndPoint.Y, 10);
    }

    [Fact]
    public void CollinearArcPointsAreRejected()
    {
        Assert.False(ArcEntity.TryCreateFromThreePoints(
            new CadPoint(0, 0),
            new CadPoint(1, 0),
            new CadPoint(2, 0),
            out _));
    }
}

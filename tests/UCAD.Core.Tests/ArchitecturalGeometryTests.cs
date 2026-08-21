using UCAD.Core.Architecture;
using UCAD.Core.Entities;
using UCAD.Core.Geometry;
using Xunit;

namespace UCAD.Core.Tests;

public sealed class ArchitecturalGeometryTests
{
    [Fact]
    public void WallSegmentCreatesClosedRectangleAroundCenterline()
    {
        var wall = CadArchitecturalGeometry.CreateWallSegment(
            new CadPoint(0, 0),
            new CadPoint(1000, 0),
            200);

        Assert.True(wall.Closed);
        Assert.Equal(4, wall.Points.Count);
        Assert.Equal(new CadPoint(0, 100), wall.Points[0]);
        Assert.Equal(new CadPoint(1000, 100), wall.Points[1]);
        Assert.Equal(new CadPoint(1000, -100), wall.Points[2]);
        Assert.Equal(new CadPoint(0, -100), wall.Points[3]);
    }

    [Fact]
    public void DoorSymbolRespectsWidthAngleAndSwingSide()
    {
        var entities = CadArchitecturalGeometry.CreateDoorSymbol(
            new CadPoint(0, 0),
            new CadVector(1, 0),
            900,
            Math.PI / 2,
            CadDoorSwingSide.Left);

        Assert.Equal(3, entities.Count);
        var guide = Assert.IsType<LineEntity>(entities[0]);
        var leaf = Assert.IsType<LineEntity>(entities[1]);
        var arc = Assert.IsType<ArcEntity>(entities[2]);
        Assert.Equal(new CadPoint(900, 0), guide.End);
        Assert.Equal(0, leaf.End.X, 8);
        Assert.Equal(900, leaf.End.Y, 8);
        Assert.Equal(900, arc.Radius, 8);
        Assert.Equal(Math.PI / 2, arc.SweepAngleRadians, 8);
    }

    [Fact]
    public void WindowSymbolCreatesFourLongitudinalLinesAndTwoJambs()
    {
        var entities = CadArchitecturalGeometry.CreateWindowSymbol(
            new CadPoint(500, 500),
            new CadVector(1, 0),
            1200,
            240);

        Assert.Equal(6, entities.Count);
        Assert.All(entities, entity => Assert.IsType<LineEntity>(entity));
        var first = Assert.IsType<LineEntity>(entities[0]);
        Assert.Equal(new CadPoint(-100, 620), first.Start);
        Assert.Equal(new CadPoint(1100, 620), first.End);
    }

    [Fact]
    public void RectangularColumnRotatesAroundItsCenter()
    {
        var column = CadArchitecturalGeometry.CreateRectangularColumn(
            new CadPoint(100, 200),
            400,
            200,
            Math.PI / 2);

        Assert.True(column.Closed);
        Assert.Equal(4, column.Points.Count);
        Assert.Equal(200, column.Points[0].X, 8);
        Assert.Equal(0, column.Points[0].Y, 8);
    }

    [Fact]
    public void CircularColumnUsesDiameter()
    {
        var column = CadArchitecturalGeometry.CreateCircularColumn(new CadPoint(10, 20), 600);

        Assert.Equal(new CadPoint(10, 20), column.Center);
        Assert.Equal(300, column.Radius, 8);
    }
}

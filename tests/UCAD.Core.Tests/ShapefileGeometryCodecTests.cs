using UCAD.Core.Entities;
using UCAD.Core.Geometry;
using UCAD.Core.Gis;
using Xunit;

namespace UCAD.Core.Tests;

public sealed class ShapefileGeometryCodecTests
{
    [Fact]
    public void PointShapefileRoundTripsCoordinates()
    {
        var first = new PointEntity(new CadPoint(120.5, 30.25));
        var second = new PointEntity(new CadPoint(-10, 5));

        var exported = CadShapefileGeometryCodec.Export([first, second]);
        var imported = CadShapefileGeometryCodec.Import(exported.ShpContent);

        Assert.Equal(CadShapefileShapeType.Point, exported.ShapeType);
        Assert.Equal(CadShapefileShapeType.Point, imported.ShapeType);
        Assert.Equal(2, imported.Entities.Count);
        Assert.Equal(first.Position, Assert.IsType<PointEntity>(imported.Entities[0]).Position);
        Assert.Equal(second.Position, Assert.IsType<PointEntity>(imported.Entities[1]).Position);
        Assert.NotEmpty(exported.Warnings);
        Assert.NotEmpty(imported.Warnings);
    }

    [Fact]
    public void PolylineShapefileRoundTripsLineAndPolylineRecords()
    {
        var line = new LineEntity(new CadPoint(0, 0), new CadPoint(10, 0));
        var polyline = new PolylineEntity([
            new CadPoint(20, 0),
            new CadPoint(30, 10),
            new CadPoint(40, 0)
        ]);

        var exported = CadShapefileGeometryCodec.Export([line, polyline]);
        var imported = CadShapefileGeometryCodec.Import(exported.ShpContent);

        Assert.Equal(CadShapefileShapeType.PolyLine, imported.ShapeType);
        var importedLine = Assert.IsType<LineEntity>(imported.Entities[0]);
        var importedPolyline = Assert.IsType<PolylineEntity>(imported.Entities[1]);
        Assert.Equal(line.Start, importedLine.Start);
        Assert.Equal(line.End, importedLine.End);
        Assert.Equal(polyline.Points, importedPolyline.Points);
        Assert.False(importedPolyline.Closed);
    }

    [Fact]
    public void PolygonShapefileClosesRingAndRestoresClosedPolyline()
    {
        var polygon = new PolylineEntity([
            new CadPoint(0, 0),
            new CadPoint(100, 0),
            new CadPoint(100, 50),
            new CadPoint(0, 50)
        ], closed: true);

        var exported = CadShapefileGeometryCodec.Export([polygon]);
        var imported = CadShapefileGeometryCodec.Import(exported.ShpContent);

        Assert.Equal(CadShapefileShapeType.Polygon, imported.ShapeType);
        var restored = Assert.IsType<PolylineEntity>(Assert.Single(imported.Entities));
        Assert.True(restored.Closed);
        Assert.Equal(polygon.Points, restored.Points);
    }

    [Fact]
    public void MixedShapeFamiliesAreRejectedInsteadOfSilentlyCoerced()
    {
        var point = new PointEntity(new CadPoint(0, 0));
        var line = new LineEntity(new CadPoint(0, 0), new CadPoint(1, 1));

        Assert.Throws<ArgumentException>(() => CadShapefileGeometryCodec.Export([point, line]));
    }

    [Fact]
    public void UnsupportedFiniteGeometryMustBeExplicitlyFlattenedFirst()
    {
        var circle = new CircleEntity(new CadPoint(0, 0), 10);

        Assert.Throws<NotSupportedException>(() => CadShapefileGeometryCodec.Export([circle]));
    }
}

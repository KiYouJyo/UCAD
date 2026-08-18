using UCAD.Core.Architecture;
using UCAD.Core.Entities;
using UCAD.Core.Geometry;
using Xunit;

namespace UCAD.Core.Tests;

public sealed class RoomLabelFactoryTests
{
    [Fact]
    public void RoomLabelUsesCentroidAndExplicitAreaScale()
    {
        var boundary = new PolylineEntity([
            new CadPoint(0, 0),
            new CadPoint(5000, 0),
            new CadPoint(5000, 4000),
            new CadPoint(0, 4000)
        ], closed: true);
        var options = new CadRoomLabelOptions(
            AreaScale: 1.0 / 1_000_000.0,
            AreaSuffix: "m²",
            Precision: 1,
            TextHeight: 300,
            TextStyleName: "Standard");

        var label = CadRoomLabelFactory.Create(boundary, "Meeting", options);

        Assert.Equal(new CadPoint(2500, 2000), label.Position);
        Assert.Equal("Meeting  20.0 m²", label.Text);
        Assert.Equal(300, label.Height, 8);
        Assert.Equal("Standard", label.StyleName);
    }

    [Fact]
    public void RoomLabelRejectsOpenBoundary()
    {
        var boundary = new PolylineEntity([
            new CadPoint(0, 0),
            new CadPoint(10, 0),
            new CadPoint(10, 10)
        ]);

        Assert.Throws<ArgumentException>(() => CadRoomLabelFactory.Create(boundary, "Room"));
    }
}

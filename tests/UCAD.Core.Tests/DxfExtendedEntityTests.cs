using UCAD.Core.Entities;
using UCAD.Core.Geometry;
using UCAD.Core.IO;
using Xunit;

namespace UCAD.Core.Tests;

public sealed class DxfExtendedEntityTests
{
    [Fact]
    public void ExtendedEntitiesRoundTripThroughNativeDxfRecords()
    {
        var document = new CadDocument();
        document.Add(new PointEntity(new CadPoint(2, 3)));
        document.Add(new EllipseEntity(new CadPoint(10, 10), new CadVector(8, 0), 0.5));
        document.Add(new SplineEntity([
            new CadPoint(0, 0),
            new CadPoint(3, 5),
            new CadPoint(7, 4),
            new CadPoint(10, 8)
        ]));
        document.Add(new RayEntity(new CadPoint(20, 20), new CadVector(2, 1)));
        document.Add(new XLineEntity(new CadPoint(30, 30), new CadVector(-1, 2)));

        var exported = CadDxfCodec.Export(document);

        Assert.False(exported.HasWarnings);
        Assert.Contains("ELLIPSE", exported.Content, StringComparison.Ordinal);
        Assert.Contains("SPLINE", exported.Content, StringComparison.Ordinal);
        Assert.Contains("RAY", exported.Content, StringComparison.Ordinal);
        Assert.Contains("XLINE", exported.Content, StringComparison.Ordinal);
        Assert.Contains("$INSUNITS", exported.Content, StringComparison.Ordinal);

        var imported = CadDxfCodec.Import(exported.Content);

        Assert.False(imported.HasWarnings);
        Assert.Equal(5, imported.Document.Entities.Count);
        Assert.IsType<PointEntity>(imported.Document.Entities[0]);
        var ellipse = Assert.IsType<EllipseEntity>(imported.Document.Entities[1]);
        Assert.Equal(8, ellipse.MajorRadius, 8);
        Assert.Equal(0.5, ellipse.Ratio, 8);
        var spline = Assert.IsType<SplineEntity>(imported.Document.Entities[2]);
        Assert.Equal(4, spline.FitPoints.Count);
        Assert.IsType<RayEntity>(imported.Document.Entities[3]);
        Assert.IsType<XLineEntity>(imported.Document.Entities[4]);
    }

    [Fact]
    public void BulgedPolylineIsNotSilentlyFlattened()
    {
        const string dxf = """
0
SECTION
2
ENTITIES
0
LWPOLYLINE
8
0
90
2
70
0
10
0
20
0
42
1
10
10
20
0
0
ENDSEC
0
EOF
""";

        var imported = CadDxfCodec.Import(dxf);

        Assert.Empty(imported.Document.Entities);
        Assert.True(imported.HasWarnings);
        Assert.Contains(imported.Warnings, warning => warning.Contains("bulge", StringComparison.OrdinalIgnoreCase));
    }
}
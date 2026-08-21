using UCAD.Core.Entities;
using UCAD.Core.Geometry;
using UCAD.Core.IO;
using Xunit;

namespace UCAD.Core.Tests;

public sealed class DxfDisplayFallbackTests
{
    [Fact]
    public void FullInteropFlattensBulgedLightweightPolylineInsteadOfDroppingIt()
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

        var result = CadDxfFullInteropCodec.Import(dxf);

        Assert.False(result.HasWarnings);
        var polyline = Assert.IsType<PolylineEntity>(Assert.Single(result.Document.Entities));
        Assert.False(polyline.Closed);
        Assert.True(polyline.Points.Count > 2);
        Assert.Equal(new CadPoint(0, 0), polyline.Points[0]);
        Assert.Equal(new CadPoint(10, 0), polyline.Points[^1]);
        Assert.Contains(polyline.Points, point => Math.Abs(point.Y) > 4.9);
    }

    [Fact]
    public void FullInteropRecoversLegacyTwoDimensionalPolylineSequence()
    {
        const string dxf = """
0
SECTION
2
ENTITIES
0
POLYLINE
8
Legacy
70
1
0
VERTEX
8
Legacy
10
0
20
0
42
0
0
VERTEX
8
Legacy
10
20
20
0
42
0
0
VERTEX
8
Legacy
10
20
20
10
42
0
0
SEQEND
8
Legacy
0
ENDSEC
0
EOF
""";

        var result = CadDxfFullInteropCodec.Import(dxf);

        Assert.False(result.HasWarnings);
        var polyline = Assert.IsType<PolylineEntity>(Assert.Single(result.Document.Entities));
        Assert.True(polyline.Closed);
        Assert.Equal(3, polyline.Points.Count);
        Assert.Equal("Legacy", result.Document.GetEntityProperties(polyline.Id).LayerName);
    }

    [Fact]
    public void FullInteropRecoversSolidTraceAndFaceDisplayGeometry()
    {
        const string dxf = """
0
SECTION
2
ENTITIES
0
SOLID
8
Fill
10
0
20
0
11
10
21
0
12
0
22
10
13
10
23
10
0
TRACE
8
Fill
10
20
20
0
11
30
21
0
12
20
22
10
13
30
23
10
0
3DFACE
8
Face
10
40
20
0
11
50
21
0
12
50
22
10
13
40
23
10
0
ENDSEC
0
EOF
""";

        var result = CadDxfFullInteropCodec.Import(dxf);

        Assert.False(result.HasWarnings);
        Assert.Equal(3, result.Document.Entities.Count);
        Assert.Equal(2, result.Document.Entities.OfType<HatchEntity>().Count());
        var face = Assert.Single(result.Document.Entities.OfType<PolylineEntity>());
        Assert.True(face.Closed);
        Assert.Equal(4, face.Points.Count);
    }
}

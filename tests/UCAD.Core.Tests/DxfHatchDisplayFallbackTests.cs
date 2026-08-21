using UCAD.Core.Entities;
using UCAD.Core.IO;
using Xunit;

namespace UCAD.Core.Tests;

public sealed class DxfHatchDisplayFallbackTests
{
    [Fact]
    public void FullInteropRecoversLineEdgeBasedHatchInsteadOfDroppingFill()
    {
        const string dxf = """
0
SECTION
2
ENTITIES
0
HATCH
8
HatchEdge
10
0
20
0
30
0
2
SOLID
70
1
71
0
91
1
92
1
93
4
72
1
10
0
20
0
11
20
21
0
72
1
10
20
20
0
11
20
21
10
72
1
10
20
20
10
11
0
21
10
72
1
10
0
20
10
11
0
21
0
97
0
75
0
76
1
98
0
0
ENDSEC
0
EOF
""";

        var result = CadDxfFullInteropCodec.Import(dxf);

        Assert.False(result.HasWarnings);
        var hatch = Assert.IsType<HatchEntity>(Assert.Single(result.Document.Entities));
        Assert.Equal("SOLID", hatch.Pattern, ignoreCase: true);
        Assert.Equal(4, hatch.Boundary.Count);
        Assert.Equal("HatchEdge", result.Document.GetEntityProperties(hatch.Id).LayerName);
    }

    [Fact]
    public void FullInteropTessellatesCircularHatchEdgeForDisplay()
    {
        const string dxf = """
0
SECTION
2
ENTITIES
0
HATCH
8
0
10
0
20
0
30
0
2
SOLID
70
1
71
0
91
1
92
1
93
1
72
2
10
10
20
10
40
5
50
0
51
360
73
1
97
0
75
0
76
1
98
0
0
ENDSEC
0
EOF
""";

        var result = CadDxfFullInteropCodec.Import(dxf);

        Assert.False(result.HasWarnings);
        var hatch = Assert.IsType<HatchEntity>(Assert.Single(result.Document.Entities));
        Assert.True(hatch.Boundary.Count >= 36);
        Assert.All(hatch.Boundary, point =>
        {
            var dx = point.X - 10;
            var dy = point.Y - 10;
            Assert.InRange(Math.Sqrt(dx * dx + dy * dy), 4.999999, 5.000001);
        });
    }

    [Fact]
    public void FullInteropFlattensBulgedPolylineHatchBoundary()
    {
        const string dxf = """
0
SECTION
2
ENTITIES
0
HATCH
8
0
10
0
20
0
30
0
2
SOLID
70
1
71
0
91
1
92
3
72
1
73
1
93
3
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
42
0
10
5
20
10
42
0
97
0
75
0
76
1
98
0
0
ENDSEC
0
EOF
""";

        var result = CadDxfFullInteropCodec.Import(dxf);

        Assert.False(result.HasWarnings);
        var hatch = Assert.IsType<HatchEntity>(Assert.Single(result.Document.Entities));
        Assert.True(hatch.Boundary.Count > 3);
    }
}

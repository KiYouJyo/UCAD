using System.Reflection;
using ACadSharp.Entities;
using ACadSharp.Objects;
using CSMath;
using UCAD.Core.Entities;
using Xunit;

namespace UCAD.Core.Tests;

public sealed class AcadMLineDisplayRepairTests
{
    [Fact]
    public void MLineElementOffsetsBecomeVisibleParallelLines()
    {
        var source = new ACadSharp.CadDocument();
        var mline = new MLine
        {
            Style = MLineStyle.Default,
            Flags = MLineFlags.Has,
            ScaleFactor = 1
        };
        mline.Vertices.Add(Vertex(0, 0, [0.5, 0], [-0.5, 0]));
        mline.Vertices.Add(Vertex(10, 0, [0.5, 0], [-0.5, 0]));
        source.Entities.Add(mline);

        var target = new UCAD.Core.CadDocument();
        var warnings = new List<string> { "DXF entity 'MLINE' could not be imported." };
        InvokeRepair(source, target, warnings);

        var lines = target.Entities.OfType<LineEntity>().ToArray();
        Assert.Equal(2, lines.Length);
        Assert.Contains(lines, line => Near(line.Start.Y, 0.5) && Near(line.End.Y, 0.5));
        Assert.Contains(lines, line => Near(line.Start.Y, -0.5) && Near(line.End.Y, -0.5));
        Assert.DoesNotContain(warnings, warning => warning.Contains("could not be imported", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void MLineBreakParametersPreserveVisibleGapsAndSimpleFill()
    {
        var source = new ACadSharp.CadDocument();
        var style = MLineStyle.Default;
        style.Name = "UCAD_FILL_TEST";
        style.Flags |= MLineStyleFlags.FillOn;
        source.MLineStyles.Add(style);
        var mline = new MLine
        {
            Style = style,
            Flags = MLineFlags.Has,
            ScaleFactor = 1
        };
        mline.Vertices.Add(Vertex(0, 0, [0.5, 0, 3, 6], [-0.5, 0]));
        mline.Vertices.Add(Vertex(10, 0, [0.5, 0], [-0.5, 0]));
        source.Entities.Add(mline);

        var target = new UCAD.Core.CadDocument();
        var warnings = new List<string>();
        InvokeRepair(source, target, warnings);

        var upper = target.Entities.OfType<LineEntity>().Where(line => Near(line.Start.Y, 0.5) && Near(line.End.Y, 0.5)).ToArray();
        Assert.Equal(2, upper.Length);
        Assert.Contains(upper, line => Near(Math.Min(line.Start.X, line.End.X), 0) && Near(Math.Max(line.Start.X, line.End.X), 3));
        Assert.Contains(upper, line => Near(Math.Min(line.Start.X, line.End.X), 6) && Near(Math.Max(line.Start.X, line.End.X), 10));
        Assert.Single(target.Entities.OfType<HatchEntity>());
    }

    private static MLine.Vertex Vertex(double x, double y, params double[][] elementParameters)
    {
        var vertex = new MLine.Vertex
        {
            Position = new XYZ(x, y, 0),
            Direction = new XYZ(1, 0, 0),
            Miter = new XYZ(0, 1, 0)
        };
        foreach (var values in elementParameters)
        {
            var segment = new MLine.Vertex.Segment();
            segment.Parameters.AddRange(values);
            vertex.Segments.Add(segment);
        }
        return vertex;
    }

    private static void InvokeRepair(ACadSharp.CadDocument source, UCAD.Core.CadDocument target, List<string> warnings)
    {
        var repairType = typeof(UCAD.Core.IO.CadAcadInteropCodec).Assembly.GetType("UCAD.Core.IO.CadAcadMLineDisplayRepair", throwOnError: true)!;
        var apply = repairType.GetMethod("Apply", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        Assert.NotNull(apply);
        apply!.Invoke(null, [source, target, warnings]);
    }

    private static bool Near(double first, double second) => Math.Abs(first - second) <= 1e-8;
}

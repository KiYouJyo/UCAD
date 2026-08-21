using System.Reflection;
using ACadSharp;
using ACadSharp.Entities;
using ACadSharp.Objects;
using CSMath;
using UCAD.Core.Entities;
using UCAD.Core.IO;
using Xunit;

namespace UCAD.Core.Tests;

public sealed class AcadDwgMultiLeaderRepairTests
{
    [Fact]
    public void NativeRepairRecoversTextMultiLeaderAsVisibleLeaderGeometry()
    {
        var source = new ACadSharp.CadDocument();
        var mleader = new MultiLeader
        {
            PathType = MultiLeaderPathType.StraightLineSegments,
            PropertyOverrideFlags = MultiLeaderPropertyOverrideFlags.ContentType |
                                    MultiLeaderPropertyOverrideFlags.TextAlignment |
                                    MultiLeaderPropertyOverrideFlags.EnableUseDefaultMText
        };
        mleader.ContextData.ContentBasePoint = new XYZ(18.6, 15, 0);
        mleader.ContextData.BasePoint = XYZ.Zero;
        mleader.ContextData.TextLabel = "Recovered\\PMLEADER";
        mleader.ContextData.TextHeight = 3.5;

        var root = new MultiLeaderObjectContextData.LeaderRoot
        {
            ConnectionPoint = new XYZ(15, 15, 0),
            ContentValid = true,
            Direction = XYZ.AxisX,
            LandingDistance = 3.6
        };
        var line = new MultiLeaderObjectContextData.LeaderLine
        {
            PathType = MultiLeaderPathType.StraightLineSegments
        };
        line.Points.Add(new XYZ(0, 0, 0));
        line.Points.Add(new XYZ(8, 8, 0));
        root.Lines.Add(line);
        mleader.ContextData.LeaderRoots.Add(root);
        source.Entities.Add(mleader);

        var target = new UCAD.Core.CadDocument();
        var warnings = new List<string> { "DXF entity 'MLEADER' could not be imported." };

        InvokeSemanticRepair(source, target, warnings);

        var recovered = Assert.IsType<LeaderEntity>(Assert.Single(target.Entities));
        Assert.Equal("Recovered\nMLEADER", recovered.Text);
        Assert.Equal(3.5, recovered.TextHeight, 8);
        Assert.Equal(4, recovered.Points.Count);
        Assert.DoesNotContain(warnings, warning => warning.Contains("MLEADER", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void NativeRepairKeepsAdditionalMultiLeaderArmsAsVisiblePolylines()
    {
        var source = new ACadSharp.CadDocument();
        var mleader = new MultiLeader();
        mleader.ContextData.ContentBasePoint = new XYZ(20, 20, 0);
        mleader.ContextData.TextLabel = "Shared note";

        foreach (var arrow in new[] { new XYZ(0, 0, 0), new XYZ(0, 10, 0) })
        {
            var root = new MultiLeaderObjectContextData.LeaderRoot
            {
                ConnectionPoint = new XYZ(15, 20, 0),
                ContentValid = true
            };
            var line = new MultiLeaderObjectContextData.LeaderLine();
            line.Points.Add(arrow);
            line.Points.Add(new XYZ(10, 15, 0));
            root.Lines.Add(line);
            mleader.ContextData.LeaderRoots.Add(root);
        }
        source.Entities.Add(mleader);

        var target = new UCAD.Core.CadDocument();
        var warnings = new List<string>();

        InvokeSemanticRepair(source, target, warnings);

        Assert.Single(target.Entities.OfType<LeaderEntity>());
        Assert.Single(target.Entities.OfType<PolylineEntity>());
        Assert.Empty(warnings);
    }

    private static void InvokeSemanticRepair(ACadSharp.CadDocument source, UCAD.Core.CadDocument target, List<string> warnings)
    {
        var repairType = typeof(CadAcadInteropCodec).Assembly.GetType("UCAD.Core.IO.CadAcadDwgSemanticRepair", throwOnError: true)!;
        var apply = repairType.GetMethod("Apply", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        Assert.NotNull(apply);
        apply!.Invoke(null, [source, target, warnings]);
    }
}

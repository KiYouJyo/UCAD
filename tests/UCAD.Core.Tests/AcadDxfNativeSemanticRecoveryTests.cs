using ACadSharp;
using ACadSharp.Entities;
using ACadSharp.IO;
using ACadSharp.Objects;
using CSMath;
using UCAD.Core.Entities;
using UCAD.Core.IO;
using Xunit;

namespace UCAD.Core.Tests;

public sealed class AcadDxfNativeSemanticRecoveryTests
{
    [Fact]
    public void DirectDxfImportUsesNativeSemanticViewToRecoverMultiLeader()
    {
        var source = new ACadSharp.CadDocument();
        var mleader = new MultiLeader
        {
            PathType = MultiLeaderPathType.StraightLineSegments,
            PropertyOverrideFlags = MultiLeaderPropertyOverrideFlags.ContentType |
                                    MultiLeaderPropertyOverrideFlags.TextAlignment |
                                    MultiLeaderPropertyOverrideFlags.EnableUseDefaultMText
        };
        mleader.ContextData.ContentBasePoint = new XYZ(30, 20, 0);
        mleader.ContextData.BasePoint = XYZ.Zero;
        mleader.ContextData.TextLabel = "Direct DXF\\PMLEADER";
        mleader.ContextData.TextHeight = 2.75;

        var root = new MultiLeaderObjectContextData.LeaderRoot
        {
            ConnectionPoint = new XYZ(25, 20, 0),
            ContentValid = true,
            Direction = XYZ.AxisX,
            LandingDistance = 5
        };
        var line = new MultiLeaderObjectContextData.LeaderLine
        {
            PathType = MultiLeaderPathType.StraightLineSegments
        };
        line.Points.Add(new XYZ(0, 0, 0));
        line.Points.Add(new XYZ(12, 10, 0));
        root.Lines.Add(line);
        mleader.ContextData.LeaderRoots.Add(root);
        source.Entities.Add(mleader);

        using var output = new MemoryStream();
        using (var writer = new DxfWriter(output, source, binary: false)) writer.Write();

        var result = CadAcadInteropCodec.ImportDxf(output.ToArray());

        var leader = Assert.Single(result.Document.Entities.OfType<LeaderEntity>());
        Assert.Equal("Direct DXF\nMLEADER", leader.Text);
        Assert.Equal(2.75, leader.TextHeight, 8);
        Assert.True(leader.Points.Count >= 3);
        Assert.DoesNotContain(result.Warnings, warning =>
            warning.Contains("MLEADER", StringComparison.OrdinalIgnoreCase) &&
            warning.Contains("could not be imported", StringComparison.OrdinalIgnoreCase));
    }
}

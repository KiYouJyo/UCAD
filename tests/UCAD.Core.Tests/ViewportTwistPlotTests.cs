using UCAD.Core.Geometry;
using UCAD.Core.Layout;
using UCAD.Core.Plot;
using Xunit;

namespace UCAD.Core.Tests;

public sealed class ViewportTwistPlotTests
{
    [Fact]
    public void PlotPlanPreservesViewportTwistAndRoundTripsCoordinates()
    {
        var setup = new CadPageSetup(CadPaperSize.A3, landscape: true);
        var viewport = new CadLayoutViewport(
            "Rotated",
            CadRect.FromPoints(new CadPoint(20, 20), new CadPoint(220, 120)),
            new CadPoint(10000, 5000),
            scaleDenominator: 100,
            twistAngleRadians: Math.PI / 2);

        var plan = CadPlotPlan.FromViewport(setup, viewport);
        var paper = plan.ModelToPaper(new CadPoint(11000, 5000));
        var restored = plan.PaperToModel(paper);

        Assert.Equal(Math.PI / 2, plan.TwistAngleRadians, 10);
        Assert.Equal(plan.PaperCenter.X, paper.X, 10);
        Assert.Equal(plan.PaperCenter.Y - 10, paper.Y, 10);
        Assert.Equal(11000, restored.X, 8);
        Assert.Equal(5000, restored.Y, 8);
    }
}

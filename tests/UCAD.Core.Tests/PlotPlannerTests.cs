using UCAD.Core.Geometry;
using UCAD.Core.Layout;
using UCAD.Core.Plot;
using Xunit;

namespace UCAD.Core.Tests;

public sealed class PlotPlannerTests
{
    [Fact]
    public void LayoutPlotAreaUsesAllActiveLayoutViewports()
    {
        var document = new CadDocument();
        var setup = new CadPageSetup(CadPaperSize.A3, plotArea: CadPlotArea.Layout);
        var first = new CadLayoutViewport(
            "A",
            CadRect.FromPoints(new CadPoint(10, 10), new CadPoint(100, 100)),
            new CadPoint(0, 0),
            100);
        var second = new CadLayoutViewport(
            "B",
            CadRect.FromPoints(new CadPoint(120, 10), new CadPoint(220, 100)),
            new CadPoint(1000, 0),
            200);
        document.SetLayoutTable([new CadLayoutDefinition("Sheet", setup, [first, second])], "Sheet");
        var fallback = CadPlotPlan.FitExtents(setup, CadRect.FromPoints(new CadPoint(0, 0), new CadPoint(100, 100)));

        var plans = CadPlotPlanner.ResolvePagePlans(document, fallback);

        Assert.Equal(2, plans.Count);
        Assert.Equal(first.PaperRectMm, plans[0].PaperRectMm);
        Assert.Equal(second.PaperRectMm, plans[1].PaperRectMm);
    }

    [Fact]
    public void ExtentsPlotAreaIgnoresStoredLayoutViewports()
    {
        var document = new CadDocument();
        var setup = new CadPageSetup(CadPaperSize.A3, plotArea: CadPlotArea.Extents);
        var viewport = new CadLayoutViewport(
            "Stored",
            CadRect.FromPoints(new CadPoint(10, 10), new CadPoint(100, 100)),
            new CadPoint(5000, 5000),
            500);
        document.SetLayoutTable([new CadLayoutDefinition("Sheet", setup, [viewport])], "Sheet");
        var fallback = CadPlotPlan.FitExtents(setup, CadRect.FromPoints(new CadPoint(0, 0), new CadPoint(1000, 1000)));

        var plans = CadPlotPlanner.ResolvePagePlans(document, fallback);

        Assert.Single(plans);
        Assert.Same(fallback, plans[0]);
    }
}

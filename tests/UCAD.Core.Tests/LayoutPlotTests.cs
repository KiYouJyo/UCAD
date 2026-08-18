using System.Text;
using UCAD.Core.Entities;
using UCAD.Core.Geometry;
using UCAD.Core.Layout;
using UCAD.Core.Plot;
using Xunit;

namespace UCAD.Core.Tests;

public sealed class LayoutPlotTests
{
    [Fact]
    public void A3LandscapePageHasExpectedPrintableArea()
    {
        var setup = new CadPageSetup(
            CadPaperSize.A3,
            landscape: true,
            marginLeftMm: 10,
            marginTopMm: 12,
            marginRightMm: 10,
            marginBottomMm: 12,
            plotScaleDenominator: 100);

        Assert.Equal(420, setup.PaperWidthMm);
        Assert.Equal(297, setup.PaperHeightMm);
        Assert.Equal(400, setup.PrintablePaperRectMm.Width);
        Assert.Equal(273, setup.PrintablePaperRectMm.Height);
    }

    [Fact]
    public void ViewportMapsModelCoordinatesUsingCadScale()
    {
        var viewport = new CadLayoutViewport(
            "Main",
            CadRect.FromPoints(new CadPoint(20, 20), new CadPoint(220, 120)),
            new CadPoint(10000, 5000),
            scaleDenominator: 100);

        Assert.Equal(new CadPoint(120, 70), viewport.ModelToPaper(new CadPoint(10000, 5000)));
        Assert.Equal(new CadPoint(130, 70), viewport.ModelToPaper(new CadPoint(11000, 5000)));
        Assert.Equal(new CadPoint(11000, 5000), viewport.PaperToModel(new CadPoint(130, 70)));
    }

    [Fact]
    public void FitExtentsChoosesLimitingAxisAndCentersDrawing()
    {
        var setup = new CadPageSetup(CadPaperSize.A4, landscape: true, plotScaleDenominator: 100);
        var extents = CadRect.FromPoints(new CadPoint(0, 0), new CadPoint(10000, 5000));

        var plan = CadPlotPlan.FitExtents(setup, extents, paddingFactor: 1);

        Assert.True(plan.ScaleDenominator > 0);
        Assert.Equal(new CadPoint(5000, 2500), plan.ModelCenter);
        Assert.Equal(plan.PaperCenter, plan.ModelToPaper(plan.ModelCenter));
    }

    [Fact]
    public void PdfExporterProducesOnePageVectorPdf()
    {
        var document = new CadDocument();
        document.Add(new LineEntity(new CadPoint(0, 0), new CadPoint(10000, 0)));
        document.Add(new CircleEntity(new CadPoint(5000, 2500), 1000));
        document.Add(new PolylineEntity([
            new CadPoint(0, 0),
            new CadPoint(10000, 0),
            new CadPoint(10000, 5000),
            new CadPoint(0, 5000)
        ], closed: true));
        document.ResetHistory();

        Assert.True(CadPlotGeometry.TryGetDocumentExtents(document, out var extents));
        var setup = new CadPageSetup(CadPaperSize.A4, landscape: true, plotStyle: CadPlotStyleMode.Monochrome);
        var plan = CadPlotPlan.FitExtents(setup, extents);
        var result = CadPdfExporter.Export(document, plan, "UCAD Test");
        var text = Encoding.ASCII.GetString(result.Content);

        Assert.False(result.HasWarnings);
        Assert.StartsWith("%PDF-1.4", text, StringComparison.Ordinal);
        Assert.Contains("/Type /Page", text, StringComparison.Ordinal);
        Assert.Contains(" m\n", text, StringComparison.Ordinal);
        Assert.Contains(" l\n", text, StringComparison.Ordinal);
        Assert.Contains(" c S\n", text, StringComparison.Ordinal);
        Assert.EndsWith("%%EOF\n", text, StringComparison.Ordinal);
    }

    [Fact]
    public void PdfExporterReportsUnicodeFontFallbackAndPatternHatchLimitation()
    {
        var document = new CadDocument();
        document.Add(new TextEntity(new CadPoint(0, 0), "规划", 250));
        document.Add(new HatchEntity([
            new CadPoint(0, 0),
            new CadPoint(1000, 0),
            new CadPoint(1000, 1000),
            new CadPoint(0, 1000)
        ], "ANSI31", 1, 0));
        var setup = new CadPageSetup(CadPaperSize.A4, landscape: true);
        var plan = new CadPlotPlan(setup, new CadPoint(500, 500), 10, setup.PrintablePaperRectMm);

        var result = CadPdfExporter.Export(document, plan);

        Assert.True(result.HasWarnings);
        Assert.Contains(result.Warnings, warning => warning.Contains("non-ASCII", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(result.Warnings, warning => warning.Contains("boundary only", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void LayoutCanReplaceAndRemoveViewportImmutably()
    {
        var viewport = new CadLayoutViewport(
            "Main",
            CadRect.FromPoints(new CadPoint(20, 20), new CadPoint(180, 100)),
            new CadPoint(0, 0),
            100);
        var layout = new CadLayoutDefinition("Layout1").AddViewport(viewport);
        var updatedViewport = new CadLayoutViewport(
            viewport.Name,
            viewport.PaperRectMm,
            viewport.ModelCenter,
            200,
            id: viewport.Id);

        var updated = layout.ReplaceViewport(updatedViewport);
        Assert.Equal(100, layout.Viewports[0].ScaleDenominator);
        Assert.Equal(200, updated.Viewports[0].ScaleDenominator);
        Assert.Empty(updated.RemoveViewport(viewport.Id).Viewports);
    }
}
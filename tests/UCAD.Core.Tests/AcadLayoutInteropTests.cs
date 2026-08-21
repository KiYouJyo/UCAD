using UCAD.Core.Geometry;
using UCAD.Core.IO;
using UCAD.Core.Layout;
using Xunit;

namespace UCAD.Core.Tests;

public sealed class AcadLayoutInteropTests
{
    [Fact]
    public void DwgRoundTripPreservesPaperLayoutPageSetupAndViewport()
    {
        var document = CreateLayoutDocument();

        var exported = CadAcadInteropCodec.ExportDwg(document);
        var imported = CadAcadInteropCodec.ImportDwg(exported.Content);

        Assert.NotEmpty(exported.Content);
        AssertImportedDwgLayout(imported.Document);
    }

    [Fact]
    public void DwtUsesTheSameLayoutTransportAsDwg()
    {
        var document = new CadDocument();
        document.SetLayoutTable(
            [new CadLayoutDefinition(
                "Layout1",
                new CadPageSetup(CadPaperSize.A4, landscape: false, plotScaleDenominator: 50))],
            "Layout1");

        var exported = CadAcadInteropCodec.ExportDwg(document, ".dwt");
        var imported = CadAcadInteropCodec.ImportDwg(exported.Content, ".dwt");

        Assert.Equal(".dwt", exported.TargetExtension);
        Assert.Equal(".dwt", imported.SourceExtension);
        var page = imported.Document.GetLayout("Layout1").PageSetup;
        Assert.False(page.Landscape);
        Assert.Equal(CadPaperSize.A4.Name, page.PaperSize.Name);
        Assert.Equal(50, page.PlotScaleDenominator, 5);
    }

    private static CadDocument CreateLayoutDocument()
    {
        var document = new CadDocument();
        var page = new CadPageSetup(
            CadPaperSize.A3,
            landscape: true,
            marginLeftMm: 12,
            marginTopMm: 14,
            marginRightMm: 16,
            marginBottomMm: 18,
            plotScaleDenominator: 200,
            plotArea: CadPlotArea.Layout,
            plotStyle: CadPlotStyleMode.Monochrome);
        var viewport = new CadLayoutViewport(
            "Main plan",
            new CadRect(20, 30, 220, 140),
            new CadPoint(1000, 2000),
            scaleDenominator: 500,
            twistAngleRadians: 0.125,
            locked: true);
        document.SetLayoutTable(
            [new CadLayoutDefinition("Layout1", page, [viewport])],
            "Layout1");
        return document;
    }

    private static void AssertImportedDwgLayout(CadDocument document)
    {
        var layout = document.GetLayout("Layout1");
        Assert.Equal(CadPaperSize.A3.Name, layout.PageSetup.PaperSize.Name);
        Assert.True(layout.PageSetup.Landscape);
        Assert.Equal(12, layout.PageSetup.MarginLeftMm, 5);
        Assert.Equal(14, layout.PageSetup.MarginTopMm, 5);
        Assert.Equal(16, layout.PageSetup.MarginRightMm, 5);
        Assert.Equal(18, layout.PageSetup.MarginBottomMm, 5);
        Assert.Equal(200, layout.PageSetup.PlotScaleDenominator, 5);
        Assert.Equal(CadPlotArea.Layout, layout.PageSetup.PlotArea);
        Assert.Equal(CadPlotStyleMode.Monochrome, layout.PageSetup.PlotStyle);

        var importedViewport = Assert.Single(layout.Viewports);
        Assert.Equal(new CadRect(20, 30, 220, 140), importedViewport.PaperRectMm);
        Assert.Equal(new CadPoint(1000, 2000), importedViewport.ModelCenter);
        Assert.Equal(500, importedViewport.ScaleDenominator, 5);
        Assert.Equal(0.125, importedViewport.TwistAngleRadians, 8);
        Assert.True(importedViewport.Locked);
    }
}

using UCAD.Core.Entities;
using UCAD.Core.Geometry;
using UCAD.Core.IO;
using UCAD.Core.Layout;
using Xunit;

namespace UCAD.Core.Tests;

public sealed class LayoutPersistenceTests
{
    [Fact]
    public void LayoutTableIsDocumentOwnedAndRevisionTracked()
    {
        var document = new CadDocument();
        var initialRevision = document.Revision;
        var setup = new CadPageSetup(CadPaperSize.A2, landscape: true, plotScaleDenominator: 500);
        var viewport = new CadLayoutViewport(
            "Master Plan",
            CadRect.FromPoints(new CadPoint(20, 20), new CadPoint(390, 250)),
            new CadPoint(120000, 45000),
            scaleDenominator: 500,
            locked: true);
        var layout = new CadLayoutDefinition("Master", setup, [viewport]);

        Assert.True(document.SetLayoutTable([new CadLayoutDefinition("Layout1"), layout], "Master"));

        Assert.Equal(initialRevision + 1, document.Revision);
        Assert.Equal("Master", document.ActiveLayoutName);
        Assert.Equal(CadPaperSize.A2, document.ActivePageSetup.PaperSize);
        Assert.Single(document.ActiveLayout.Viewports);
        Assert.False(document.SetLayoutTable(document.Layouts, document.ActiveLayoutName));
        Assert.Equal(initialRevision + 1, document.Revision);
    }

    [Fact]
    public void LayoutCodecRoundTripsMultipleLayoutsAndViewports()
    {
        var document = new CadDocument();
        var customPaper = new CadPaperSize("Planning Sheet", 600, 900);
        var modelWindow = CadRect.FromPoints(new CadPoint(1000, 2000), new CadPoint(51000, 42000));
        var setup = new CadPageSetup(
            customPaper,
            landscape: false,
            marginLeftMm: 15,
            marginTopMm: 20,
            marginRightMm: 15,
            marginBottomMm: 20,
            plotScaleDenominator: 1000,
            plotArea: CadPlotArea.Window,
            plotStyle: CadPlotStyleMode.Grayscale,
            modelWindow: modelWindow);
        var viewportId = Guid.NewGuid();
        var viewport = new CadLayoutViewport(
            "Overview",
            CadRect.FromPoints(new CadPoint(30, 40), new CadPoint(570, 850)),
            new CadPoint(26000, 22000),
            scaleDenominator: 1000,
            twistAngleRadians: Math.PI / 12,
            locked: true,
            id: viewportId);
        var layout = new CadLayoutDefinition("Planning", setup, [viewport]);
        document.SetLayoutTable([new CadLayoutDefinition("Layout1"), layout], "Planning");

        var json = CadNativeDocumentCodecLayout.Serialize(document);
        var restored = CadNativeDocumentCodecLayout.Deserialize(json);

        Assert.True(CadNativeDocumentCodecLayout.HasLayoutExtension(json));
        Assert.Equal(2, restored.Layouts.Count);
        Assert.Equal("Planning", restored.ActiveLayoutName);
        Assert.Equal("Planning Sheet", restored.ActivePageSetup.PaperSize.Name);
        Assert.Equal(600, restored.ActivePageSetup.PaperSize.WidthMm);
        Assert.Equal(900, restored.ActivePageSetup.PaperSize.HeightMm);
        Assert.Equal(CadPlotArea.Window, restored.ActivePageSetup.PlotArea);
        Assert.Equal(CadPlotStyleMode.Grayscale, restored.ActivePageSetup.PlotStyle);
        Assert.Equal(modelWindow, restored.ActivePageSetup.ModelWindow);
        var restoredViewport = Assert.Single(restored.ActiveLayout.Viewports);
        Assert.Equal(viewportId, restoredViewport.Id);
        Assert.Equal("Overview", restoredViewport.Name);
        Assert.Equal(1000, restoredViewport.ScaleDenominator);
        Assert.Equal(Math.PI / 12, restoredViewport.TwistAngleRadians, 10);
        Assert.True(restoredViewport.Locked);
    }

    [Fact]
    public void LayoutCodecPreservesCurrentAssociativeMetadata()
    {
        var document = new CadDocument();
        var boundary = new PolylineEntity([
            new CadPoint(0, 0),
            new CadPoint(100, 0),
            new CadPoint(100, 80),
            new CadPoint(0, 80)
        ], closed: true);
        document.Add(boundary);
        document.Add(CadHatchFactory.CreateFromClosedPolyline(boundary, associative: true));
        var setup = new CadPageSetup(CadPaperSize.A3, plotScaleDenominator: 200);
        document.SetLayoutTable([new CadLayoutDefinition("Plan", setup)], "Plan");

        var json = CadNativeDocumentCodecLayout.Serialize(document);
        var restored = CadNativeDocumentCodecLayout.Deserialize(json);

        Assert.True(CadNativeDocumentCodecCurrent.HasCurrentExtension(json));
        Assert.True(CadNativeDocumentCodecLayout.HasLayoutExtension(json));
        var restoredBoundary = Assert.IsType<PolylineEntity>(restored.Entities[0]);
        var restoredHatch = Assert.IsType<HatchEntity>(restored.Entities[1]);
        Assert.True(restoredHatch.Associative);
        Assert.Single(restoredHatch.SourceEntityIds);
        Assert.Equal(restoredBoundary.Id, restoredHatch.SourceEntityIds[0]);
        Assert.Equal("Plan", restored.ActiveLayoutName);
        Assert.Equal(200, restored.ActivePageSetup.PlotScaleDenominator);
    }

    [Fact]
    public void LayoutCodecOpensCurrentCodecFilesWithoutLayoutExtension()
    {
        var document = new CadDocument();
        var legacyCurrentJson = CadNativeDocumentCodecCurrent.Serialize(document);

        var restored = CadNativeDocumentCodecLayout.Deserialize(legacyCurrentJson);

        Assert.False(CadNativeDocumentCodecLayout.HasLayoutExtension(legacyCurrentJson));
        Assert.Single(restored.Layouts);
        Assert.Equal("Layout1", restored.ActiveLayoutName);
        Assert.Equal(CadPaperSize.A3, restored.ActivePageSetup.PaperSize);
    }
}

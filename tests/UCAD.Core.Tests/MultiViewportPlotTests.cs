using System.Text;
using UCAD.Core.Entities;
using UCAD.Core.Geometry;
using UCAD.Core.Layout;
using UCAD.Core.Plot;
using Xunit;

namespace UCAD.Core.Tests;

public sealed class MultiViewportPlotTests
{
    [Fact]
    public void PdfExporterEmitsIndependentClipForEachViewport()
    {
        var document = new CadDocument();
        document.Add(new LineEntity(new CadPoint(0, 0), new CadPoint(10000, 10000)));
        var setup = new CadPageSetup(CadPaperSize.A3, landscape: true, plotScaleDenominator: 100);
        var left = new CadLayoutViewport(
            "Left",
            CadRect.FromPoints(new CadPoint(20, 20), new CadPoint(200, 277)),
            new CadPoint(2500, 5000),
            scaleDenominator: 100);
        var right = new CadLayoutViewport(
            "Right",
            CadRect.FromPoints(new CadPoint(220, 20), new CadPoint(400, 277)),
            new CadPoint(7500, 5000),
            scaleDenominator: 100);
        var plans = new[]
        {
            CadPlotPlan.FromViewport(setup, left),
            CadPlotPlan.FromViewport(setup, right)
        };

        var result = CadPdfExporter.Export(document, plans, "Two Viewports");
        var pdf = Encoding.ASCII.GetString(result.Content);

        Assert.Equal(2, CountOccurrences(pdf, " re W n"));
        Assert.Contains("Two Viewports", pdf, StringComparison.Ordinal);
        Assert.True(result.Content.Length > 500);
    }

    [Fact]
    public void PdfExporterRejectsDifferentPhysicalPaperSizesOnOnePage()
    {
        var document = new CadDocument();
        document.Add(new LineEntity(new CadPoint(0, 0), new CadPoint(100, 100)));
        var a3 = CadPlotPlan.FitExtents(
            new CadPageSetup(CadPaperSize.A3, landscape: true),
            CadRect.FromPoints(new CadPoint(0, 0), new CadPoint(100, 100)));
        var a4 = CadPlotPlan.FitExtents(
            new CadPageSetup(CadPaperSize.A4, landscape: true),
            CadRect.FromPoints(new CadPoint(0, 0), new CadPoint(100, 100)));

        Assert.Throws<ArgumentException>(() => CadPdfExporter.Export(document, [a3, a4], "Invalid"));
    }

    private static int CountOccurrences(string value, string token)
    {
        var count = 0;
        var index = 0;
        while ((index = value.IndexOf(token, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += token.Length;
        }
        return count;
    }
}

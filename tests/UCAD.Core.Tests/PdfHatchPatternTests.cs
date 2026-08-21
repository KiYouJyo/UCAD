using System.Text;
using UCAD.Core.Entities;
using UCAD.Core.Geometry;
using UCAD.Core.Layout;
using UCAD.Core.Plot;
using Xunit;

namespace UCAD.Core.Tests;

public sealed class PdfHatchPatternTests
{
    [Fact]
    public void Ansi31ExportsPatternStrokesInsteadOfBoundaryFallback()
    {
        var document = new CadDocument();
        document.Add(new HatchEntity(
            [new CadPoint(0, 0), new CadPoint(100, 0), new CadPoint(100, 100), new CadPoint(0, 100)],
            "ANSI31",
            4,
            0));
        var plan = CadPlotPlan.FitExtents(
            new CadPageSetup(CadPaperSize.A4, landscape: true),
            CadRect.FromPoints(new CadPoint(0, 0), new CadPoint(100, 100)));

        var result = CadPdfExporter.Export(document, plan, "ANSI31");
        var pdf = Encoding.ASCII.GetString(result.Content).Replace("\r\n", "\n", StringComparison.Ordinal);

        Assert.DoesNotContain(result.Warnings, warning => warning.Contains("boundary only", StringComparison.OrdinalIgnoreCase));
        Assert.True(CountOccurrences(pdf, "S\n") > 2);
    }

    [Fact]
    public void UnknownPatternRemainsExplicitBoundaryOnlyFallback()
    {
        var document = new CadDocument();
        document.Add(new HatchEntity(
            [new CadPoint(0, 0), new CadPoint(100, 0), new CadPoint(100, 100), new CadPoint(0, 100)],
            "USERPAT",
            1,
            0));
        var plan = CadPlotPlan.FitExtents(
            new CadPageSetup(CadPaperSize.A4, landscape: true),
            CadRect.FromPoints(new CadPoint(0, 0), new CadPoint(100, 100)));

        var result = CadPdfExporter.Export(document, plan, "Unknown Pattern");

        Assert.Contains(result.Warnings, warning => warning.Contains("boundary only", StringComparison.OrdinalIgnoreCase));
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

using System.Text;
using UCAD.Core.Entities;
using UCAD.Core.Geometry;
using UCAD.Core.Layout;
using UCAD.Core.Plot;
using UCAD.Core.Styles;
using Xunit;

namespace UCAD.Core.Tests;

public sealed class PdfUnicodeOutlineTests
{
    [Fact]
    public void UnicodeTextUsesVectorOutlineWithoutQuestionMarkFallback()
    {
        var document = new CadDocument();
        document.Add(new TextEntity(new CadPoint(10, 20), "都市計画", 250));
        var plan = CadPlotPlan.FitExtents(
            new CadPageSetup(CadPaperSize.A4, landscape: true),
            CadRect.FromPoints(new CadPoint(0, 0), new CadPoint(2000, 1000)));
        var provider = new FakeOutlineProvider(succeed: true);

        var result = CadPdfExporter.Export(document, plan, "Unicode", provider);
        var pdf = Encoding.ASCII.GetString(result.Content);

        Assert.Equal("都市計画", Assert.Single(provider.RequestedTexts));
        Assert.False(result.Warnings.Any(warning => warning.Contains("non-ASCII", StringComparison.OrdinalIgnoreCase)));
        Assert.DoesNotContain("(?)", pdf, StringComparison.Ordinal);
        Assert.Contains("f\n", pdf, StringComparison.Ordinal);
    }

    [Fact]
    public void FailedOutlineFallsBackToAsciiWithExplicitWarning()
    {
        var document = new CadDocument();
        document.Add(new TextEntity(new CadPoint(10, 20), "规划", 250));
        var plan = CadPlotPlan.FitExtents(
            new CadPageSetup(CadPaperSize.A4, landscape: true),
            CadRect.FromPoints(new CadPoint(0, 0), new CadPoint(2000, 1000)));
        var provider = new FakeOutlineProvider(succeed: false);

        var result = CadPdfExporter.Export(document, plan, "Fallback", provider);
        var pdf = Encoding.ASCII.GetString(result.Content);

        Assert.True(result.Warnings.Any(warning => warning.Contains("outline", StringComparison.OrdinalIgnoreCase)));
        Assert.True(result.Warnings.Any(warning => warning.Contains("non-ASCII", StringComparison.OrdinalIgnoreCase)));
        Assert.Contains("??", pdf, StringComparison.Ordinal);
    }

    private sealed class FakeOutlineProvider : ICadPdfTextOutlineProvider
    {
        private readonly bool _succeed;

        public FakeOutlineProvider(bool succeed) => _succeed = succeed;

        public List<string> RequestedTexts { get; } = [];

        public bool TryCreateOutline(
            string text,
            CadTextStyle style,
            out CadPdfTextOutline? outline,
            out string? warning)
        {
            RequestedTexts.Add(text);
            if (!_succeed)
            {
                outline = null;
                warning = "Synthetic outline failure.";
                return false;
            }

            outline = new CadPdfTextOutline([
                new CadPdfTextOutlineFigure([
                    new CadPoint(0, 0),
                    new CadPoint(1, 0),
                    new CadPoint(0.5, 1)
                ], closed: true)
            ]);
            warning = null;
            return true;
        }
    }
}

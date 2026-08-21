using UCAD.Core.Geometry;
using UCAD.Core.Layout;
using Xunit;

namespace UCAD.Core.Tests;

public sealed class LayoutViewportTilerTests
{
    [Fact]
    public void TilePlacesFourViewportsInsidePrintableAreaWithoutOverlap()
    {
        var setup = new CadPageSetup(CadPaperSize.A3, landscape: true, marginLeftMm: 10, marginTopMm: 10, marginRightMm: 10, marginBottomMm: 10);
        var printable = setup.PrintablePaperRectMm;
        var source = Enumerable.Range(0, 4)
            .Select(index => new CadLayoutViewport(
                "VP" + index,
                printable,
                new CadPoint(index * 1000, index * 500),
                100 + index,
                index * 0.1,
                locked: index % 2 == 0))
            .ToArray();
        var layout = new CadLayoutDefinition("Sheet", setup, source);

        var tiled = CadLayoutViewportTiler.Tile(layout, gapMm: 5);

        Assert.Equal(4, tiled.Viewports.Count);
        foreach (var viewport in tiled.Viewports)
            Assert.True(printable.Contains(viewport.PaperRectMm, 1e-9));
        for (var first = 0; first < tiled.Viewports.Count; first++)
        for (var second = first + 1; second < tiled.Viewports.Count; second++)
            Assert.False(HasPositiveAreaOverlap(tiled.Viewports[first].PaperRectMm, tiled.Viewports[second].PaperRectMm));
        for (var index = 0; index < source.Length; index++)
        {
            Assert.Equal(source[index].Id, tiled.Viewports[index].Id);
            Assert.Equal(source[index].ModelCenter, tiled.Viewports[index].ModelCenter);
            Assert.Equal(source[index].ScaleDenominator, tiled.Viewports[index].ScaleDenominator);
            Assert.Equal(source[index].TwistAngleRadians, tiled.Viewports[index].TwistAngleRadians);
            Assert.Equal(source[index].Locked, tiled.Viewports[index].Locked);
        }
    }

    private static bool HasPositiveAreaOverlap(CadRect first, CadRect second) =>
        Math.Min(first.Right, second.Right) - Math.Max(first.Left, second.Left) > 1e-9 &&
        Math.Min(first.Top, second.Top) - Math.Max(first.Bottom, second.Bottom) > 1e-9;
}

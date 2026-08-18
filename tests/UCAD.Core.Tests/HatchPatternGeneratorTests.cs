using UCAD.Core.Entities;
using UCAD.Core.Geometry;
using UCAD.Core.Hatching;
using Xunit;

namespace UCAD.Core.Tests;

public sealed class HatchPatternGeneratorTests
{
    [Fact]
    public void Ansi31ProducesFortyFiveDegreeSegments()
    {
        var hatch = new HatchEntity(
            [new CadPoint(0, 0), new CadPoint(100, 0), new CadPoint(100, 100), new CadPoint(0, 100)],
            "ANSI31",
            4,
            0);

        var result = CadHatchPatternGenerator.Generate(hatch);

        Assert.NotEmpty(result.Segments);
        Assert.False(result.DensityReduced);
        foreach (var segment in result.Segments.Take(10))
        {
            var delta = segment.End - segment.Start;
            Assert.Equal(Math.Abs(delta.X), Math.Abs(delta.Y), 8);
        }
    }

    [Fact]
    public void Ansi31EvenOddClippingDoesNotDrawThroughIsland()
    {
        var hatch = new HatchEntity(
            [new CadPoint(0, 0), new CadPoint(100, 0), new CadPoint(100, 100), new CadPoint(0, 100)],
            "ANSI31",
            2,
            0,
            islands:
            [
                [new CadPoint(30, 30), new CadPoint(70, 30), new CadPoint(70, 70), new CadPoint(30, 70)]
            ],
            associative: false,
            sourceEntityIds: null,
            islandDetection: HatchIslandDetection.Normal);

        var result = CadHatchPatternGenerator.Generate(hatch);

        Assert.NotEmpty(result.Segments);
        foreach (var segment in result.Segments)
        {
            var midpoint = new CadPoint(
                (segment.Start.X + segment.End.X) / 2,
                (segment.Start.Y + segment.End.Y) / 2);
            Assert.False(midpoint.X > 30 && midpoint.X < 70 && midpoint.Y > 30 && midpoint.Y < 70);
        }
    }

    [Fact]
    public void VeryDensePatternUsesBoundedRenderingDensity()
    {
        var hatch = new HatchEntity(
            [new CadPoint(0, 0), new CadPoint(100000, 0), new CadPoint(100000, 100000), new CadPoint(0, 100000)],
            "ANSI31",
            0.1,
            0);

        var result = CadHatchPatternGenerator.Generate(hatch, maxScanLines: 100);

        Assert.True(result.DensityReduced);
        Assert.True(result.EffectiveSpacing > result.RequestedSpacing);
        Assert.InRange(result.Segments.Count, 1, 100);
    }
}

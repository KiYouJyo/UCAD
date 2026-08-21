using UCAD.Core.Entities;
using UCAD.Core.Geometry;
using Xunit;

namespace UCAD.Core.Tests;

public sealed class HatchEnhancementTests
{
    [Fact]
    public void AdvancedHatchPreservesIslandsAndAssociativeSources()
    {
        var sourceA = Guid.NewGuid();
        var sourceB = Guid.NewGuid();
        var hatch = new HatchEntity(
            [new CadPoint(0, 0), new CadPoint(20, 0), new CadPoint(20, 20), new CadPoint(0, 20)],
            "ANSI31",
            2,
            Math.PI / 4,
            islands:
            [
                [new CadPoint(5, 5), new CadPoint(10, 5), new CadPoint(10, 10), new CadPoint(5, 10)],
                [new CadPoint(12, 12), new CadPoint(15, 12), new CadPoint(15, 15), new CadPoint(12, 15)]
            ],
            associative: true,
            sourceEntityIds: [sourceA, sourceB],
            islandDetection: HatchIslandDetection.Normal);

        Assert.Equal(2, hatch.Islands.Count);
        Assert.True(hatch.Associative);
        Assert.Equal(2, hatch.SourceEntityIds.Count);
        Assert.Equal(2, hatch.EffectiveIslandLoops.Count());
    }

    [Fact]
    public void IslandDetectionModesControlEffectiveLoops()
    {
        var outer = new[] { new CadPoint(0, 0), new CadPoint(20, 0), new CadPoint(20, 20), new CadPoint(0, 20) };
        var islands = new[]
        {
            new[] { new CadPoint(2, 2), new CadPoint(4, 2), new CadPoint(4, 4), new CadPoint(2, 4) },
            new[] { new CadPoint(8, 8), new CadPoint(10, 8), new CadPoint(10, 10), new CadPoint(8, 10) }
        };

        var normal = new HatchEntity(outer, "Solid", 1, 0, islands, false, null, HatchIslandDetection.Normal);
        var outerOnly = new HatchEntity(outer, "Solid", 1, 0, islands, false, null, HatchIslandDetection.Outer);
        var ignored = new HatchEntity(outer, "Solid", 1, 0, islands, false, null, HatchIslandDetection.Ignore);

        Assert.Equal(2, normal.EffectiveIslandLoops.Count());
        Assert.Single(outerOnly.EffectiveIslandLoops);
        Assert.Empty(ignored.EffectiveIslandLoops);
    }

    [Fact]
    public void AssociativeHatchRequiresBoundarySourceIds()
    {
        var boundary = new[] { new CadPoint(0, 0), new CadPoint(10, 0), new CadPoint(10, 10), new CadPoint(0, 10) };
        Assert.Throws<ArgumentException>(() => new HatchEntity(boundary, "Solid", 1, 0, null, true, null));
    }
}
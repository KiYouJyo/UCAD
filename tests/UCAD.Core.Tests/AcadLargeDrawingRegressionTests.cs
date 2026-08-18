using UCAD.Core.Blocks;
using UCAD.Core.Entities;
using UCAD.Core.Geometry;
using UCAD.Core.IO;
using UCAD.Core.Layers;
using UCAD.Core.Layout;
using UCAD.Core.Styles;
using Xunit;

namespace UCAD.Core.Tests;

public sealed class AcadLargeDrawingRegressionTests
{
    [Fact]
    public void LargeComplexPlanningDrawingSurvivesDwgRoundTripWithoutSemanticCollapse()
    {
        var source = CreateLargePlanningDocument();

        Assert.Equal(12_000, source.Entities.Count);

        var exported = CadAcadInteropCodec.ExportDwg(source);
        var imported = CadAcadInteropCodec.ImportDwg(exported.Content);

        Assert.True(exported.Content.Length > 500_000, $"Stress DWG unexpectedly small: {exported.Content.Length} bytes.");
        Assert.True(imported.Document.Entities.Count >= 12_000, $"Expected at least 12,000 semantic entities after round-trip, got {imported.Document.Entities.Count}.");
        Assert.True(imported.Document.Entities.OfType<LineEntity>().Count() >= 4_000);
        Assert.True(imported.Document.Entities.OfType<PolylineEntity>().Count() >= 3_500);
        Assert.True(imported.Document.Entities.OfType<TextEntity>().Count() >= 2_000);
        Assert.True(imported.Document.Entities.OfType<CircleEntity>().Count() >= 1_000);
        Assert.True(imported.Document.Entities.OfType<LinearDimensionEntity>().Count() >= 400);
        Assert.True(imported.Document.Entities.OfType<LeaderEntity>().Count() >= 250);
        Assert.True(imported.Document.Entities.OfType<BlockReferenceEntity>().Count() >= 600);
        Assert.True(imported.Document.Entities.OfType<HatchEntity>().Count() >= 250);
        Assert.True(imported.Document.TryGetBlock("SITE_TAG", out var block));
        Assert.NotNull(block);
        Assert.Single(block!.AttributeDefinitions);
        Assert.True(imported.Document.TryGetDimensionStyle("Plan-500", out _));

        var layout = imported.Document.GetLayout("Layout1");
        Assert.Equal(CadPaperSize.A1.Name, layout.PageSetup.PaperSize.Name);
        Assert.True(layout.PageSetup.Landscape);
        Assert.Single(layout.Viewports);
    }

    private static CadDocument CreateLargePlanningDocument()
    {
        var document = new CadDocument();
        var layerDefinitions = new[]
        {
            new CadLayer("GRID", "#8A8A8A", 0.13, "Continuous"),
            new CadLayer("ROAD", "#D0A060", 0.35, "Continuous"),
            new CadLayer("PARCEL", "#80A080", 0.25, "Continuous"),
            new CadLayer("BUILDING", "#A08080", 0.25, "Continuous"),
            new CadLayer("TEXT", "#D8D8D8", 0.18, "Continuous"),
            new CadLayer("ANNO", "#A8B8D8", 0.18, "Continuous"),
            new CadLayer("HATCH", "#B8B0A0", 0.13, "Continuous"),
            new CadLayer("TAG", "#D0D0A0", 0.18, "Continuous")
        };
        foreach (var layer in layerDefinitions) document.CreateLayer(layer);

        var dimensionStyle = new CadDimensionStyle("Plan-500", textHeight: 2.5, arrowSize: 2.0, precision: 1, suffix: " m");
        document.DefineDimensionStyle(dimensionStyle);
        document.SetCurrentDimensionStyle(dimensionStyle.Name);

        var tagDefinition = new CadBlockDefinition(
            "SITE_TAG",
            new CadPoint(0, 0),
            [
                new CircleEntity(new CadPoint(0, 0), 2.5),
                new LineEntity(new CadPoint(-2.5, 0), new CadPoint(2.5, 0))
            ],
            [new CadBlockAttributeDefinition("ID", "Site identifier", "0000", new CadPoint(3.5, 0), 2.0)]);
        document.DefineBlock(tagDefinition);

        var entities = new List<(ICadEntity Entity, CadEntityProperties Properties)>(12_000);

        // 4,000 grid/road lines.
        for (var i = 0; i < 2_000; i++)
        {
            var x = (i % 100) * 20.0;
            var offset = (i / 100) * 0.25;
            entities.Add((
                new LineEntity(new CadPoint(x + offset, 0), new CadPoint(x + offset, 1_000)),
                new CadEntityProperties(i % 5 == 0 ? "ROAD" : "GRID")));

            var y = (i % 50) * 20.0 + (i / 50) * 0.125;
            entities.Add((
                new LineEntity(new CadPoint(0, y), new CadPoint(2_000, y)),
                new CadEntityProperties(i % 7 == 0 ? "ROAD" : "GRID")));
        }

        // 3,500 parcel polygons.
        for (var i = 0; i < 3_500; i++)
        {
            var column = i % 70;
            var row = i / 70;
            var left = column * 25.0 + 1.0;
            var bottom = row * 18.0 + 1.0;
            entities.Add((
                new PolylineEntity(
                    [
                        new CadPoint(left, bottom),
                        new CadPoint(left + 22, bottom),
                        new CadPoint(left + 22, bottom + 15),
                        new CadPoint(left, bottom + 15)
                    ],
                    closed: true),
                new CadEntityProperties("PARCEL")));
        }

        // 2,000 labels.
        for (var i = 0; i < 2_000; i++)
        {
            entities.Add((
                new TextEntity(
                    new CadPoint((i % 100) * 18.0 + 2, (i / 100) * 24.0 + 3),
                    $"P-{i + 1:0000}",
                    2.2,
                    (i % 4) * Math.PI / 24),
                new CadEntityProperties("TEXT")));
        }

        // 1,000 survey / facility circles.
        for (var i = 0; i < 1_000; i++)
        {
            entities.Add((
                new CircleEntity(new CadPoint((i % 50) * 36.0 + 10, (i / 50) * 30.0 + 10), 2 + (i % 4) * 0.5),
                new CadEntityProperties("BUILDING")));
        }

        // 400 dimensions.
        for (var i = 0; i < 400; i++)
        {
            var x = (i % 40) * 45.0;
            var y = 760 + (i / 40) * 8.0;
            entities.Add((
                new LinearDimensionEntity(
                    new CadPoint(x, y),
                    new CadPoint(x + 30, y),
                    new CadPoint(x, y + 5),
                    styleName: dimensionStyle.Name),
                new CadEntityProperties("ANNO")));
        }

        // 250 leaders.
        for (var i = 0; i < 250; i++)
        {
            var x = (i % 25) * 70.0 + 5;
            var y = 860 + (i / 25) * 12.0;
            entities.Add((
                new LeaderEntity(
                    [new CadPoint(x, y), new CadPoint(x + 8, y + 5), new CadPoint(x + 18, y + 5)],
                    $"NOTE {i + 1}",
                    2.2,
                    dimensionStyle.Name),
                new CadEntityProperties("ANNO")));
        }

        // 600 attributed block references.
        for (var i = 0; i < 600; i++)
        {
            var reference = CadBlockFactory.CreateReference(
                tagDefinition,
                new CadPoint((i % 60) * 30.0 + 8, 520 + (i / 60) * 22.0),
                1.0,
                (i % 8) * Math.PI / 16,
                new Dictionary<string, string> { ["ID"] = $"S-{i + 1:0000}" });
            entities.Add((reference, new CadEntityProperties("TAG")));
        }

        // 250 bounded hatches.
        for (var i = 0; i < 250; i++)
        {
            var x = (i % 25) * 70.0 + 4;
            var y = 650 + (i / 25) * 10.0;
            entities.Add((
                new HatchEntity(
                    [
                        new CadPoint(x, y),
                        new CadPoint(x + 24, y),
                        new CadPoint(x + 24, y + 7),
                        new CadPoint(x, y + 7)
                    ],
                    "ANSI31",
                    1.5,
                    Math.PI / 4),
                new CadEntityProperties("HATCH")));
        }

        document.AddRange(entities);
        document.SetLayoutTable(
            [new CadLayoutDefinition(
                "Layout1",
                new CadPageSetup(CadPaperSize.A1, landscape: true, plotScaleDenominator: 500),
                [new CadLayoutViewport(
                    "Overall plan",
                    new CadRect(20, 20, 820, 570),
                    new CadPoint(1_000, 500),
                    scaleDenominator: 500,
                    locked: true)])],
            "Layout1");
        document.ResetHistory();
        return document;
    }
}

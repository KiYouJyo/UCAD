using UCAD.Core.Entities;
using UCAD.Core.Geometry;
using UCAD.Core.IO;
using UCAD.Core.Layers;
using Xunit;

namespace UCAD.Core.Tests;

public sealed class DxfCodecTests
{
    [Fact]
    public void ExportImportRoundTripPreservesFoundationalGeometryAndProperties()
    {
        var document = new CadDocument();
        document.CreateLayer(new CadLayer("Road", "#8AA4B8", 0.50, "Continuous", isVisible: true, isLocked: false));
        document.SetCurrentLayer("Road");

        var line = new LineEntity(new CadPoint(0, 0), new CadPoint(100, 20));
        var polyline = new PolylineEntity(
            [new CadPoint(0, 0), new CadPoint(10, 0), new CadPoint(10, 10), new CadPoint(0, 10)],
            closed: true);
        var circle = new CircleEntity(new CadPoint(50, 50), 12.5);
        Assert.True(ArcEntity.TryCreateFromThreePoints(
            new CadPoint(20, 0),
            new CadPoint(25, 5),
            new CadPoint(30, 0),
            out var arc));
        var text = new TextEntity(new CadPoint(5, 7), "UCAD", 3.5, Math.PI / 6);

        document.Add(line, new CadEntityProperties("Road", "#336699", 0.35, "Continuous"));
        document.Add(polyline, new CadEntityProperties("Road"));
        document.Add(circle, new CadEntityProperties("Road"));
        document.Add(arc!, new CadEntityProperties("Road"));
        document.Add(text, new CadEntityProperties("Road"));

        var exported = CadDxfCodec.Export(document);
        Assert.False(exported.HasWarnings);
        Assert.Contains("AC1032", exported.Content, StringComparison.Ordinal);
        Assert.Contains("LWPOLYLINE", exported.Content, StringComparison.Ordinal);

        var imported = CadDxfCodec.Import(exported.Content);
        Assert.False(imported.HasWarnings);
        Assert.Equal(5, imported.Document.Entities.Count);
        Assert.Equal("Road", imported.Document.CurrentLayerName);

        var importedLayer = imported.Document.GetLayer("Road");
        Assert.Equal("#8AA4B8", importedLayer.ColorHex);
        Assert.Equal(0.50, importedLayer.LineWeight, 6);

        var importedLine = Assert.IsType<LineEntity>(imported.Document.Entities[0]);
        Assert.Equal(line.Start, importedLine.Start);
        Assert.Equal(line.End, importedLine.End);
        var importedLineProperties = imported.Document.GetEntityProperties(importedLine.Id);
        Assert.Equal("Road", importedLineProperties.LayerName);
        Assert.Equal("#336699", importedLineProperties.ColorHex);
        Assert.Equal(0.35, importedLineProperties.LineWeight);

        var importedPolyline = Assert.IsType<PolylineEntity>(imported.Document.Entities[1]);
        Assert.True(importedPolyline.Closed);
        Assert.Equal(4, importedPolyline.Points.Count);

        var importedCircle = Assert.IsType<CircleEntity>(imported.Document.Entities[2]);
        Assert.Equal(circle.Center, importedCircle.Center);
        Assert.Equal(circle.Radius, importedCircle.Radius, 8);

        var importedArc = Assert.IsType<ArcEntity>(imported.Document.Entities[3]);
        Assert.Equal(arc!.Center.X, importedArc.Center.X, 8);
        Assert.Equal(arc.Center.Y, importedArc.Center.Y, 8);
        Assert.Equal(arc.Radius, importedArc.Radius, 8);

        var importedText = Assert.IsType<TextEntity>(imported.Document.Entities[4]);
        Assert.Equal("UCAD", importedText.Text);
        Assert.Equal(3.5, importedText.Height, 8);
        Assert.Equal(Math.PI / 6, importedText.RotationRadians, 8);
    }

    [Fact]
    public void MTextRoundTripPreservesContentGeometryAndStyle()
    {
        var document = new CadDocument();
        var mtext = new MTextEntity(
            new CadPoint(12, 34),
            "First line\nSecond line",
            textHeight: 4.2,
            width: 88,
            rotationRadians: Math.PI / 5,
            styleName: "Standard");
        document.Add(mtext);

        var exported = CadDxfCodec.Export(document);
        var imported = CadDxfCodec.Import(exported.Content);

        Assert.False(exported.HasWarnings);
        Assert.False(imported.HasWarnings);
        Assert.Contains("MTEXT", exported.Content, StringComparison.Ordinal);
        Assert.Contains("\\P", exported.Content, StringComparison.Ordinal);
        var actual = Assert.IsType<MTextEntity>(Assert.Single(imported.Document.Entities));
        Assert.Equal(mtext.Position, actual.Position);
        Assert.Equal(mtext.Text, actual.Text);
        Assert.Equal(mtext.TextHeight, actual.TextHeight, 8);
        Assert.Equal(mtext.Width, actual.Width, 8);
        Assert.Equal(mtext.RotationRadians, actual.RotationRadians, 8);
        Assert.Equal(mtext.StyleName, actual.StyleName);
    }

    [Fact]
    public void HatchRoundTripPreservesPolylineBoundaryIslandsAndPatternMetadata()
    {
        var document = new CadDocument();
        var boundary = new[]
        {
            new CadPoint(0, 0),
            new CadPoint(100, 0),
            new CadPoint(100, 80),
            new CadPoint(0, 80)
        };
        var island = new[]
        {
            new CadPoint(20, 20),
            new CadPoint(40, 20),
            new CadPoint(40, 40),
            new CadPoint(20, 40)
        };
        var hatch = new HatchEntity(
            boundary,
            "ANSI31",
            patternScale: 2.5,
            patternAngleRadians: Math.PI / 4,
            islands: [island],
            associative: false,
            sourceEntityIds: null,
            islandDetection: HatchIslandDetection.Normal);
        document.Add(hatch);

        var exported = CadDxfCodec.Export(document);
        var imported = CadDxfCodec.Import(exported.Content);

        Assert.False(exported.HasWarnings);
        Assert.False(imported.HasWarnings);
        Assert.Contains("HATCH", exported.Content, StringComparison.Ordinal);
        var actual = Assert.IsType<HatchEntity>(Assert.Single(imported.Document.Entities));
        Assert.Equal(hatch.Pattern, actual.Pattern);
        Assert.Equal(hatch.PatternScale, actual.PatternScale, 8);
        Assert.Equal(hatch.PatternAngleRadians, actual.PatternAngleRadians, 8);
        Assert.Equal(4, actual.Boundary.Count);
        Assert.Single(actual.Islands);
        Assert.Equal(4, actual.Islands[0].Count);
    }

    [Fact]
    public void ImportSkipsUnsupportedEntitiesWithExplicitWarning()
    {
        const string dxf = """
0
SECTION
2
ENTITIES
0
3DSOLID
8
0
0
ENDSEC
0
EOF
""";

        var result = CadDxfCodec.Import(dxf);

        Assert.Empty(result.Document.Entities);
        Assert.True(result.HasWarnings);
        Assert.Contains(result.Warnings, warning => warning.Contains("3DSOLID", StringComparison.Ordinal));
    }

    [Fact]
    public void ExportReportsAuthoringEntitiesThatNeedLaterDxfMilestones()
    {
        var document = new CadDocument();
        document.Add(new LinearDimensionEntity(
            new CadPoint(0, 0),
            new CadPoint(10, 0),
            new CadPoint(0, 5)));

        var result = CadDxfCodec.Export(document);

        Assert.True(result.HasWarnings);
        Assert.Contains(result.Warnings, warning => warning.Contains(nameof(LinearDimensionEntity), StringComparison.Ordinal));
        Assert.DoesNotContain("DIMENSION", result.Content, StringComparison.Ordinal);
    }
}

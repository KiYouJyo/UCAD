using UCAD.Core.Blocks;
using UCAD.Core.Entities;
using UCAD.Core.Geometry;
using UCAD.Core.IO;
using UCAD.Core.Styles;
using Xunit;

namespace UCAD.Core.Tests;

public sealed class DxfAdvancedInteropCodecTests
{
    [Fact]
    public void AdvancedRoundTripPreservesDimensionsAndDimensionStyle()
    {
        var document = new CadDocument();
        var style = new CadDimensionStyle("Architectural", textHeight: 3.2, arrowSize: 2.0, precision: 1, prefix: "[", suffix: "]");
        document.DefineDimensionStyle(style);
        document.SetCurrentDimensionStyle(style.Name);
        document.Add(new LinearDimensionEntity(
            new CadPoint(0, 0),
            new CadPoint(20, 0),
            new CadPoint(0, 5),
            "20 EQ",
            style.Name));
        document.Add(new AngularDimensionEntity(
            new CadPoint(0, 0),
            new CadPoint(10, 0),
            new CadPoint(0, 10),
            new CadPoint(7, 7),
            textOverride: null,
            styleName: style.Name));

        var exported = CadDxfAdvancedInteropCodec.Export(document);
        var imported = CadDxfAdvancedInteropCodec.Import(exported.Content);

        Assert.False(exported.HasWarnings);
        Assert.False(imported.HasWarnings);
        Assert.Contains("DIMENSION", exported.Content, StringComparison.Ordinal);
        Assert.Contains("DIMSTYLE", exported.Content, StringComparison.Ordinal);
        Assert.Equal("Architectural", imported.Document.CurrentDimensionStyleName);

        var importedStyle = imported.Document.GetDimensionStyle("Architectural");
        Assert.Equal(3.2, importedStyle.TextHeight, 8);
        Assert.Equal(2.0, importedStyle.ArrowSize, 8);
        Assert.Equal(1, importedStyle.Precision);
        Assert.Equal("[", importedStyle.Prefix);
        Assert.Equal("]", importedStyle.Suffix);

        var linear = Assert.IsType<LinearDimensionEntity>(imported.Document.Entities[0]);
        Assert.Equal(new CadPoint(0, 0), linear.FirstExtensionPoint);
        Assert.Equal(new CadPoint(20, 0), linear.SecondExtensionPoint);
        Assert.Equal(new CadPoint(0, 5), linear.DimensionLinePoint);
        Assert.Equal("20 EQ", linear.TextOverride);
        Assert.Equal("Architectural", linear.StyleName);

        var angular = Assert.IsType<AngularDimensionEntity>(imported.Document.Entities[1]);
        Assert.Equal(new CadPoint(0, 0), angular.Vertex);
        Assert.Equal(new CadPoint(10, 0), angular.FirstRayPoint);
        Assert.Equal(new CadPoint(0, 10), angular.SecondRayPoint);
        Assert.Equal(new CadPoint(7, 7), angular.ArcPoint);
        Assert.Equal("Architectural", angular.StyleName);
    }

    [Fact]
    public void AdvancedRoundTripPreservesBlockInsertAndAttributeValues()
    {
        var document = new CadDocument();
        var definition = new CadBlockDefinition(
            "DOOR_TAG",
            new CadPoint(0, 0),
            [new LineEntity(new CadPoint(0, 0), new CadPoint(10, 0))],
            [new CadBlockAttributeDefinition("ID", "Identifier", "A1", new CadPoint(2, 1), textHeight: 2)]);
        document.DefineBlock(definition);
        var reference = CadBlockFactory.CreateReference(
            definition,
            new CadPoint(100, 20),
            scale: 2,
            rotationRadians: Math.PI / 2,
            attributeValues: new Dictionary<string, string> { ["ID"] = "B7" });
        document.Add(reference);

        var exported = CadDxfAdvancedInteropCodec.Export(document);
        var imported = CadDxfAdvancedInteropCodec.Import(exported.Content);

        Assert.False(exported.HasWarnings);
        Assert.False(imported.HasWarnings);
        Assert.Contains("BLOCK", exported.Content, StringComparison.Ordinal);
        Assert.Contains("ATTDEF", exported.Content, StringComparison.Ordinal);
        Assert.Contains("INSERT", exported.Content, StringComparison.Ordinal);
        Assert.Contains("ATTRIB", exported.Content, StringComparison.Ordinal);

        var importedDefinition = imported.Document.GetBlock("DOOR_TAG");
        Assert.Single(importedDefinition.Entities);
        var attribute = Assert.Single(importedDefinition.AttributeDefinitions);
        Assert.Equal("ID", attribute.Tag);
        Assert.Equal("A1", attribute.DefaultValue);

        var importedReference = Assert.IsType<BlockReferenceEntity>(Assert.Single(imported.Document.Entities));
        Assert.Equal("DOOR_TAG", importedReference.DefinitionName);
        Assert.Equal(new CadPoint(100, 20), importedReference.InsertionPoint);
        Assert.Equal(2, importedReference.Scale, 8);
        Assert.Equal(Math.PI / 2, importedReference.RotationRadians, 8);
        Assert.Equal("B7", importedReference.AttributeValues["ID"]);
        Assert.Single(importedReference.Contents);
    }

    [Fact]
    public void AdvancedRoundTripPreservesLeaderThroughLinkedMTextAnnotation()
    {
        var document = new CadDocument();
        document.Add(new LeaderEntity(
            [new CadPoint(0, 0), new CadPoint(10, 5), new CadPoint(20, 5)],
            "Road width\n20.0 m",
            textHeight: 3.0,
            styleName: "Standard"));

        var exported = CadDxfAdvancedInteropCodec.Export(document);
        var imported = CadDxfAdvancedInteropCodec.Import(exported.Content);

        Assert.False(exported.HasWarnings);
        Assert.False(imported.HasWarnings);
        Assert.Contains("LEADER", exported.Content, StringComparison.Ordinal);
        Assert.Contains("MTEXT", exported.Content, StringComparison.Ordinal);
        var leader = Assert.IsType<LeaderEntity>(Assert.Single(imported.Document.Entities));
        Assert.Equal("Road width\n20.0 m", leader.Text);
        Assert.Equal(3.0, leader.TextHeight, 8);
        Assert.Equal(3, leader.Points.Count);
        Assert.Equal(new CadPoint(20, 5), leader.Points[^1]);
    }

    [Fact]
    public void NonUniformInsertIsRejectedInsteadOfDistortingBlockGeometry()
    {
        const string dxf = """
0
SECTION
2
BLOCKS
0
BLOCK
8
0
2
TAG
70
0
10
0
20
0
30
0
3
TAG
1

0
LINE
8
0
10
0
20
0
11
10
21
0
0
ENDBLK
8
0
0
ENDSEC
0
SECTION
2
ENTITIES
0
INSERT
8
0
2
TAG
10
0
20
0
41
2
42
3
0
ENDSEC
0
EOF
""";

        var imported = CadDxfAdvancedInteropCodec.Import(dxf);

        Assert.Empty(imported.Document.Entities);
        Assert.True(imported.HasWarnings);
        Assert.Contains(imported.Warnings, warning => warning.Contains("non-uniform", StringComparison.OrdinalIgnoreCase));
        Assert.True(imported.Document.TryGetBlock("TAG", out _));
    }
}

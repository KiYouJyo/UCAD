using UCAD.Core.Blocks;
using UCAD.Core.Entities;
using UCAD.Core.Geometry;
using UCAD.Core.IO;
using UCAD.Core.Styles;
using Xunit;

namespace UCAD.Core.Tests;

public sealed class AcadAdvancedInteropTests
{
    [Fact]
    public void DwgRoundTripPreservesDimensionLeaderAndAttributedBlockReference()
    {
        var document = new CadDocument();
        var dimensionStyle = new CadDimensionStyle("Plan", textHeight: 3, arrowSize: 2.5, precision: 1, suffix: " m");
        document.DefineDimensionStyle(dimensionStyle);
        document.SetCurrentDimensionStyle(dimensionStyle.Name);

        document.Add(new LinearDimensionEntity(
            new CadPoint(0, 0),
            new CadPoint(25, 0),
            new CadPoint(0, 4),
            textOverride: null,
            styleName: dimensionStyle.Name));
        document.Add(new LeaderEntity(
            [new CadPoint(0, 10), new CadPoint(8, 14), new CadPoint(15, 14)],
            "SETBACK",
            2.5,
            dimensionStyle.Name));

        var definition = new CadBlockDefinition(
            "PARCEL_TAG",
            new CadPoint(0, 0),
            [new CircleEntity(new CadPoint(0, 0), 2)],
            [new CadBlockAttributeDefinition("NO", "Parcel number", "001", new CadPoint(3, 0), 2)]);
        document.DefineBlock(definition);
        document.Add(CadBlockFactory.CreateReference(
            definition,
            new CadPoint(50, 50),
            scale: 1.5,
            rotationRadians: Math.PI / 6,
            attributeValues: new Dictionary<string, string> { ["NO"] = "A-12" }));

        var exported = CadAcadInteropCodec.ExportDwg(document);
        var imported = CadAcadInteropCodec.ImportDwg(exported.Content);

        Assert.NotEmpty(exported.Content);
        Assert.Contains(imported.Document.Entities, entity => entity is LinearDimensionEntity);
        Assert.Contains(imported.Document.Entities, entity => entity is LeaderEntity);
        var reference = Assert.Single(imported.Document.Entities.OfType<BlockReferenceEntity>());
        Assert.Equal("PARCEL_TAG", reference.DefinitionName);
        Assert.Equal("A-12", reference.AttributeValues["NO"]);
        Assert.True(imported.Document.TryGetBlock("PARCEL_TAG", out var importedDefinition));
        Assert.NotNull(importedDefinition);
        Assert.Single(importedDefinition!.AttributeDefinitions);
        Assert.True(imported.Document.TryGetDimensionStyle("Plan", out _));
    }

    [Fact]
    public void BinaryDxfRoundTripUsesSameAdvancedSemanticBridge()
    {
        var document = new CadDocument();
        document.Add(new AngularDimensionEntity(
            new CadPoint(0, 0),
            new CadPoint(10, 0),
            new CadPoint(0, 10),
            new CadPoint(6, 6)));

        var exported = CadAcadInteropCodec.ExportBinaryDxf(document);
        var imported = CadAcadInteropCodec.ImportDxf(exported.Content);

        var dimension = Assert.IsType<AngularDimensionEntity>(Assert.Single(imported.Document.Entities));
        Assert.Equal(new CadPoint(0, 0), dimension.Vertex);
        Assert.Equal(new CadPoint(10, 0), dimension.FirstRayPoint);
        Assert.Equal(new CadPoint(0, 10), dimension.SecondRayPoint);
    }
}

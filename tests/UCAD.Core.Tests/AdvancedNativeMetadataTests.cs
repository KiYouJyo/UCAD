using UCAD.Core.Blocks;
using UCAD.Core.Entities;
using UCAD.Core.Geometry;
using UCAD.Core.IO;
using UCAD.Core.Modify;
using Xunit;

namespace UCAD.Core.Tests;

public sealed class AdvancedNativeMetadataTests
{
    [Fact]
    public void V11NativeCodecPreservesAdvancedHatchMetadata()
    {
        var document = new CadDocument();
        var source = new PolylineEntity([
            new CadPoint(0, 0),
            new CadPoint(20, 0),
            new CadPoint(20, 20),
            new CadPoint(0, 20)
        ], closed: true);
        document.Add(source);
        var hatch = new HatchEntity(
            source.Points,
            "ANSI31",
            2,
            Math.PI / 4,
            islands:
            [
                [new CadPoint(5, 5), new CadPoint(10, 5), new CadPoint(10, 10), new CadPoint(5, 10)]
            ],
            associative: true,
            sourceEntityIds: [source.Id],
            islandDetection: HatchIslandDetection.Outer);
        document.Add(hatch);

        var json = CadNativeDocumentCodecV11.Serialize(document);
        var restored = CadNativeDocumentCodecV11.Deserialize(json);

        Assert.True(CadNativeDocumentCodecV11.HasV11Extension(json));
        var restoredHatch = Assert.IsType<HatchEntity>(restored.Entities[1]);
        Assert.Single(restoredHatch.Islands);
        Assert.True(restoredHatch.Associative);
        Assert.Equal(HatchIslandDetection.Outer, restoredHatch.IslandDetection);
        Assert.Single(restoredHatch.SourceEntityIds);
    }

    [Fact]
    public void V11NativeCodecPreservesBlockAttributesAndXrefPath()
    {
        var document = new CadDocument();
        var xrefPath = Path.Combine(Path.GetTempPath(), "ucad-xref-test.ucad");
        var definition = new CadBlockDefinition(
            "TREE",
            new CadPoint(0, 0),
            [new CircleEntity(new CadPoint(0, 0), 1)],
            [
                new CadBlockAttributeDefinition("ID", "Tree ID", "T-001", new CadPoint(0, 2)),
                new CadBlockAttributeDefinition("TYPE", "Type", "Oak", new CadPoint(0, 3), constant: true)
            ],
            xrefPath);
        document.DefineBlock(definition);
        document.Add(CadBlockFactory.CreateReference(
            definition,
            new CadPoint(10, 10),
            attributeValues: new Dictionary<string, string> { ["ID"] = "T-008" }));

        var restored = CadNativeDocumentCodecV11.Deserialize(CadNativeDocumentCodecV11.Serialize(document));

        var restoredDefinition = restored.GetBlock("TREE");
        Assert.True(restoredDefinition.IsExternalReference);
        Assert.Equal(Path.GetFullPath(xrefPath), restoredDefinition.ExternalSourcePath);
        Assert.Equal(2, restoredDefinition.AttributeDefinitions.Count);
        var reference = Assert.IsType<BlockReferenceEntity>(restored.Entities[0]);
        Assert.Equal("T-008", reference.AttributeValues["ID"]);
        Assert.Equal("Oak", reference.AttributeValues["TYPE"]);
    }

    [Fact]
    public void V11CodecLeavesPlainV1DocumentsWithoutExtension()
    {
        var document = new CadDocument();
        document.Add(new LineEntity(new CadPoint(0, 0), new CadPoint(10, 0)));

        var json = CadNativeDocumentCodecV11.Serialize(document);
        var restored = CadNativeDocumentCodecV11.Deserialize(json);

        Assert.False(CadNativeDocumentCodecV11.HasV11Extension(json));
        Assert.Single(restored.Entities);
        Assert.IsType<LineEntity>(restored.Entities[0]);
    }

    [Fact]
    public void AdvancedTransformPreservesHatchAssociationAndIslands()
    {
        var sourceId = Guid.NewGuid();
        var hatch = new HatchEntity(
            [new CadPoint(0, 0), new CadPoint(10, 0), new CadPoint(10, 10), new CadPoint(0, 10)],
            "ANSI31",
            1.5,
            0.25,
            [[new CadPoint(2, 2), new CadPoint(4, 2), new CadPoint(4, 4), new CadPoint(2, 4)]],
            true,
            [sourceId],
            HatchIslandDetection.Normal);

        var moved = Assert.IsType<HatchEntity>(CadAdvancedEntityTransform.Translate(hatch, new CadVector(100, 50)));
        var rotated = Assert.IsType<HatchEntity>(CadAdvancedEntityTransform.Rotate(moved, new CadPoint(0, 0), Math.PI / 2));
        var scaled = Assert.IsType<HatchEntity>(CadAdvancedEntityTransform.Scale(rotated, new CadPoint(0, 0), 2));

        Assert.Equal(hatch.Id, scaled.Id);
        Assert.True(scaled.Associative);
        Assert.Single(scaled.Islands);
        Assert.Equal(sourceId, scaled.SourceEntityIds.Single());
        Assert.Equal(3, scaled.PatternScale, 8);
        Assert.Equal(hatch.PatternAngleRadians + (Math.PI / 2), scaled.PatternAngleRadians, 8);
    }

    [Fact]
    public void AdvancedTransformPreservesBlockAttributeValues()
    {
        var definition = new CadBlockDefinition(
            "TREE",
            new CadPoint(0, 0),
            [new CircleEntity(new CadPoint(0, 0), 1)],
            [new CadBlockAttributeDefinition("ID", "ID", "1", new CadPoint(0, 2))]);
        var reference = CadBlockFactory.CreateReference(
            definition,
            new CadPoint(10, 10),
            attributeValues: new Dictionary<string, string> { ["ID"] = "42" });

        var copied = Assert.IsType<BlockReferenceEntity>(CadAdvancedEntityTransform.Translate(
            reference,
            new CadVector(5, 7),
            preserveIdentity: false));

        Assert.NotEqual(reference.Id, copied.Id);
        Assert.Equal("42", copied.AttributeValues["ID"]);
        Assert.Equal(new CadPoint(15, 17), copied.InsertionPoint);
    }
}
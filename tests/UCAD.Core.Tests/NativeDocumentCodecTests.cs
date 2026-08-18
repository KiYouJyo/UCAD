using UCAD.Core.Blocks;
using UCAD.Core.Entities;
using UCAD.Core.Geometry;
using UCAD.Core.IO;
using UCAD.Core.Layers;
using Xunit;

namespace UCAD.Core.Tests;

public sealed class NativeDocumentCodecTests
{
    [Fact]
    public void NativeRoundTripPreservesAllCurrentAuthoringEntityKinds()
    {
        var document = new CadDocument();
        document.CreateLayer(new CadLayer("Planning", "#7A9E88", 0.35, "Continuous"));
        document.SetCurrentLayer("Planning");

        var blockGeometry = new ICadEntity[]
        {
            new LineEntity(new CadPoint(0, 0), new CadPoint(2, 0)),
            new CircleEntity(new CadPoint(1, 1), 0.5)
        };
        document.DefineBlock(new CadBlockDefinition("TREE", new CadPoint(0, 0), blockGeometry));

        var entities = new ICadEntity[]
        {
            new LineEntity(new CadPoint(0, 0), new CadPoint(10, 0)),
            new PolylineEntity([new CadPoint(0, 0), new CadPoint(5, 0), new CadPoint(5, 5)], closed: true),
            new CircleEntity(new CadPoint(20, 20), 4),
            ArcEntity.Create(new CadPoint(30, 30), 6, Math.PI / 4, Math.PI / 2),
            new TextEntity(new CadPoint(2, 3), "规划", 2.5, 0.25),
            new LinearDimensionEntity(new CadPoint(0, 0), new CadPoint(10, 0), new CadPoint(0, 3), "10.00"),
            new HatchEntity([new CadPoint(0, 0), new CadPoint(4, 0), new CadPoint(4, 4), new CadPoint(0, 4)], "ANSI31", 2, Math.PI / 4),
            new BlockReferenceEntity("TREE", new CadPoint(50, 50), blockGeometry, 1.5, 0.5)
        };

        foreach (var entity in entities)
        {
            document.Add(entity, new CadEntityProperties("Planning", "#335577", 0.50, "Continuous"));
        }

        var json = CadNativeDocumentCodec.Serialize(document);
        var restored = CadNativeDocumentCodec.Deserialize(json);

        Assert.Contains("\"schema\": \"ucad-document\"", json, StringComparison.Ordinal);
        Assert.Equal("Planning", restored.CurrentLayerName);
        Assert.Equal(8, restored.Entities.Count);
        Assert.Single(restored.Blocks);
        Assert.Equal("TREE", restored.Blocks[0].Name);
        Assert.IsType<LineEntity>(restored.Entities[0]);
        Assert.IsType<PolylineEntity>(restored.Entities[1]);
        Assert.IsType<CircleEntity>(restored.Entities[2]);
        Assert.IsType<ArcEntity>(restored.Entities[3]);
        Assert.IsType<TextEntity>(restored.Entities[4]);
        Assert.IsType<LinearDimensionEntity>(restored.Entities[5]);
        Assert.IsType<HatchEntity>(restored.Entities[6]);
        Assert.IsType<BlockReferenceEntity>(restored.Entities[7]);

        foreach (var entity in restored.Entities)
        {
            var properties = restored.GetEntityProperties(entity.Id);
            Assert.Equal("Planning", properties.LayerName);
            Assert.Equal("#335577", properties.ColorHex);
            Assert.Equal(0.50, properties.LineWeight);
        }

        var hatch = Assert.IsType<HatchEntity>(restored.Entities[6]);
        Assert.Equal("ANSI31", hatch.Pattern);
        Assert.Equal(2, hatch.PatternScale);
        Assert.Equal(Math.PI / 4, hatch.PatternAngleRadians, 8);

        var reference = Assert.IsType<BlockReferenceEntity>(restored.Entities[7]);
        Assert.Equal("TREE", reference.DefinitionName);
        Assert.Equal(2, reference.Contents.Count);
        Assert.Equal(1.5, reference.Scale);
    }

    [Fact]
    public void NativeCodecRejectsUnknownSchemaAndUnknownFields()
    {
        const string wrongSchema = """
        { "schema": "other", "formatVersion": 1, "layers": [], "blocks": [], "entities": [] }
        """;
        Assert.Throws<FormatException>(() => CadNativeDocumentCodec.Deserialize(wrongSchema));

        const string unknownField = """
        { "schema": "ucad-document", "formatVersion": 1, "layers": [], "blocks": [], "entities": [], "mystery": true }
        """;
        Assert.ThrowsAny<Exception>(() => CadNativeDocumentCodec.Deserialize(unknownField));
    }
}

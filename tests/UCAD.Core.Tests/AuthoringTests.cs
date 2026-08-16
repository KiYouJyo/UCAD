using UCAD.Core.Blocks;
using UCAD.Core.Entities;
using UCAD.Core.Geometry;
using UCAD.Core.Interaction;
using UCAD.Core.Modify;
using Xunit;

namespace UCAD.Core.Tests;

public sealed class AuthoringTests
{
    [Fact]
    public void TextDimensionAndHatchHaveSelectableGeometry()
    {
        var text = new TextEntity(new CadPoint(0, 0), "UCAD", 2.5);
        var dimension = new LinearDimensionEntity(new CadPoint(0, 0), new CadPoint(10, 0), new CadPoint(0, 3));
        var hatch = new HatchEntity([
            new CadPoint(0, 0), new CadPoint(5, 0), new CadPoint(5, 5), new CadPoint(0, 5)
        ]);

        Assert.True(CadEntityGeometry.GetBounds(text).Right > 0);
        Assert.Equal(10, dimension.Measurement, 8);
        Assert.Equal(0, CadEntityGeometry.DistanceTo(hatch, new CadPoint(2, 2)), 8);
        Assert.Contains(dimension.DimensionLinePoint, CadEntityGeometry.GetGripPoints(dimension));
    }

    [Fact]
    public void AnnotationEntitiesParticipateInSharedTransforms()
    {
        var text = new TextEntity(new CadPoint(1, 1), "A", 2);
        var moved = Assert.IsType<TextEntity>(CadEntityTransform.Translate(text, new CadVector(4, 5)));
        Assert.Equal(text.Id, moved.Id);
        Assert.Equal(new CadPoint(5, 6), moved.Position);

        var dimension = new LinearDimensionEntity(new CadPoint(0, 0), new CadPoint(10, 0), new CadPoint(0, 2));
        var scaled = Assert.IsType<LinearDimensionEntity>(CadEntityTransform.Scale(dimension, new CadPoint(0, 0), 2));
        Assert.Equal(20, scaled.Measurement, 8);
    }

    [Fact]
    public void BlockDefinitionCanInsertMultipleIndependentReferences()
    {
        var source = new LineEntity(new CadPoint(10, 10), new CadPoint(20, 10));
        var definition = new CadBlockDefinition("Desk", new CadPoint(10, 10), [source]);
        var first = CadBlockFactory.CreateReference(definition, new CadPoint(0, 0));
        var second = CadBlockFactory.CreateReference(definition, new CadPoint(100, 50), 2, Math.PI / 2);

        Assert.NotEqual(first.Id, second.Id);
        var firstLine = Assert.IsType<LineEntity>(Assert.Single(first.Contents));
        Assert.Equal(new CadPoint(0, 0), firstLine.Start);
        Assert.Equal(new CadPoint(10, 0), firstLine.End);
        var secondLine = Assert.IsType<LineEntity>(Assert.Single(second.Contents));
        Assert.InRange((secondLine.Start - new CadPoint(100, 50)).Length, 0, 1e-7);
        Assert.InRange((secondLine.End - new CadPoint(100, 70)).Length, 0, 1e-7);
    }

    [Fact]
    public void BlockDefinitionsAreDocumentStateAndUndoable()
    {
        var document = new CadDocument();
        var definition = new CadBlockDefinition(
            "Tree",
            new CadPoint(0, 0),
            [new CircleEntity(new CadPoint(0, 0), 1)]);

        document.DefineBlock(definition);
        Assert.Equal("Tree", Assert.Single(document.Blocks).Name);
        Assert.True(document.Undo());
        Assert.Empty(document.Blocks);
        Assert.True(document.Redo());
        Assert.Equal("Tree", Assert.Single(document.Blocks).Name);
    }

    [Fact]
    public void ExplodeCreatesFreshTopLevelGeometry()
    {
        var definition = new CadBlockDefinition(
            "Axis",
            new CadPoint(0, 0),
            [new LineEntity(new CadPoint(0, 0), new CadPoint(10, 0))]);
        var reference = CadBlockFactory.CreateReference(definition, new CadPoint(5, 5));
        var child = Assert.Single(CadBlockFactory.Explode(reference));
        Assert.NotEqual(Assert.Single(reference.Contents).Id, child.Id);
        Assert.IsType<LineEntity>(child);
    }
}

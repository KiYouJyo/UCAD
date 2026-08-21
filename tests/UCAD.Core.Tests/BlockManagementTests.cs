using UCAD.Core.Blocks;
using UCAD.Core.Entities;
using UCAD.Core.Geometry;
using Xunit;

namespace UCAD.Core.Tests;

public sealed class BlockManagementTests
{
    [Fact]
    public void BlockReferenceUsesDefaultsAndInstanceValues()
    {
        var definition = new CadBlockDefinition(
            "TREE",
            new CadPoint(0, 0),
            [new CircleEntity(new CadPoint(0, 0), 1)],
            [
                new CadBlockAttributeDefinition("ID", "Tree ID", "T-001", new CadPoint(0, 2)),
                new CadBlockAttributeDefinition("TYPE", "Type", "Oak", new CadPoint(0, 3), constant: true)
            ]);

        var reference = CadBlockFactory.CreateReference(
            definition,
            new CadPoint(10, 10),
            attributeValues: new Dictionary<string, string> { ["ID"] = "T-021", ["TYPE"] = "Ignored" });

        Assert.Equal("T-021", reference.AttributeValues["ID"]);
        Assert.Equal("Oak", reference.AttributeValues["TYPE"]);
    }

    [Fact]
    public void RenameBlockUpdatesReferencesAndUndoesAsOneStep()
    {
        var document = new CadDocument();
        var definition = new CadBlockDefinition("TREE", new CadPoint(0, 0), [new CircleEntity(new CadPoint(0, 0), 1)]);
        document.DefineBlock(definition);
        var reference = CadBlockFactory.CreateReference(definition, new CadPoint(20, 20));
        document.Add(reference);
        document.ResetHistory();

        document.RenameBlock("TREE", "TREE_NEW");

        Assert.True(document.TryGetBlock("TREE_NEW", out _));
        Assert.False(document.TryGetBlock("TREE", out _));
        Assert.Equal("TREE_NEW", Assert.IsType<BlockReferenceEntity>(document.Entities[0]).DefinitionName);
        Assert.True(document.Undo());
        Assert.True(document.TryGetBlock("TREE", out _));
        Assert.Equal("TREE", Assert.IsType<BlockReferenceEntity>(document.Entities[0]).DefinitionName);
        Assert.False(document.CanUndo);
    }

    [Fact]
    public void RedefineRefreshesInstanceGeometryAndPreservesAttributes()
    {
        var document = new CadDocument();
        var attribute = new CadBlockAttributeDefinition("ID", "ID", "1", new CadPoint(0, 2));
        var initial = new CadBlockDefinition(
            "SYMBOL",
            new CadPoint(0, 0),
            [new LineEntity(new CadPoint(0, 0), new CadPoint(1, 0))],
            [attribute]);
        document.DefineBlock(initial);
        document.Add(CadBlockFactory.CreateReference(
            initial,
            new CadPoint(10, 0),
            attributeValues: new Dictionary<string, string> { ["ID"] = "42" }));
        document.ResetHistory();

        var redefined = new CadBlockDefinition(
            "SYMBOL",
            new CadPoint(0, 0),
            [new LineEntity(new CadPoint(0, 0), new CadPoint(5, 0))],
            [attribute]);
        document.RedefineBlock(redefined);

        var reference = Assert.IsType<BlockReferenceEntity>(document.Entities[0]);
        var line = Assert.IsType<LineEntity>(reference.Contents[0]);
        Assert.Equal(5, line.Length, 8);
        Assert.Equal("42", reference.AttributeValues["ID"]);
        Assert.True(document.Undo());
        Assert.Equal(1, Assert.IsType<LineEntity>(Assert.IsType<BlockReferenceEntity>(document.Entities[0]).Contents[0]).Length, 8);
    }

    [Fact]
    public void ExternalReferenceKeepsSourceAndReloads()
    {
        var document = new CadDocument();
        var source = Path.Combine(Path.GetTempPath(), "site-base.ucad");
        var definition = new CadBlockDefinition(
            "XREF_SITE",
            new CadPoint(0, 0),
            [new LineEntity(new CadPoint(0, 0), new CadPoint(10, 0))],
            attributeDefinitions: null,
            externalSourcePath: source);
        document.DefineBlock(definition);
        document.Add(CadBlockFactory.CreateReference(definition, new CadPoint(0, 0)));
        document.ResetHistory();

        document.ReloadExternalReference(
            "XREF_SITE",
            [new LineEntity(new CadPoint(0, 0), new CadPoint(25, 0))]);

        Assert.True(document.GetBlock("XREF_SITE").IsExternalReference);
        Assert.Equal(Path.GetFullPath(source), document.GetBlock("XREF_SITE").ExternalSourcePath);
        var line = Assert.IsType<LineEntity>(Assert.IsType<BlockReferenceEntity>(document.Entities[0]).Contents[0]);
        Assert.Equal(25, line.Length, 8);
    }
}
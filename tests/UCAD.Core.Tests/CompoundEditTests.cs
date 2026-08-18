using UCAD.Core.Entities;
using UCAD.Core.Geometry;
using Xunit;

namespace UCAD.Core.Tests;

public sealed class CompoundEditTests
{
    [Fact]
    public void CompoundEditReplacesRemovesAddsAndUndoesAsOneStep()
    {
        var document = new CadDocument();
        var first = new LineEntity(new CadPoint(0, 0), new CadPoint(10, 0));
        var second = new LineEntity(new CadPoint(0, 0), new CadPoint(0, 10));
        document.Add(first);
        document.Add(second);
        document.ResetHistory();

        var firstReplacement = new LineEntity(new CadPoint(2, 0), new CadPoint(10, 0), first.Id);
        var arc = ArcEntity.Create(new CadPoint(2, 2), 2, -Math.PI / 2, -Math.PI / 2);
        var inherited = document.GetEntityProperties(first.Id);

        Assert.True(document.ApplyCompoundEdit(
            replacements: [firstReplacement],
            removals: [second.Id],
            additions: [(arc, inherited)]));

        Assert.Equal(2, document.Entities.Count);
        Assert.Contains(document.Entities, entity => entity.Id == first.Id);
        Assert.Contains(document.Entities, entity => entity.Id == arc.Id);
        Assert.True(document.CanUndo);

        Assert.True(document.Undo());
        Assert.Equal(2, document.Entities.Count);
        Assert.Contains(document.Entities, entity => entity.Id == first.Id);
        Assert.Contains(document.Entities, entity => entity.Id == second.Id);
        Assert.DoesNotContain(document.Entities, entity => entity.Id == arc.Id);
        Assert.False(document.CanUndo);
    }
}
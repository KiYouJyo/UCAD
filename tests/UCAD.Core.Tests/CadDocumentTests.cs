using UCAD.Core;
using UCAD.Core.Entities;
using UCAD.Core.Geometry;
using Xunit;

namespace UCAD.Core.Tests;

public sealed class CadDocumentTests
{
    [Fact]
    public void AddLineStoresEntityAndLength()
    {
        var document = new CadDocument();
        var line = new LineEntity(new CadPoint(0, 0), new CadPoint(3, 4));

        document.Add(line);

        Assert.Single(document.Entities);
        Assert.Equal(5, line.Length, 10);
    }

    [Fact]
    public void RemoveDeletesMatchingEntity()
    {
        var document = new CadDocument();
        var line = new LineEntity(new CadPoint(0, 0), new CadPoint(10, 0));
        document.Add(line);

        Assert.True(document.Remove(line.Id));
        Assert.Empty(document.Entities);
    }

    [Fact]
    public void RemoveRangeErasesSelectionAsOneUndoableMutation()
    {
        var document = new CadDocument();
        var first = new LineEntity(new CadPoint(0, 0), new CadPoint(10, 0));
        var second = new CircleEntity(new CadPoint(20, 0), 5);
        var survivor = new LineEntity(new CadPoint(0, 20), new CadPoint(10, 20));
        document.AddRange([first, second, survivor]);

        var beforeEraseRevision = document.Revision;
        var removed = document.RemoveRange([first.Id, second.Id]);

        Assert.Equal(2, removed);
        Assert.Equal(beforeEraseRevision + 1, document.Revision);
        Assert.Single(document.Entities);
        Assert.Equal(survivor.Id, document.Entities[0].Id);

        Assert.True(document.Undo());
        Assert.Equal(3, document.Entities.Count);
        Assert.Contains(document.Entities, entity => entity.Id == first.Id);
        Assert.Contains(document.Entities, entity => entity.Id == second.Id);
        Assert.Contains(document.Entities, entity => entity.Id == survivor.Id);
    }

    [Fact]
    public void RemoveRangeDoesNotCreateHistoryWhenNothingMatches()
    {
        var document = new CadDocument();
        var line = new LineEntity(new CadPoint(0, 0), new CadPoint(10, 0));
        document.Add(line);
        var revision = document.Revision;

        Assert.Equal(0, document.RemoveRange([Guid.NewGuid()]));
        Assert.Equal(revision, document.Revision);
    }
}

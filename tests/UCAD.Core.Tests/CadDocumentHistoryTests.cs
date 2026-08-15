using UCAD.Core;
using UCAD.Core.Entities;
using UCAD.Core.Geometry;
using Xunit;

namespace UCAD.Core.Tests;

public sealed class CadDocumentHistoryTests
{
    [Fact]
    public void AddUndoRedoRoundTripsEntity()
    {
        var document = new CadDocument();
        var line = new LineEntity(new CadPoint(0, 0), new CadPoint(5, 0));
        document.Add(line);

        Assert.True(document.CanUndo);
        Assert.True(document.Undo());
        Assert.Empty(document.Entities);
        Assert.True(document.CanRedo);
        Assert.True(document.Redo());
        Assert.Single(document.Entities);
        Assert.Same(line, document.Entities[0]);
    }

    [Fact]
    public void NewMutationClearsRedoHistory()
    {
        var document = new CadDocument();
        document.Add(new LineEntity(new CadPoint(0, 0), new CadPoint(1, 0)));
        document.Undo();

        document.Add(new CircleEntity(new CadPoint(0, 0), 2));

        Assert.False(document.CanRedo);
        Assert.False(document.Redo());
    }

    [Fact]
    public void ClearCanBeUndoneAsOneMutation()
    {
        var document = new CadDocument();
        document.AddRange([
            new LineEntity(new CadPoint(0, 0), new CadPoint(1, 0)),
            new CircleEntity(new CadPoint(0, 0), 3)
        ]);
        document.Clear();

        Assert.Empty(document.Entities);
        Assert.True(document.Undo());
        Assert.Equal(2, document.Entities.Count);
    }
}

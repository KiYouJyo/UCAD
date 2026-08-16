using UCAD.Core.Entities;
using UCAD.Core.Geometry;
using UCAD.Core.Layers;
using Xunit;

namespace UCAD.Core.Tests;

public sealed class LayerPropertyTests
{
    [Fact]
    public void NewEntitiesInheritCurrentLayerAndLayerStateIsUndoable()
    {
        var document = new CadDocument();
        document.CreateLayer(new CadLayer("Walls", "#FF4040", 0.50));
        document.SetCurrentLayer("Walls");
        var line = new LineEntity(new CadPoint(0, 0), new CadPoint(10, 0));
        document.Add(line);

        Assert.Equal("Walls", document.GetEntityProperties(line.Id).LayerName);
        Assert.Equal("Walls", document.CurrentLayerName);
        Assert.Single(document.VisibleEntities);

        document.UpdateLayer("Walls", isVisible: false);
        Assert.Empty(document.VisibleEntities);
        Assert.True(document.Undo());
        Assert.True(document.GetLayer("Walls").IsVisible);
    }

    [Fact]
    public void LockedOrHiddenLayersAreNotSelectable()
    {
        var document = new CadDocument();
        document.CreateLayer(new CadLayer("Locked", isLocked: true));
        document.SetCurrentLayer("Locked");
        var locked = new LineEntity(new CadPoint(0, 0), new CadPoint(1, 0));
        document.Add(locked);
        document.SetCurrentLayer("0");
        var normal = new LineEntity(new CadPoint(0, 1), new CadPoint(1, 1));
        document.Add(normal);

        Assert.DoesNotContain(document.SelectableEntities, entity => entity.Id == locked.Id);
        Assert.Contains(document.SelectableEntities, entity => entity.Id == normal.Id);

        document.UpdateLayer("Locked", isLocked: false, isVisible: false);
        Assert.DoesNotContain(document.SelectableEntities, entity => entity.Id == locked.Id);
    }

    [Fact]
    public void EntityOverridesAndLayerChangesUndoTogether()
    {
        var document = new CadDocument();
        document.CreateLayer(new CadLayer("Site"));
        var circle = new CircleEntity(new CadPoint(0, 0), 5);
        document.Add(circle);

        Assert.Equal(1, document.SetEntitiesLayer([circle.Id], "Site"));
        Assert.Equal(1, document.SetEntitiesColor([circle.Id], "#00AAFF"));
        Assert.Equal(1, document.SetEntitiesLineWeight([circle.Id], 0.70));
        var properties = document.GetEntityProperties(circle.Id);
        Assert.Equal("Site", properties.LayerName);
        Assert.Equal("#00AAFF", properties.ColorHex);
        Assert.Equal(0.70, properties.LineWeight);

        Assert.True(document.Undo());
        Assert.Null(document.GetEntityProperties(circle.Id).LineWeight);
        Assert.True(document.Redo());
        Assert.Equal(0.70, document.GetEntityProperties(circle.Id).LineWeight);
    }

    [Fact]
    public void TrimSplitPiecesInheritOriginalProperties()
    {
        var document = new CadDocument();
        document.CreateLayer(new CadLayer("Road"));
        document.SetCurrentLayer("Road");
        var source = new LineEntity(new CadPoint(0, 0), new CadPoint(10, 0));
        document.Add(source);
        document.SetEntitiesColor([source.Id], "#FFFF00");

        var first = new LineEntity(new CadPoint(0, 0), new CadPoint(4, 0));
        var second = new LineEntity(new CadPoint(6, 0), new CadPoint(10, 0));
        Assert.True(document.Replace(source.Id, [first, second]));

        Assert.All(document.Entities, entity =>
        {
            var properties = document.GetEntityProperties(entity.Id);
            Assert.Equal("Road", properties.LayerName);
            Assert.Equal("#FFFF00", properties.ColorHex);
        });
    }

    [Fact]
    public void DeletingLayerMovesEntitiesToLayerZeroAsOneUndoableMutation()
    {
        var document = new CadDocument();
        document.CreateLayer(new CadLayer("Temp"));
        document.SetCurrentLayer("Temp");
        var line = new LineEntity(new CadPoint(0, 0), new CadPoint(1, 1));
        document.Add(line);

        Assert.True(document.DeleteLayer("Temp"));
        Assert.Equal("0", document.GetEntityProperties(line.Id).LayerName);
        Assert.Equal("0", document.CurrentLayerName);
        Assert.True(document.Undo());
        Assert.Equal("Temp", document.GetEntityProperties(line.Id).LayerName);
        Assert.Equal("Temp", document.CurrentLayerName);
    }
}

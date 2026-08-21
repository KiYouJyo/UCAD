using UCAD.Core.Entities;
using UCAD.Core.Geometry;
using UCAD.Core.Spatial;
using Xunit;

namespace UCAD.Core.Tests;

public sealed class DocumentSpatialIndexCacheTests
{
    [Fact]
    public void CacheBuildsLazilyAndRebuildsOnceAfterMultipleEdits()
    {
        var document = new CadDocument();
        document.Add(new LineEntity(new CadPoint(0, 0), new CadPoint(100, 0)));
        using var cache = new CadDocumentSpatialIndexCache(document);

        Assert.Equal(0, cache.RebuildCount);
        Assert.Single(cache.Query(new CadRect(-10, -10, 110, 10)));
        Assert.Equal(1, cache.RebuildCount);
        Assert.Single(cache.Query(new CadRect(-10, -10, 110, 10)));
        Assert.Equal(1, cache.RebuildCount);

        document.Add(new LineEntity(new CadPoint(0, 100), new CadPoint(100, 100)));
        document.Add(new PointEntity(new CadPoint(50, 50)));
        Assert.Equal(1, cache.RebuildCount);

        var all = cache.Query(new CadRect(-10, -10, 110, 110));
        Assert.Equal(3, all.Count);
        Assert.Equal(2, cache.RebuildCount);
    }

    [Fact]
    public void CustomEntitySourceCanIndexOnlySelectableEntities()
    {
        var document = new CadDocument();
        document.CreateLayer(new UCAD.Core.Layers.CadLayer("LOCKED", isLocked: true));
        var selectable = new LineEntity(new CadPoint(0, 0), new CadPoint(100, 0));
        var locked = new LineEntity(new CadPoint(0, 50), new CadPoint(100, 50));
        document.Add(selectable);
        document.Add(locked, new UCAD.Core.Layers.CadEntityProperties("LOCKED"));
        using var cache = new CadDocumentSpatialIndexCache(document, current => current.SelectableEntities);

        var result = cache.Query(new CadRect(-10, -10, 110, 60));

        Assert.Single(result);
        Assert.Equal(selectable.Id, result[0].Id);
    }
}

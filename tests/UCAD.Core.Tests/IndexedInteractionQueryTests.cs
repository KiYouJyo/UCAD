using UCAD.Core.Entities;
using UCAD.Core.Geometry;
using UCAD.Core.Interaction;
using UCAD.Core.Spatial;
using Xunit;

namespace UCAD.Core.Tests;

public sealed class IndexedInteractionQueryTests
{
    [Fact]
    public void IndexedWindowAndCrossingMatchOriginalQueries()
    {
        var entities = new ICadEntity[]
        {
            new LineEntity(new CadPoint(0, 0), new CadPoint(100, 0)),
            new LineEntity(new CadPoint(50, -100), new CadPoint(50, 100)),
            new CircleEntity(new CadPoint(200, 200), 25),
            new PolylineEntity([new CadPoint(10, 10), new CadPoint(20, 10), new CadPoint(20, 20)], closed: false)
        };
        var index = CadEntitySpatialIndex.Build(entities);
        var rectangle = new CadRect(5, -5, 75, 25);

        var indexedWindow = CadIndexedInteractionQuery.QueryWindow(index, rectangle, crossing: false).OrderBy(id => id).ToArray();
        var originalWindow = CadSelectionQuery.QueryWindow(entities, rectangle, crossing: false).OrderBy(id => id).ToArray();
        var indexedCrossing = CadIndexedInteractionQuery.QueryWindow(index, rectangle, crossing: true).OrderBy(id => id).ToArray();
        var originalCrossing = CadSelectionQuery.QueryWindow(entities, rectangle, crossing: true).OrderBy(id => id).ToArray();

        Assert.Equal(originalWindow, indexedWindow);
        Assert.Equal(originalCrossing, indexedCrossing);
    }

    [Fact]
    public void IndexedObjectSnapMatchesOriginalResolverForNearbyGeometry()
    {
        var first = new LineEntity(new CadPoint(0, 0), new CadPoint(100, 0));
        var second = new LineEntity(new CadPoint(50, -50), new CadPoint(50, 50));
        var far = new CircleEntity(new CadPoint(5000, 5000), 100);
        var entities = new ICadEntity[] { first, second, far };
        var index = CadEntitySpatialIndex.Build(entities);
        var cursor = new CadPoint(51, 1);
        var modes = ObjectSnapMode.Endpoint | ObjectSnapMode.Midpoint | ObjectSnapMode.Intersection | ObjectSnapMode.Center;

        var indexed = CadIndexedInteractionQuery.ResolveObjectSnap(index, cursor, 10, modes);
        var original = ObjectSnapResolver.Resolve(entities, cursor, 10, modes);

        Assert.NotNull(indexed);
        Assert.NotNull(original);
        Assert.Equal(original!.Kind, indexed!.Kind);
        Assert.Equal(original.Point.X, indexed.Point.X, 8);
        Assert.Equal(original.Point.Y, indexed.Point.Y, 8);
    }
}

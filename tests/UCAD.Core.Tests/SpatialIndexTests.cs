using UCAD.Core.Entities;
using UCAD.Core.Geometry;
using UCAD.Core.Interaction;
using UCAD.Core.Spatial;
using Xunit;

namespace UCAD.Core.Tests;

public sealed class SpatialIndexTests
{
    [Fact]
    public void TenThousandPointGridReturnsSameWindowCandidatesAsBruteForce()
    {
        var entities = new List<ICadEntity>(10_000);
        for (var y = 0; y < 100; y++)
        for (var x = 0; x < 100; x++)
            entities.Add(new PointEntity(new CadPoint(x * 10, y * 10)));
        var index = CadEntitySpatialIndex.Build(entities);
        var query = new CadRect(205, 305, 395, 495);

        var indexed = index.Query(query).Select(entity => entity.Id).OrderBy(id => id).ToArray();
        var brute = entities
            .Where(entity => CadEntityGeometry.GetBounds(entity).Intersects(query))
            .Select(entity => entity.Id)
            .OrderBy(id => id)
            .ToArray();

        Assert.Equal(10_000, index.Count);
        Assert.Equal(brute, indexed);
        Assert.NotEmpty(indexed);
    }

    [Fact]
    public void NearestQueryReturnsClosestGeometryWithinMaximumDistance()
    {
        var near = new LineEntity(new CadPoint(0, 0), new CadPoint(100, 0));
        var far = new CircleEntity(new CadPoint(1000, 1000), 50);
        var index = CadEntitySpatialIndex.Build([near, far]);

        var result = index.FindNearest(new CadPoint(25, 4), 10);
        var none = index.FindNearest(new CadPoint(25, 40), 10);

        Assert.Same(near, result);
        Assert.Null(none);
    }

    [Fact]
    public void EmptySpatialIndexIsSafe()
    {
        var index = CadEntitySpatialIndex.Build([]);

        Assert.Empty(index.Query(new CadRect(0, 0, 10, 10)));
        Assert.Null(index.FindNearest(new CadPoint(0, 0), 10));
    }
}

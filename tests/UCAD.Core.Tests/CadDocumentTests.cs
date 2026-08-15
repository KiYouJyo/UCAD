using UCAD.Core;
using UCAD.Core.Entities;
using UCAD.Core.Geometry;

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
}

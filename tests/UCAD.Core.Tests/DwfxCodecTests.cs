using UCAD.Core.Entities;
using UCAD.Core.Geometry;
using UCAD.Core.IO;
using Xunit;

namespace UCAD.Core.Tests;

public sealed class DwfxCodecTests
{
    [Fact]
    public void FixedPageVectorSubsetRoundTripsGeometry()
    {
        var document = new CadDocument();
        document.Add(new LineEntity(new CadPoint(0, 0), new CadPoint(100, 0)));
        document.Add(new PolylineEntity([new CadPoint(0, 10), new CadPoint(50, 40), new CadPoint(100, 10)], false));
        document.Add(new CircleEntity(new CadPoint(50, 70), 10));
        document.Add(ArcEntity.Create(new CadPoint(20, 70), 8, 0, Math.PI));

        var exported = CadDwfxCodec.Export(document);
        Assert.NotEmpty(exported.Content);
        Assert.Empty(exported.Warnings);

        var imported = CadDwfxCodec.Import(exported.Content);
        Assert.True(imported.Document.Entities.Count >= 4);
        Assert.Contains(imported.Document.Entities, entity => entity is LineEntity);
        Assert.Contains(imported.Document.Entities, entity => entity is PolylineEntity);
    }

    [Fact]
    public void UnsupportedAnnotationProducesExplicitPublishWarning()
    {
        var document = new CadDocument();
        document.Add(new TextEntity(new CadPoint(0, 0), "Planning note"));
        var exported = CadDwfxCodec.Export(document);
        Assert.NotEmpty(exported.Warnings);
    }
}

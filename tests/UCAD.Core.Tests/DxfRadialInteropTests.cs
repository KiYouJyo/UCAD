using UCAD.Core.Entities;
using UCAD.Core.Geometry;
using UCAD.Core.IO;
using Xunit;

namespace UCAD.Core.Tests;

public sealed class DxfRadialInteropTests
{
    [Fact]
    public void TextDxfRoundTripsRadiusAndDiameterDimensions()
    {
        var document = BuildDocument();
        var exported = CadDxfFullInteropCodec.Export(document);
        Assert.Contains("\n70\n4\n", exported.Content);
        Assert.Contains("\n70\n3\n", exported.Content);

        var imported = CadDxfFullInteropCodec.Import(exported.Content);
        var radial = imported.Document.Entities.OfType<RadialDimensionEntity>().ToArray();
        Assert.Equal(2, radial.Length);
        Assert.Contains(radial, entity => !entity.Diameter);
        Assert.Contains(radial, entity => entity.Diameter);
    }

    [Fact]
    public void BinaryDxfRoundTripsRadiusAndDiameterDimensions()
    {
        var binary = CadAcadInteropCodec.ExportBinaryDxf(BuildDocument());
        var imported = CadAcadInteropCodec.ImportDxf(binary.Content);
        Assert.Equal(2, imported.Document.Entities.OfType<RadialDimensionEntity>().Count());
    }

    [Fact]
    public void DwgRoundTripsRadiusAndDiameterDimensions()
    {
        var dwg = CadAcadInteropCodec.ExportDwg(BuildDocument());
        var imported = CadAcadInteropCodec.ImportDwg(dwg.Content);
        var radial = imported.Document.Entities.OfType<RadialDimensionEntity>().ToArray();
        Assert.Equal(2, radial.Length);
        Assert.Contains(radial, entity => entity.Diameter);
        Assert.Contains(radial, entity => !entity.Diameter);
    }

    private static CadDocument BuildDocument()
    {
        var document = new CadDocument();
        document.Add(new RadialDimensionEntity(
            new CadPoint(0, 0), new CadPoint(10, 0), new CadPoint(13, 3), false, null, document.CurrentDimensionStyleName));
        document.Add(new RadialDimensionEntity(
            new CadPoint(30, 0), new CadPoint(38, 0), new CadPoint(41, 3), true, "Ø16", document.CurrentDimensionStyleName));
        document.ResetHistory();
        return document;
    }
}

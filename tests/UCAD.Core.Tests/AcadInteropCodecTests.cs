using System.Text;
using UCAD.Core.Entities;
using UCAD.Core.Geometry;
using UCAD.Core.IO;
using UCAD.Core.Layers;
using Xunit;

namespace UCAD.Core.Tests;

public sealed class AcadInteropCodecTests
{
    [Fact]
    public void DwgRoundTripPreservesFoundationalGeometryThroughSharedDxfBridge()
    {
        var source = CreateDocument();

        var exported = CadAcadInteropCodec.ExportDwg(source);

        Assert.NotEmpty(exported.Content);
        Assert.Equal(".dwg", exported.TargetExtension);
        Assert.Equal("AC1032", Encoding.ASCII.GetString(exported.Content.AsSpan(0, Math.Min(6, exported.Content.Length))));

        var imported = CadAcadInteropCodec.ImportDwg(exported.Content);

        Assert.Equal(".dwg", imported.SourceExtension);
        Assert.Equal(3, imported.Document.Entities.Count);
        Assert.Equal("Road", imported.Document.CurrentLayerName);
        Assert.IsType<LineEntity>(imported.Document.Entities[0]);
        Assert.IsType<CircleEntity>(imported.Document.Entities[1]);
        Assert.IsType<TextEntity>(imported.Document.Entities[2]);
    }

    [Fact]
    public void DrawingTemplateUsesRealDwgContainerTransport()
    {
        var source = CreateDocument();

        var exported = CadAcadInteropCodec.ExportDwg(source, ".dwt");
        var imported = CadAcadInteropCodec.ImportDwg(exported.Content, ".dwt");

        Assert.Equal(".dwt", exported.TargetExtension);
        Assert.Equal(".dwt", imported.SourceExtension);
        Assert.Equal(source.Entities.Count, imported.Document.Entities.Count);
    }

    [Fact]
    public void BinaryDxfCanBeWrittenAndNormalizedBackIntoUcad()
    {
        var source = CreateDocument();

        var exported = CadAcadInteropCodec.ExportBinaryDxf(source);

        Assert.NotEmpty(exported.Content);
        Assert.Contains("AutoCAD Binary DXF", Encoding.ASCII.GetString(exported.Content.AsSpan(0, Math.Min(32, exported.Content.Length))), StringComparison.Ordinal);

        var imported = CadAcadInteropCodec.ImportDxf(exported.Content);

        Assert.Equal(".dxf", imported.SourceExtension);
        Assert.Equal(3, imported.Document.Entities.Count);
        Assert.IsType<LineEntity>(imported.Document.Entities[0]);
        Assert.IsType<CircleEntity>(imported.Document.Entities[1]);
        Assert.IsType<TextEntity>(imported.Document.Entities[2]);
    }

    [Fact]
    public void ExportDwgRejectsStandardsFileUntilStandardsSemanticsExist()
    {
        var document = CreateDocument();

        var exception = Assert.Throws<NotSupportedException>(() => CadAcadInteropCodec.ExportDwg(document, ".dws"));

        Assert.Contains(".dws", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static CadDocument CreateDocument()
    {
        var document = new CadDocument();
        document.CreateLayer(new CadLayer("Road", "#8AA4B8", 0.35, "Continuous"));
        document.SetCurrentLayer("Road");
        document.Add(
            new LineEntity(new CadPoint(0, 0), new CadPoint(100, 25)),
            new CadEntityProperties("Road", "#336699", 0.35, "Continuous"));
        document.Add(new CircleEntity(new CadPoint(40, 50), 12), new CadEntityProperties("Road"));
        document.Add(new TextEntity(new CadPoint(5, 6), "UCAD DWG", 3.5, Math.PI / 12), new CadEntityProperties("Road"));
        return document;
    }
}

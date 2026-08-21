using System.Text;
using UCAD.Core.Entities;
using UCAD.Core.Geometry;
using UCAD.Core.IO;
using UCAD.Core.Layers;
using Xunit;

namespace UCAD.Core.Tests;

public sealed class DxbCodecTests
{
    [Fact]
    public void DxbRoundTripPreservesSupportedLegacy2DGeometry()
    {
        var document = new CadDocument();
        document.CreateLayer(new CadLayer("Road"));
        document.Add(new LineEntity(new CadPoint(1, 2), new CadPoint(30, 40)), new CadEntityProperties("Road"));
        document.Add(new PointEntity(new CadPoint(6, 7)), new CadEntityProperties("Road"));
        document.Add(new CircleEntity(new CadPoint(20, 25), 8), new CadEntityProperties("Road"));
        document.Add(ArcEntity.Create(new CadPoint(50, 60), 12, Math.PI / 6, Math.PI / 2), new CadEntityProperties("Road"));
        document.Add(new PolylineEntity(
            [new CadPoint(0, 0), new CadPoint(10, 0), new CadPoint(10, 10), new CadPoint(0, 10)],
            closed: true), new CadEntityProperties("Road"));

        var exported = CadDxbCodec.Export(document);

        Assert.Equal(".dxb", exported.TargetExtension);
        Assert.Equal("DXB 1.0", exported.TargetCadVersion);
        Assert.StartsWith("AutoCAD DXB 1.0", Encoding.ASCII.GetString(exported.Content), StringComparison.Ordinal);
        Assert.False(exported.HasWarnings);

        var imported = CadDxbCodec.Import(exported.Content);

        Assert.Equal(".dxb", imported.SourceExtension);
        Assert.Equal("DXB 1.0", imported.SourceCadVersion);
        Assert.Equal(5, imported.Document.Entities.Count);
        Assert.IsType<LineEntity>(imported.Document.Entities[0]);
        Assert.IsType<PointEntity>(imported.Document.Entities[1]);
        Assert.IsType<CircleEntity>(imported.Document.Entities[2]);
        Assert.IsType<ArcEntity>(imported.Document.Entities[3]);
        var polyline = Assert.IsType<PolylineEntity>(imported.Document.Entities[4]);
        Assert.True(polyline.Closed);
        Assert.Equal(4, polyline.Points.Count);
        Assert.All(imported.Document.Entities, entity => Assert.Equal("Road", imported.Document.GetEntityProperties(entity.Id).LayerName));
    }

    [Fact]
    public void DxbExportReportsUnsupportedModernEntitiesInsteadOfSilentlyDroppingThem()
    {
        var document = new CadDocument();
        document.Add(new TextEntity(new CadPoint(0, 0), "DXB does not carry UCAD text", 2.5));
        document.Add(new LineEntity(new CadPoint(0, 0), new CadPoint(1, 1)));

        var exported = CadDxbCodec.Export(document);
        var imported = CadDxbCodec.Import(exported.Content);

        Assert.True(exported.HasWarnings);
        Assert.Contains(exported.Warnings, warning => warning.Contains(nameof(TextEntity), StringComparison.Ordinal));
        Assert.Single(imported.Document.Entities);
        Assert.IsType<LineEntity>(imported.Document.Entities[0]);
    }

    [Fact]
    public void DxbExportMovesNonByteLayerNamesToZeroWithExplicitWarning()
    {
        var document = new CadDocument();
        document.CreateLayer(new CadLayer("道路"));
        document.Add(new LineEntity(new CadPoint(0, 0), new CadPoint(10, 0)), new CadEntityProperties("道路"));

        var exported = CadDxbCodec.Export(document);
        var imported = CadDxbCodec.Import(exported.Content);

        Assert.Contains(exported.Warnings, warning => warning.Contains("layer 0", StringComparison.OrdinalIgnoreCase));
        var line = Assert.IsType<LineEntity>(Assert.Single(imported.Document.Entities));
        Assert.Equal("0", imported.Document.GetEntityProperties(line.Id).LayerName);
    }
}

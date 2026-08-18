using System.Text.Json;
using UCAD.Core.Entities;
using UCAD.Core.Geometry;
using UCAD.Core.Gis;
using Xunit;

namespace UCAD.Core.Tests;

public sealed class GeoJsonCrsExchangeTests
{
    [Fact]
    public void WebMercatorDocumentExportsAsWgs84AndImportsBack()
    {
        var tokyo = new CadPoint(139.6917, 35.6895);
        var projected = CadCoordinateTransform.Wgs84ToWebMercator(tokyo);
        var document = new CadDocument();
        document.Add(new PointEntity(projected));

        var exported = CadGeoJsonCrsExchange.ExportWgs84(
            document,
            CadCoordinateReferenceSystem.WebMercator);
        using var json = JsonDocument.Parse(exported.Json);
        var coordinates = json.RootElement
            .GetProperty("features")[0]
            .GetProperty("geometry")
            .GetProperty("coordinates");
        Assert.Equal(tokyo.X, coordinates[0].GetDouble(), 7);
        Assert.Equal(tokyo.Y, coordinates[1].GetDouble(), 7);

        var imported = CadGeoJsonCrsExchange.ImportWgs84(
            exported.Json,
            CadCoordinateReferenceSystem.WebMercator);
        var restored = Assert.IsType<PointEntity>(Assert.Single(imported.Entities).Entity);
        Assert.Equal(projected.X, restored.Position.X, 4);
        Assert.Equal(projected.Y, restored.Position.Y, 4);
    }

    [Fact]
    public void Wgs84DocumentPassesThroughWithoutCoordinateMutation()
    {
        var document = new CadDocument();
        document.Add(new LineEntity(
            new CadPoint(116.391, 39.907),
            new CadPoint(116.401, 39.917)));

        var raw = CadGeoJsonCodec.Export(document);
        var safe = CadGeoJsonCrsExchange.ExportWgs84(
            document,
            CadCoordinateReferenceSystem.Wgs84LongitudeLatitude);

        Assert.Equal(raw.Json, safe.Json);
        Assert.Equal(raw.Warnings, safe.Warnings);
    }

    [Fact]
    public void LocalPlanarCannotBeSilentlyLabeledAsGeoJsonWgs84()
    {
        var document = new CadDocument();
        document.Add(new PointEntity(new CadPoint(500000, 3500000)));

        Assert.Throws<NotSupportedException>(() =>
            CadGeoJsonCrsExchange.ExportWgs84(document, CadCoordinateReferenceSystem.LocalPlanar));
        Assert.Throws<NotSupportedException>(() =>
            CadGeoJsonCrsExchange.ImportWgs84(
                "{\"type\":\"FeatureCollection\",\"features\":[]}",
                CadCoordinateReferenceSystem.LocalPlanar));
    }
}

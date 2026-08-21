using UCAD.Core.Entities;
using UCAD.Core.Geometry;
using UCAD.Core.Gis;
using Xunit;

namespace UCAD.Core.Tests;

public sealed class GisDocumentBuilderTests
{
    [Fact]
    public void GeoJsonImportBuildsSuggestedLayersAndStartsWithCleanHistory()
    {
        const string json = """
        {
          "type": "FeatureCollection",
          "features": [
            {
              "type": "Feature",
              "properties": { "layer": "PARCEL" },
              "geometry": { "type": "Point", "coordinates": [10, 20] }
            },
            {
              "type": "Feature",
              "properties": { "layer": "ROAD" },
              "geometry": { "type": "LineString", "coordinates": [[0,0],[100,0]] }
            }
          ]
        }
        """;
        var imported = CadGeoJsonCodec.Import(json);

        var built = CadGisDocumentBuilder.FromGeoJson(imported);

        Assert.Equal(2, built.Document.Entities.Count);
        Assert.True(built.Document.TryGetLayer("PARCEL", out _));
        Assert.True(built.Document.TryGetLayer("ROAD", out _));
        Assert.False(built.Document.CanUndo);
        Assert.False(built.Document.CanRedo);
    }

    [Fact]
    public void CsvPointImportFallsBackToGisLayerWhenLayerIsMissing()
    {
        var imported = CadCsvPointCodec.Import("X,Y,Name,Layer\r\n1,2,A,\r\n3,4,B,\r\n");

        var built = CadGisDocumentBuilder.FromCsvPoints(imported);

        Assert.Equal(2, built.Document.Entities.Count);
        Assert.True(built.Document.TryGetLayer("GIS", out _));
        Assert.All(built.Document.Entities, entity =>
            Assert.Equal("GIS", built.Document.GetEntityProperties(entity.Id).LayerName));
        Assert.False(built.Document.CanUndo);
    }
}

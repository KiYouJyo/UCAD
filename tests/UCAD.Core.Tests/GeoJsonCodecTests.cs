using UCAD.Core.Entities;
using UCAD.Core.Geometry;
using UCAD.Core.Gis;
using Xunit;

namespace UCAD.Core.Tests;

public sealed class GeoJsonCodecTests
{
    [Fact]
    public void ExportAndImportRoundTripsBasicFiniteGeometry()
    {
        var document = new CadDocument();
        document.Add(new PointEntity(new CadPoint(1, 2)));
        document.Add(new LineEntity(new CadPoint(0, 0), new CadPoint(10, 20)));
        document.Add(new PolylineEntity([
            new CadPoint(0, 0), new CadPoint(10, 0), new CadPoint(10, 10), new CadPoint(0, 10)
        ], closed: true));

        var exported = CadGeoJsonCodec.Export(document);
        var imported = CadGeoJsonCodec.Import(exported.Json);

        Assert.Empty(exported.Warnings);
        Assert.Equal(3, imported.Entities.Count);
        Assert.Contains(imported.Entities, item => item.Entity is PointEntity);
        Assert.Contains(imported.Entities, item => item.Entity is LineEntity);
        Assert.Contains(imported.Entities, item => item.Entity is PolylineEntity { Closed: true });
    }

    [Fact]
    public void PolygonHolesImportAsSeparateClosedPolylinesWithWarning()
    {
        const string json = """
        {
          "type": "FeatureCollection",
          "features": [
            {
              "type": "Feature",
              "properties": { "layer": "PARCEL" },
              "geometry": {
                "type": "Polygon",
                "coordinates": [
                  [[0,0],[100,0],[100,100],[0,100],[0,0]],
                  [[25,25],[75,25],[75,75],[25,75],[25,25]]
                ]
              }
            }
          ]
        }
        """;

        var result = CadGeoJsonCodec.Import(json);

        Assert.Equal(2, result.Entities.Count);
        Assert.All(result.Entities, item => Assert.IsType<PolylineEntity>(item.Entity));
        Assert.All(result.Entities, item => Assert.Equal("PARCEL", item.SuggestedLayerName));
        Assert.True(result.Warnings.Any(warning => warning.Contains("holes", StringComparison.OrdinalIgnoreCase)));
        Assert.Equal("interior-ring", result.Entities[1].Properties["ucadGeoJsonRole"]);
    }

    [Fact]
    public void InfiniteGeometryIsSkippedWithExplicitWarning()
    {
        var document = new CadDocument();
        document.Add(new XLineEntity(new CadPoint(0, 0), new CadVector(1, 0)));
        document.Add(new RayEntity(new CadPoint(0, 0), new CadVector(0, 1)));

        var result = CadGeoJsonCodec.Export(document);

        Assert.Equal(2, result.Warnings.Count);
        Assert.True(result.Warnings.All(warning => warning.Contains("Infinite", StringComparison.OrdinalIgnoreCase)));
        Assert.True(result.Json.Contains("\"features\": []", StringComparison.Ordinal) || result.Json.Contains("\"features\":[]", StringComparison.Ordinal));
    }

    [Fact]
    public void HatchExportsPolygonWithIslandRing()
    {
        var document = new CadDocument();
        var hatch = new HatchEntity(
            [new CadPoint(0,0), new CadPoint(100,0), new CadPoint(100,100), new CadPoint(0,100)],
            "Solid",
            islands:
            [
                [new CadPoint(20,20), new CadPoint(80,20), new CadPoint(80,80), new CadPoint(20,80)]
            ]);
        document.Add(hatch);

        var result = CadGeoJsonCodec.Export(document);

        Assert.Empty(result.Warnings);
        Assert.True(result.Json.Contains("\"type\": \"Polygon\"", StringComparison.Ordinal) || result.Json.Contains("\"type\":\"Polygon\"", StringComparison.Ordinal));
        var imported = CadGeoJsonCodec.Import(result.Json);
        Assert.Equal(2, imported.Entities.Count);
        Assert.Single(imported.Warnings);
    }
}

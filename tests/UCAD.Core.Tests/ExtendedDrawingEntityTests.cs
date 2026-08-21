using UCAD.Core.Entities;
using UCAD.Core.Geometry;
using UCAD.Core.IO;
using Xunit;

namespace UCAD.Core.Tests;

public sealed class ExtendedDrawingEntityTests
{
    [Fact]
    public void EllipseSamplesRespectMajorAndMinorRadii()
    {
        var ellipse = new EllipseEntity(new CadPoint(10, 20), new CadVector(8, 0), 0.5);
        Assert.Equal(new CadPoint(18, 20), ellipse.PointAtParameter(0));
        var quarter = ellipse.PointAtParameter(Math.PI / 2);
        Assert.Equal(10, quarter.X, 8);
        Assert.Equal(24, quarter.Y, 8);
        Assert.True(ellipse.IsFullEllipse);
        Assert.True(ellipse.SamplePoints().Count >= 90);
    }

    [Fact]
    public void SplineSamplingPreservesFitEndpoints()
    {
        var fit = new[]
        {
            new CadPoint(0, 0),
            new CadPoint(5, 8),
            new CadPoint(12, 4),
            new CadPoint(20, 10)
        };
        var spline = new SplineEntity(fit);
        var samples = spline.SamplePoints();
        Assert.Equal(fit[0], samples[0]);
        Assert.Equal(fit[^1], samples[^1]);
        Assert.True(samples.Count > fit.Length);
    }

    [Fact]
    public void RayAndXLineNormalizeDirections()
    {
        var ray = new RayEntity(new CadPoint(2, 3), new CadVector(10, 0));
        var xline = new XLineEntity(new CadPoint(4, 5), new CadVector(0, -7));
        Assert.Equal(1, ray.Direction.Length, 10);
        Assert.Equal(1, xline.Direction.Length, 10);
        Assert.Equal(1, ray.Direction.X, 10);
        Assert.Equal(-1, xline.Direction.Y, 10);
    }

    [Fact]
    public void NativeRoundTripPreservesExtendedDrawingEntities()
    {
        var document = new CadDocument();
        document.Add(new PointEntity(new CadPoint(1, 2)));
        document.Add(new EllipseEntity(new CadPoint(10, 10), new CadVector(6, 0), 0.4));
        document.Add(new SplineEntity([
            new CadPoint(0, 0),
            new CadPoint(2, 5),
            new CadPoint(8, 4),
            new CadPoint(10, 10)
        ]));
        document.Add(new RayEntity(new CadPoint(3, 3), new CadVector(1, 2)));
        document.Add(new XLineEntity(new CadPoint(7, 7), new CadVector(-2, 1)));

        var restored = CadNativeDocumentCodec.Deserialize(CadNativeDocumentCodec.Serialize(document));

        Assert.Equal(5, restored.Entities.Count);
        Assert.IsType<PointEntity>(restored.Entities[0]);
        Assert.IsType<EllipseEntity>(restored.Entities[1]);
        Assert.IsType<SplineEntity>(restored.Entities[2]);
        Assert.IsType<RayEntity>(restored.Entities[3]);
        Assert.IsType<XLineEntity>(restored.Entities[4]);

        var ellipse = Assert.IsType<EllipseEntity>(restored.Entities[1]);
        Assert.Equal(0.4, ellipse.Ratio, 8);
        var spline = Assert.IsType<SplineEntity>(restored.Entities[2]);
        Assert.Equal(4, spline.FitPoints.Count);
    }

    [Fact]
    public void ResetHistoryCreatesPersistenceBaseline()
    {
        var document = new CadDocument();
        document.Add(new PointEntity(new CadPoint(1, 1)));
        Assert.True(document.CanUndo);

        document.ResetHistory();
        Assert.False(document.CanUndo);
        Assert.False(document.CanRedo);
        Assert.Single(document.Entities);

        document.Add(new PointEntity(new CadPoint(2, 2)));
        Assert.True(document.CanUndo);
        Assert.True(document.Undo());
        Assert.Single(document.Entities);
    }
}
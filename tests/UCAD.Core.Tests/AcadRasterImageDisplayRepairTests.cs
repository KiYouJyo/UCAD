using System.Reflection;
using ACadSharp.Entities;
using ACadSharp.Objects;
using CSMath;
using UCAD.Core.Entities;
using UCAD.Core.Geometry;
using UCAD.Core.IO;
using Xunit;

namespace UCAD.Core.Tests;

public sealed class AcadRasterImageDisplayRepairTests
{
    [Fact]
    public void NativeRasterImagePreservesPlacementClipAndSourceOrder()
    {
        var definition = new ImageDefinition
        {
            Name = "site-image",
            FileName = @"images\site.png",
            Size = new XY(20, 10)
        };
        var image = new RasterImage(definition)
        {
            InsertPoint = new XYZ(100, 50, 0),
            UVector = new XYZ(2, 0, 0),
            VVector = new XYZ(0, 3, 0),
            Size = new XY(20, 10),
            Flags = ImageDisplayFlags.ShowImage | ImageDisplayFlags.ShowNotAlignedImage
        };
        var source = new ACadSharp.CadDocument();
        source.Entities.Add(new Line(new XYZ(0, 0, 0), new XYZ(1, 0, 0)));
        source.Entities.Add(image);

        var target = new UCAD.Core.CadDocument();
        var warnings = new List<string>();
        InvokeRepair(source, target, warnings);

        var raster = Assert.Single(target.Entities.OfType<RasterImageEntity>());
        Assert.Equal(@"images\site.png", raster.ReferencePath);
        Assert.Equal(100, raster.InsertionPoint.X, 8);
        Assert.Equal(50, raster.InsertionPoint.Y, 8);
        Assert.Equal(2, raster.UVectorPerPixel.X, 8);
        Assert.Equal(3, raster.VVectorPerPixel.Y, 8);
        Assert.Equal(20, raster.PixelWidth, 8);
        Assert.Equal(10, raster.PixelHeight, 8);
        Assert.Equal(4, raster.ClipBoundary.Count);
        Assert.Equal(1, target.GetEntityProperties(raster.Id).SourceOrder);
    }

    [Fact]
    public void ResolverUsesSourceDrawingDirectoryForRelativeRasterPath()
    {
        var root = Path.Combine(Path.GetTempPath(), "ucad-raster-" + Guid.NewGuid().ToString("N"));
        var imageDirectory = Path.Combine(root, "images");
        Directory.CreateDirectory(imageDirectory);
        var drawing = Path.Combine(root, "drawing.dwg");
        var rasterPath = Path.Combine(imageDirectory, "site.png");
        File.WriteAllBytes(rasterPath, [1, 2, 3]);

        try
        {
            var document = new UCAD.Core.CadDocument();
            var raster = new RasterImageEntity(
                @"images\site.png",
                new CadPoint(0, 0),
                new CadVector(1, 0),
                new CadVector(0, 1),
                10,
                10,
                [new CadPoint(0, 0), new CadPoint(10, 0), new CadPoint(10, 10), new CadPoint(0, 10)]);
            document.Add(raster);

            var warnings = CadExternalReferenceResolver.Resolve(document, drawing);

            Assert.Empty(warnings);
            Assert.True(raster.IsResolved);
            Assert.Equal(Path.GetFullPath(rasterPath), raster.ResolvedPath);
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    private static void InvokeRepair(ACadSharp.CadDocument source, UCAD.Core.CadDocument target, List<string> warnings)
    {
        var type = typeof(CadAcadInteropCodec).Assembly.GetType("UCAD.Core.IO.CadAcadRasterImageDisplayRepair", throwOnError: true)!;
        var method = type.GetMethod("Apply", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        Assert.NotNull(method);
        method!.Invoke(null, [source, target, warnings]);
    }
}

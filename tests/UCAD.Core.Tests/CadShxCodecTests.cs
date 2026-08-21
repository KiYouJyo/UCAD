using System.Text;
using UCAD.Core.Entities;
using UCAD.Core.Geometry;
using UCAD.Core.IO;
using Xunit;

namespace UCAD.Core.Tests;

public sealed class CadShxCodecTests
{
    [Fact]
    public void LegacyShapeFileDecodesNormalVectorsIntoClosedStroke()
    {
        var bytes = BuildLegacyShx("BOX", [0x10, 0x14, 0x18, 0x1C, 0x00]);

        var file = CadShxCodec.Read(bytes);
        var strokes = CadShxCodec.RenderShape(file, "BOX");

        var stroke = Assert.Single(strokes);
        Assert.Equal(5, stroke.Count);
        AssertPoint(stroke[0], 0, 0);
        AssertPoint(stroke[1], 1, 0);
        AssertPoint(stroke[2], 1, 1);
        AssertPoint(stroke[3], 0, 1);
        AssertPoint(stroke[4], 0, 0);
    }

    [Fact]
    public void ShapeWorldTransformAppliesSizeXScaleObliqueAndRotation()
    {
        var file = CadShxCodec.Read(BuildLegacyShx("L", [0x10, 0x14, 0x00]));
        var shape = new ShapeReferenceEntity(
            "L",
            ["test.shx"],
            new CadPoint(10, 20),
            size: 2,
            xScale: 3,
            rotationRadians: Math.PI / 2,
            obliqueRadians: 0);

        var stroke = Assert.Single(CadShxCodec.RenderShapeWorld(file, shape));
        AssertPoint(stroke[0], 10, 20);
        AssertPoint(stroke[1], 10, 26);
        AssertPoint(stroke[2], 8, 26);
    }

    [Fact]
    public void ExternalResolverReplacesShapeReferenceWithDecodedPolylines()
    {
        var root = Path.Combine(Path.GetTempPath(), "ucad-shx-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var drawing = Path.Combine(root, "drawing.dwg");
        var shx = Path.Combine(root, "site.shx");
        File.WriteAllBytes(shx, BuildLegacyShx("BOX", [0x10, 0x14, 0x18, 0x1C, 0x00]));

        try
        {
            var document = new UCAD.Core.CadDocument();
            var shape = new ShapeReferenceEntity("BOX", ["site.shx"], new CadPoint(5, 7), 2, 1, 0, 0);
            document.Add(shape);

            var warnings = CadExternalReferenceResolver.Resolve(document, drawing);

            Assert.Empty(warnings);
            Assert.DoesNotContain(document.Entities, entity => entity is ShapeReferenceEntity);
            var polyline = Assert.Single(document.Entities.OfType<PolylineEntity>());
            AssertPoint(polyline.Points[0], 5, 7);
            AssertPoint(polyline.Points[1], 7, 7);
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void BulgeInstructionProducesCurvedMultiPointStroke()
    {
        // 0C, dx=10, dy=0, bulge=127 => semicircle.
        var file = CadShxCodec.Read(BuildLegacyShx("ARC", [0x0C, 10, 0, 127, 0]));
        var stroke = Assert.Single(CadShxCodec.RenderShape(file, "ARC"));

        Assert.True(stroke.Count > 8);
        AssertPoint(stroke[0], 0, 0);
        AssertPoint(stroke[^1], 10, 0);
        Assert.Contains(stroke, point => Math.Abs(point.Y) > 4.5);
    }

    private static byte[] BuildLegacyShx(string name, byte[] instructions)
    {
        var header = Encoding.ASCII.GetBytes("AutoCAD-86 shapes 1.0");
        var bytes = new List<byte>();
        bytes.AddRange(header);
        while (bytes.Count < 0x17) bytes.Add(0);
        bytes.Add(0x1A);
        AddUInt16(bytes, 1);
        AddUInt16(bytes, 1);
        AddUInt16(bytes, 1);
        var nameBytes = Encoding.Latin1.GetBytes(name);
        var recordLength = nameBytes.Length + 1 + instructions.Length;
        AddUInt16(bytes, 1);
        AddUInt16(bytes, (ushort)recordLength);
        bytes.AddRange(nameBytes);
        bytes.Add(0);
        bytes.AddRange(instructions);
        return bytes.ToArray();
    }

    private static void AddUInt16(List<byte> bytes, ushort value)
    {
        bytes.Add((byte)(value & 0xFF));
        bytes.Add((byte)(value >> 8));
    }

    private static void AssertPoint(CadPoint point, double x, double y)
    {
        Assert.Equal(x, point.X, 6);
        Assert.Equal(y, point.Y, 6);
    }
}

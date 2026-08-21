using System.Buffers.Binary;
using UCAD.Core.Gis;
using Xunit;

namespace UCAD.Core.Tests;

public sealed class ShapefileMappedDocumentBuilderTests
{
    [Fact]
    public void MultipartPartsRestoreSharedSourceLayerWithoutGuessing()
    {
        var shp = BuildTwoPartPolylineShp();
        var dbf = CadDbfCodec.Export(new CadDbfTable(
            [
                new CadDbfFieldDefinition("LAYER", CadDbfFieldType.Character, 24),
                new CadDbfFieldDefinition("COLOR", CadDbfFieldType.Character, 12),
                new CadDbfFieldDefinition("LWEIGHT", CadDbfFieldType.Numeric, 10, 2),
                new CadDbfFieldDefinition("LTYPE", CadDbfFieldType.Character, 16)
            ],
            [new CadDbfRecord(new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
            {
                ["LAYER"] = "ROAD",
                ["COLOR"] = "#808080",
                ["LWEIGHT"] = "0.35",
                ["LTYPE"] = "Center"
            })]),
            new DateTime(2026, 8, 18));
        var shx = CadShapefileIndexCodec.Build(shp);
        var imported = CadShapefilePackage.Import(shp, shx, dbf, CadDbfCodec.CreateCpgUtf8());

        var built = CadShapefileMappedDocumentBuilder.Build(imported, shp, dbf);

        Assert.Equal(2, built.Document.Entities.Count);
        Assert.True(built.Document.TryGetLayer("ROAD", out _));
        Assert.All(built.Document.Entities, entity =>
        {
            var properties = built.Document.GetEntityProperties(entity.Id);
            Assert.Equal("ROAD", properties.LayerName);
            Assert.Equal("#808080", properties.ColorHex);
            Assert.Equal(0.35, properties.LineWeight);
            Assert.Equal("Center", properties.LineType);
        });
        Assert.False(built.Document.CanUndo);
        Assert.False(built.Document.CanRedo);
    }

    private static byte[] BuildTwoPartPolylineShp()
    {
        const int headerBytes = 100;
        const int contentBytes = 116;
        var file = new byte[headerBytes + 8 + contentBytes];
        BinaryPrimitives.WriteInt32BigEndian(file.AsSpan(0, 4), 9994);
        BinaryPrimitives.WriteInt32BigEndian(file.AsSpan(24, 4), file.Length / 2);
        BinaryPrimitives.WriteInt32LittleEndian(file.AsSpan(28, 4), 1000);
        BinaryPrimitives.WriteInt32LittleEndian(file.AsSpan(32, 4), 3);
        WriteDouble(file, 36, 0); WriteDouble(file, 44, 0); WriteDouble(file, 52, 30); WriteDouble(file, 60, 10);

        const int recordOffset = headerBytes;
        BinaryPrimitives.WriteInt32BigEndian(file.AsSpan(recordOffset, 4), 1);
        BinaryPrimitives.WriteInt32BigEndian(file.AsSpan(recordOffset + 4, 4), contentBytes / 2);
        const int content = recordOffset + 8;
        BinaryPrimitives.WriteInt32LittleEndian(file.AsSpan(content, 4), 3);
        WriteDouble(file, content + 4, 0); WriteDouble(file, content + 12, 0); WriteDouble(file, content + 20, 30); WriteDouble(file, content + 28, 10);
        BinaryPrimitives.WriteInt32LittleEndian(file.AsSpan(content + 36, 4), 2);
        BinaryPrimitives.WriteInt32LittleEndian(file.AsSpan(content + 40, 4), 4);
        BinaryPrimitives.WriteInt32LittleEndian(file.AsSpan(content + 44, 4), 0);
        BinaryPrimitives.WriteInt32LittleEndian(file.AsSpan(content + 48, 4), 2);
        WritePoint(file, content + 52, 0, 0);
        WritePoint(file, content + 68, 10, 0);
        WritePoint(file, content + 84, 20, 10);
        WritePoint(file, content + 100, 30, 10);
        return file;
    }

    private static void WritePoint(byte[] buffer, int offset, double x, double y)
    {
        WriteDouble(buffer, offset, x);
        WriteDouble(buffer, offset + 8, y);
    }

    private static void WriteDouble(byte[] buffer, int offset, double value) =>
        BinaryPrimitives.WriteInt64LittleEndian(buffer.AsSpan(offset, 8), BitConverter.DoubleToInt64Bits(value));
}

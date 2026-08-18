using System.Buffers.Binary;
using UCAD.Core.Gis;
using Xunit;

namespace UCAD.Core.Tests;

public sealed class ShapefileAttributeBindingTests
{
    [Fact]
    public void MultipartShapePartsShareTheirAuthoritativeSourceDbfRecord()
    {
        var shp = BuildTwoPartPolylineShp();
        var dbf = CadDbfCodec.Export(new CadDbfTable(
            [new CadDbfFieldDefinition("LAYER", CadDbfFieldType.Character, 24)],
            [new CadDbfRecord(new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
            {
                ["LAYER"] = "ROAD"
            })]),
            new DateTime(2026, 8, 18));
        var geometry = CadShapefileGeometryCodec.Import(shp);
        var attributes = CadDbfCodec.Import(dbf);

        var binding = CadShapefileAttributeBinding.Bind(shp, dbf, geometry, attributes);

        Assert.Equal(2, geometry.Entities.Count);
        Assert.True(binding.CanBind);
        Assert.Equal(2, binding.AttributesByEntity.Count);
        Assert.Equal("ROAD", binding.AttributesByEntity[0]!.Values["LAYER"]);
        Assert.Equal("ROAD", binding.AttributesByEntity[1]!.Values["LAYER"]);
        Assert.Empty(binding.Warnings);
    }

    [Fact]
    public void DeletedDbfRowDisablesAutomaticRecordBinding()
    {
        var shp = BuildTwoPartPolylineShp();
        var dbf = CadDbfCodec.Export(new CadDbfTable(
            [new CadDbfFieldDefinition("LAYER", CadDbfFieldType.Character, 24)],
            [new CadDbfRecord(new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
            {
                ["LAYER"] = "ROAD"
            })]),
            new DateTime(2026, 8, 18));
        var headerLength = BinaryPrimitives.ReadUInt16LittleEndian(dbf.AsSpan(8, 2));
        dbf[headerLength] = (byte)'*';
        var geometry = CadShapefileGeometryCodec.Import(shp);
        var attributes = CadDbfCodec.Import(dbf);

        var binding = CadShapefileAttributeBinding.Bind(shp, dbf, geometry, attributes);

        Assert.False(binding.CanBind);
        Assert.All(binding.AttributesByEntity, Assert.Null);
        Assert.True(binding.Warnings.Any(warning => warning.Contains("deleted", StringComparison.OrdinalIgnoreCase)));
    }

    private static byte[] BuildTwoPartPolylineShp()
    {
        const int headerBytes = 100;
        const int contentBytes = 112;
        const int recordBytes = 8 + contentBytes;
        var file = new byte[headerBytes + recordBytes];
        BinaryPrimitives.WriteInt32BigEndian(file.AsSpan(0, 4), 9994);
        BinaryPrimitives.WriteInt32BigEndian(file.AsSpan(24, 4), file.Length / 2);
        BinaryPrimitives.WriteInt32LittleEndian(file.AsSpan(28, 4), 1000);
        BinaryPrimitives.WriteInt32LittleEndian(file.AsSpan(32, 4), 3);
        WriteDouble(file, 36, 0);
        WriteDouble(file, 44, 0);
        WriteDouble(file, 52, 30);
        WriteDouble(file, 60, 10);

        var recordOffset = headerBytes;
        BinaryPrimitives.WriteInt32BigEndian(file.AsSpan(recordOffset, 4), 1);
        BinaryPrimitives.WriteInt32BigEndian(file.AsSpan(recordOffset + 4, 4), contentBytes / 2);
        var content = recordOffset + 8;
        BinaryPrimitives.WriteInt32LittleEndian(file.AsSpan(content, 4), 3);
        WriteDouble(file, content + 4, 0);
        WriteDouble(file, content + 12, 0);
        WriteDouble(file, content + 20, 30);
        WriteDouble(file, content + 28, 10);
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

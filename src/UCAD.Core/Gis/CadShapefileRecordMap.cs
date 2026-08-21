using System.Buffers.Binary;

namespace UCAD.Core.Gis;

public sealed record CadShapefileRecordMap(
    int ShapeRecordCount,
    IReadOnlyList<int> SourceRecordIndexByEntity)
{
    public static CadShapefileRecordMap Read(ReadOnlySpan<byte> shpContent)
    {
        const int headerBytes = 100;
        if (shpContent.Length < headerBytes) throw new FormatException("SHP file is shorter than the 100-byte header.");
        var declaredWords = BinaryPrimitives.ReadInt32BigEndian(shpContent.Slice(24, 4));
        var declaredBytes = checked(declaredWords * 2);
        if (declaredBytes < headerBytes || declaredBytes > shpContent.Length) throw new FormatException("SHP declares an invalid file length.");
        var fileShapeType = BinaryPrimitives.ReadInt32LittleEndian(shpContent.Slice(32, 4));

        var map = new List<int>();
        var offset = headerBytes;
        var recordIndex = 0;
        while (offset < declaredBytes)
        {
            if (offset + 8 > declaredBytes) throw new FormatException("SHP record header is truncated.");
            var contentWords = BinaryPrimitives.ReadInt32BigEndian(shpContent.Slice(offset + 4, 4));
            var contentBytes = checked(contentWords * 2);
            if (contentBytes < 4 || offset + 8 + contentBytes > declaredBytes)
                throw new FormatException($"SHP record {recordIndex + 1} has an invalid content length.");
            var content = shpContent.Slice(offset + 8, contentBytes);
            var shapeType = BinaryPrimitives.ReadInt32LittleEndian(content[..4]);
            if (shapeType != 0 && shapeType != fileShapeType)
                throw new FormatException($"SHP record {recordIndex + 1} type {shapeType} does not match file type {fileShapeType}.");

            var entityCount = shapeType switch
            {
                0 => 0,
                (int)CadShapefileShapeType.Point => 1,
                (int)CadShapefileShapeType.PolyLine or (int)CadShapefileShapeType.Polygon => ReadPartCount(content, recordIndex + 1),
                _ => throw new NotSupportedException($"SHP shape type {shapeType} is not supported by the record mapper.")
            };
            for (var index = 0; index < entityCount; index++) map.Add(recordIndex);
            recordIndex++;
            offset += 8 + contentBytes;
        }
        if (offset != declaredBytes) throw new FormatException("SHP records do not terminate at the declared file length.");
        return new CadShapefileRecordMap(recordIndex, map.AsReadOnly());
    }

    private static int ReadPartCount(ReadOnlySpan<byte> content, int recordNumber)
    {
        if (content.Length < 44) throw new FormatException($"SHP multipart record {recordNumber} is truncated.");
        var count = BinaryPrimitives.ReadInt32LittleEndian(content.Slice(36, 4));
        if (count <= 0) throw new FormatException($"SHP multipart record {recordNumber} declares an invalid part count.");
        return count;
    }
}

public static class CadDbfRecordLayout
{
    public static CadDbfRecordLayoutInfo Read(ReadOnlySpan<byte> dbfContent)
    {
        if (dbfContent.Length < 32) throw new FormatException("DBF file is too short.");
        var recordCount = BinaryPrimitives.ReadInt32LittleEndian(dbfContent.Slice(4, 4));
        var headerLength = BinaryPrimitives.ReadUInt16LittleEndian(dbfContent.Slice(8, 2));
        var recordLength = BinaryPrimitives.ReadUInt16LittleEndian(dbfContent.Slice(10, 2));
        if (recordCount < 0 || headerLength < 33 || recordLength < 2 || headerLength > dbfContent.Length)
            throw new FormatException("DBF header contains invalid record layout.");

        var deleted = new List<int>();
        var offset = headerLength;
        for (var index = 0; index < recordCount; index++)
        {
            if (offset + recordLength > dbfContent.Length) throw new FormatException($"DBF record {index + 1} is truncated.");
            if (dbfContent[offset] == (byte)'*') deleted.Add(index);
            offset += recordLength;
        }
        return new CadDbfRecordLayoutInfo(recordCount, deleted.AsReadOnly());
    }
}

public sealed record CadDbfRecordLayoutInfo(
    int RecordCount,
    IReadOnlyList<int> DeletedRecordIndexes)
{
    public bool HasDeletedRecords => DeletedRecordIndexes.Count > 0;
}

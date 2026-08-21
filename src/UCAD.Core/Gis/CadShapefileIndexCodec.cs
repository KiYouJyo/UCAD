using System.Buffers.Binary;

namespace UCAD.Core.Gis;

public sealed record CadShapefileIndexValidation(
    int RecordCount,
    bool IsConsistent,
    IReadOnlyList<string> Warnings);

/// <summary>
/// Builds the standard .shx positional index directly from a .shp stream and validates
/// an index against the authoritative SHP record headers. Offsets and lengths follow the
/// Shapefile specification's big-endian 16-bit-word units.
/// </summary>
public static class CadShapefileIndexCodec
{
    private const int HeaderBytes = 100;
    private const int FileCode = 9994;
    private const int Version = 1000;

    public static byte[] Build(ReadOnlySpan<byte> shpContent)
    {
        ValidateShpHeader(shpContent, out var declaredBytes);
        var entries = ReadShpEntries(shpContent, declaredBytes);
        var shxBytes = checked(HeaderBytes + (entries.Count * 8));
        var shx = new byte[shxBytes];
        shpContent[..HeaderBytes].CopyTo(shx);
        BinaryPrimitives.WriteInt32BigEndian(shx.AsSpan(24, 4), shxBytes / 2);
        var offset = HeaderBytes;
        foreach (var entry in entries)
        {
            BinaryPrimitives.WriteInt32BigEndian(shx.AsSpan(offset, 4), entry.OffsetWords);
            BinaryPrimitives.WriteInt32BigEndian(shx.AsSpan(offset + 4, 4), entry.ContentLengthWords);
            offset += 8;
        }
        return shx;
    }

    public static CadShapefileIndexValidation Validate(
        ReadOnlySpan<byte> shpContent,
        ReadOnlySpan<byte> shxContent)
    {
        ValidateShpHeader(shpContent, out var shpDeclaredBytes);
        if (shxContent.Length < HeaderBytes) throw new FormatException("SHX file is shorter than the 100-byte header.");
        if (BinaryPrimitives.ReadInt32BigEndian(shxContent[..4]) != FileCode) throw new FormatException("Invalid SHX file code.");
        if (BinaryPrimitives.ReadInt32LittleEndian(shxContent.Slice(28, 4)) != Version) throw new FormatException("Unsupported SHX version.");
        var shxDeclaredWords = BinaryPrimitives.ReadInt32BigEndian(shxContent.Slice(24, 4));
        var shxDeclaredBytes = checked(shxDeclaredWords * 2);
        if (shxDeclaredBytes < HeaderBytes || shxDeclaredBytes > shxContent.Length || ((shxDeclaredBytes - HeaderBytes) % 8) != 0)
            throw new FormatException("SHX declares an invalid file length.");
        var shpShapeType = BinaryPrimitives.ReadInt32LittleEndian(shpContent.Slice(32, 4));
        var shxShapeType = BinaryPrimitives.ReadInt32LittleEndian(shxContent.Slice(32, 4));
        if (shpShapeType != shxShapeType) throw new FormatException("SHX shape type does not match SHP shape type.");

        var authoritative = ReadShpEntries(shpContent, shpDeclaredBytes);
        var indexCount = (shxDeclaredBytes - HeaderBytes) / 8;
        var warnings = new List<string>();
        var consistent = authoritative.Count == indexCount;
        if (!consistent)
            warnings.Add($"SHX contains {indexCount} records while SHP contains {authoritative.Count} records.");

        var compareCount = Math.Min(authoritative.Count, indexCount);
        for (var index = 0; index < compareCount; index++)
        {
            var entryOffset = HeaderBytes + (index * 8);
            var offsetWords = BinaryPrimitives.ReadInt32BigEndian(shxContent.Slice(entryOffset, 4));
            var contentWords = BinaryPrimitives.ReadInt32BigEndian(shxContent.Slice(entryOffset + 4, 4));
            var expected = authoritative[index];
            if (offsetWords == expected.OffsetWords && contentWords == expected.ContentLengthWords) continue;
            consistent = false;
            warnings.Add(
                $"SHX record {index + 1} points to offset/length {offsetWords}/{contentWords} words; " +
                $"SHP requires {expected.OffsetWords}/{expected.ContentLengthWords}.");
        }

        return new CadShapefileIndexValidation(authoritative.Count, consistent, warnings.AsReadOnly());
    }

    private static void ValidateShpHeader(ReadOnlySpan<byte> content, out int declaredBytes)
    {
        if (content.Length < HeaderBytes) throw new FormatException("SHP file is shorter than the 100-byte header.");
        if (BinaryPrimitives.ReadInt32BigEndian(content[..4]) != FileCode) throw new FormatException("Invalid SHP file code.");
        if (BinaryPrimitives.ReadInt32LittleEndian(content.Slice(28, 4)) != Version) throw new FormatException("Unsupported SHP version.");
        var declaredWords = BinaryPrimitives.ReadInt32BigEndian(content.Slice(24, 4));
        declaredBytes = checked(declaredWords * 2);
        if (declaredBytes < HeaderBytes || declaredBytes > content.Length)
            throw new FormatException("SHP declares an invalid file length.");
    }

    private static IReadOnlyList<IndexEntry> ReadShpEntries(ReadOnlySpan<byte> content, int declaredBytes)
    {
        var entries = new List<IndexEntry>();
        var offset = HeaderBytes;
        while (offset < declaredBytes)
        {
            if (offset + 8 > declaredBytes) throw new FormatException("SHP record header is truncated.");
            var contentWords = BinaryPrimitives.ReadInt32BigEndian(content.Slice(offset + 4, 4));
            if (contentWords < 2) throw new FormatException($"SHP record {entries.Count + 1} declares an invalid content length.");
            var recordBytes = checked(8 + (contentWords * 2));
            if (offset + recordBytes > declaredBytes) throw new FormatException($"SHP record {entries.Count + 1} extends past the declared file length.");
            entries.Add(new IndexEntry(offset / 2, contentWords));
            offset += recordBytes;
        }
        if (offset != declaredBytes) throw new FormatException("SHP records do not terminate at the declared file length.");
        return entries.AsReadOnly();
    }

    private readonly record struct IndexEntry(int OffsetWords, int ContentLengthWords);
}

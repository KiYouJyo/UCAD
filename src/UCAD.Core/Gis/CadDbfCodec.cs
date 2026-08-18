using System.Buffers.Binary;
using System.Globalization;
using System.Text;

namespace UCAD.Core.Gis;

public enum CadDbfFieldType
{
    Character,
    Numeric,
    Logical
}

public sealed record CadDbfFieldDefinition(
    string Name,
    CadDbfFieldType Type,
    byte Width,
    byte DecimalCount = 0)
{
    public CadDbfFieldDefinition Validate()
    {
        if (string.IsNullOrWhiteSpace(Name)) throw new ArgumentException("DBF field name cannot be empty.", nameof(Name));
        if (Name.Length > 10) throw new ArgumentException("dBASE III field names are limited to 10 characters.", nameof(Name));
        if (Name.Any(character => character > 0x7F)) throw new ArgumentException("DBF field names must be ASCII; use a localized alias outside the dBASE field name.", nameof(Name));
        if (Width is 0 or > 254) throw new ArgumentOutOfRangeException(nameof(Width));
        if (Type == CadDbfFieldType.Logical && Width != 1) throw new ArgumentException("Logical DBF fields must have width 1.", nameof(Width));
        if (Type != CadDbfFieldType.Numeric && DecimalCount != 0) throw new ArgumentException("Only numeric DBF fields may declare decimals.", nameof(DecimalCount));
        if (DecimalCount >= Width) throw new ArgumentException("DBF decimal count must be smaller than field width.", nameof(DecimalCount));
        return this;
    }
}

public sealed record CadDbfRecord(IReadOnlyDictionary<string, string?> Values);

public sealed record CadDbfTable(
    IReadOnlyList<CadDbfFieldDefinition> Fields,
    IReadOnlyList<CadDbfRecord> Records);

/// <summary>
/// dBASE III attribute-table codec for Shapefile sidecars. Field names remain ASCII as
/// required by the legacy format; field values use UTF-8 and should be paired with a
/// .cpg file containing UTF-8. Character, numeric and logical fields are supported.
/// </summary>
public static class CadDbfCodec
{
    private static readonly Encoding Utf8 = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    public static byte[] Export(CadDbfTable table, DateTime? modifiedDate = null)
    {
        ArgumentNullException.ThrowIfNull(table);
        var fields = table.Fields.Select(field => field.Validate()).ToArray();
        if (fields.Length == 0) throw new ArgumentException("DBF requires at least one field.", nameof(table));
        if (fields.Select(field => field.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count() != fields.Length)
            throw new ArgumentException("DBF field names must be unique.", nameof(table));
        var headerLength = checked(32 + (fields.Length * 32) + 1);
        var recordLength = checked(1 + fields.Sum(field => field.Width));
        if (headerLength > ushort.MaxValue) throw new ArgumentException("DBF header is too large.", nameof(table));
        if (recordLength > ushort.MaxValue) throw new ArgumentException("DBF record is too wide.", nameof(table));

        var date = (modifiedDate ?? DateTime.UtcNow).Date;
        using var stream = new MemoryStream();
        Span<byte> header = stackalloc byte[32];
        header.Clear();
        header[0] = 0x03;
        header[1] = checked((byte)Math.Clamp(date.Year - 1900, 0, 255));
        header[2] = (byte)date.Month;
        header[3] = (byte)date.Day;
        BinaryPrimitives.WriteInt32LittleEndian(header[4..8], table.Records.Count);
        BinaryPrimitives.WriteUInt16LittleEndian(header[8..10], (ushort)headerLength);
        BinaryPrimitives.WriteUInt16LittleEndian(header[10..12], (ushort)recordLength);
        stream.Write(header);

        Span<byte> descriptor = stackalloc byte[32];
        foreach (var field in fields)
        {
            descriptor.Clear();
            var nameBytes = Encoding.ASCII.GetBytes(field.Name);
            nameBytes.CopyTo(descriptor[..Math.Min(11, nameBytes.Length)]);
            descriptor[11] = field.Type switch
            {
                CadDbfFieldType.Character => (byte)'C',
                CadDbfFieldType.Numeric => (byte)'N',
                CadDbfFieldType.Logical => (byte)'L',
                _ => throw new NotSupportedException()
            };
            descriptor[16] = field.Width;
            descriptor[17] = field.DecimalCount;
            stream.Write(descriptor);
        }
        stream.WriteByte(0x0D);

        foreach (var record in table.Records)
        {
            stream.WriteByte((byte)' ');
            foreach (var field in fields)
            {
                record.Values.TryGetValue(field.Name, out var value);
                var bytes = EncodeField(field, value);
                stream.Write(bytes);
            }
        }
        stream.WriteByte(0x1A);
        return stream.ToArray();
    }

    public static CadDbfTable Import(ReadOnlySpan<byte> content)
    {
        if (content.Length < 33) throw new FormatException("DBF file is too short.");
        if (content[0] != 0x03) throw new NotSupportedException($"DBF version 0x{content[0]:X2} is not supported by the dBASE III foundation codec.");
        var recordCount = BinaryPrimitives.ReadInt32LittleEndian(content[4..8]);
        var headerLength = BinaryPrimitives.ReadUInt16LittleEndian(content[8..10]);
        var recordLength = BinaryPrimitives.ReadUInt16LittleEndian(content[10..12]);
        if (recordCount < 0 || headerLength < 33 || headerLength > content.Length || recordLength < 2)
            throw new FormatException("DBF header contains invalid lengths.");

        var fields = new List<CadDbfFieldDefinition>();
        var descriptorOffset = 32;
        while (descriptorOffset < headerLength)
        {
            if (content[descriptorOffset] == 0x0D) break;
            if (descriptorOffset + 32 > headerLength) throw new FormatException("DBF field descriptor is truncated.");
            var descriptor = content.Slice(descriptorOffset, 32);
            var zero = descriptor[..11].IndexOf((byte)0);
            var nameLength = zero >= 0 ? zero : 11;
            var name = Encoding.ASCII.GetString(descriptor[..nameLength]).Trim();
            var type = descriptor[11] switch
            {
                (byte)'C' => CadDbfFieldType.Character,
                (byte)'N' or (byte)'F' => CadDbfFieldType.Numeric,
                (byte)'L' => CadDbfFieldType.Logical,
                var unsupported => throw new NotSupportedException($"DBF field type '{(char)unsupported}' is not supported.")
            };
            fields.Add(new CadDbfFieldDefinition(name, type, descriptor[16], descriptor[17]).Validate());
            descriptorOffset += 32;
        }
        if (fields.Count == 0) throw new FormatException("DBF contains no supported fields.");
        var expectedRecordLength = 1 + fields.Sum(field => field.Width);
        if (expectedRecordLength != recordLength) throw new FormatException("DBF record length does not match field descriptors.");

        var records = new List<CadDbfRecord>(recordCount);
        var offset = headerLength;
        for (var recordIndex = 0; recordIndex < recordCount; recordIndex++)
        {
            if (offset + recordLength > content.Length) throw new FormatException($"DBF record {recordIndex + 1} is truncated.");
            var deleted = content[offset] == (byte)'*';
            var values = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
            var fieldOffset = offset + 1;
            foreach (var field in fields)
            {
                var raw = content.Slice(fieldOffset, field.Width);
                var value = DecodeField(field, raw);
                values[field.Name] = value;
                fieldOffset += field.Width;
            }
            if (!deleted) records.Add(new CadDbfRecord(values));
            offset += recordLength;
        }
        return new CadDbfTable(fields.AsReadOnly(), records.AsReadOnly());
    }

    public static byte[] CreateCpgUtf8() => Encoding.ASCII.GetBytes("UTF-8\r\n");

    private static byte[] EncodeField(CadDbfFieldDefinition field, string? value)
    {
        return field.Type switch
        {
            CadDbfFieldType.Character => EncodeCharacter(value ?? string.Empty, field.Width),
            CadDbfFieldType.Numeric => EncodeNumeric(value, field.Width, field.DecimalCount),
            CadDbfFieldType.Logical => EncodeLogical(value),
            _ => throw new NotSupportedException()
        };
    }

    private static byte[] EncodeCharacter(string value, int width)
    {
        var buffer = Enumerable.Repeat((byte)' ', width).ToArray();
        var offset = 0;
        foreach (var rune in value.EnumerateRunes())
        {
            var bytes = Utf8.GetBytes(rune.ToString());
            if (offset + bytes.Length > width) break;
            bytes.CopyTo(buffer, offset);
            offset += bytes.Length;
        }
        return buffer;
    }

    private static byte[] EncodeNumeric(string? value, int width, int decimals)
    {
        var buffer = Enumerable.Repeat((byte)' ', width).ToArray();
        if (string.IsNullOrWhiteSpace(value)) return buffer;
        if (!double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var number) || !double.IsFinite(number))
            throw new FormatException($"DBF numeric value '{value}' is not a finite invariant number.");
        var format = decimals > 0 ? "F" + decimals.ToString(CultureInfo.InvariantCulture) : "0";
        var text = number.ToString(format, CultureInfo.InvariantCulture);
        if (text.Length > width) throw new FormatException($"DBF numeric value '{text}' exceeds field width {width}.");
        Encoding.ASCII.GetBytes(text).CopyTo(buffer, width - text.Length);
        return buffer;
    }

    private static byte[] EncodeLogical(string? value)
    {
        var normalized = value?.Trim();
        var character = normalized?.ToUpperInvariant() switch
        {
            "TRUE" or "T" or "Y" or "YES" or "1" => 'T',
            "FALSE" or "F" or "N" or "NO" or "0" => 'F',
            null or "" or "?" => '?',
            _ => throw new FormatException($"DBF logical value '{value}' is invalid.")
        };
        return [(byte)character];
    }

    private static string? DecodeField(CadDbfFieldDefinition field, ReadOnlySpan<byte> raw)
    {
        if (field.Type == CadDbfFieldType.Character)
        {
            var end = raw.Length;
            while (end > 0 && raw[end - 1] is (byte)' ' or 0) end--;
            if (end == 0) return null;
            return Utf8.GetString(raw[..end]);
        }
        var text = Encoding.ASCII.GetString(raw).Trim();
        if (text.Length == 0) return null;
        if (field.Type == CadDbfFieldType.Logical)
        {
            return text.ToUpperInvariant() switch
            {
                "T" or "Y" => "true",
                "F" or "N" => "false",
                _ => null
            };
        }
        return text;
    }
}

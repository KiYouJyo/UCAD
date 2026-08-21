using System.Buffers.Binary;
using System.Text;

namespace UCAD.Core.IO;

/// <summary>
/// Decoder for classic AutoCAD BIGFONT SHX containers. BIGFONT changes only the glyph
/// index/container; glyph bodies still use AutoCAD SHX drawing bytecode, so the result is
/// exposed as <see cref="CadShxFile"/> and rendered by the same vector engine as normal SHX.
/// </summary>
public static class CadShxBigFontCodec
{
    private static readonly byte[] Signature = Encoding.ASCII.GetBytes("AutoCAD-86 bigfont 1.0");
    private const int MaxGlyphCount = 65536;
    private const int MaxInstructions = 20000;

    public static bool IsBigFont(ReadOnlySpan<byte> content) =>
        content.Length >= Signature.Length && content.StartsWith(Signature);

    public static CadShxFile Read(ReadOnlyMemory<byte> content)
    {
        var data = content.ToArray();
        if (!IsBigFont(data)) throw new InvalidDataException("The resource is not an AutoCAD-86 bigfont 1.0 SHX file.");

        var marker = FindHeaderTerminator(data);
        if (marker < 0) throw new InvalidDataException("BIGFONT SHX header terminator CR/LF/1A was not found.");
        var cursor = marker + 3;
        Ensure(data, cursor, 6);
        _ = ReadUInt16(data, ref cursor); // item length/version-dependent index descriptor size
        var count = ReadUInt16(data, ref cursor);
        var changeCount = ReadUInt16(data, ref cursor);
        if (count == 0 || count > MaxGlyphCount) throw new InvalidDataException("BIGFONT glyph count is invalid.");
        var changeBytes = checked(changeCount * 4);
        Ensure(data, cursor, changeBytes);
        cursor += changeBytes;

        var entries = new List<BigFontIndexEntry>(count);
        for (var i = 0; i < count; i++)
        {
            Ensure(data, cursor, 8);
            var code = ReadUInt16(data, ref cursor);
            var length = ReadUInt16(data, ref cursor);
            var offset = ReadUInt32(data, ref cursor);
            if (length == 0 || offset >= data.Length) continue;
            if ((ulong)offset + length > (ulong)data.Length) continue;
            entries.Add(new BigFontIndexEntry(code, length, offset));
        }
        if (entries.Count == 0) throw new InvalidDataException("BIGFONT SHX contains no readable glyph index entries.");

        var above = 0;
        var below = 0;
        var name = "AutoCAD BIGFONT";
        var symbols = new Dictionary<int, CadShxSymbol>();
        foreach (var entry in entries)
        {
            var body = data.AsSpan((int)entry.Offset, entry.Length);
            if (entry.Code == 0)
            {
                ParseMetrics(body, ref name, ref above, ref below);
                continue;
            }

            try
            {
                var instructions = ParseInstructions(body, wideSubshape: true);
                symbols[entry.Code] = new CadShxSymbol(entry.Code, entry.Code.ToString("X4"), instructions);
            }
            catch (Exception ex) when (ex is EndOfStreamException or InvalidDataException or OverflowException)
            {
                // A corrupt individual glyph must not make an otherwise useful BigFont unusable.
            }
        }
        if (symbols.Count == 0) throw new InvalidDataException("BIGFONT SHX contains no decodable vector glyphs.");
        return new CadShxFile(name, false, false, above, below, symbols);
    }

    public static bool TryRead(ReadOnlyMemory<byte> content, out CadShxFile? file, out string? warning)
    {
        try
        {
            file = Read(content);
            warning = null;
            return true;
        }
        catch (Exception ex) when (ex is InvalidDataException or EndOfStreamException or ArgumentException or OverflowException)
        {
            file = null;
            warning = ex.Message;
            return false;
        }
    }

    private static void ParseMetrics(ReadOnlySpan<byte> body, ref string name, ref int above, ref int below)
    {
        var terminator = body.IndexOf((byte)0);
        if (terminator >= 0)
        {
            var decoded = Encoding.UTF8.GetString(body[..terminator]);
            if (!string.IsNullOrWhiteSpace(decoded)) name = decoded.Trim();
        }
        else terminator = -1;

        var start = terminator + 1;
        while (start < body.Length && body[start] == 0) start++;
        var remaining = body.Length - start;
        if (remaining >= 5)
        {
            // Extended BIGFONT: character height, reserved, modes, character width, terminator.
            above = body[start];
            below = 0;
        }
        else if (remaining == 4)
        {
            // Non-extended: above, below, modes, terminator.
            above = body[start];
            below = body[start + 1];
        }
        else if (remaining >= 2)
        {
            // Short tail seen in common East-Asian BIGFONT resources.
            above = body[start];
            below = 0;
        }
    }

    private static IReadOnlyList<CadShxInstruction> ParseInstructions(ReadOnlySpan<byte> bytes, bool wideSubshape)
    {
        var instructions = new List<CadShxInstruction>();
        var i = 0;
        while (i < bytes.Length && instructions.Count < MaxInstructions)
        {
            var code = bytes[i++];
            if (code == 0) break;
            switch (code)
            {
                case 1 or 2 or 5 or 6 or 14:
                    instructions.Add(new CadShxInstruction(code, []));
                    break;
                case 3 or 4:
                    Need(bytes, i, 1);
                    instructions.Add(new CadShxInstruction(code, [bytes[i++]]));
                    break;
                case 7:
                    Need(bytes, i, wideSubshape ? 2 : 1);
                    var subshape = wideSubshape ? bytes[i] | (bytes[i + 1] << 8) : bytes[i];
                    i += wideSubshape ? 2 : 1;
                    instructions.Add(new CadShxInstruction(code, [subshape]));
                    break;
                case 8:
                    Need(bytes, i, 2);
                    instructions.Add(new CadShxInstruction(code, [Signed(bytes[i++]), Signed(bytes[i++])]));
                    break;
                case 9:
                {
                    var args = new List<int>();
                    while (true)
                    {
                        Need(bytes, i, 2);
                        var x = Signed(bytes[i++]);
                        var y = Signed(bytes[i++]);
                        if (x == 0 && y == 0) break;
                        args.Add(x); args.Add(y);
                    }
                    instructions.Add(new CadShxInstruction(code, args));
                    break;
                }
                case 10:
                    Need(bytes, i, 2);
                    instructions.Add(new CadShxInstruction(code, [bytes[i++], Octant(bytes[i++])]));
                    break;
                case 11:
                    Need(bytes, i, 5);
                    instructions.Add(new CadShxInstruction(code, [bytes[i++], bytes[i++], bytes[i++], bytes[i++], Octant(bytes[i++])]));
                    break;
                case 12:
                    Need(bytes, i, 3);
                    instructions.Add(new CadShxInstruction(code, [Signed(bytes[i++]), Signed(bytes[i++]), Signed(bytes[i++])]));
                    break;
                case 13:
                {
                    var args = new List<int>();
                    while (true)
                    {
                        Need(bytes, i, 2);
                        var x = Signed(bytes[i++]);
                        var y = Signed(bytes[i++]);
                        if (x == 0 && y == 0) break;
                        Need(bytes, i, 1);
                        args.Add(x); args.Add(y); args.Add(Signed(bytes[i++]));
                    }
                    instructions.Add(new CadShxInstruction(code, args));
                    break;
                }
                default:
                    instructions.Add(new CadShxInstruction(code, []));
                    break;
            }
        }
        if (instructions.Count >= MaxInstructions) throw new InvalidDataException("BIGFONT glyph instruction limit was exceeded.");
        return instructions;
    }

    private static int FindHeaderTerminator(ReadOnlySpan<byte> data)
    {
        for (var i = Signature.Length; i + 2 < data.Length; i++)
            if (data[i] == 0x0D && data[i + 1] == 0x0A && data[i + 2] == 0x1A) return i;
        return -1;
    }

    private static ushort ReadUInt16(byte[] data, ref int cursor)
    {
        Ensure(data, cursor, 2);
        var value = BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(cursor, 2));
        cursor += 2;
        return value;
    }

    private static uint ReadUInt32(byte[] data, ref int cursor)
    {
        Ensure(data, cursor, 4);
        var value = BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(cursor, 4));
        cursor += 4;
        return value;
    }

    private static void Ensure(byte[] data, int offset, int count)
    {
        if (offset < 0 || count < 0 || offset + count > data.Length) throw new EndOfStreamException("BIGFONT SHX is truncated.");
    }

    private static void Need(ReadOnlySpan<byte> data, int offset, int count)
    {
        if (offset < 0 || count < 0 || offset + count > data.Length) throw new EndOfStreamException("BIGFONT glyph bytecode is truncated.");
    }

    private static int Signed(byte value) => value > 127 ? value - 256 : value;
    private static int Octant(byte value) => (value & 0x80) != 0 ? -(value & 0x7F) : value;
    private readonly record struct BigFontIndexEntry(int Code, int Length, uint Offset);
}

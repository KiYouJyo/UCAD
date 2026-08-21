using System.Globalization;
using System.Text;
using UCAD.Core.Entities;
using UCAD.Core.Geometry;

namespace UCAD.Core.IO;

/// <summary>
/// Safe, non-executing decoder for AutoCAD shape/font resources. The decoder interprets
/// the documented SHX/SHP drawing bytecode into ordinary vector strokes; it never loads
/// native code or invokes AutoCAD. AutoCAD-86 shape 1.0/1.1 and unifont 1.0 containers
/// are supported, together with ASCII SHP source files.
/// </summary>
public static class CadShxCodec
{
    private const string Shape10 = "AutoCAD-86 shapes 1.0";
    private const string Shape11 = "AutoCAD-86 shapes 1.1";
    private const string UniFont10 = "AutoCAD-86 unifont 1.0";
    private const string BigFont10 = "AutoCAD-86 bigfont 1.0";
    private const int MaxRecursionDepth = 32;
    private const int MaxStackDepth = 16;
    private const int MaxInstructionsPerShape = 20000;
    private const double Epsilon = 1e-10;
    private const double MaxArcStep = Math.PI / 36.0;

    public static CadShxFile Read(ReadOnlyMemory<byte> content)
    {
        if (content.IsEmpty) throw new ArgumentException("SHX/SHP content cannot be empty.", nameof(content));
        var data = content.ToArray();
        if (LooksLikeAsciiShp(data)) return ReadAsciiShp(data);
        if (StartsWithAscii(data, Shape10) || StartsWithAscii(data, Shape11)) return ReadLegacyShapes(data);
        if (StartsWithAscii(data, UniFont10)) return ReadUniFont(data);
        if (StartsWithAscii(data, BigFont10))
            throw new NotSupportedException("AutoCAD BigFont SHX uses a separate multibyte index format; use the BigFont decoder path rather than treating it as a normal SHX font.");
        throw new InvalidDataException("The resource is not a recognized AutoCAD SHX/SHP shape file.");
    }

    public static bool TryRead(ReadOnlyMemory<byte> content, out CadShxFile? file, out string? warning)
    {
        try
        {
            file = Read(content);
            warning = null;
            return true;
        }
        catch (Exception ex) when (ex is InvalidDataException or FormatException or ArgumentException or NotSupportedException or EndOfStreamException)
        {
            file = null;
            warning = ex.Message;
            return false;
        }
    }

    public static IReadOnlyList<IReadOnlyList<CadPoint>> RenderShape(CadShxFile file, string shapeName)
    {
        ArgumentNullException.ThrowIfNull(file);
        ArgumentException.ThrowIfNullOrWhiteSpace(shapeName);
        var symbol = file.Symbols.Values.FirstOrDefault(candidate => string.Equals(candidate.Name, shapeName.Trim(), StringComparison.OrdinalIgnoreCase));
        if (symbol is null) throw new KeyNotFoundException($"SHX shape '{shapeName}' was not found in '{file.Name}'.");
        return RenderSymbol(file, symbol.Number);
    }

    public static IReadOnlyList<IReadOnlyList<CadPoint>> RenderGlyph(CadShxFile file, int codePoint)
    {
        ArgumentNullException.ThrowIfNull(file);
        return RenderSymbol(file, codePoint);
    }

    public static IReadOnlyList<IReadOnlyList<CadPoint>> RenderShapeWorld(CadShxFile file, ShapeReferenceEntity shape)
    {
        var local = RenderShape(file, shape.ShapeName);
        var sin = Math.Sin(shape.RotationRadians);
        var cos = Math.Cos(shape.RotationRadians);
        var shear = Math.Tan(shape.ObliqueRadians);
        var result = new List<IReadOnlyList<CadPoint>>(local.Count);
        foreach (var stroke in local)
        {
            var transformed = new CadPoint[stroke.Count];
            for (var i = 0; i < stroke.Count; i++)
            {
                var x = stroke[i].X * shape.Size * shape.XScale;
                var y = stroke[i].Y * shape.Size;
                x += y * shear;
                transformed[i] = new CadPoint(
                    shape.InsertionPoint.X + (x * cos) - (y * sin),
                    shape.InsertionPoint.Y + (x * sin) + (y * cos));
            }
            if (transformed.Length >= 2) result.Add(transformed);
        }
        return result;
    }

    private static IReadOnlyList<IReadOnlyList<CadPoint>> RenderSymbol(CadShxFile file, int number)
    {
        if (!file.Symbols.ContainsKey(number)) throw new KeyNotFoundException($"SHX symbol {number} was not found in '{file.Name}'.");
        var state = new RenderState();
        Execute(file, number, state, 0);
        state.FlushStroke();
        return state.Strokes.Select(stroke => (IReadOnlyList<CadPoint>)stroke.ToArray()).ToArray();
    }

    private static void Execute(CadShxFile file, int number, RenderState state, int depth)
    {
        if (depth > MaxRecursionDepth) throw new InvalidDataException("SHX subshape recursion limit was exceeded.");
        if (!file.Symbols.TryGetValue(number, out var symbol)) throw new InvalidDataException($"SHX subshape {number} is missing.");
        if (symbol.Instructions.Count > MaxInstructionsPerShape) throw new InvalidDataException("SHX shape exceeds the safe instruction limit.");

        for (var index = 0; index < symbol.Instructions.Count; index++)
        {
            var instruction = symbol.Instructions[index];
            switch (instruction.Code)
            {
                case 1:
                    state.PenDown = true;
                    break;
                case 2:
                    state.PenDown = false;
                    state.FlushStroke();
                    break;
                case 3:
                    if (instruction.Args[0] == 0) throw new InvalidDataException("SHX divide-by-zero scale instruction.");
                    state.VectorLength /= instruction.Args[0];
                    break;
                case 4:
                    state.VectorLength *= instruction.Args[0];
                    break;
                case 5:
                    if (state.PositionStack.Count >= MaxStackDepth) throw new InvalidDataException("SHX position stack overflow.");
                    state.PositionStack.Push(state.Position);
                    break;
                case 6:
                    if (state.PositionStack.Count == 0) throw new InvalidDataException("SHX position stack underflow.");
                    state.MoveWithoutDrawing(state.PositionStack.Pop());
                    break;
                case 7:
                    Execute(file, instruction.Args[0], state, depth + 1);
                    break;
                case 8:
                    state.Displace(instruction.Args[0], instruction.Args[1]);
                    break;
                case 9:
                    for (var i = 0; i + 1 < instruction.Args.Count; i += 2)
                        state.Displace(instruction.Args[i], instruction.Args[i + 1]);
                    break;
                case 10:
                    DrawOctantArc(state, instruction.Args[0], instruction.Args[1]);
                    break;
                case 11:
                    DrawFractionalArc(state, instruction.Args);
                    break;
                case 12:
                    DrawBulge(state, instruction.Args[0], instruction.Args[1], instruction.Args[2]);
                    break;
                case 13:
                    for (var i = 0; i + 2 < instruction.Args.Count; i += 3)
                        DrawBulge(state, instruction.Args[i], instruction.Args[i + 1], instruction.Args[i + 2]);
                    break;
                case 14:
                    index++;
                    break;
                default:
                    if (instruction.Code > 14) DrawNormalVector(state, instruction.Code);
                    else throw new InvalidDataException($"Unsupported SHX instruction {instruction.Code}.");
                    break;
            }
        }
    }

    private static void DrawNormalVector(RenderState state, int code)
    {
        var length = (code >> 4) & 0xF;
        var direction = code & 0xF;
        var angle = direction * Math.PI / 8.0;
        state.Displace(Math.Cos(angle) * length, Math.Sin(angle) * length);
    }

    private static void DrawOctantArc(RenderState state, int radiusByte, int octantSpec)
    {
        var (startOctant, spanOctants, ccw) = DecodeOctant(octantSpec);
        if (spanOctants == 0) spanOctants = 8;
        var start = startOctant * Math.PI / 4.0;
        var sweep = spanOctants * Math.PI / 4.0 * (ccw ? 1 : -1);
        DrawArcFromCurrent(state, radiusByte * state.VectorLength, start, sweep);
    }

    private static void DrawFractionalArc(RenderState state, IReadOnlyList<int> args)
    {
        var startOffset = args[0];
        var endOffset = args[1] == 0 ? 256 : args[1];
        var radius = ((args[2] << 8) + args[3]) * state.VectorLength;
        var (startOctant, spanOctants, ccw) = DecodeOctant(args[4]);
        if (spanOctants == 0) spanOctants = 8;
        var binaryAngle = (Math.PI / 4.0) / 256.0;
        double start;
        double end;
        if (ccw)
        {
            var endOctant = startOctant + spanOctants - 1;
            start = startOctant * Math.PI / 4.0 + startOffset * binaryAngle;
            end = endOctant * Math.PI / 4.0 + endOffset * binaryAngle;
        }
        else
        {
            var endOctant = startOctant - spanOctants + 1;
            start = startOctant * Math.PI / 4.0 - startOffset * binaryAngle;
            end = endOctant * Math.PI / 4.0 - endOffset * binaryAngle;
        }
        var sweep = NormalizeSweep(end - start, ccw);
        DrawArcFromCurrent(state, radius, start, sweep);
    }

    private static void DrawArcFromCurrent(RenderState state, double radius, double startAngle, double sweep)
    {
        if (!double.IsFinite(radius) || radius <= Epsilon) return;
        var center = new CadPoint(
            state.Position.X - Math.Cos(startAngle) * radius,
            state.Position.Y - Math.Sin(startAngle) * radius);
        var segments = Math.Max(2, (int)Math.Ceiling(Math.Abs(sweep) / MaxArcStep));
        for (var i = 1; i <= segments; i++)
        {
            var angle = startAngle + sweep * i / segments;
            state.MoveTo(new CadPoint(center.X + Math.Cos(angle) * radius, center.Y + Math.Sin(angle) * radius));
        }
    }

    private static void DrawBulge(RenderState state, int dxByte, int dyByte, int bulgeByte)
    {
        var dx = dxByte * state.VectorLength;
        var dy = dyByte * state.VectorLength;
        var end = new CadPoint(state.Position.X + dx, state.Position.Y + dy);
        if (bulgeByte == 0)
        {
            state.MoveTo(end);
            return;
        }

        var chord = Math.Sqrt(dx * dx + dy * dy);
        if (chord <= Epsilon)
        {
            state.MoveWithoutDrawing(end);
            return;
        }

        var bulge = bulgeByte / 127.0;
        var sweep = 4.0 * Math.Atan(bulge);
        var midpoint = new CadPoint((state.Position.X + end.X) * 0.5, (state.Position.Y + end.Y) * 0.5);
        var centerOffset = chord * (1.0 - bulge * bulge) / (4.0 * bulge);
        var ux = dx / chord;
        var uy = dy / chord;
        var center = new CadPoint(midpoint.X - uy * centerOffset, midpoint.Y + ux * centerOffset);
        var radius = Math.Sqrt(Math.Pow(state.Position.X - center.X, 2) + Math.Pow(state.Position.Y - center.Y, 2));
        var start = Math.Atan2(state.Position.Y - center.Y, state.Position.X - center.X);
        var segments = Math.Max(2, (int)Math.Ceiling(Math.Abs(sweep) / MaxArcStep));
        for (var i = 1; i <= segments; i++)
        {
            var angle = start + sweep * i / segments;
            state.MoveTo(new CadPoint(center.X + Math.Cos(angle) * radius, center.Y + Math.Sin(angle) * radius));
        }
        state.Position = end;
    }

    private static (int Start, int Span, bool Ccw) DecodeOctant(int spec)
    {
        var ccw = spec >= 0;
        var value = Math.Abs(spec);
        return ((value >> 4) & 0xF, value & 0xF, ccw);
    }

    private static double NormalizeSweep(double sweep, bool ccw)
    {
        if (ccw)
        {
            while (sweep <= 0) sweep += Math.Tau;
        }
        else
        {
            while (sweep >= 0) sweep -= Math.Tau;
        }
        return sweep;
    }

    private static CadShxFile ReadLegacyShapes(byte[] data)
    {
        var reader = new ByteReader(data, 0x17);
        if (reader.ReadByte() != 0x1A) throw new InvalidDataException("SHX shapes signature terminator is missing.");
        var first = reader.ReadUInt16();
        var last = reader.ReadUInt16();
        var count = reader.ReadUInt16();
        if (count == 0) throw new InvalidDataException("SHX shape index is empty or invalid.");
        var index = new List<(int Number, int Length)>(count);
        for (var i = 0; i < count; i++) index.Add((reader.ReadUInt16(), reader.ReadUInt16()));
        if (index[0].Number != first || index[^1].Number != last) throw new InvalidDataException("SHX shape index range is inconsistent.");

        var symbols = new Dictionary<int, CadShxSymbol>();
        foreach (var entry in index)
        {
            var record = reader.ReadBytes(entry.Length);
            var zero = Array.IndexOf(record, (byte)0);
            if (zero < 0) throw new InvalidDataException($"SHX symbol {entry.Number} has no name terminator.");
            var name = Encoding.Latin1.GetString(record, 0, zero).Trim();
            var instructions = ParseInstructions(record.AsSpan(zero + 1), unicodeSubshape: false);
            symbols[entry.Number] = new CadShxSymbol(entry.Number, name, instructions);
        }
        var header = Encoding.ASCII.GetString(data, 0, Math.Min(0x17, data.Length)).TrimEnd('\0', '\r', '\n', ' ');
        return new CadShxFile(header, IsUnicode: false, IsShapeFile: true, Above: 0, Below: 0, symbols);
    }

    private static CadShxFile ReadUniFont(byte[] data)
    {
        var reader = new ByteReader(data, 0x18);
        if (reader.ReadByte() != 0x1A) throw new InvalidDataException("SHX unifont signature terminator is missing.");
        reader.Skip(6);
        var name = reader.ReadNullTerminatedLatin1();
        var above = reader.ReadByte();
        var below = reader.ReadByte();
        _ = reader.ReadByte();
        _ = reader.ReadByte();
        _ = reader.ReadByte();
        if (reader.Remaining > 0) reader.Skip(1);

        var symbols = new Dictionary<int, CadShxSymbol>();
        while (reader.Remaining >= 4)
        {
            var number = reader.ReadUInt16();
            var byteCount = reader.ReadUInt16();
            if (byteCount == 0 || byteCount > reader.Remaining) break;
            var record = reader.ReadBytes(byteCount);
            var zero = Array.IndexOf(record, (byte)0);
            if (zero < 0) continue;
            var symbolName = Encoding.Latin1.GetString(record, 0, zero).Trim();
            var instructions = ParseInstructions(record.AsSpan(zero + 1), unicodeSubshape: true);
            symbols[number] = new CadShxSymbol(number, symbolName, instructions);
        }
        if (symbols.Count == 0) throw new InvalidDataException("SHX unifont contains no readable glyph records.");
        return new CadShxFile(string.IsNullOrWhiteSpace(name) ? UniFont10 : name, IsUnicode: true, IsShapeFile: false, above, below, symbols);
    }

    private static CadShxFile ReadAsciiShp(byte[] data)
    {
        var text = Encoding.Latin1.GetString(data);
        var records = new List<(string Header, List<string> Lines)>();
        string? header = null;
        var body = new List<string>();
        foreach (var raw in text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n').Split('\n'))
        {
            var line = raw.Split(';', 2)[0].Trim();
            if (line.Length == 0) continue;
            if (line.StartsWith('*'))
            {
                if (header is not null) records.Add((header, body));
                header = line;
                body = [];
            }
            else if (header is not null) body.Add(line);
        }
        if (header is not null) records.Add((header, body));
        if (records.Count == 0) throw new InvalidDataException("SHP source contains no shape definitions.");

        var symbols = new Dictionary<int, CadShxSymbol>();
        var above = 0;
        var below = 0;
        var unicode = false;
        var isShape = true;
        var fileName = "SHP shape file";
        foreach (var record in records)
        {
            var headerParts = record.Header.Split(',', 3, StringSplitOptions.TrimEntries);
            if (headerParts.Length < 2) continue;
            if (headerParts[0].Equals("*UNIFONT", StringComparison.OrdinalIgnoreCase) || headerParts[0] == "*0")
            {
                isShape = false;
                unicode = headerParts[0].Equals("*UNIFONT", StringComparison.OrdinalIgnoreCase);
                fileName = headerParts.Length > 2 ? headerParts[2] : "SHP font";
                var definitionTokens = record.Lines.SelectMany(ParseAsciiIntegers).ToArray();
                if (definitionTokens.Length >= 2)
                {
                    above = Math.Abs(definitionTokens[0]);
                    below = Math.Abs(definitionTokens[1]);
                }
                continue;
            }

            var numberToken = headerParts[0][1..];
            var number = numberToken.StartsWith('0')
                ? int.Parse(numberToken, NumberStyles.HexNumber, CultureInfo.InvariantCulture)
                : int.Parse(numberToken, NumberStyles.Integer, CultureInfo.InvariantCulture);
            var name = headerParts.Length > 2 ? headerParts[2].Trim() : number.ToString(CultureInfo.InvariantCulture);
            var values = record.Lines.SelectMany(ParseAsciiIntegers).ToArray();
            symbols[number] = new CadShxSymbol(number, name, ParseAsciiInstructions(values, unicode));
        }
        if (symbols.Count == 0) throw new InvalidDataException("SHP source contains no drawable shapes.");
        return new CadShxFile(fileName, unicode, isShape, above, below, symbols);
    }

    private static IEnumerable<int> ParseAsciiIntegers(string line)
    {
        foreach (var raw in line.Replace("(", string.Empty, StringComparison.Ordinal).Replace(")", string.Empty, StringComparison.Ordinal).Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            var token = raw.Trim();
            if (token.StartsWith("-0", StringComparison.Ordinal) && token.Length > 2)
                yield return -Convert.ToInt32(token[2..], 16);
            else if (token.StartsWith('0') && token.Length > 1)
                yield return Convert.ToInt32(token, 16);
            else yield return int.Parse(token, CultureInfo.InvariantCulture);
        }
    }

    private static IReadOnlyList<CadShxInstruction> ParseAsciiInstructions(IReadOnlyList<int> values, bool unicodeSubshape)
    {
        var instructions = new List<CadShxInstruction>();
        var i = 0;
        while (i < values.Count && instructions.Count < MaxInstructionsPerShape)
        {
            var code = values[i++];
            if (code == 0) break;
            switch (code)
            {
                case 1 or 2 or 5 or 6 or 14:
                    instructions.Add(new CadShxInstruction(code, []));
                    break;
                case 3 or 4:
                    Ensure(values, i, 1); instructions.Add(new CadShxInstruction(code, [values[i++]])); break;
                case 7:
                    Ensure(values, i, 1); instructions.Add(new CadShxInstruction(code, [values[i++]])); break;
                case 8:
                    Ensure(values, i, 2); instructions.Add(new CadShxInstruction(code, [values[i++], values[i++]])); break;
                case 9:
                {
                    var args = new List<int>();
                    while (true)
                    {
                        Ensure(values, i, 2);
                        var x = values[i++]; var y = values[i++];
                        if (x == 0 && y == 0) break;
                        args.Add(x); args.Add(y);
                    }
                    instructions.Add(new CadShxInstruction(code, args));
                    break;
                }
                case 10:
                    Ensure(values, i, 2); instructions.Add(new CadShxInstruction(code, [values[i++], values[i++]])); break;
                case 11:
                    Ensure(values, i, 5); instructions.Add(new CadShxInstruction(code, [values[i++], values[i++], values[i++], values[i++], values[i++]])); break;
                case 12:
                    Ensure(values, i, 3); instructions.Add(new CadShxInstruction(code, [values[i++], values[i++], values[i++]])); break;
                case 13:
                {
                    var args = new List<int>();
                    while (true)
                    {
                        Ensure(values, i, 2);
                        var x = values[i++]; var y = values[i++];
                        if (x == 0 && y == 0) break;
                        Ensure(values, i, 1);
                        args.Add(x); args.Add(y); args.Add(values[i++]);
                    }
                    instructions.Add(new CadShxInstruction(code, args));
                    break;
                }
                default:
                    if (code < 0 || code > 255) throw new InvalidDataException($"SHP instruction {code} is outside byte range.");
                    instructions.Add(new CadShxInstruction(code, []));
                    break;
            }
        }
        if (instructions.Count >= MaxInstructionsPerShape) throw new InvalidDataException("SHP instruction limit exceeded.");
        return instructions;
    }

    private static IReadOnlyList<CadShxInstruction> ParseInstructions(ReadOnlySpan<byte> bytes, bool unicodeSubshape)
    {
        var instructions = new List<CadShxInstruction>();
        var i = 0;
        while (i < bytes.Length && instructions.Count < MaxInstructionsPerShape)
        {
            var code = bytes[i++];
            if (code == 0) break;
            switch (code)
            {
                case 1 or 2 or 5 or 6 or 14:
                    instructions.Add(new CadShxInstruction(code, []));
                    break;
                case 3 or 4:
                    Ensure(bytes, i, 1);
                    instructions.Add(new CadShxInstruction(code, [bytes[i++]]));
                    break;
                case 7:
                    Ensure(bytes, i, unicodeSubshape ? 2 : 1);
                    var subshape = unicodeSubshape ? bytes[i] | (bytes[i + 1] << 8) : bytes[i];
                    i += unicodeSubshape ? 2 : 1;
                    instructions.Add(new CadShxInstruction(code, [subshape]));
                    break;
                case 8:
                    Ensure(bytes, i, 2);
                    instructions.Add(new CadShxInstruction(code, [ToSigned(bytes[i++]), ToSigned(bytes[i++])]));
                    break;
                case 9:
                {
                    var args = new List<int>();
                    while (true)
                    {
                        Ensure(bytes, i, 2);
                        var x = ToSigned(bytes[i++]);
                        var y = ToSigned(bytes[i++]);
                        if (x == 0 && y == 0) break;
                        args.Add(x); args.Add(y);
                    }
                    instructions.Add(new CadShxInstruction(code, args));
                    break;
                }
                case 10:
                    Ensure(bytes, i, 2);
                    instructions.Add(new CadShxInstruction(code, [bytes[i++], ToOctant(bytes[i++])]));
                    break;
                case 11:
                    Ensure(bytes, i, 5);
                    instructions.Add(new CadShxInstruction(code, [bytes[i++], bytes[i++], bytes[i++], bytes[i++], ToOctant(bytes[i++])]));
                    break;
                case 12:
                    Ensure(bytes, i, 3);
                    instructions.Add(new CadShxInstruction(code, [ToSigned(bytes[i++]), ToSigned(bytes[i++]), ToSigned(bytes[i++])]));
                    break;
                case 13:
                {
                    var args = new List<int>();
                    while (true)
                    {
                        Ensure(bytes, i, 2);
                        var x = ToSigned(bytes[i++]);
                        var y = ToSigned(bytes[i++]);
                        if (x == 0 && y == 0) break;
                        Ensure(bytes, i, 1);
                        args.Add(x); args.Add(y); args.Add(ToSigned(bytes[i++]));
                    }
                    instructions.Add(new CadShxInstruction(code, args));
                    break;
                }
                default:
                    instructions.Add(new CadShxInstruction(code, []));
                    break;
            }
        }
        if (instructions.Count >= MaxInstructionsPerShape) throw new InvalidDataException("SHX instruction limit exceeded.");
        return instructions;
    }

    private static bool LooksLikeAsciiShp(byte[] data)
    {
        var prefix = Encoding.Latin1.GetString(data, 0, Math.Min(data.Length, 128)).TrimStart('\uFEFF', ' ', '\t', '\r', '\n');
        return prefix.StartsWith('*') && !prefix.StartsWith(Shape10, StringComparison.Ordinal) && !prefix.StartsWith(UniFont10, StringComparison.Ordinal);
    }

    private static bool StartsWithAscii(byte[] data, string value)
    {
        var bytes = Encoding.ASCII.GetBytes(value);
        return data.AsSpan().StartsWith(bytes);
    }

    private static void Ensure(ReadOnlySpan<byte> bytes, int offset, int count)
    {
        if (offset < 0 || count < 0 || offset + count > bytes.Length) throw new EndOfStreamException("SHX shape instruction is truncated.");
    }

    private static void Ensure(IReadOnlyList<int> values, int offset, int count)
    {
        if (offset < 0 || count < 0 || offset + count > values.Count) throw new EndOfStreamException("SHP shape instruction is truncated.");
    }

    private static int ToSigned(byte value) => value > 127 ? value - 256 : value;
    private static int ToOctant(byte value) => (value & 0x80) != 0 ? -(value & 0x7F) : value;

    private sealed class RenderState
    {
        private List<CadPoint>? _stroke;
        public CadPoint Position { get; set; }
        public bool PenDown { get; set; } = true;
        public double VectorLength { get; set; } = 1;
        public Stack<CadPoint> PositionStack { get; } = new();
        public List<List<CadPoint>> Strokes { get; } = [];

        public void Displace(double x, double y) => MoveTo(new CadPoint(Position.X + x * VectorLength, Position.Y + y * VectorLength));

        public void MoveTo(CadPoint target)
        {
            if (!PenDown)
            {
                MoveWithoutDrawing(target);
                return;
            }
            _stroke ??= [Position];
            if ((_stroke[^1] - target).Length > Epsilon) _stroke.Add(target);
            Position = target;
        }

        public void MoveWithoutDrawing(CadPoint target)
        {
            FlushStroke();
            Position = target;
        }

        public void FlushStroke()
        {
            if (_stroke is { Count: >= 2 }) Strokes.Add(_stroke);
            _stroke = null;
        }
    }

    private sealed class ByteReader(byte[] data, int offset)
    {
        private int _position = offset;
        public int Remaining => data.Length - _position;
        public byte ReadByte() { EnsureCount(1); return data[_position++]; }
        public ushort ReadUInt16() { EnsureCount(2); var value = (ushort)(data[_position] | data[_position + 1] << 8); _position += 2; return value; }
        public void Skip(int count) { EnsureCount(count); _position += count; }
        public byte[] ReadBytes(int count) { EnsureCount(count); var result = data.AsSpan(_position, count).ToArray(); _position += count; return result; }
        public string ReadNullTerminatedLatin1()
        {
            var start = _position;
            while (_position < data.Length && data[_position] != 0) _position++;
            if (_position >= data.Length) throw new EndOfStreamException("SHX string terminator is missing.");
            var result = Encoding.Latin1.GetString(data, start, _position - start);
            _position++;
            return result;
        }
        private void EnsureCount(int count) { if (count < 0 || _position + count > data.Length) throw new EndOfStreamException("SHX file is truncated."); }
    }
}

public sealed record CadShxFile(
    string Name,
    bool IsUnicode,
    bool IsShapeFile,
    int Above,
    int Below,
    IReadOnlyDictionary<int, CadShxSymbol> Symbols);

public sealed record CadShxSymbol(int Number, string Name, IReadOnlyList<CadShxInstruction> Instructions);
public sealed record CadShxInstruction(int Code, IReadOnlyList<int> Args);

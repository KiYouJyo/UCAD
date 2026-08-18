using System.Buffers.Binary;
using UCAD.Core.Entities;
using UCAD.Core.Geometry;

namespace UCAD.Core.Gis;

public enum CadShapefileShapeType
{
    Point = 1,
    PolyLine = 3,
    Polygon = 5
}

public sealed record CadShapefileExportResult(
    byte[] ShpContent,
    CadShapefileShapeType ShapeType,
    IReadOnlyList<string> Warnings);

public sealed record CadShapefileImportResult(
    IReadOnlyList<ICadEntity> Entities,
    CadShapefileShapeType ShapeType,
    IReadOnlyList<string> Warnings);

/// <summary>
/// Geometry-only ESRI Shapefile (.shp) codec. DBF attributes, SHX indexing and PRJ CRS
/// metadata are deliberately separate sidecars; this codec never pretends those files
/// exist. Export requires one homogeneous shape family per .shp, matching the format.
/// </summary>
public static class CadShapefileGeometryCodec
{
    private const int HeaderBytes = 100;
    private const int FileCode = 9994;
    private const int Version = 1000;

    public static CadShapefileExportResult Export(IEnumerable<ICadEntity> entities)
    {
        ArgumentNullException.ThrowIfNull(entities);
        var snapshot = entities.ToArray();
        if (snapshot.Length == 0) throw new ArgumentException("Shapefile export requires at least one entity.", nameof(entities));
        var shapeType = Classify(snapshot[0]);
        if (snapshot.Any(entity => Classify(entity) != shapeType))
            throw new ArgumentException("One Shapefile can contain only one homogeneous shape type.", nameof(entities));

        var records = snapshot.Select((entity, index) => BuildRecord(entity, shapeType, index + 1)).ToArray();
        var bounds = Union(snapshot.Select(GetBounds));
        var fileBytes = HeaderBytes + records.Sum(record => record.Length);
        if ((fileBytes & 1) != 0) throw new InvalidOperationException("Shapefile length must be an even number of bytes.");

        using var stream = new MemoryStream(fileBytes);
        WriteHeader(stream, shapeType, bounds, fileBytes / 2);
        foreach (var record in records) stream.Write(record);
        return new CadShapefileExportResult(
            stream.ToArray(),
            shapeType,
            ["Geometry-only SHP exported. DBF attributes, SHX index and PRJ CRS sidecars are not included in this foundation export."]);
    }

    public static CadShapefileImportResult Import(ReadOnlySpan<byte> shpContent)
    {
        if (shpContent.Length < HeaderBytes) throw new FormatException("Shapefile is shorter than the 100-byte header.");
        if (ReadInt32BigEndian(shpContent, 0) != FileCode) throw new FormatException("Invalid Shapefile file code.");
        var declaredWords = ReadInt32BigEndian(shpContent, 24);
        if (declaredWords <= 0 || declaredWords * 2 > shpContent.Length)
            throw new FormatException("Shapefile declares an invalid file length.");
        if (ReadInt32LittleEndian(shpContent, 28) != Version) throw new FormatException("Unsupported Shapefile version.");
        var rawShapeType = ReadInt32LittleEndian(shpContent, 32);
        if (!Enum.IsDefined(typeof(CadShapefileShapeType), rawShapeType))
            throw new NotSupportedException($"Shapefile shape type {rawShapeType} is not supported by the UCAD geometry foundation.");
        var shapeType = (CadShapefileShapeType)rawShapeType;

        var entities = new List<ICadEntity>();
        var warnings = new List<string>
        {
            "Geometry-only SHP imported. DBF attributes, SHX index and PRJ CRS sidecars were not read by this foundation codec."
        };
        var offset = HeaderBytes;
        while (offset + 8 <= declaredWords * 2)
        {
            var recordNumber = ReadInt32BigEndian(shpContent, offset);
            var contentWords = ReadInt32BigEndian(shpContent, offset + 4);
            if (contentWords < 2) throw new FormatException($"Shapefile record {recordNumber} has an invalid content length.");
            var contentBytes = checked(contentWords * 2);
            var contentOffset = offset + 8;
            if (contentOffset + contentBytes > declaredWords * 2)
                throw new FormatException($"Shapefile record {recordNumber} extends past the declared file length.");
            var record = shpContent.Slice(contentOffset, contentBytes);
            var recordType = ReadInt32LittleEndian(record, 0);
            if (recordType == 0)
            {
                offset = contentOffset + contentBytes;
                continue;
            }
            if (recordType != rawShapeType)
                throw new FormatException($"Shapefile record {recordNumber} type {recordType} does not match file type {rawShapeType}.");

            switch (shapeType)
            {
                case CadShapefileShapeType.Point:
                    entities.Add(ReadPointRecord(record, recordNumber));
                    break;
                case CadShapefileShapeType.PolyLine:
                    ReadMultipartRecord(record, recordNumber, closed: false, entities, warnings);
                    break;
                case CadShapefileShapeType.Polygon:
                    ReadMultipartRecord(record, recordNumber, closed: true, entities, warnings);
                    break;
            }
            offset = contentOffset + contentBytes;
        }
        return new CadShapefileImportResult(entities.AsReadOnly(), shapeType, warnings.AsReadOnly());
    }

    private static byte[] BuildRecord(ICadEntity entity, CadShapefileShapeType type, int recordNumber)
    {
        byte[] content = type switch
        {
            CadShapefileShapeType.Point => BuildPointContent((PointEntity)entity),
            CadShapefileShapeType.PolyLine => BuildPathContent(entity, type, closed: false),
            CadShapefileShapeType.Polygon => BuildPathContent(entity, type, closed: true),
            _ => throw new NotSupportedException()
        };
        using var stream = new MemoryStream(content.Length + 8);
        WriteInt32BigEndian(stream, recordNumber);
        WriteInt32BigEndian(stream, content.Length / 2);
        stream.Write(content);
        return stream.ToArray();
    }

    private static byte[] BuildPointContent(PointEntity point)
    {
        using var stream = new MemoryStream(20);
        WriteInt32LittleEndian(stream, (int)CadShapefileShapeType.Point);
        WriteDoubleLittleEndian(stream, point.Position.X);
        WriteDoubleLittleEndian(stream, point.Position.Y);
        return stream.ToArray();
    }

    private static byte[] BuildPathContent(ICadEntity entity, CadShapefileShapeType type, bool closed)
    {
        var points = entity switch
        {
            LineEntity line => new[] { line.Start, line.End },
            PolylineEntity polyline => polyline.Points.ToArray(),
            _ => throw new ArgumentException($"Entity {entity.GetType().Name} is not a Shapefile path geometry.", nameof(entity))
        };
        if (closed && (points[0] - points[^1]).Length > 1e-9)
            points = points.Concat([points[0]]).ToArray();
        var bounds = BoundsOf(points);
        using var stream = new MemoryStream();
        WriteInt32LittleEndian(stream, (int)type);
        WriteBounds(stream, bounds);
        WriteInt32LittleEndian(stream, 1);
        WriteInt32LittleEndian(stream, points.Length);
        WriteInt32LittleEndian(stream, 0);
        foreach (var point in points)
        {
            WriteDoubleLittleEndian(stream, point.X);
            WriteDoubleLittleEndian(stream, point.Y);
        }
        return stream.ToArray();
    }

    private static PointEntity ReadPointRecord(ReadOnlySpan<byte> record, int recordNumber)
    {
        if (record.Length < 20) throw new FormatException($"Point record {recordNumber} is truncated.");
        return new PointEntity(new CadPoint(
            ReadDoubleLittleEndian(record, 4),
            ReadDoubleLittleEndian(record, 12)));
    }

    private static void ReadMultipartRecord(
        ReadOnlySpan<byte> record,
        int recordNumber,
        bool closed,
        List<ICadEntity> output,
        List<string> warnings)
    {
        if (record.Length < 44) throw new FormatException($"Path record {recordNumber} is truncated.");
        var partCount = ReadInt32LittleEndian(record, 36);
        var pointCount = ReadInt32LittleEndian(record, 40);
        if (partCount <= 0 || pointCount <= 0) throw new FormatException($"Path record {recordNumber} has invalid part/point counts.");
        var partsOffset = 44;
        var pointsOffset = checked(partsOffset + (partCount * 4));
        var required = checked(pointsOffset + (pointCount * 16));
        if (required > record.Length) throw new FormatException($"Path record {recordNumber} point data is truncated.");

        var starts = new int[partCount + 1];
        for (var index = 0; index < partCount; index++) starts[index] = ReadInt32LittleEndian(record, partsOffset + (index * 4));
        starts[^1] = pointCount;
        for (var index = 0; index < partCount; index++)
        {
            var start = starts[index];
            var end = starts[index + 1];
            if (start < 0 || end <= start || end > pointCount) throw new FormatException($"Path record {recordNumber} has invalid part offsets.");
            var points = new List<CadPoint>(end - start);
            for (var pointIndex = start; pointIndex < end; pointIndex++)
            {
                var byteOffset = pointsOffset + (pointIndex * 16);
                points.Add(new CadPoint(
                    ReadDoubleLittleEndian(record, byteOffset),
                    ReadDoubleLittleEndian(record, byteOffset + 8)));
            }
            if (closed && points.Count > 1 && (points[0] - points[^1]).Length <= 1e-9) points.RemoveAt(points.Count - 1);
            if (closed && points.Count < 3) throw new FormatException($"Polygon record {recordNumber} part {index} has fewer than three distinct points.");
            if (!closed && points.Count < 2) throw new FormatException($"Polyline record {recordNumber} part {index} has fewer than two points.");
            output.Add(points.Count == 2 && !closed
                ? new LineEntity(points[0], points[1])
                : new PolylineEntity(points, closed));
        }
        if (partCount > 1)
        {
            warnings.Add(closed
                ? $"Polygon record {recordNumber} contains {partCount} rings/parts; they were imported as separate closed polylines because UCAD polylines do not encode hole topology."
                : $"Polyline record {recordNumber} contains {partCount} parts; they were imported as separate CAD entities.");
        }
    }

    private static CadShapefileShapeType Classify(ICadEntity entity) => entity switch
    {
        PointEntity => CadShapefileShapeType.Point,
        LineEntity => CadShapefileShapeType.PolyLine,
        PolylineEntity { Closed: false } => CadShapefileShapeType.PolyLine,
        PolylineEntity { Closed: true } => CadShapefileShapeType.Polygon,
        _ => throw new NotSupportedException(
            $"Entity {entity.GetType().Name} cannot be written directly to the geometry-only Shapefile codec. " +
            "Explode/sample it into Point, Line or Polyline geometry first.")
    };

    private static CadRect GetBounds(ICadEntity entity) => entity switch
    {
        PointEntity point => new CadRect(point.Position.X, point.Position.Y, point.Position.X, point.Position.Y),
        LineEntity line => BoundsOf([line.Start, line.End]),
        PolylineEntity polyline => BoundsOf(polyline.Points),
        _ => throw new NotSupportedException()
    };

    private static CadRect BoundsOf(IReadOnlyList<CadPoint> points)
    {
        if (points.Count == 0) throw new ArgumentException("Bounds require at least one point.", nameof(points));
        return new CadRect(
            points.Min(point => point.X),
            points.Min(point => point.Y),
            points.Max(point => point.X),
            points.Max(point => point.Y));
    }

    private static CadRect Union(IEnumerable<CadRect> rectangles)
    {
        var snapshot = rectangles.ToArray();
        if (snapshot.Length == 0) return default;
        return new CadRect(
            snapshot.Min(rectangle => rectangle.Left),
            snapshot.Min(rectangle => rectangle.Bottom),
            snapshot.Max(rectangle => rectangle.Right),
            snapshot.Max(rectangle => rectangle.Top));
    }

    private static void WriteHeader(Stream stream, CadShapefileShapeType type, CadRect bounds, int fileLengthWords)
    {
        WriteInt32BigEndian(stream, FileCode);
        for (var index = 0; index < 5; index++) WriteInt32BigEndian(stream, 0);
        WriteInt32BigEndian(stream, fileLengthWords);
        WriteInt32LittleEndian(stream, Version);
        WriteInt32LittleEndian(stream, (int)type);
        WriteBounds(stream, bounds);
        for (var index = 0; index < 4; index++) WriteDoubleLittleEndian(stream, 0);
        if (stream.Position != HeaderBytes) throw new InvalidOperationException("Shapefile header length is invalid.");
    }

    private static void WriteBounds(Stream stream, CadRect bounds)
    {
        WriteDoubleLittleEndian(stream, bounds.Left);
        WriteDoubleLittleEndian(stream, bounds.Bottom);
        WriteDoubleLittleEndian(stream, bounds.Right);
        WriteDoubleLittleEndian(stream, bounds.Top);
    }

    private static int ReadInt32BigEndian(ReadOnlySpan<byte> data, int offset)
    {
        Ensure(data, offset, 4);
        return BinaryPrimitives.ReadInt32BigEndian(data.Slice(offset, 4));
    }

    private static int ReadInt32LittleEndian(ReadOnlySpan<byte> data, int offset)
    {
        Ensure(data, offset, 4);
        return BinaryPrimitives.ReadInt32LittleEndian(data.Slice(offset, 4));
    }

    private static double ReadDoubleLittleEndian(ReadOnlySpan<byte> data, int offset)
    {
        Ensure(data, offset, 8);
        var bits = BinaryPrimitives.ReadInt64LittleEndian(data.Slice(offset, 8));
        var value = BitConverter.Int64BitsToDouble(bits);
        if (!double.IsFinite(value)) throw new FormatException("Shapefile contains a non-finite coordinate.");
        return value;
    }

    private static void WriteInt32BigEndian(Stream stream, int value)
    {
        Span<byte> buffer = stackalloc byte[4];
        BinaryPrimitives.WriteInt32BigEndian(buffer, value);
        stream.Write(buffer);
    }

    private static void WriteInt32LittleEndian(Stream stream, int value)
    {
        Span<byte> buffer = stackalloc byte[4];
        BinaryPrimitives.WriteInt32LittleEndian(buffer, value);
        stream.Write(buffer);
    }

    private static void WriteDoubleLittleEndian(Stream stream, double value)
    {
        if (!double.IsFinite(value)) throw new ArgumentOutOfRangeException(nameof(value));
        Span<byte> buffer = stackalloc byte[8];
        BinaryPrimitives.WriteInt64LittleEndian(buffer, BitConverter.DoubleToInt64Bits(value));
        stream.Write(buffer);
    }

    private static void Ensure(ReadOnlySpan<byte> data, int offset, int count)
    {
        if (offset < 0 || count < 0 || offset > data.Length - count) throw new FormatException("Shapefile is truncated.");
    }
}

using System.Globalization;
using System.Text;
using UCAD.Core.Entities;
using UCAD.Core.Geometry;

namespace UCAD.Core.Gis;

public sealed record CadCsvPointSchema(
    string X = "X",
    string Y = "Y",
    string Name = "Name",
    string Layer = "Layer")
{
    public CadCsvPointSchema Validate()
    {
        foreach (var value in new[] { X, Y, Name, Layer })
            if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("CSV field names cannot be empty.");
        if (new[] { X, Y, Name, Layer }.Distinct(StringComparer.OrdinalIgnoreCase).Count() != 4)
            throw new ArgumentException("CSV point field names must be distinct.");
        return this;
    }
}

public sealed record CadCsvPointRecord(
    PointEntity Point,
    string? Name,
    string? SuggestedLayerName,
    IReadOnlyDictionary<string, string?> Properties);

public sealed record CadCsvPointImportResult(
    IReadOnlyList<CadCsvPointRecord> Records,
    IReadOnlyList<string> Warnings);

public static class CadCsvPointCodec
{
    public static string Export(
        IEnumerable<CadCsvPointRecord> records,
        CadCsvPointSchema? schema = null,
        char delimiter = ',')
    {
        ArgumentNullException.ThrowIfNull(records);
        schema = (schema ?? new CadCsvPointSchema()).Validate();
        ValidateDelimiter(delimiter);
        var snapshot = records.ToArray();
        var reserved = new HashSet<string>([schema.X, schema.Y, schema.Name, schema.Layer], StringComparer.OrdinalIgnoreCase);
        var extraHeaders = snapshot
            .SelectMany(record => record.Properties.Keys)
            .Where(header => !reserved.Contains(header))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(header => header, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var headers = new[] { schema.X, schema.Y, schema.Name, schema.Layer }.Concat(extraHeaders).ToArray();

        var builder = new StringBuilder();
        AppendRow(builder, headers, delimiter);
        foreach (var record in snapshot)
        {
            ArgumentNullException.ThrowIfNull(record.Point);
            var values = new List<string?>(headers.Length)
            {
                F(record.Point.Position.X),
                F(record.Point.Position.Y),
                record.Name,
                record.SuggestedLayerName
            };
            foreach (var header in extraHeaders)
                values.Add(record.Properties.TryGetValue(header, out var value) ? value : null);
            AppendRow(builder, values, delimiter);
        }
        return builder.ToString();
    }

    public static CadCsvPointImportResult Import(
        string csv,
        CadCsvPointSchema? schema = null,
        char delimiter = ',')
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(csv);
        schema = (schema ?? new CadCsvPointSchema()).Validate();
        ValidateDelimiter(delimiter);
        var rows = Parse(csv, delimiter);
        if (rows.Count == 0) throw new FormatException("CSV contains no rows.");
        var headers = rows[0];
        if (headers.Count == 0) throw new FormatException("CSV header row is empty.");
        var headerLookup = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (var index = 0; index < headers.Count; index++)
        {
            var header = headers[index].Trim();
            if (header.Length == 0) throw new FormatException($"CSV header column {index + 1} is empty.");
            if (!headerLookup.TryAdd(header, index)) throw new FormatException($"Duplicate CSV header '{header}'.");
        }
        if (!headerLookup.TryGetValue(schema.X, out var xIndex)) throw new FormatException($"CSV is missing X field '{schema.X}'.");
        if (!headerLookup.TryGetValue(schema.Y, out var yIndex)) throw new FormatException($"CSV is missing Y field '{schema.Y}'.");
        headerLookup.TryGetValue(schema.Name, out var nameIndex);
        var hasName = headerLookup.ContainsKey(schema.Name);
        headerLookup.TryGetValue(schema.Layer, out var layerIndex);
        var hasLayer = headerLookup.ContainsKey(schema.Layer);
        var reserved = new HashSet<string>([schema.X, schema.Y, schema.Name, schema.Layer], StringComparer.OrdinalIgnoreCase);

        var records = new List<CadCsvPointRecord>();
        var warnings = new List<string>();
        for (var rowIndex = 1; rowIndex < rows.Count; rowIndex++)
        {
            var row = rows[rowIndex];
            if (row.All(string.IsNullOrWhiteSpace)) continue;
            try
            {
                var x = ParseCoordinate(Cell(row, xIndex), schema.X);
                var y = ParseCoordinate(Cell(row, yIndex), schema.Y);
                var name = hasName ? NullIfWhiteSpace(Cell(row, nameIndex)) : null;
                var layer = hasLayer ? NullIfWhiteSpace(Cell(row, layerIndex)) : null;
                var properties = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
                for (var column = 0; column < headers.Count; column++)
                {
                    var header = headers[column].Trim();
                    if (reserved.Contains(header)) continue;
                    properties[header] = NullIfWhiteSpace(Cell(row, column));
                }
                records.Add(new CadCsvPointRecord(
                    new PointEntity(new CadPoint(x, y)),
                    name,
                    layer,
                    properties));
            }
            catch (FormatException ex)
            {
                warnings.Add($"CSV row {rowIndex + 1} was skipped: {ex.Message}");
            }
        }
        return new CadCsvPointImportResult(records.AsReadOnly(), warnings.AsReadOnly());
    }

    private static IReadOnlyList<IReadOnlyList<string>> Parse(string csv, char delimiter)
    {
        var rows = new List<IReadOnlyList<string>>();
        var row = new List<string>();
        var field = new StringBuilder();
        var quoted = false;
        for (var index = 0; index < csv.Length; index++)
        {
            var character = csv[index];
            if (quoted)
            {
                if (character == '"')
                {
                    if (index + 1 < csv.Length && csv[index + 1] == '"')
                    {
                        field.Append('"');
                        index++;
                    }
                    else
                    {
                        quoted = false;
                    }
                }
                else field.Append(character);
                continue;
            }

            if (character == '"')
            {
                if (field.Length != 0) throw new FormatException("A quoted CSV field must begin with a quote.");
                quoted = true;
            }
            else if (character == delimiter)
            {
                row.Add(field.ToString());
                field.Clear();
            }
            else if (character is '\r' or '\n')
            {
                if (character == '\r' && index + 1 < csv.Length && csv[index + 1] == '\n') index++;
                row.Add(field.ToString());
                field.Clear();
                rows.Add(row.AsReadOnly());
                row = [];
            }
            else field.Append(character);
        }
        if (quoted) throw new FormatException("CSV ends inside a quoted field.");
        if (field.Length > 0 || row.Count > 0)
        {
            row.Add(field.ToString());
            rows.Add(row.AsReadOnly());
        }
        return rows;
    }

    private static string Cell(IReadOnlyList<string> row, int index) => index < row.Count ? row[index] : string.Empty;

    private static double ParseCoordinate(string value, string field)
    {
        if (!double.TryParse(value.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var coordinate) || !double.IsFinite(coordinate))
            throw new FormatException($"Field '{field}' requires a finite invariant numeric coordinate, got '{value}'.");
        return coordinate;
    }

    private static string? NullIfWhiteSpace(string value) => string.IsNullOrWhiteSpace(value) ? null : value;

    private static void AppendRow(StringBuilder builder, IReadOnlyList<string?> values, char delimiter)
    {
        for (var index = 0; index < values.Count; index++)
        {
            if (index > 0) builder.Append(delimiter);
            builder.Append(Escape(values[index] ?? string.Empty, delimiter));
        }
        builder.Append("\r\n");
    }

    private static string Escape(string value, char delimiter)
    {
        var quote = value.Contains(delimiter) || value.Contains('"') || value.Contains('\r') || value.Contains('\n');
        if (!quote) return value;
        return '"' + value.Replace("\"", "\"\"", StringComparison.Ordinal) + '"';
    }

    private static string F(double value) => value.ToString("0.###########", CultureInfo.InvariantCulture);

    private static void ValidateDelimiter(char delimiter)
    {
        if (delimiter is '\r' or '\n' or '"') throw new ArgumentOutOfRangeException(nameof(delimiter));
    }
}

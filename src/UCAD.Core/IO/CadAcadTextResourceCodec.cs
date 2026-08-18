using System.Globalization;
using System.Text;

namespace UCAD.Core.IO;

/// <summary>
/// Text-based AutoCAD support-file codecs. These codecs deliberately parse resource data
/// without executing it. PAT and LIN resources can therefore be migrated safely, while
/// PGP imports only command alias records (not external process definitions).
/// </summary>
public static class CadAcadTextResourceCodec
{
    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

    public static IReadOnlyList<CadAcadHatchPatternDefinition> ParsePat(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var result = new List<CadAcadHatchPatternDefinition>();
        string? currentName = null;
        string currentDescription = string.Empty;
        var currentLines = new List<CadAcadHatchPatternLine>();

        void Flush()
        {
            if (currentName is null) return;
            result.Add(new CadAcadHatchPatternDefinition(currentName, currentDescription, currentLines.ToArray()));
            currentName = null;
            currentDescription = string.Empty;
            currentLines = new List<CadAcadHatchPatternLine>();
        }

        foreach (var rawLine in EnumerateLines(text))
        {
            var line = rawLine.Trim();
            if (line.Length == 0 || line.StartsWith(';')) continue;
            if (line.StartsWith('*'))
            {
                Flush();
                var header = line[1..];
                var comma = header.IndexOf(',');
                currentName = (comma < 0 ? header : header[..comma]).Trim();
                currentDescription = comma < 0 ? string.Empty : header[(comma + 1)..].Trim();
                if (currentName.Length == 0) throw new FormatException("PAT pattern name cannot be empty.");
                continue;
            }

            if (currentName is null)
                throw new FormatException($"PAT definition line '{line}' appears before a *pattern header.");

            var tokens = line.Split(',', StringSplitOptions.TrimEntries);
            if (tokens.Length < 5)
                throw new FormatException($"PAT pattern '{currentName}' requires at least five numeric values per definition line.");

            var dashes = new double[Math.Max(0, tokens.Length - 5)];
            for (var i = 5; i < tokens.Length; i++) dashes[i - 5] = ParseFiniteDouble(tokens[i], $"PAT {currentName}");
            currentLines.Add(new CadAcadHatchPatternLine(
                ParseFiniteDouble(tokens[0], $"PAT {currentName}"),
                ParseFiniteDouble(tokens[1], $"PAT {currentName}"),
                ParseFiniteDouble(tokens[2], $"PAT {currentName}"),
                ParseFiniteDouble(tokens[3], $"PAT {currentName}"),
                ParseFiniteDouble(tokens[4], $"PAT {currentName}"),
                dashes));
        }

        Flush();
        return result;
    }

    public static string WritePat(IEnumerable<CadAcadHatchPatternDefinition> patterns)
    {
        ArgumentNullException.ThrowIfNull(patterns);
        var sb = new StringBuilder();
        foreach (var pattern in patterns)
        {
            ValidateName(pattern.Name, "PAT pattern");
            sb.Append('*').Append(pattern.Name);
            if (!string.IsNullOrWhiteSpace(pattern.Description)) sb.Append(',').Append(pattern.Description.Trim());
            sb.AppendLine();
            foreach (var line in pattern.Lines)
            {
                AppendNumber(sb, line.AngleDegrees);
                sb.Append(','); AppendNumber(sb, line.BaseX);
                sb.Append(','); AppendNumber(sb, line.BaseY);
                sb.Append(','); AppendNumber(sb, line.OffsetX);
                sb.Append(','); AppendNumber(sb, line.OffsetY);
                foreach (var dash in line.DashLengths)
                {
                    sb.Append(',');
                    AppendNumber(sb, dash);
                }
                sb.AppendLine();
            }
        }
        return sb.ToString();
    }

    public static IReadOnlyList<CadAcadLinetypeDefinition> ParseLin(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var result = new List<CadAcadLinetypeDefinition>();
        string? name = null;
        string description = string.Empty;
        var body = new List<string>();

        void Flush()
        {
            if (name is null) return;
            if (body.Count == 0) throw new FormatException($"LIN linetype '{name}' has no definition line.");
            result.Add(new CadAcadLinetypeDefinition(name, description, string.Join(Environment.NewLine, body)));
            name = null;
            description = string.Empty;
            body = new List<string>();
        }

        foreach (var rawLine in EnumerateLines(text))
        {
            var line = rawLine.Trim();
            if (line.Length == 0 || line.StartsWith(';')) continue;
            if (line.StartsWith('*'))
            {
                Flush();
                var header = line[1..];
                var comma = header.IndexOf(',');
                name = (comma < 0 ? header : header[..comma]).Trim();
                description = comma < 0 ? string.Empty : header[(comma + 1)..].Trim();
                ValidateName(name, "LIN linetype");
                continue;
            }

            if (name is null)
                throw new FormatException($"LIN definition line '{line}' appears before a *linetype header.");
            body.Add(line);
        }

        Flush();
        return result;
    }

    public static string WriteLin(IEnumerable<CadAcadLinetypeDefinition> linetypes)
    {
        ArgumentNullException.ThrowIfNull(linetypes);
        var sb = new StringBuilder();
        foreach (var linetype in linetypes)
        {
            ValidateName(linetype.Name, "LIN linetype");
            if (string.IsNullOrWhiteSpace(linetype.Definition))
                throw new ArgumentException($"LIN linetype '{linetype.Name}' has no definition.", nameof(linetypes));
            sb.Append('*').Append(linetype.Name);
            if (!string.IsNullOrWhiteSpace(linetype.Description)) sb.Append(',').Append(linetype.Description.Trim());
            sb.AppendLine();
            foreach (var definitionLine in EnumerateLines(linetype.Definition))
            {
                if (!string.IsNullOrWhiteSpace(definitionLine)) sb.AppendLine(definitionLine.Trim());
            }
        }
        return sb.ToString();
    }

    public static IReadOnlyList<CadAcadCommandAlias> ParsePgpAliases(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var result = new List<CadAcadCommandAlias>();
        foreach (var rawLine in EnumerateLines(text))
        {
            var line = StripInlineComment(rawLine).Trim();
            if (line.Length == 0) continue;
            var comma = line.IndexOf(',');
            if (comma <= 0) continue;
            var alias = line[..comma].Trim();
            var target = line[(comma + 1)..].Trim();

            // PGP also contains external-command records. Only the canonical alias syntax
            // ALIAS, *COMMAND is accepted so importing the file never launches a process.
            if (!target.StartsWith('*')) continue;
            target = target[1..].Trim();
            if (alias.Length == 0 || target.Length == 0) continue;
            result.Add(new CadAcadCommandAlias(alias, target));
        }
        return result;
    }

    public static string WritePgpAliases(IEnumerable<CadAcadCommandAlias> aliases)
    {
        ArgumentNullException.ThrowIfNull(aliases);
        var sb = new StringBuilder();
        foreach (var alias in aliases)
        {
            ValidateName(alias.Alias, "PGP alias");
            ValidateName(alias.Command, "PGP command");
            sb.Append(alias.Alias.Trim()).Append(", *").AppendLine(alias.Command.Trim());
        }
        return sb.ToString();
    }

    private static IEnumerable<string> EnumerateLines(string text)
    {
        using var reader = new StringReader(text);
        while (reader.ReadLine() is { } line) yield return line;
    }

    private static string StripInlineComment(string line)
    {
        var index = line.IndexOf(';');
        return index < 0 ? line : line[..index];
    }

    private static double ParseFiniteDouble(string token, string context)
    {
        if (!double.TryParse(token, NumberStyles.Float, Invariant, out var value) || !double.IsFinite(value))
            throw new FormatException($"{context} contains invalid numeric value '{token}'.");
        return value;
    }

    private static void AppendNumber(StringBuilder sb, double value)
    {
        if (!double.IsFinite(value)) throw new ArgumentOutOfRangeException(nameof(value), "Resource values must be finite.");
        sb.Append(value.ToString("0.###############", Invariant));
    }

    private static void ValidateName(string? value, string kind)
    {
        if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException($"{kind} name cannot be empty.");
        if (value.Contains('\r') || value.Contains('\n')) throw new ArgumentException($"{kind} name cannot contain line breaks.");
    }
}

public sealed record CadAcadHatchPatternDefinition(
    string Name,
    string Description,
    IReadOnlyList<CadAcadHatchPatternLine> Lines);

public sealed record CadAcadHatchPatternLine(
    double AngleDegrees,
    double BaseX,
    double BaseY,
    double OffsetX,
    double OffsetY,
    IReadOnlyList<double> DashLengths);

public sealed record CadAcadLinetypeDefinition(string Name, string Description, string Definition);

public sealed record CadAcadCommandAlias(string Alias, string Command);

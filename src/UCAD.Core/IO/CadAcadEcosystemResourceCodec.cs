using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace UCAD.Core.IO;

/// <summary>
/// Bounded, non-executing migration adapters for AutoCAD support/customization resources.
/// These APIs intentionally separate lossless byte preservation from semantic interpretation:
/// proprietary/compiled resources can be inventoried and carried without ever being executed.
/// </summary>
public static class CadAcadEcosystemResourceCodec
{
    private static readonly UTF8Encoding Utf8NoBom = new(false);
    private static readonly Regex LispCommand = new(
        @"\(\s*command(?:-s)?\s+\"(?<name>[^\"]+)\"",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly HashSet<string> LosslessBinaryResources = new(StringComparer.OrdinalIgnoreCase)
    {
        ".ctb", ".stb", ".pc3", ".pmp", ".psf", ".pss", ".shx", ".mnc", ".sld", ".slb",
        ".fas", ".vlx", ".dvb"
    };

    private static readonly HashSet<string> TextMigrationResources = new(StringComparer.OrdinalIgnoreCase)
    {
        ".fmp", ".unt", ".cfg", ".arg", ".rx", ".dcl", ".cui", ".mnu", ".mns", ".atc", ".dsd", ".bp3"
    };

    public static CadAcadOpaqueResource ImportLosslessBinary(ReadOnlyMemory<byte> content, string extension)
    {
        var normalized = NormalizeExtension(extension);
        if (!LosslessBinaryResources.Contains(normalized))
            throw new NotSupportedException($"'{normalized}' is not a registered lossless binary migration resource.");
        if (content.IsEmpty) throw new ArgumentException("Resource content cannot be empty.", nameof(content));

        var bytes = content.ToArray();
        var metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["length"] = bytes.Length.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["sha256"] = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant(),
            ["kind"] = normalized switch
            {
                ".ctb" => "color-dependent plot style",
                ".stb" => "named plot style",
                ".pc3" => "plotter configuration",
                ".pmp" => "plot model parameter",
                ".shx" => "compiled shape/font",
                ".fas" => "compiled AutoLISP",
                ".vlx" => "Visual LISP application",
                ".dvb" => "VBA project",
                _ => "AutoCAD binary support resource"
            }
        };

        var warnings = new List<string>();
        if (normalized == ".shx")
        {
            var header = ExtractPrintablePrefix(bytes, 96);
            if (!string.IsNullOrWhiteSpace(header)) metadata["header"] = header;
            warnings.Add("SHX is preserved and inventoried only; UCAD uses its normal text fallback unless a compatible glyph mapper is available.");
        }
        if (normalized is ".fas" or ".vlx" or ".dvb")
            warnings.Add($"{normalized.ToUpperInvariant()} is preserved for migration inventory only and is never executed by UCAD.");
        if (normalized is ".ctb" or ".stb")
            warnings.Add("Plot-style bytes are preserved exactly; semantic CTB/STB table editing is not claimed by this adapter.");
        if (normalized is ".pc3" or ".pmp")
            warnings.Add("Plot configuration bytes are preserved exactly; device-driver execution is not performed by UCAD.");

        return new CadAcadOpaqueResource(normalized, bytes, metadata, warnings);
    }

    public static byte[] ExportLosslessBinary(CadAcadOpaqueResource resource)
    {
        ArgumentNullException.ThrowIfNull(resource);
        if (!LosslessBinaryResources.Contains(resource.Extension))
            throw new NotSupportedException($"'{resource.Extension}' is not a registered lossless binary migration resource.");
        var hash = Convert.ToHexString(SHA256.HashData(resource.Content)).ToLowerInvariant();
        if (resource.Metadata.TryGetValue("sha256", out var expected) && !string.Equals(hash, expected, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Opaque AutoCAD resource integrity check failed.");
        return resource.Content.ToArray();
    }

    public static CadAcadTextMigrationResource ImportTextResource(string text, string extension)
    {
        ArgumentNullException.ThrowIfNull(text);
        var normalized = NormalizeExtension(extension);
        if (!TextMigrationResources.Contains(normalized))
            throw new NotSupportedException($"'{normalized}' is not a registered text migration resource.");

        var sections = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
        var current = "General";
        sections[current] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var rawLines = NormalizeLines(text);
        foreach (var raw in rawLines)
        {
            var line = raw.Trim();
            if (line.Length == 0 || line.StartsWith(';') || line.StartsWith('#')) continue;
            if (line.StartsWith('[') && line.EndsWith(']') && line.Length > 2)
            {
                current = line[1..^1].Trim();
                if (!sections.ContainsKey(current)) sections[current] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                continue;
            }

            var separator = line.IndexOf('=');
            if (separator <= 0) separator = line.IndexOf(':');
            if (separator <= 0)
            {
                sections[current][$"line-{sections[current].Count + 1}"] = line;
                continue;
            }
            var key = Unquote(line[..separator].Trim());
            var value = Unquote(line[(separator + 1)..].Trim());
            if (key.Length > 0) sections[current][key] = value;
        }

        var warnings = new List<string>();
        if (normalized == ".dcl") warnings.Add("DCL source is parsed for migration inventory only; dialogs are not executed by UCAD.");
        if (normalized is ".cui" or ".mnu" or ".mns" or ".atc") warnings.Add("Customization source is imported as migration metadata; macros are not auto-executed.");
        return new CadAcadTextMigrationResource(normalized, text, sections, warnings);
    }

    public static string ExportTextResource(CadAcadTextMigrationResource resource)
    {
        ArgumentNullException.ThrowIfNull(resource);
        if (!TextMigrationResources.Contains(resource.Extension))
            throw new NotSupportedException($"'{resource.Extension}' is not a registered text migration resource.");
        // Preserve the exact source text unless callers explicitly build a new resource model.
        return resource.OriginalText;
    }

    public static CadAcadCuixMigration ImportCuix(ReadOnlyMemory<byte> content)
    {
        if (content.IsEmpty) throw new ArgumentException("CUIX content cannot be empty.", nameof(content));
        using var stream = new MemoryStream(content.ToArray(), writable: false);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: false);
        var entries = archive.Entries.Select(entry => entry.FullName).OrderBy(name => name, StringComparer.OrdinalIgnoreCase).ToArray();
        var commands = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var warnings = new List<string>();

        foreach (var entry in archive.Entries.Where(entry => entry.FullName.EndsWith(".xml", StringComparison.OrdinalIgnoreCase)))
        {
            try
            {
                using var entryStream = entry.Open();
                var document = XDocument.Load(entryStream, LoadOptions.None);
                foreach (var attribute in document.Descendants().Attributes())
                {
                    var local = attribute.Name.LocalName;
                    if (local.Contains("Command", StringComparison.OrdinalIgnoreCase) ||
                        local.Contains("Macro", StringComparison.OrdinalIgnoreCase) ||
                        local.Equals("Name", StringComparison.OrdinalIgnoreCase) ||
                        local.Contains("ElementID", StringComparison.OrdinalIgnoreCase))
                    {
                        var value = attribute.Value.Trim();
                        if (value.Length > 0 && value.Length <= 512) commands.Add(value);
                    }
                }
            }
            catch (Exception ex) when (ex is InvalidDataException or System.Xml.XmlException)
            {
                warnings.Add($"CUIX XML entry '{entry.FullName}' could not be inspected: {ex.Message}");
            }
        }

        warnings.Add("CUIX migration inventories XML commands/UI metadata only; embedded binaries and macros are never executed automatically.");
        return new CadAcadCuixMigration(content.ToArray(), entries, commands.OrderBy(value => value, StringComparer.OrdinalIgnoreCase).ToArray(), warnings);
    }

    public static byte[] ExportCuix(CadAcadCuixMigration migration)
    {
        ArgumentNullException.ThrowIfNull(migration);
        return migration.OriginalContent.ToArray();
    }

    public static CadAcadScript ParseScript(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var statements = new List<CadAcadScriptStatement>();
        foreach (var raw in NormalizeLines(text))
        {
            var line = raw.Trim();
            if (line.Length == 0 || line.StartsWith(';')) continue;
            var tokens = TokenizeScriptLine(line);
            if (tokens.Count == 0) continue;
            statements.Add(new CadAcadScriptStatement(tokens[0], tokens.Skip(1).ToArray(), raw));
        }
        return new CadAcadScript(statements);
    }

    public static CadAcadLispCompatibilityReport AnalyzeLispSource(string source, string extension = ".lsp")
    {
        ArgumentNullException.ThrowIfNull(source);
        var normalized = NormalizeExtension(extension);
        if (normalized is not ".lsp" and not ".mnl")
            throw new NotSupportedException("Source-level AutoLISP analysis is limited to .lsp and .mnl files.");

        var commands = LispCommand.Matches(source)
            .Select(match => match.Groups["name"].Value.Trim())
            .Where(value => value.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var definedFunctions = Regex.Matches(source, @"\(\s*defun\s+(?<name>[^\s()]+)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)
            .Select(match => match.Groups["name"].Value)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return new CadAcadLispCompatibilityReport(
            normalized,
            definedFunctions,
            commands,
            ["UCAD analyzes AutoLISP source for migration only; it does not evaluate Lisp expressions or load arbitrary code."]);
    }

    public static CadAcadShxFallback InspectShxForFallback(ReadOnlyMemory<byte> content, string fallbackFont = "Segoe UI")
    {
        var resource = ImportLosslessBinary(content, ".shx");
        if (string.IsNullOrWhiteSpace(fallbackFont)) fallbackFont = "Segoe UI";
        var nameHint = resource.Metadata.TryGetValue("header", out var header) ? header : "SHX resource";
        return new CadAcadShxFallback(nameHint, fallbackFont.Trim(), resource);
    }

    private static IReadOnlyList<string> TokenizeScriptLine(string line)
    {
        var tokens = new List<string>();
        var current = new StringBuilder();
        var quoted = false;
        for (var i = 0; i < line.Length; i++)
        {
            var ch = line[i];
            if (ch == '"')
            {
                quoted = !quoted;
                continue;
            }
            if (!quoted && char.IsWhiteSpace(ch))
            {
                if (current.Length > 0) { tokens.Add(current.ToString()); current.Clear(); }
                continue;
            }
            current.Append(ch);
        }
        if (current.Length > 0) tokens.Add(current.ToString());
        return tokens;
    }

    private static string[] NormalizeLines(string text) => text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n').Split('\n');

    private static string Unquote(string value) => value.Length >= 2 && value[0] == '"' && value[^1] == '"' ? value[1..^1] : value;

    private static string ExtractPrintablePrefix(byte[] content, int length)
    {
        var take = Math.Min(length, content.Length);
        var builder = new StringBuilder(take);
        for (var i = 0; i < take; i++)
        {
            var ch = (char)content[i];
            if (ch is >= ' ' and <= '~') builder.Append(ch);
            else if (builder.Length > 0) break;
        }
        return builder.ToString().Trim();
    }

    private static string NormalizeExtension(string extension)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(extension);
        return extension.StartsWith('.') ? extension.ToLowerInvariant() : "." + extension.ToLowerInvariant();
    }
}

public sealed record CadAcadOpaqueResource(
    string Extension,
    byte[] Content,
    IReadOnlyDictionary<string, string> Metadata,
    IReadOnlyList<string> Warnings);

public sealed record CadAcadTextMigrationResource(
    string Extension,
    string OriginalText,
    IReadOnlyDictionary<string, Dictionary<string, string>> Sections,
    IReadOnlyList<string> Warnings);

public sealed record CadAcadCuixMigration(
    byte[] OriginalContent,
    IReadOnlyList<string> Entries,
    IReadOnlyList<string> CommandMetadata,
    IReadOnlyList<string> Warnings);

public sealed record CadAcadScriptStatement(string Command, IReadOnlyList<string> Arguments, string SourceLine);
public sealed record CadAcadScript(IReadOnlyList<CadAcadScriptStatement> Statements);
public sealed record CadAcadLispCompatibilityReport(
    string Extension,
    IReadOnlyList<string> DefinedFunctions,
    IReadOnlyList<string> CommandInvocations,
    IReadOnlyList<string> Warnings);
public sealed record CadAcadShxFallback(string ResourceNameHint, string FallbackFontFamily, CadAcadOpaqueResource Resource);

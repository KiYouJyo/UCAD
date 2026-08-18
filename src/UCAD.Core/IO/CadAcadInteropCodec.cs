using System.Text;
using ACadSharp;
using ACadSharp.IO;
using AcadDocument = ACadSharp.CadDocument;
using UcadDocument = UCAD.Core.CadDocument;

namespace UCAD.Core.IO;

/// <summary>
/// AutoCAD transport bridge for formats whose container is not implemented natively by UCAD.
/// ACadSharp owns DWG/binary-DXF parsing and writing; UCAD's DXF codec remains the semantic
/// boundary into the UCAD document model so all interoperability paths share one entity mapping.
/// </summary>
public static class CadAcadInteropCodec
{
    private static readonly UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);

    static CadAcadInteropCodec()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    public static CadAcadImportResult ImportDwg(ReadOnlyMemory<byte> content, string sourceExtension = ".dwg")
    {
        if (content.IsEmpty) throw new ArgumentException("DWG content cannot be empty.", nameof(content));
        var warnings = new List<string>();

        using var input = new MemoryStream(content.ToArray(), writable: false);
        using var reader = new DwgReader(input);
        reader.OnNotification += (_, args) => AddNotification(warnings, "DWG read", args);
        var acadDocument = reader.Read();

        var bridgeText = WriteAsciiDxfBridge(acadDocument, warnings);
        var imported = CadDxfCodec.Import(bridgeText);
        AppendWarnings(warnings, "UCAD DXF bridge", imported.Warnings);
        imported.Document.ResetHistory();

        return new CadAcadImportResult(
            imported.Document,
            warnings,
            NormalizeExtension(sourceExtension),
            acadDocument.Header.Version.ToString());
    }

    public static CadAcadImportResult ImportDxf(ReadOnlyMemory<byte> content, string sourceExtension = ".dxf")
    {
        if (content.IsEmpty) throw new ArgumentException("DXF content cannot be empty.", nameof(content));
        var warnings = new List<string>();

        using var input = new MemoryStream(content.ToArray(), writable: false);
        using var reader = new DxfReader(input);
        reader.OnNotification += (_, args) => AddNotification(warnings, "DXF read", args);
        var acadDocument = reader.Read();

        var bridgeText = WriteAsciiDxfBridge(acadDocument, warnings);
        var imported = CadDxfCodec.Import(bridgeText);
        AppendWarnings(warnings, "UCAD DXF bridge", imported.Warnings);
        imported.Document.ResetHistory();

        return new CadAcadImportResult(
            imported.Document,
            warnings,
            NormalizeExtension(sourceExtension),
            acadDocument.Header.Version.ToString());
    }

    public static CadAcadBinaryExportResult ExportDwg(UcadDocument document, string targetExtension = ".dwg")
    {
        ArgumentNullException.ThrowIfNull(document);
        var extension = NormalizeExtension(targetExtension);
        if (extension is not ".dwg" and not ".dwt")
            throw new NotSupportedException($"UCAD does not export DWG transport bytes as '{extension}'.");

        var warnings = new List<string>();
        var acadDocument = BuildAcadDocument(document, warnings);

        using var output = new MemoryStream();
        using (var writer = new DwgWriter(output, acadDocument))
        {
            writer.OnNotification += (_, args) => AddNotification(warnings, "DWG write", args);
            writer.Write();
        }

        return new CadAcadBinaryExportResult(
            output.ToArray(),
            warnings,
            extension,
            acadDocument.Header.Version.ToString());
    }

    public static CadAcadBinaryExportResult ExportBinaryDxf(UcadDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        var warnings = new List<string>();
        var acadDocument = BuildAcadDocument(document, warnings);

        using var output = new MemoryStream();
        using (var writer = new DxfWriter(output, acadDocument, binary: true))
        {
            writer.OnNotification += (_, args) => AddNotification(warnings, "binary DXF write", args);
            writer.Write();
        }

        return new CadAcadBinaryExportResult(
            output.ToArray(),
            warnings,
            ".dxf",
            acadDocument.Header.Version.ToString());
    }

    private static AcadDocument BuildAcadDocument(UcadDocument document, List<string> warnings)
    {
        var dxf = CadDxfCodec.Export(document);
        AppendWarnings(warnings, "UCAD DXF export", dxf.Warnings);

        using var input = new MemoryStream(Utf8NoBom.GetBytes(dxf.Content), writable: false);
        using var reader = new DxfReader(input);
        reader.OnNotification += (_, args) => AddNotification(warnings, "DXF bridge read", args);
        return reader.Read();
    }

    private static string WriteAsciiDxfBridge(AcadDocument acadDocument, List<string> warnings)
    {
        using var output = new MemoryStream();
        using (var writer = new DxfWriter(output, acadDocument, binary: false))
        {
            writer.OnNotification += (_, args) => AddNotification(warnings, "DXF bridge write", args);
            writer.Write();
        }

        var encoding = GetDxfTextEncoding(acadDocument, warnings);
        return encoding.GetString(output.ToArray());
    }

    private static Encoding GetDxfTextEncoding(AcadDocument document, List<string> warnings)
    {
        if (document.Header.Version >= ACadVersion.AC1021) return Utf8NoBom;

        try
        {
            return Encoding.GetEncoding(document.Header.CodePage);
        }
        catch (ArgumentException ex)
        {
            warnings.Add($"DXF bridge: code page {document.Header.CodePage} is unavailable; UTF-8 fallback was used. {ex.Message}");
            return Utf8NoBom;
        }
    }

    private static void AddNotification(List<string> warnings, string phase, NotificationEventArgs args)
    {
        if (string.IsNullOrWhiteSpace(args.Message)) return;
        var message = $"{phase}: {args.Message}";
        if (!warnings.Contains(message, StringComparer.Ordinal)) warnings.Add(message);
    }

    private static void AppendWarnings(List<string> destination, string phase, IEnumerable<string> warnings)
    {
        foreach (var warning in warnings)
        {
            var message = $"{phase}: {warning}";
            if (!destination.Contains(message, StringComparer.Ordinal)) destination.Add(message);
        }
    }

    private static string NormalizeExtension(string extension)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(extension);
        return extension.StartsWith('.') ? extension.ToLowerInvariant() : "." + extension.ToLowerInvariant();
    }
}

public sealed record CadAcadImportResult(
    UcadDocument Document,
    IReadOnlyList<string> Warnings,
    string SourceExtension,
    string SourceCadVersion)
{
    public bool HasWarnings => Warnings.Count > 0;
}

public sealed record CadAcadBinaryExportResult(
    byte[] Content,
    IReadOnlyList<string> Warnings,
    string TargetExtension,
    string TargetCadVersion)
{
    public bool HasWarnings => Warnings.Count > 0;
}

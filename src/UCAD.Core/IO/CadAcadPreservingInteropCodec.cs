namespace UCAD.Core.IO;

/// <summary>
/// Safety wrapper around the editable AutoCAD semantic bridge. DWG-compatible and DXF
/// imports retain an immutable copy of the original container. Untouched same-format
/// export can reuse those exact bytes; edited output is rebuilt from UCAD semantics with
/// explicit warnings when source-only custom/proxy data cannot be merged back safely.
/// </summary>
public static class CadAcadPreservingInteropCodec
{
    private const string PreservationReason =
        "Original AutoCAD container retained as an opaque recovery source for ObjectARX/proxy/custom data outside the editable UCAD semantic model.";

    public static CadAcadImportResult ImportDwg(ReadOnlyMemory<byte> content, string sourceExtension = ".dwg")
    {
        var imported = CadAcadInteropCodec.ImportDwg(content, sourceExtension);
        AttachSource(imported, content);
        var actionableWarnings = CadAcadInteropDiagnostics.KeepActionableWarnings(imported.Warnings);
        return imported with { Warnings = actionableWarnings };
    }

    public static CadAcadImportResult ImportDxf(ReadOnlyMemory<byte> content, string sourceExtension = ".dxf")
    {
        var imported = CadAcadInteropCodec.ImportDxf(content, sourceExtension);
        imported.Document.AttachAutoCadSourceEnvelope(new CadAutoCadSourceEnvelope(
            content,
            imported.SourceExtension,
            imported.SourceCadVersion,
            preservationReasons:
            [
                "Original DXF container retained exactly so proxy/custom OBJECTS, dictionaries, layout metadata, application XDATA, and unknown group-code payloads remain recoverable even when they are outside UCAD's editable 2D model."
            ]));
        return imported;
    }

    public static CadAcadBinaryExportResult ExportDwg(CadDocument document, string targetExtension = ".dwg")
    {
        ArgumentNullException.ThrowIfNull(document);
        var extension = NormalizeExtension(targetExtension);
        if (extension is not ".dwg" and not ".dwt")
            throw new NotSupportedException($"UCAD does not export DWG transport bytes as '{extension}'.");

        if (TryExportUntouchedSource(document, extension, out var passthrough)) return passthrough;

        var semantic = CadAcadInteropCodec.ExportDwg(document, extension);
        return AppendEditedOpaqueWarning(document, semantic, "DWG/DWT");
    }

    public static CadAcadBinaryExportResult ExportDxf(CadDocument document, bool binary = false)
    {
        ArgumentNullException.ThrowIfNull(document);
        if (TryExportUntouchedSource(document, ".dxf", out var passthrough)) return passthrough;

        CadAcadBinaryExportResult semantic;
        if (binary)
        {
            semantic = CadAcadInteropCodec.ExportBinaryDxf(document);
        }
        else
        {
            var text = CadDxfFullInteropCodec.Export(document);
            semantic = new CadAcadBinaryExportResult(
                System.Text.Encoding.UTF8.GetBytes(text.Content),
                text.Warnings,
                ".dxf",
                "AC1032");
        }
        return AppendEditedOpaqueWarning(document, semantic, "DXF");
    }

    public static CadAcadBinaryExportResult ExportOriginalAutoCadSource(CadDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        var envelope = document.AutoCadSourceEnvelope
            ?? throw new InvalidOperationException("The document does not contain an original AutoCAD source envelope.");
        return new CadAcadBinaryExportResult(envelope.CopyContent(), [], envelope.SourceExtension, envelope.SourceCadVersion);
    }

    private static void AttachSource(CadAcadImportResult imported, ReadOnlyMemory<byte> content) =>
        imported.Document.AttachAutoCadSourceEnvelope(new CadAutoCadSourceEnvelope(
            content,
            imported.SourceExtension,
            imported.SourceCadVersion,
            preservationReasons: [PreservationReason]));

    private static bool TryExportUntouchedSource(CadDocument document, string extension, out CadAcadBinaryExportResult result)
    {
        if (document.AutoCadSourceEnvelope is { } envelope &&
            string.Equals(envelope.SourceExtension, extension, StringComparison.OrdinalIgnoreCase) &&
            envelope.IsDocumentUnmodified(document))
        {
            result = new CadAcadBinaryExportResult(envelope.CopyContent(), [], extension, envelope.SourceCadVersion);
            return true;
        }
        result = null!;
        return false;
    }

    private static CadAcadBinaryExportResult AppendEditedOpaqueWarning(CadDocument document, CadAcadBinaryExportResult semantic, string format)
    {
        if (document.AutoCadSourceEnvelope is not { HasOpaqueRisk: true } source) return semantic;
        var warnings = semantic.Warnings.ToList();
        warnings.Add(
            $"The drawing was edited after importing {source.SourceExtension} content retained for opaque recovery. " +
            $"The rebuilt {format} contains UCAD-supported semantic data, but source-only proxy/custom payloads cannot yet be merged back into an edited container. " +
            "The untouched original remains available through ExportOriginalAutoCadSource and native .ucad persistence.");
        return semantic with { Warnings = warnings };
    }

    private static string NormalizeExtension(string extension)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(extension);
        return extension.StartsWith('.') ? extension.ToLowerInvariant() : "." + extension.ToLowerInvariant();
    }
}

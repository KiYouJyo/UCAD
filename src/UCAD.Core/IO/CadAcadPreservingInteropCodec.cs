namespace UCAD.Core.IO;

/// <summary>
/// Safety wrapper around the editable AutoCAD semantic bridge. Every DWG-compatible import retains
/// an immutable copy of the original container. If the document is exported back to the same format
/// without edits, the exact original bytes are reused. After edits, UCAD emits its editable semantic
/// model and warns that source-only ObjectARX/custom data cannot yet be merged into the rebuilt DWG;
/// the untouched original remains recoverable from the envelope and native .ucad persistence.
/// </summary>
public static class CadAcadPreservingInteropCodec
{
    private const string PreservationReason =
        "Original AutoCAD container retained as an opaque recovery source for ObjectARX/proxy/custom data outside the editable UCAD semantic model.";

    public static CadAcadImportResult ImportDwg(ReadOnlyMemory<byte> content, string sourceExtension = ".dwg")
    {
        var imported = CadAcadInteropCodec.ImportDwg(content, sourceExtension);
        imported.Document.AttachAutoCadSourceEnvelope(new CadAutoCadSourceEnvelope(
            content,
            imported.SourceExtension,
            imported.SourceCadVersion,
            preservationReasons: [PreservationReason]));

        // ACadSharp reports unsupported custom dictionaries and other non-graphical objects through
        // the same notification channel as real geometry/semantic problems. Since UCAD retains the
        // exact original DWG-compatible container, source-only metadata remains recoverable and must
        // not turn a successful drawing open into a blocking warning wall. Keep all actionable
        // graphical/semantic diagnostics, but remove only explicitly classified opaque metadata noise.
        var actionableWarnings = CadAcadInteropDiagnostics.KeepActionableWarnings(imported.Warnings);
        return imported with { Warnings = actionableWarnings };
    }

    public static CadAcadBinaryExportResult ExportDwg(CadDocument document, string targetExtension = ".dwg")
    {
        ArgumentNullException.ThrowIfNull(document);
        var extension = NormalizeExtension(targetExtension);
        if (extension is not ".dwg" and not ".dwt")
            throw new NotSupportedException($"UCAD does not export DWG transport bytes as '{extension}'.");

        if (document.AutoCadSourceEnvelope is { } envelope &&
            string.Equals(envelope.SourceExtension, extension, StringComparison.OrdinalIgnoreCase) &&
            envelope.IsDocumentUnmodified(document))
        {
            return new CadAcadBinaryExportResult(
                envelope.CopyContent(),
                [],
                extension,
                envelope.SourceCadVersion);
        }

        var semantic = CadAcadInteropCodec.ExportDwg(document, extension);
        if (document.AutoCadSourceEnvelope is not { HasOpaqueRisk: true } source)
            return semantic;

        var warnings = semantic.Warnings.ToList();
        warnings.Add(
            $"The drawing was edited after importing {source.SourceExtension} content retained for opaque recovery. " +
            "The rebuilt DWG/DWT contains UCAD-supported semantic data, but source-only ObjectARX/proxy/custom data cannot yet be merged back into an edited container. " +
            "The untouched original remains available through ExportOriginalAutoCadSource and native .ucad persistence.");
        return semantic with { Warnings = warnings };
    }

    /// <summary>
    /// Returns the exact original AutoCAD container even after the editable UCAD document has changed.
    /// This is a recovery/export-original operation and deliberately does not apply document edits.
    /// </summary>
    public static CadAcadBinaryExportResult ExportOriginalAutoCadSource(CadDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        var envelope = document.AutoCadSourceEnvelope
            ?? throw new InvalidOperationException("The document does not contain an original AutoCAD source envelope.");
        return new CadAcadBinaryExportResult(
            envelope.CopyContent(),
            [],
            envelope.SourceExtension,
            envelope.SourceCadVersion);
    }

    private static string NormalizeExtension(string extension)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(extension);
        return extension.StartsWith('.') ? extension.ToLowerInvariant() : "." + extension.ToLowerInvariant();
    }
}

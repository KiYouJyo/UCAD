using System.Text;
using UCAD.Core;
using UCAD.Core.IO;

namespace UCAD.Services;

/// <summary>
/// File-system boundary for the document lifecycle. Native .ucad files preserve
/// the complete current authoring model. AutoCAD drawing containers are routed
/// through the shared Core interoperability layer and never silently claimed as
/// full-fidelity when the UCAD semantic model cannot preserve an object.
/// </summary>
public sealed class CadDocumentFileService
{
    private static readonly UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);

    public async Task<CadDocument> OpenNativeAsync(string filePath, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        var fullPath = Path.GetFullPath(filePath);
        var text = await File.ReadAllTextAsync(fullPath, Utf8NoBom, cancellationToken);
        var document = CadNativeDocumentCodecAutoCad.Deserialize(text);
        document.ResetHistory();
        return document;
    }

    public async Task SaveNativeAsync(string filePath, CadDocument document, bool createBackup, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        ArgumentNullException.ThrowIfNull(document);
        var content = CadNativeDocumentCodecAutoCad.Serialize(document);
        await WriteAtomicTextAsync(filePath, content, createBackup, cancellationToken);
    }

    public async Task<DxfImportResult> OpenDxfAsync(string filePath, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        var fullPath = Path.GetFullPath(filePath);
        var bytes = await File.ReadAllBytesAsync(fullPath, cancellationToken);
        var import = CadAcadPreservingInteropCodec.ImportDxf(bytes, ".dxf");
        var resourceWarnings = CadExternalReferenceResolver.Resolve(import.Document, fullPath);
        import.Document.ResetHistory();
        return new DxfImportResult(
            import.Document,
            import.Warnings.Concat(resourceWarnings).Distinct(StringComparer.Ordinal).ToArray());
    }

    public async Task<CadAcadImportResult> OpenAutoCadAsync(string filePath, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        var fullPath = Path.GetFullPath(filePath);
        var descriptor = CadAcadFileFormatRegistry.GetRequiredByPath(fullPath);
        if (!descriptor.CanOpen || descriptor.Family != CadFileFormatFamily.AutoCadDrawing)
            throw new NotSupportedException($"{descriptor.DisplayName} is recognized but cannot be opened by the current UCAD interoperability layer.");

        var extension = descriptor.Extension;
        var bytes = await File.ReadAllBytesAsync(fullPath, cancellationToken);
        CadAcadImportResult import;
        if (string.Equals(extension, ".dxf", StringComparison.OrdinalIgnoreCase)) import = CadAcadPreservingInteropCodec.ImportDxf(bytes, extension);
        else if (string.Equals(extension, ".dxb", StringComparison.OrdinalIgnoreCase)) import = CadDxbCodec.Import(bytes);
        else import = CadAcadPreservingInteropCodec.ImportDwg(bytes, extension);

        var resourceWarnings = CadExternalReferenceResolver.Resolve(import.Document, fullPath);
        if (resourceWarnings.Count == 0) return import;
        return import with
        {
            Warnings = import.Warnings.Concat(resourceWarnings).Distinct(StringComparer.Ordinal).ToArray()
        };
    }

    public async Task<DxfExportResult> ExportDxfAsync(string filePath, CadDocument document, bool createBackup, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        ArgumentNullException.ThrowIfNull(document);
        var export = CadAcadPreservingInteropCodec.ExportDxf(document, binary: false);
        await WriteAtomicBytesAsync(filePath, export.Content, createBackup, cancellationToken);
        var text = Encoding.UTF8.GetString(export.Content);
        return new DxfExportResult(text, export.Warnings);
    }

    public async Task<CadAcadBinaryExportResult> ExportAutoCadBinaryAsync(string filePath, CadDocument document, bool createBackup, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        ArgumentNullException.ThrowIfNull(document);
        var descriptor = CadAcadFileFormatRegistry.GetRequiredByPath(filePath);
        if (!descriptor.CanExport || descriptor.Family != CadFileFormatFamily.AutoCadDrawing)
            throw new NotSupportedException($"{descriptor.DisplayName} is recognized but cannot be exported by the current UCAD interoperability layer.");

        CadAcadBinaryExportResult export;
        if (string.Equals(descriptor.Extension, ".dxf", StringComparison.OrdinalIgnoreCase)) export = CadAcadPreservingInteropCodec.ExportDxf(document, binary: true);
        else if (string.Equals(descriptor.Extension, ".dxb", StringComparison.OrdinalIgnoreCase)) export = CadDxbCodec.Export(document);
        else export = CadAcadPreservingInteropCodec.ExportDwg(document, descriptor.Extension);
        await WriteAtomicBytesAsync(filePath, export.Content, createBackup, cancellationToken);
        return export;
    }

    public static string GetAutoSavePath(string sourcePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);
        return Path.GetFullPath(sourcePath) + ".autosave" + CadNativeDocumentCodec.FileExtension;
    }

    private static async Task WriteAtomicTextAsync(string filePath, string content, bool createBackup, CancellationToken cancellationToken)
    {
        var fullPath = PrepareAtomicWrite(filePath, createBackup);
        var tempPath = fullPath + ".tmp";
        try { await File.WriteAllTextAsync(tempPath, content, Utf8NoBom, cancellationToken); File.Move(tempPath, fullPath, overwrite: true); }
        finally { if (File.Exists(tempPath)) File.Delete(tempPath); }
    }

    private static async Task WriteAtomicBytesAsync(string filePath, byte[] content, bool createBackup, CancellationToken cancellationToken)
    {
        var fullPath = PrepareAtomicWrite(filePath, createBackup);
        var tempPath = fullPath + ".tmp";
        try { await File.WriteAllBytesAsync(tempPath, content, cancellationToken); File.Move(tempPath, fullPath, overwrite: true); }
        finally { if (File.Exists(tempPath)) File.Delete(tempPath); }
    }

    private static string PrepareAtomicWrite(string filePath, bool createBackup)
    {
        var fullPath = Path.GetFullPath(filePath);
        var directory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrWhiteSpace(directory)) Directory.CreateDirectory(directory);
        if (createBackup && File.Exists(fullPath)) File.Copy(fullPath, fullPath + ".bak", overwrite: true);
        return fullPath;
    }
}

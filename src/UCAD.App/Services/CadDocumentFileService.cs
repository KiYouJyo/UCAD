using System.Text;
using UCAD.Core;
using UCAD.Core.IO;

namespace UCAD.Services;

/// <summary>
/// File-system boundary for the document lifecycle. Native .ucad files preserve
/// the complete current authoring model; DXF remains the interoperable exchange format.
/// WinUI pickers stay in the shell while encoding, backup and atomic replacement live here.
/// </summary>
public sealed class CadDocumentFileService
{
    private static readonly UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);

    public async Task<CadDocument> OpenNativeAsync(string filePath, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        var fullPath = Path.GetFullPath(filePath);
        var text = await File.ReadAllTextAsync(fullPath, Utf8NoBom, cancellationToken);
        var document = CadNativeDocumentCodecCurrent.Deserialize(text);
        document.ResetHistory();
        return document;
    }

    public async Task SaveNativeAsync(
        string filePath,
        CadDocument document,
        bool createBackup,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        ArgumentNullException.ThrowIfNull(document);
        var content = CadNativeDocumentCodecCurrent.Serialize(document);
        await WriteAtomicAsync(filePath, content, createBackup, cancellationToken);
    }

    public async Task<DxfImportResult> OpenDxfAsync(string filePath, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        var fullPath = Path.GetFullPath(filePath);
        var text = await File.ReadAllTextAsync(fullPath, Utf8NoBom, cancellationToken);
        var import = CadDxfCodec.Import(text);
        import.Document.ResetHistory();
        return import;
    }

    public async Task<DxfExportResult> ExportDxfAsync(
        string filePath,
        CadDocument document,
        bool createBackup,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        ArgumentNullException.ThrowIfNull(document);
        var export = CadDxfCodec.Export(document);
        await WriteAtomicAsync(filePath, export.Content, createBackup, cancellationToken);
        return export;
    }

    public static string GetAutoSavePath(string sourcePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);
        var fullPath = Path.GetFullPath(sourcePath);
        return fullPath + ".autosave" + CadNativeDocumentCodec.FileExtension;
    }

    private static async Task WriteAtomicAsync(
        string filePath,
        string content,
        bool createBackup,
        CancellationToken cancellationToken)
    {
        var fullPath = Path.GetFullPath(filePath);
        var directory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrWhiteSpace(directory)) Directory.CreateDirectory(directory);

        if (createBackup && File.Exists(fullPath))
        {
            File.Copy(fullPath, fullPath + ".bak", overwrite: true);
        }

        var tempPath = fullPath + ".tmp";
        try
        {
            await File.WriteAllTextAsync(tempPath, content, Utf8NoBom, cancellationToken);
            File.Move(tempPath, fullPath, overwrite: true);
        }
        finally
        {
            if (File.Exists(tempPath)) File.Delete(tempPath);
        }
    }
}
using System.Text;
using UCAD.Core;
using UCAD.Core.IO;

namespace UCAD.Services;

/// <summary>
/// File-system boundary for the v0.8 DXF-first document lifecycle.
/// WinUI pickers stay in the shell; encoding, backup and Core codec calls live here.
/// </summary>
public sealed class CadDocumentFileService
{
    private static readonly UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);

    public async Task<DxfImportResult> OpenDxfAsync(string filePath, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        var fullPath = Path.GetFullPath(filePath);
        var text = await File.ReadAllTextAsync(fullPath, Utf8NoBom, cancellationToken);
        return CadDxfCodec.Import(text);
    }

    public async Task<DxfExportResult> SaveDxfAsync(
        string filePath,
        CadDocument document,
        bool createBackup,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        ArgumentNullException.ThrowIfNull(document);

        var fullPath = Path.GetFullPath(filePath);
        var directory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrWhiteSpace(directory)) Directory.CreateDirectory(directory);

        var export = CadDxfCodec.Export(document);
        if (createBackup && File.Exists(fullPath))
        {
            File.Copy(fullPath, fullPath + ".bak", overwrite: true);
        }

        var tempPath = fullPath + ".tmp";
        await File.WriteAllTextAsync(tempPath, export.Content, Utf8NoBom, cancellationToken);
        File.Move(tempPath, fullPath, overwrite: true);
        return export;
    }

    public static string GetAutoSavePath(string sourcePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);
        var fullPath = Path.GetFullPath(sourcePath);
        return fullPath + ".autosave.dxf";
    }
}

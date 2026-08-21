namespace UCAD.Core.Import;

/// <summary>
/// Common entry point for CAD file importers.
/// Keeps file format readers separated from the document model.
/// </summary>
public interface ICadImporter
{
    string Format { get; }
    bool CanImport(string extension);
    CadImportResult Import(string filePath);
}

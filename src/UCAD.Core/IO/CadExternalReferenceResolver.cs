using UCAD.Core.Entities;

namespace UCAD.Core.IO;

/// <summary>
/// Resolves file-backed AutoCAD display references after the app has supplied the actual
/// drawing path. Core import deliberately does not assume a working directory because that
/// would make relative IMAGE/UNDERLAY links depend on process launch location.
/// </summary>
public static class CadExternalReferenceResolver
{
    public static IReadOnlyList<string> Resolve(CadDocument document, string sourceDrawingPath)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceDrawingPath);

        var warnings = new List<string>();
        var fullDrawingPath = Path.GetFullPath(sourceDrawingPath);
        var sourceDirectory = Path.GetDirectoryName(fullDrawingPath) ?? Environment.CurrentDirectory;

        foreach (var image in document.Entities.OfType<RasterImageEntity>())
        {
            var resolved = ResolveExistingPath(image.ReferencePath, sourceDirectory);
            image.SetResolvedPath(resolved);
            if (resolved is null)
            {
                var notice = $"AutoCAD IMAGE external resource '{image.ReferencePath}' could not be found relative to '{sourceDirectory}'; placement and clipping remain visible as an unresolved reference.";
                if (!warnings.Contains(notice, StringComparer.Ordinal)) warnings.Add(notice);
            }
        }

        return warnings;
    }

    private static string? ResolveExistingPath(string referencePath, string sourceDirectory)
    {
        if (string.IsNullOrWhiteSpace(referencePath) || referencePath.StartsWith('<')) return null;

        var expanded = Environment.ExpandEnvironmentVariables(referencePath.Trim().Trim('"'));
        var candidates = new List<string>();
        try
        {
            if (Path.IsPathRooted(expanded)) candidates.Add(Path.GetFullPath(expanded));
            else candidates.Add(Path.GetFullPath(Path.Combine(sourceDirectory, expanded)));

            var fileName = Path.GetFileName(expanded);
            if (!string.IsNullOrWhiteSpace(fileName))
            {
                var siblingCandidate = Path.GetFullPath(Path.Combine(sourceDirectory, fileName));
                if (!candidates.Contains(siblingCandidate, StringComparer.OrdinalIgnoreCase)) candidates.Add(siblingCandidate);
            }
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return null;
        }

        return candidates.FirstOrDefault(File.Exists);
    }
}

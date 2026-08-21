using UCAD.Core.Entities;

namespace UCAD.Core.IO;

/// <summary>
/// Resolves file-backed AutoCAD display references after the app has supplied the actual
/// drawing path. Core import deliberately does not assume a working directory because that
/// would make relative IMAGE/UNDERLAY/SHX/XREF links depend on process launch location.
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

        foreach (var underlay in document.Entities.OfType<UnderlayReferenceEntity>())
        {
            var resolved = ResolveExistingPath(underlay.ReferencePath, sourceDirectory);
            underlay.SetResolvedPath(resolved);
            if (resolved is null)
            {
                var notice = $"AutoCAD {underlay.Kind.ToString().ToUpperInvariant()} underlay resource '{underlay.ReferencePath}' could not be found relative to '{sourceDirectory}'; placement/page/clipping metadata remain available.";
                if (!warnings.Contains(notice, StringComparer.Ordinal)) warnings.Add(notice);
            }
        }

        ResolveShapeReferences(document, sourceDirectory, warnings);
        return warnings;
    }

    private static void ResolveShapeReferences(CadDocument document, string sourceDirectory, List<string> warnings)
    {
        foreach (var shape in document.Entities.OfType<ShapeReferenceEntity>().ToArray())
        {
            var resolvedAny = false;
            var decodeErrors = new List<string>();
            foreach (var reference in shape.ReferencePaths)
            {
                var resolved = ResolveExistingPath(reference, sourceDirectory);
                if (resolved is null) continue;
                resolvedAny = true;
                try
                {
                    var content = File.ReadAllBytes(resolved);
                    if (!CadShxCodec.TryRead(content, out var file, out var warning) || file is null)
                    {
                        if (!string.IsNullOrWhiteSpace(warning)) decodeErrors.Add($"{Path.GetFileName(resolved)}: {warning}");
                        continue;
                    }

                    IReadOnlyList<IReadOnlyList<UCAD.Core.Geometry.CadPoint>> strokes;
                    try
                    {
                        strokes = CadShxCodec.RenderShapeWorld(file, shape);
                    }
                    catch (KeyNotFoundException)
                    {
                        // Some DWG readers expose the owning shape-style name rather than the
                        // original group-2 symbol name. A single-symbol shape resource is
                        // unambiguous and can still be rendered exactly.
                        var drawable = file.Symbols.Values.Where(symbol => symbol.Number != 0).ToArray();
                        if (drawable.Length != 1) continue;
                        var local = CadShxCodec.RenderGlyph(file, drawable[0].Number);
                        strokes = TransformShapeStrokes(local, shape);
                    }

                    var vectors = strokes
                        .Where(stroke => stroke.Count >= 2)
                        .Select(stroke => (ICadEntity)new PolylineEntity(stroke, closed: false))
                        .ToArray();
                    if (vectors.Length == 0) continue;
                    document.Replace(shape.Id, vectors);
                    break;
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException or ArgumentException or NotSupportedException or FormatException)
                {
                    decodeErrors.Add($"{Path.GetFileName(resolved)}: {ex.Message}");
                }
            }

            if (!document.Entities.Any(entity => entity.Id == shape.Id)) continue;
            var notice = !resolvedAny
                ? $"AutoCAD SHAPE '{shape.ShapeName}' could not resolve any SHX/SHP resource ({string.Join(", ", shape.ReferencePaths)}); its insertion marker remains visible."
                : $"AutoCAD SHAPE '{shape.ShapeName}' resource was found but no matching drawable symbol could be decoded. {string.Join("; ", decodeErrors.Distinct(StringComparer.Ordinal))}";
            if (!warnings.Contains(notice, StringComparer.Ordinal)) warnings.Add(notice);
        }
    }

    private static IReadOnlyList<IReadOnlyList<UCAD.Core.Geometry.CadPoint>> TransformShapeStrokes(
        IReadOnlyList<IReadOnlyList<UCAD.Core.Geometry.CadPoint>> local,
        ShapeReferenceEntity shape)
    {
        var sin = Math.Sin(shape.RotationRadians);
        var cos = Math.Cos(shape.RotationRadians);
        var shear = Math.Tan(shape.ObliqueRadians);
        var result = new List<IReadOnlyList<UCAD.Core.Geometry.CadPoint>>(local.Count);
        foreach (var stroke in local)
        {
            var transformed = stroke.Select(point =>
            {
                var x = point.X * shape.Size * shape.XScale;
                var y = point.Y * shape.Size;
                x += y * shear;
                return new UCAD.Core.Geometry.CadPoint(
                    shape.InsertionPoint.X + x * cos - y * sin,
                    shape.InsertionPoint.Y + x * sin + y * cos);
            }).ToArray();
            if (transformed.Length >= 2) result.Add(transformed);
        }
        return result;
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

                // Common packaged-drawing convention: support resources are kept beside the
                // drawing in Fonts or Support subdirectories.
                foreach (var folder in new[] { "Fonts", "fonts", "Support", "support" })
                {
                    var supportCandidate = Path.GetFullPath(Path.Combine(sourceDirectory, folder, fileName));
                    if (!candidates.Contains(supportCandidate, StringComparer.OrdinalIgnoreCase)) candidates.Add(supportCandidate);
                }
            }
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return null;
        }

        return candidates.FirstOrDefault(File.Exists);
    }
}

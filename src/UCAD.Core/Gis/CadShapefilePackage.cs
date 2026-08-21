namespace UCAD.Core.Gis;

public sealed record CadShapefilePackageExportResult(
    byte[] ShpContent,
    byte[] ShxContent,
    byte[] DbfContent,
    byte[] CpgContent,
    byte[]? PrjContent,
    CadShapefileShapeType ShapeType,
    IReadOnlyList<string> Warnings);

public sealed record CadShapefilePackageImportResult(
    CadShapefileBundleImportResult Bundle,
    CadShapefileIndexValidation? IndexValidation,
    IReadOnlyList<string> Warnings);

/// <summary>
/// Physical Shapefile package layer: SHP geometry, SHX index, DBF attributes, CPG text
/// encoding and optional PRJ coordinate reference. The semantic bundle remains reusable
/// independently; this package adds the disk-side index contract around it.
/// </summary>
public static class CadShapefilePackage
{
    public static CadShapefilePackageExportResult Export(
        IEnumerable<CadShapefileFeature> features,
        IReadOnlyList<CadDbfFieldDefinition> fields,
        CadCoordinateReferenceSystem? crs = null,
        DateTime? modifiedDate = null)
    {
        var bundle = CadShapefileBundle.Export(features, fields, crs, modifiedDate);
        var shx = CadShapefileIndexCodec.Build(bundle.ShpContent);
        return new CadShapefilePackageExportResult(
            bundle.ShpContent,
            shx,
            bundle.DbfContent,
            bundle.CpgContent,
            bundle.PrjContent,
            bundle.ShapeType,
            bundle.Warnings);
    }

    public static CadShapefilePackageImportResult Import(
        ReadOnlySpan<byte> shpContent,
        ReadOnlySpan<byte> shxContent = default,
        ReadOnlySpan<byte> dbfContent = default,
        ReadOnlySpan<byte> cpgContent = default,
        ReadOnlySpan<byte> prjContent = default)
    {
        CadShapefileIndexValidation? indexValidation = null;
        var warnings = new List<string>();
        if (shxContent.IsEmpty)
        {
            warnings.Add("No SHX index sidecar was supplied; SHP geometry was read sequentially.");
        }
        else
        {
            indexValidation = CadShapefileIndexCodec.Validate(shpContent, shxContent);
            if (!indexValidation.IsConsistent)
            {
                warnings.Add("SHX does not match the authoritative SHP record layout; UCAD ignored the index for geometry reading.");
                warnings.AddRange(indexValidation.Warnings);
            }
        }

        var bundle = CadShapefileBundle.Import(shpContent, dbfContent, cpgContent, prjContent);
        warnings.AddRange(bundle.Warnings);
        return new CadShapefilePackageImportResult(
            bundle,
            indexValidation,
            warnings.Distinct(StringComparer.Ordinal).ToArray());
    }
}

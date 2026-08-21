using System.Text;
using UCAD.Core.Entities;

namespace UCAD.Core.Gis;

public sealed record CadShapefileFeature(
    ICadEntity Geometry,
    IReadOnlyDictionary<string, string?> Attributes);

public sealed record CadShapefileBundleExportResult(
    byte[] ShpContent,
    byte[] DbfContent,
    byte[] CpgContent,
    byte[]? PrjContent,
    CadShapefileShapeType ShapeType,
    IReadOnlyList<string> Warnings);

public sealed record CadShapefileBundleImportResult(
    CadShapefileImportResult Geometry,
    CadDbfTable? Attributes,
    CadCoordinateReferenceSystem? IdentifiedCrs,
    bool CanMapRecordsOneToOne,
    IReadOnlyList<string> Warnings);

/// <summary>
/// Coordinates the standard Shapefile sidecars without hiding format boundaries.
/// Geometry remains in SHP, attributes in DBF, text encoding in CPG, and CRS in PRJ.
/// One-to-one attribute mapping is reported only when the imported geometry count still
/// matches the DBF record count; multipart expansion never receives guessed attributes.
/// </summary>
public static class CadShapefileBundle
{
    public static CadShapefileBundleExportResult Export(
        IEnumerable<CadShapefileFeature> features,
        IReadOnlyList<CadDbfFieldDefinition> fields,
        CadCoordinateReferenceSystem? crs = null,
        DateTime? modifiedDate = null)
    {
        ArgumentNullException.ThrowIfNull(features);
        ArgumentNullException.ThrowIfNull(fields);
        var snapshot = features.ToArray();
        if (snapshot.Length == 0) throw new ArgumentException("Shapefile bundle requires at least one feature.", nameof(features));
        if (snapshot.Any(feature => feature.Geometry is null)) throw new ArgumentException("Shapefile feature geometry cannot be null.", nameof(features));

        var geometry = CadShapefileGeometryCodec.Export(snapshot.Select(feature => feature.Geometry));
        var table = new CadDbfTable(
            fields,
            snapshot.Select(feature => new CadDbfRecord(feature.Attributes)).ToArray());
        var dbf = CadDbfCodec.Export(table, modifiedDate);
        var warnings = geometry.Warnings.Where(warning => !IsGeometryOnlyDisclaimer(warning)).ToList();

        byte[]? prj = null;
        if (crs is CadCoordinateReferenceSystem coordinateReferenceSystem)
        {
            if (coordinateReferenceSystem == CadCoordinateReferenceSystem.LocalPlanar)
            {
                warnings.Add("No PRJ was emitted because LocalPlanar does not imply a coordinate reference system.");
            }
            else
            {
                prj = CadPrjCodec.Export(coordinateReferenceSystem);
            }
        }
        else
        {
            warnings.Add("No PRJ was emitted because no coordinate reference system was supplied.");
        }

        return new CadShapefileBundleExportResult(
            geometry.ShpContent,
            dbf,
            CadDbfCodec.CreateCpgUtf8(),
            prj,
            geometry.ShapeType,
            warnings.AsReadOnly());
    }

    public static CadShapefileBundleImportResult Import(
        ReadOnlySpan<byte> shpContent,
        ReadOnlySpan<byte> dbfContent = default,
        ReadOnlySpan<byte> cpgContent = default,
        ReadOnlySpan<byte> prjContent = default)
    {
        var geometry = CadShapefileGeometryCodec.Import(shpContent);
        CadDbfTable? attributes = null;
        var warnings = geometry.Warnings.Where(warning => !IsGeometryOnlyDisclaimer(warning)).ToList();

        if (!dbfContent.IsEmpty)
        {
            attributes = CadDbfCodec.Import(dbfContent);
            if (cpgContent.IsEmpty)
            {
                warnings.Add("DBF values were decoded as UTF-8 because no CPG sidecar was supplied.");
            }
            else
            {
                var cpg = Encoding.ASCII.GetString(cpgContent).Trim();
                if (!string.Equals(cpg, "UTF-8", StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(cpg, "UTF8", StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(cpg, "65001", StringComparison.OrdinalIgnoreCase))
                {
                    warnings.Add($"CPG declares '{cpg}', but the current DBF foundation decodes character fields as UTF-8.");
                }
            }
        }
        else
        {
            warnings.Add("No DBF attribute sidecar was supplied.");
        }

        CadCoordinateReferenceSystem? identifiedCrs = null;
        if (!prjContent.IsEmpty)
        {
            var wkt = Encoding.UTF8.GetString(prjContent);
            identifiedCrs = CadPrjCodec.IdentifyKnown(wkt);
            if (identifiedCrs is null)
                warnings.Add("PRJ was preserved as an unknown projection; UCAD Core did not guess an EPSG/CRS mapping.");
        }
        else
        {
            warnings.Add("No PRJ coordinate-reference sidecar was supplied.");
        }

        var oneToOne = attributes is not null && geometry.Entities.Count == attributes.Records.Count;
        if (attributes is not null && !oneToOne)
        {
            warnings.Add(
                $"SHP geometry expanded to {geometry.Entities.Count} CAD entities while DBF contains {attributes.Records.Count} records; " +
                "attributes were not guessed onto multipart geometry. Preserve record mapping at a higher exchange layer.");
        }

        return new CadShapefileBundleImportResult(
            geometry,
            attributes,
            identifiedCrs,
            oneToOne,
            warnings.AsReadOnly());
    }

    private static bool IsGeometryOnlyDisclaimer(string warning) =>
        warning.StartsWith("Geometry-only SHP ", StringComparison.Ordinal);
}

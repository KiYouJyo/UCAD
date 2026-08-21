namespace UCAD.Core.Gis;

public sealed record CadShapefileAttributeBindingResult(
    bool CanBind,
    IReadOnlyList<CadDbfRecord?> AttributesByEntity,
    IReadOnlyList<string> Warnings);

public static class CadShapefileAttributeBinding
{
    public static CadShapefileAttributeBindingResult Bind(
        ReadOnlySpan<byte> shpContent,
        ReadOnlySpan<byte> dbfContent,
        CadShapefileImportResult geometry,
        CadDbfTable attributes)
    {
        ArgumentNullException.ThrowIfNull(geometry);
        ArgumentNullException.ThrowIfNull(attributes);
        var warnings = new List<string>();
        var map = CadShapefileRecordMap.Read(shpContent);
        var dbfLayout = CadDbfRecordLayout.Read(dbfContent);

        if (dbfLayout.HasDeletedRecords)
        {
            warnings.Add(
                $"DBF contains {dbfLayout.DeletedRecordIndexes.Count} deleted record(s); " +
                "automatic multipart attribute binding was disabled to avoid record-index drift.");
            return Failure(geometry.Entities.Count, warnings);
        }
        if (map.ShapeRecordCount != dbfLayout.RecordCount)
        {
            warnings.Add($"SHP contains {map.ShapeRecordCount} shape records while DBF declares {dbfLayout.RecordCount} records.");
            return Failure(geometry.Entities.Count, warnings);
        }
        if (attributes.Records.Count != dbfLayout.RecordCount)
        {
            warnings.Add(
                $"Decoded DBF contains {attributes.Records.Count} active records while the raw DBF layout declares {dbfLayout.RecordCount}; " +
                "automatic binding was disabled.");
            return Failure(geometry.Entities.Count, warnings);
        }
        if (map.SourceRecordIndexByEntity.Count != geometry.Entities.Count)
        {
            warnings.Add(
                $"SHP record map expands to {map.SourceRecordIndexByEntity.Count} CAD entities while geometry import produced {geometry.Entities.Count}; " +
                "automatic binding was disabled.");
            return Failure(geometry.Entities.Count, warnings);
        }

        var bound = map.SourceRecordIndexByEntity
            .Select(recordIndex => (CadDbfRecord?)attributes.Records[recordIndex])
            .ToArray();
        return new CadShapefileAttributeBindingResult(true, Array.AsReadOnly(bound), warnings.AsReadOnly());
    }

    private static CadShapefileAttributeBindingResult Failure(int entityCount, List<string> warnings) =>
        new(
            false,
            Array.AsReadOnly(Enumerable.Repeat<CadDbfRecord?>(null, entityCount).ToArray()),
            warnings.AsReadOnly());
}

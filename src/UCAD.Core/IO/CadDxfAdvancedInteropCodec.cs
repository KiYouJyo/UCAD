using System.Globalization;
using System.Text;
using UCAD.Core.Blocks;
using UCAD.Core.Entities;
using UCAD.Core.Geometry;
using UCAD.Core.Layers;
using UCAD.Core.Styles;

namespace UCAD.Core.IO;

/// <summary>
/// Higher-fidelity DXF semantic layer built on top of <see cref="CadDxfCodec"/>.
/// The foundational codec remains intentionally small; this layer adds AutoCAD
/// annotation and block semantics that need cross-record/section context.
/// </summary>
public static class CadDxfAdvancedInteropCodec
{
    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;
    private static readonly HashSet<string> AdvancedEntityTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "DIMENSION", "LEADER", "INSERT", "ATTRIB", "SEQEND"
    };

    public static DxfImportResult Import(string text)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        var pairs = ParsePairs(text);
        var entityRecords = ReadEntityRecords(pairs, "ENTITIES");
        var linkedAnnotationHandles = entityRecords
            .Where(record => EqualsToken(record.Type, "LEADER"))
            .Select(record => GetString(record.Data, 340, string.Empty))
            .Where(handle => !string.IsNullOrWhiteSpace(handle))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var sanitized = BuildBaselineDxf(pairs, linkedAnnotationHandles);
        var baseline = CadDxfCodec.Import(sanitized);
        var document = baseline.Document;
        var warnings = baseline.Warnings.ToList();

        ImportDimensionStyles(pairs, document, warnings);
        ImportBlocks(pairs, document, warnings);
        ImportAdvancedEntities(entityRecords, document, warnings, linkedAnnotationHandles);
        RestoreCurrentDimensionStyle(pairs, document, warnings);
        document.ResetHistory();
        return new DxfImportResult(document, warnings);
    }

    public static DxfExportResult Export(CadDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        var baseline = CadDxfCodec.Export(document);
        var warnings = baseline.Warnings.Where(warning => !IsBaselineAdvancedSkipWarning(warning)).ToList();
        var pairs = ParsePairs(baseline.Content);
        var output = new StringBuilder(Math.Max(baseline.Content.Length + 4096, 8192));
        string? section = null;
        var blocksWritten = false;
        long nextHandle = 0xF0000;

        for (var i = 0; i < pairs.Count; i++)
        {
            var pair = pairs[i];
            if (pair.Code == 0 && EqualsToken(pair.Value, "SECTION") && i + 1 < pairs.Count && pairs[i + 1].Code == 2)
            {
                var nextSection = pairs[i + 1].Value;
                if (EqualsToken(nextSection, "ENTITIES") && !blocksWritten)
                {
                    WriteBlocksSection(output, document, warnings, ref nextHandle);
                    blocksWritten = true;
                }

                WritePair(output, pair.Code, pair.Value);
                WritePair(output, pairs[i + 1].Code, pairs[i + 1].Value);
                section = nextSection;
                i++;
                continue;
            }

            if (pair.Code == 0 && EqualsToken(pair.Value, "ENDSEC"))
            {
                if (EqualsToken(section, "HEADER"))
                {
                    WritePair(output, 9, "$DIMSTYLE");
                    WritePair(output, 2, document.CurrentDimensionStyleName);
                }
                else if (EqualsToken(section, "TABLES"))
                {
                    WriteDimensionStyleTable(output, document);
                }
                else if (EqualsToken(section, "ENTITIES"))
                {
                    WriteAdvancedEntities(output, document, warnings, ref nextHandle);
                }

                WritePair(output, pair.Code, pair.Value);
                section = null;
                continue;
            }

            WritePair(output, pair.Code, pair.Value);
        }

        if (!blocksWritten)
        {
            warnings.Add("DXF export could not locate an ENTITIES section for advanced AutoCAD records.");
        }

        return new DxfExportResult(output.ToString(), warnings);
    }

    private static void ImportDimensionStyles(IReadOnlyList<DxfPair> pairs, CadDocument document, List<string> warnings)
    {
        string? section = null;
        for (var i = 0; i < pairs.Count; i++)
        {
            var pair = pairs[i];
            if (pair.Code == 0 && EqualsToken(pair.Value, "SECTION") && i + 1 < pairs.Count && pairs[i + 1].Code == 2)
            {
                section = pairs[++i].Value;
                continue;
            }
            if (pair.Code == 0 && EqualsToken(pair.Value, "ENDSEC"))
            {
                section = null;
                continue;
            }
            if (!EqualsToken(section, "TABLES") || pair.Code != 0 || !EqualsToken(pair.Value, "DIMSTYLE")) continue;

            var record = ReadRecord(pairs, i + 1, out var nextIndex);
            i = nextIndex - 1;
            var name = GetString(record, 2, string.Empty);
            if (string.IsNullOrWhiteSpace(name)) continue;

            try
            {
                var textHeight = PositiveOrDefault(GetDouble(record, 140, 2.5), 2.5);
                var arrowSize = PositiveOrDefault(GetDouble(record, 41, 2.5), 2.5);
                var precision = Math.Clamp(GetInt(record, 271, 2), 0, 8);
                var post = GetString(record, 3, "<>");
                SplitDimensionPost(post, out var prefix, out var suffix);
                document.DefineDimensionStyle(new CadDimensionStyle(name, textHeight, arrowSize, precision, prefix, suffix), replaceExisting: true);
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
            {
                warnings.Add($"DXF DIMSTYLE '{name}' could not be imported: {ex.Message}");
            }
        }
    }

    private static void RestoreCurrentDimensionStyle(IReadOnlyList<DxfPair> pairs, CadDocument document, List<string> warnings)
    {
        for (var i = 0; i < pairs.Count - 1; i++)
        {
            if (pairs[i].Code != 9 || !EqualsToken(pairs[i].Value, "$DIMSTYLE")) continue;
            var name = pairs[i + 1].Value;
            if (string.IsNullOrWhiteSpace(name)) return;
            try
            {
                EnsureDimensionStyle(document, name);
                document.SetCurrentDimensionStyle(name);
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
            {
                warnings.Add($"DXF current dimension style '{name}' could not be restored: {ex.Message}");
            }
            return;
        }
    }

    private static void ImportBlocks(IReadOnlyList<DxfPair> pairs, CadDocument document, List<string> warnings)
    {
        string? section = null;
        for (var i = 0; i < pairs.Count; i++)
        {
            var pair = pairs[i];
            if (pair.Code == 0 && EqualsToken(pair.Value, "SECTION") && i + 1 < pairs.Count && pairs[i + 1].Code == 2)
            {
                section = pairs[++i].Value;
                continue;
            }
            if (pair.Code == 0 && EqualsToken(pair.Value, "ENDSEC"))
            {
                section = null;
                continue;
            }
            if (!EqualsToken(section, "BLOCKS") || pair.Code != 0 || !EqualsToken(pair.Value, "BLOCK")) continue;

            var header = ReadRecord(pairs, i + 1, out var cursor);
            var name = GetString(header, 2, GetString(header, 3, string.Empty));
            var basePoint = new CadPoint(GetDouble(header, 10, 0), GetDouble(header, 20, 0));
            var entities = new List<ICadEntity>();
            var attributes = new List<CadBlockAttributeDefinition>();

            while (cursor < pairs.Count)
            {
                if (pairs[cursor].Code != 0)
                {
                    cursor++;
                    continue;
                }

                var type = pairs[cursor].Value;
                if (EqualsToken(type, "ENDBLK"))
                {
                    ReadRecord(pairs, cursor + 1, out cursor);
                    break;
                }
                if (EqualsToken(type, "ENDSEC")) break;

                var record = ReadRecord(pairs, cursor + 1, out var nextRecord);
                cursor = nextRecord;
                if (EqualsToken(type, "ATTDEF"))
                {
                    try
                    {
                        attributes.Add(ParseAttributeDefinition(record));
                    }
                    catch (Exception ex) when (ex is ArgumentException or FormatException)
                    {
                        warnings.Add($"DXF block '{name}' ATTDEF could not be imported: {ex.Message}");
                    }
                    continue;
                }

                if (EqualsToken(type, "INSERT"))
                {
                    warnings.Add($"DXF block '{name}' contains a nested INSERT; nested block definitions are not flattened into the UCAD definition snapshot yet.");
                    continue;
                }

                var converted = ParseFoundationalBlockEntity(type, record, warnings, name);
                if (converted is not null) entities.Add(converted);
            }

            i = Math.Max(i, cursor - 1);
            if (string.IsNullOrWhiteSpace(name) || IsLayoutBlockName(name)) continue;
            if (entities.Count == 0)
            {
                warnings.Add($"DXF block '{name}' had no UCAD-compatible geometry and was not added to the block table.");
                continue;
            }

            try
            {
                document.DefineBlock(new CadBlockDefinition(name, basePoint, entities, attributes), replaceExisting: true);
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
            {
                warnings.Add($"DXF block '{name}' could not be defined: {ex.Message}");
            }
        }
    }

    private static CadBlockAttributeDefinition ParseAttributeDefinition(IReadOnlyList<DxfPair> record)
    {
        var tag = GetString(record, 2, string.Empty);
        if (string.IsNullOrWhiteSpace(tag)) throw new FormatException("ATTDEF tag group 2 is missing.");
        var prompt = GetString(record, 3, tag);
        var defaultValue = GetString(record, 1, string.Empty);
        var position = new CadPoint(RequiredDouble(record, 10), RequiredDouble(record, 20));
        var textHeight = PositiveOrDefault(GetDouble(record, 40, 2.5), 2.5);
        var constant = (GetInt(record, 70, 0) & 2) != 0;
        return new CadBlockAttributeDefinition(tag, prompt, defaultValue, position, textHeight, constant);
    }

    private static ICadEntity? ParseFoundationalBlockEntity(string type, IReadOnlyList<DxfPair> record, List<string> warnings, string blockName)
    {
        if (EqualsToken(type, "ENDBLK") || EqualsToken(type, "SEQEND") || EqualsToken(type, "ATTRIB")) return null;
        var mini = BuildSingleEntityDxf(type, record);
        var parsed = CadDxfCodec.Import(mini);
        if (parsed.Document.Entities.Count == 1 && !parsed.HasWarnings) return parsed.Document.Entities[0];

        foreach (var warning in parsed.Warnings)
            warnings.Add($"DXF block '{blockName}': {warning}");
        return parsed.Document.Entities.Count == 1 ? parsed.Document.Entities[0] : null;
    }

    private static void ImportAdvancedEntities(
        IReadOnlyList<DxfEntityRecord> records,
        CadDocument document,
        List<string> warnings,
        IReadOnlySet<string> linkedAnnotationHandles)
    {
        var byHandle = records
            .Select(record => (Record: record, Handle: GetString(record.Data, 5, string.Empty)))
            .Where(item => !string.IsNullOrWhiteSpace(item.Handle))
            .GroupBy(item => item.Handle, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First().Record, StringComparer.OrdinalIgnoreCase);

        for (var i = 0; i < records.Count; i++)
        {
            var record = records[i];
            try
            {
                if (EqualsToken(record.Type, "DIMENSION"))
                {
                    var dimension = ParseDimension(record.Data, document, warnings);
                    if (dimension is not null) document.Add(dimension, ParseEntityProperties(record.Data, document));
                    continue;
                }

                if (EqualsToken(record.Type, "LEADER"))
                {
                    var leader = ParseLeader(record, byHandle, document, warnings);
                    if (leader is not null) document.Add(leader, ParseEntityProperties(record.Data, document));
                    continue;
                }

                if (EqualsToken(record.Type, "INSERT"))
                {
                    var attributeRecords = new List<DxfEntityRecord>();
                    var cursor = i + 1;
                    while (cursor < records.Count && EqualsToken(records[cursor].Type, "ATTRIB"))
                    {
                        attributeRecords.Add(records[cursor]);
                        cursor++;
                    }
                    if (cursor < records.Count && EqualsToken(records[cursor].Type, "SEQEND")) i = cursor;
                    else if (attributeRecords.Count > 0) i = cursor - 1;

                    var reference = ParseInsert(record.Data, attributeRecords, document, warnings);
                    if (reference is not null) document.Add(reference, ParseEntityProperties(record.Data, document));
                    continue;
                }

                if ((EqualsToken(record.Type, "TEXT") || EqualsToken(record.Type, "MTEXT")) &&
                    linkedAnnotationHandles.Contains(GetString(record.Data, 5, string.Empty)))
                {
                    continue;
                }
            }
            catch (Exception ex) when (ex is ArgumentException or FormatException or InvalidOperationException or KeyNotFoundException)
            {
                warnings.Add($"DXF entity '{record.Type}' could not be imported by the advanced bridge: {ex.Message}");
            }
        }
    }

    private static ICadEntity? ParseDimension(IReadOnlyList<DxfPair> record, CadDocument document, List<string> warnings)
    {
        var type = GetInt(record, 70, 0) & 0x0F;
        var style = EnsureDimensionStyle(document, GetString(record, 3, CadDimensionStyle.DefaultName));
        var textOverride = NormalizeDimensionTextOverride(GetNullableString(record, 1));

        if (type is 0 or 1)
        {
            var first = ReadPoint(record, 13, 23);
            var second = ReadPoint(record, 14, 24);
            var dimensionLine = ReadPoint(record, 10, 20);
            if (type == 0)
            {
                var requested = DegreesToRadians(GetDouble(record, 50, 0));
                var measured = Math.Atan2(second.Y - first.Y, second.X - first.X);
                if (AngularDistanceModuloPi(requested, measured) > 1e-5)
                {
                    warnings.Add("DXF rotated DIMENSION uses an independent measurement axis that UCAD's current aligned dimension model cannot preserve; it was skipped.");
                    return null;
                }
            }
            return new LinearDimensionEntity(first, second, dimensionLine, textOverride, style);
        }

        if (type == 5)
        {
            var vertex = ReadPoint(record, 15, 25);
            var firstRay = ReadPoint(record, 13, 23);
            var secondRay = ReadPoint(record, 14, 24);
            var arcPoint = ReadPoint(record, 10, 20);
            return new AngularDimensionEntity(vertex, firstRay, secondRay, arcPoint, textOverride, style);
        }

        if (type == 2)
        {
            var firstStart = ReadPoint(record, 13, 23);
            var firstEnd = ReadPoint(record, 14, 24);
            var secondStart = ReadPoint(record, 15, 25);
            var secondEnd = ReadPoint(record, 16, 26);
            if (!TryLineIntersection(firstStart, firstEnd, secondStart, secondEnd, out var vertex))
            {
                warnings.Add("DXF two-line angular DIMENSION has parallel/degenerate extension lines and was skipped.");
                return null;
            }
            var firstRay = FartherPoint(vertex, firstStart, firstEnd);
            var secondRay = FartherPoint(vertex, secondStart, secondEnd);
            var arcPoint = ReadPoint(record, 10, 20);
            return new AngularDimensionEntity(vertex, firstRay, secondRay, arcPoint, textOverride, style);
        }

        warnings.Add($"DXF DIMENSION type {type} is recognized but has no matching UCAD 2D dimension entity yet.");
        return null;
    }

    private static ICadEntity? ParseLeader(
        DxfEntityRecord leaderRecord,
        IReadOnlyDictionary<string, DxfEntityRecord> byHandle,
        CadDocument document,
        List<string> warnings)
    {
        var points = ReadRepeatedPoints(leaderRecord.Data, 10, 20);
        if (points.Count < 2)
        {
            warnings.Add("DXF LEADER with fewer than two vertices was skipped.");
            return null;
        }

        var annotationHandle = GetString(leaderRecord.Data, 340, string.Empty);
        if (string.IsNullOrWhiteSpace(annotationHandle) || !byHandle.TryGetValue(annotationHandle, out var annotation))
        {
            warnings.Add("DXF annotation-less LEADER was imported as an open polyline because UCAD's LeaderEntity requires annotation text.");
            return new PolylineEntity(points, closed: false);
        }

        var text = ReadAnnotationText(annotation);
        if (string.IsNullOrWhiteSpace(text))
        {
            warnings.Add("DXF LEADER annotation text was empty; leader geometry was imported as an open polyline.");
            return new PolylineEntity(points, closed: false);
        }

        var height = PositiveOrDefault(GetDouble(annotation.Data, 40, GetDouble(leaderRecord.Data, 40, 2.5)), 2.5);
        var style = EnsureDimensionStyle(document, GetString(leaderRecord.Data, 3, CadDimensionStyle.DefaultName));
        return new LeaderEntity(points, text, height, style);
    }

    private static BlockReferenceEntity? ParseInsert(
        IReadOnlyList<DxfPair> record,
        IReadOnlyList<DxfEntityRecord> attributes,
        CadDocument document,
        List<string> warnings)
    {
        var name = GetString(record, 2, string.Empty);
        if (string.IsNullOrWhiteSpace(name) || !document.TryGetBlock(name, out var definition) || definition is null)
        {
            warnings.Add($"DXF INSERT references unavailable block '{name}' and was skipped.");
            return null;
        }

        var xScale = GetDouble(record, 41, 1);
        var yScale = GetDouble(record, 42, xScale);
        if (xScale <= 1e-9 || yScale <= 1e-9 || Math.Abs(xScale - yScale) > Math.Max(1e-8, Math.Max(Math.Abs(xScale), Math.Abs(yScale)) * 1e-8))
        {
            warnings.Add($"DXF INSERT '{name}' uses mirrored or non-uniform X/Y scale ({xScale}, {yScale}); UCAD's current block reference supports positive uniform scale only, so the reference was skipped.");
            return null;
        }

        var insertion = ReadPoint(record, 10, 20);
        var rotation = DegreesToRadians(GetDouble(record, 50, 0));
        var validTags = definition.AttributeDefinitions.Select(attribute => attribute.Tag).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var attribute in attributes)
        {
            var tag = GetString(attribute.Data, 2, string.Empty);
            if (string.IsNullOrWhiteSpace(tag)) continue;
            if (!validTags.Contains(tag))
            {
                warnings.Add($"DXF INSERT '{name}' attribute '{tag}' has no matching ATTDEF in the imported block and was ignored.");
                continue;
            }
            values[tag] = GetString(attribute.Data, 1, string.Empty);
        }

        return CadBlockFactory.CreateReference(definition, insertion, xScale, rotation, values);
    }

    private static string BuildBaselineDxf(IReadOnlyList<DxfPair> pairs, IReadOnlySet<string> linkedAnnotationHandles)
    {
        var output = new StringBuilder();
        string? section = null;
        for (var i = 0; i < pairs.Count; i++)
        {
            var pair = pairs[i];
            if (pair.Code == 0 && EqualsToken(pair.Value, "SECTION") && i + 1 < pairs.Count && pairs[i + 1].Code == 2)
            {
                WritePair(output, pair.Code, pair.Value);
                WritePair(output, pairs[i + 1].Code, pairs[i + 1].Value);
                section = pairs[i + 1].Value;
                i++;
                continue;
            }
            if (pair.Code == 0 && EqualsToken(pair.Value, "ENDSEC"))
            {
                WritePair(output, pair.Code, pair.Value);
                section = null;
                continue;
            }

            if (EqualsToken(section, "ENTITIES") && pair.Code == 0)
            {
                var type = pair.Value;
                var record = ReadRecord(pairs, i + 1, out var nextIndex);
                var handle = GetString(record, 5, string.Empty);
                var linkedAnnotation = (EqualsToken(type, "TEXT") || EqualsToken(type, "MTEXT")) && linkedAnnotationHandles.Contains(handle);
                if (!AdvancedEntityTypes.Contains(type) && !linkedAnnotation)
                {
                    WritePair(output, 0, type);
                    foreach (var data in record) WritePair(output, data.Code, data.Value);
                }
                i = nextIndex - 1;
                continue;
            }

            WritePair(output, pair.Code, pair.Value);
        }
        return output.ToString();
    }

    private static void WriteDimensionStyleTable(StringBuilder output, CadDocument document)
    {
        WritePair(output, 0, "TABLE");
        WritePair(output, 2, "DIMSTYLE");
        WritePair(output, 70, document.DimensionStyles.Count);
        foreach (var style in document.DimensionStyles)
        {
            WritePair(output, 0, "DIMSTYLE");
            WritePair(output, 2, style.Name);
            WritePair(output, 70, 0);
            WritePair(output, 3, style.Prefix + "<>" + style.Suffix);
            WritePair(output, 41, style.ArrowSize);
            WritePair(output, 140, style.TextHeight);
            WritePair(output, 271, style.Precision);
        }
        WritePair(output, 0, "ENDTAB");
    }

    private static void WriteBlocksSection(StringBuilder output, CadDocument document, List<string> warnings, ref long nextHandle)
    {
        if (document.Blocks.Count == 0) return;
        WritePair(output, 0, "SECTION");
        WritePair(output, 2, "BLOCKS");
        foreach (var block in document.Blocks)
        {
            WritePair(output, 0, "BLOCK");
            WritePair(output, 8, CadLayer.DefaultLayerName);
            WritePair(output, 2, block.Name);
            WritePair(output, 70, 0);
            WritePoint(output, 10, 20, block.BasePoint);
            WritePair(output, 30, 0);
            WritePair(output, 3, block.Name);
            WritePair(output, 1, string.Empty);

            foreach (var entity in block.Entities)
                WriteBlockDefinitionEntity(output, entity, document, warnings, ref nextHandle);
            foreach (var attribute in block.AttributeDefinitions)
                WriteAttributeDefinition(output, attribute);

            WritePair(output, 0, "ENDBLK");
            WritePair(output, 8, CadLayer.DefaultLayerName);
        }
        WritePair(output, 0, "ENDSEC");
    }

    private static void WriteBlockDefinitionEntity(
        StringBuilder output,
        ICadEntity entity,
        CadDocument document,
        List<string> warnings,
        ref long nextHandle)
    {
        if (entity is BlockReferenceEntity reference)
        {
            WriteBlockReference(output, reference, new CadEntityProperties(CadLayer.DefaultLayerName), document, warnings);
            return;
        }
        if (entity is LinearDimensionEntity linear)
        {
            WriteLinearDimension(output, linear, new CadEntityProperties(CadLayer.DefaultLayerName));
            return;
        }
        if (entity is AngularDimensionEntity angular)
        {
            WriteAngularDimension(output, angular, new CadEntityProperties(CadLayer.DefaultLayerName));
            return;
        }
        if (entity is LeaderEntity leader)
        {
            WriteLeader(output, leader, new CadEntityProperties(CadLayer.DefaultLayerName), ref nextHandle);
            return;
        }

        var temp = new CadDocument();
        temp.Add(entity, new CadEntityProperties(CadLayer.DefaultLayerName));
        var exported = CadDxfCodec.Export(temp);
        if (exported.HasWarnings)
        {
            foreach (var warning in exported.Warnings)
                warnings.Add($"DXF block definition entity {entity.GetType().Name}: {warning}");
        }
        foreach (var record in ReadEntityRecords(ParsePairs(exported.Content), "ENTITIES"))
            WriteRecord(output, record);
    }

    private static void WriteAttributeDefinition(StringBuilder output, CadBlockAttributeDefinition attribute)
    {
        WritePair(output, 0, "ATTDEF");
        WritePair(output, 8, CadLayer.DefaultLayerName);
        WritePoint(output, 10, 20, attribute.Position);
        WritePair(output, 30, 0);
        WritePair(output, 40, attribute.TextHeight);
        WritePair(output, 1, attribute.DefaultValue);
        WritePair(output, 3, attribute.Prompt);
        WritePair(output, 2, attribute.Tag);
        WritePair(output, 70, attribute.Constant ? 2 : 0);
        WritePair(output, 7, "Standard");
    }

    private static void WriteAdvancedEntities(StringBuilder output, CadDocument document, List<string> warnings, ref long nextHandle)
    {
        foreach (var entity in document.Entities)
        {
            var properties = document.GetEntityProperties(entity.Id);
            switch (entity)
            {
                case LinearDimensionEntity linear:
                    WriteLinearDimension(output, linear, properties);
                    break;
                case AngularDimensionEntity angular:
                    WriteAngularDimension(output, angular, properties);
                    break;
                case LeaderEntity leader:
                    WriteLeader(output, leader, properties, ref nextHandle);
                    break;
                case BlockReferenceEntity reference:
                    WriteBlockReference(output, reference, properties, document, warnings);
                    break;
            }
        }
    }

    private static void WriteLinearDimension(StringBuilder output, LinearDimensionEntity dimension, CadEntityProperties properties)
    {
        WritePair(output, 0, "DIMENSION");
        WriteEntityProperties(output, properties);
        WritePoint(output, 10, 20, dimension.DimensionLinePoint);
        WritePair(output, 30, 0);
        var endpoints = dimension.GetDimensionLineEndpoints();
        var textPoint = new CadPoint((endpoints.First.X + endpoints.Second.X) / 2.0, (endpoints.First.Y + endpoints.Second.Y) / 2.0);
        WritePoint(output, 11, 21, textPoint);
        WritePair(output, 31, 0);
        WritePair(output, 70, 1); // aligned dimension
        if (dimension.TextOverride is not null) WritePair(output, 1, dimension.TextOverride);
        WritePair(output, 3, dimension.StyleName);
        WritePoint(output, 13, 23, dimension.FirstExtensionPoint);
        WritePair(output, 33, 0);
        WritePoint(output, 14, 24, dimension.SecondExtensionPoint);
        WritePair(output, 34, 0);
    }

    private static void WriteAngularDimension(StringBuilder output, AngularDimensionEntity dimension, CadEntityProperties properties)
    {
        WritePair(output, 0, "DIMENSION");
        WriteEntityProperties(output, properties);
        WritePoint(output, 10, 20, dimension.ArcPoint);
        WritePair(output, 30, 0);
        WritePoint(output, 11, 21, dimension.GetArcMidpoint());
        WritePair(output, 31, 0);
        WritePair(output, 70, 5); // three-point angular dimension
        if (dimension.TextOverride is not null) WritePair(output, 1, dimension.TextOverride);
        WritePair(output, 3, dimension.StyleName);
        WritePoint(output, 13, 23, dimension.FirstRayPoint);
        WritePair(output, 33, 0);
        WritePoint(output, 14, 24, dimension.SecondRayPoint);
        WritePair(output, 34, 0);
        WritePoint(output, 15, 25, dimension.Vertex);
        WritePair(output, 35, 0);
    }

    private static void WriteLeader(StringBuilder output, LeaderEntity leader, CadEntityProperties properties, ref long nextHandle)
    {
        var leaderHandle = ToHandle(nextHandle++);
        var annotationHandle = ToHandle(nextHandle++);
        WritePair(output, 0, "LEADER");
        WritePair(output, 5, leaderHandle);
        WriteEntityProperties(output, properties);
        WritePair(output, 3, leader.StyleName);
        WritePair(output, 71, 1); // arrowhead enabled
        WritePair(output, 72, 0); // straight segments
        WritePair(output, 73, 0); // text annotation
        WritePair(output, 74, 0);
        WritePair(output, 75, 0);
        WritePair(output, 76, leader.Points.Count);
        foreach (var point in leader.Points)
        {
            WritePoint(output, 10, 20, point);
            WritePair(output, 30, 0);
        }
        WritePair(output, 40, leader.TextHeight);
        WritePair(output, 340, annotationHandle);

        WritePair(output, 0, "MTEXT");
        WritePair(output, 5, annotationHandle);
        WriteEntityProperties(output, properties);
        WritePoint(output, 10, 20, leader.Points[^1]);
        WritePair(output, 30, 0);
        WritePair(output, 40, leader.TextHeight);
        WritePair(output, 41, Math.Max(leader.TextHeight * 4, leader.Text.Length * leader.TextHeight * 0.6));
        WritePair(output, 71, 1);
        WritePair(output, 7, "Standard");
        WriteMTextContent(output, leader.Text);
    }

    private static void WriteBlockReference(
        StringBuilder output,
        BlockReferenceEntity reference,
        CadEntityProperties properties,
        CadDocument document,
        List<string> warnings)
    {
        if (!document.TryGetBlock(reference.DefinitionName, out var definition) || definition is null)
        {
            warnings.Add($"DXF INSERT '{reference.DefinitionName}' could not be exported because its block definition is absent.");
            return;
        }

        WritePair(output, 0, "INSERT");
        WriteEntityProperties(output, properties);
        WritePair(output, 2, reference.DefinitionName);
        WritePoint(output, 10, 20, reference.InsertionPoint);
        WritePair(output, 30, 0);
        WritePair(output, 41, reference.Scale);
        WritePair(output, 42, reference.Scale);
        WritePair(output, 43, 1);
        WritePair(output, 50, NormalizeDegrees(RadiansToDegrees(reference.RotationRadians)));
        if (reference.AttributeValues.Count == 0) return;

        WritePair(output, 66, 1);
        foreach (var attribute in definition.AttributeDefinitions)
        {
            if (!reference.AttributeValues.TryGetValue(attribute.Tag, out var value)) value = attribute.DefaultValue;
            var position = TransformBlockPoint(attribute.Position, definition.BasePoint, reference.InsertionPoint, reference.Scale, reference.RotationRadians);
            WritePair(output, 0, "ATTRIB");
            WriteEntityProperties(output, properties);
            WritePoint(output, 10, 20, position);
            WritePair(output, 30, 0);
            WritePair(output, 40, attribute.TextHeight * reference.Scale);
            WritePair(output, 1, value);
            WritePair(output, 2, attribute.Tag);
            WritePair(output, 70, attribute.Constant ? 2 : 0);
            WritePair(output, 7, "Standard");
        }
        WritePair(output, 0, "SEQEND");
        WritePair(output, 8, properties.LayerName);
    }

    private static CadPoint TransformBlockPoint(CadPoint point, CadPoint basePoint, CadPoint insertion, double scale, double rotation)
    {
        var x = (point.X - basePoint.X) * scale;
        var y = (point.Y - basePoint.Y) * scale;
        var cos = Math.Cos(rotation);
        var sin = Math.Sin(rotation);
        return new CadPoint(
            insertion.X + (x * cos) - (y * sin),
            insertion.Y + (x * sin) + (y * cos));
    }

    private static CadEntityProperties ParseEntityProperties(IReadOnlyList<DxfPair> record, CadDocument document)
    {
        var layerName = GetString(record, 8, CadLayer.DefaultLayerName);
        EnsureLayer(document, layerName);
        var trueColor = GetInt(record, 420, -1);
        var color = trueColor >= 0 ? $"#{(trueColor & 0xFFFFFF):X6}" : null;
        var rawLineWeight = GetInt(record, 370, -1);
        var lineWeight = rawLineWeight > 0 ? rawLineWeight / 100.0 : null;
        var lineType = GetString(record, 6, "ByLayer");
        return new CadEntityProperties(layerName, color, lineWeight, lineType);
    }

    private static void WriteEntityProperties(StringBuilder output, CadEntityProperties properties)
    {
        WritePair(output, 8, properties.LayerName);
        if (properties.ColorHex is not null) WritePair(output, 420, int.Parse(properties.ColorHex.AsSpan(1), NumberStyles.HexNumber, Invariant));
        if (properties.LineWeight is not null) WritePair(output, 370, Math.Max(1, (int)Math.Round(properties.LineWeight.Value * 100, MidpointRounding.AwayFromZero)));
        if (!string.Equals(properties.LineType, "ByLayer", StringComparison.OrdinalIgnoreCase)) WritePair(output, 6, properties.LineType);
    }

    private static void EnsureLayer(CadDocument document, string layerName)
    {
        if (!document.TryGetLayer(layerName, out _)) document.CreateLayer(new CadLayer(layerName));
    }

    private static string EnsureDimensionStyle(CadDocument document, string styleName)
    {
        var normalized = string.IsNullOrWhiteSpace(styleName) ? CadDimensionStyle.DefaultName : styleName.Trim();
        if (!document.TryGetDimensionStyle(normalized, out _))
            document.DefineDimensionStyle(new CadDimensionStyle(normalized));
        return document.GetDimensionStyle(normalized).Name;
    }

    private static string BuildSingleEntityDxf(string type, IReadOnlyList<DxfPair> record)
    {
        var sb = new StringBuilder();
        WritePair(sb, 0, "SECTION");
        WritePair(sb, 2, "ENTITIES");
        WritePair(sb, 0, type);
        foreach (var pair in record) WritePair(sb, pair.Code, pair.Value);
        WritePair(sb, 0, "ENDSEC");
        WritePair(sb, 0, "EOF");
        return sb.ToString();
    }

    private static IReadOnlyList<DxfEntityRecord> ReadEntityRecords(IReadOnlyList<DxfPair> pairs, string requestedSection)
    {
        var records = new List<DxfEntityRecord>();
        string? section = null;
        for (var i = 0; i < pairs.Count; i++)
        {
            var pair = pairs[i];
            if (pair.Code == 0 && EqualsToken(pair.Value, "SECTION") && i + 1 < pairs.Count && pairs[i + 1].Code == 2)
            {
                section = pairs[++i].Value;
                continue;
            }
            if (pair.Code == 0 && EqualsToken(pair.Value, "ENDSEC"))
            {
                section = null;
                continue;
            }
            if (!EqualsToken(section, requestedSection) || pair.Code != 0) continue;
            var data = ReadRecord(pairs, i + 1, out var nextIndex);
            records.Add(new DxfEntityRecord(pair.Value, data));
            i = nextIndex - 1;
        }
        return records;
    }

    private static IReadOnlyList<DxfPair> ReadRecord(IReadOnlyList<DxfPair> pairs, int start, out int nextIndex)
    {
        var record = new List<DxfPair>();
        var i = start;
        while (i < pairs.Count && pairs[i].Code != 0) record.Add(pairs[i++]);
        nextIndex = i;
        return record;
    }

    private static IReadOnlyList<DxfPair> ParsePairs(string text)
    {
        var pairs = new List<DxfPair>();
        using var reader = new StringReader(text);
        var lineNumber = 0;
        while (true)
        {
            var codeLine = reader.ReadLine();
            if (codeLine is null) break;
            lineNumber++;
            var valueLine = reader.ReadLine();
            if (valueLine is null) throw new FormatException($"DXF group code at line {lineNumber} has no value line.");
            lineNumber++;
            if (!int.TryParse(codeLine.Trim().TrimStart('\uFEFF'), NumberStyles.Integer, Invariant, out var code))
                throw new FormatException($"Invalid DXF group code '{codeLine}' at line {lineNumber - 1}.");
            pairs.Add(new DxfPair(code, valueLine.Trim()));
        }
        return pairs;
    }

    private static List<CadPoint> ReadRepeatedPoints(IReadOnlyList<DxfPair> record, int xCode, int yCode)
    {
        var points = new List<CadPoint>();
        double? x = null;
        foreach (var pair in record)
        {
            if (pair.Code == xCode) x = ParseDouble(pair.Value, xCode);
            else if (pair.Code == yCode && x is not null)
            {
                points.Add(new CadPoint(x.Value, ParseDouble(pair.Value, yCode)));
                x = null;
            }
        }
        return points;
    }

    private static CadPoint ReadPoint(IReadOnlyList<DxfPair> record, int xCode, int yCode) =>
        new(RequiredDouble(record, xCode), RequiredDouble(record, yCode));

    private static CadPoint FartherPoint(CadPoint origin, CadPoint first, CadPoint second) =>
        DistanceSquared(origin, first) >= DistanceSquared(origin, second) ? first : second;

    private static double DistanceSquared(CadPoint first, CadPoint second)
    {
        var dx = second.X - first.X;
        var dy = second.Y - first.Y;
        return (dx * dx) + (dy * dy);
    }

    private static bool TryLineIntersection(CadPoint a1, CadPoint a2, CadPoint b1, CadPoint b2, out CadPoint point)
    {
        var adx = a2.X - a1.X;
        var ady = a2.Y - a1.Y;
        var bdx = b2.X - b1.X;
        var bdy = b2.Y - b1.Y;
        var denominator = (adx * bdy) - (ady * bdx);
        if (Math.Abs(denominator) <= 1e-12)
        {
            point = default;
            return false;
        }
        var dx = b1.X - a1.X;
        var dy = b1.Y - a1.Y;
        var t = ((dx * bdy) - (dy * bdx)) / denominator;
        point = new CadPoint(a1.X + (t * adx), a1.Y + (t * ady));
        return true;
    }

    private static string ReadAnnotationText(DxfEntityRecord annotation)
    {
        if (EqualsToken(annotation.Type, "MTEXT"))
        {
            return string.Concat(annotation.Data.Where(pair => pair.Code is 1 or 3).Select(pair => pair.Value))
                .Replace("\\P", "\n", StringComparison.OrdinalIgnoreCase);
        }
        return GetString(annotation.Data, 1, string.Empty);
    }

    private static string? GetNullableString(IReadOnlyList<DxfPair> record, int code) =>
        record.FirstOrDefault(pair => pair.Code == code)?.Value;

    private static string GetString(IReadOnlyList<DxfPair> record, int code, string fallback) =>
        record.FirstOrDefault(pair => pair.Code == code)?.Value ?? fallback;

    private static double RequiredDouble(IReadOnlyList<DxfPair> record, int code)
    {
        var pair = record.FirstOrDefault(candidate => candidate.Code == code);
        if (pair is null) throw new FormatException($"Required DXF group {code} is missing.");
        return ParseDouble(pair.Value, code);
    }

    private static double GetDouble(IReadOnlyList<DxfPair> record, int code, double fallback)
    {
        var pair = record.FirstOrDefault(candidate => candidate.Code == code);
        return pair is null ? fallback : ParseDouble(pair.Value, code);
    }

    private static int GetInt(IReadOnlyList<DxfPair> record, int code, int fallback)
    {
        var pair = record.FirstOrDefault(candidate => candidate.Code == code);
        return pair is not null && int.TryParse(pair.Value, NumberStyles.Integer, Invariant, out var value) ? value : fallback;
    }

    private static double ParseDouble(string value, int code)
    {
        if (!double.TryParse(value, NumberStyles.Float, Invariant, out var parsed) || !double.IsFinite(parsed))
            throw new FormatException($"DXF group {code} has invalid numeric value '{value}'.");
        return parsed;
    }

    private static void SplitDimensionPost(string post, out string prefix, out string suffix)
    {
        var marker = post.IndexOf("<>", StringComparison.Ordinal);
        if (marker < 0)
        {
            prefix = string.Empty;
            suffix = post;
            return;
        }
        prefix = post[..marker];
        suffix = post[(marker + 2)..];
    }

    private static string? NormalizeDimensionTextOverride(string? value) =>
        string.IsNullOrWhiteSpace(value) || string.Equals(value, "<>", StringComparison.Ordinal) ? null : value;

    private static double PositiveOrDefault(double value, double fallback) =>
        double.IsFinite(value) && value > 0 ? value : fallback;

    private static double AngularDistanceModuloPi(double first, double second)
    {
        var delta = Math.Abs((first - second) % Math.PI);
        return Math.Min(delta, Math.PI - delta);
    }

    private static bool IsLayoutBlockName(string name) =>
        name.StartsWith("*MODEL_SPACE", StringComparison.OrdinalIgnoreCase) ||
        name.StartsWith("*PAPER_SPACE", StringComparison.OrdinalIgnoreCase);

    private static bool IsBaselineAdvancedSkipWarning(string warning) =>
        warning.Contains(nameof(LinearDimensionEntity), StringComparison.Ordinal) ||
        warning.Contains(nameof(AngularDimensionEntity), StringComparison.Ordinal) ||
        warning.Contains(nameof(LeaderEntity), StringComparison.Ordinal) ||
        warning.Contains(nameof(BlockReferenceEntity), StringComparison.Ordinal);

    private static void WriteRecord(StringBuilder output, DxfEntityRecord record)
    {
        WritePair(output, 0, record.Type);
        foreach (var pair in record.Data) WritePair(output, pair.Code, pair.Value);
    }

    private static void WritePoint(StringBuilder output, int xCode, int yCode, CadPoint point)
    {
        WritePair(output, xCode, point.X);
        WritePair(output, yCode, point.Y);
    }

    private static void WriteMTextContent(StringBuilder output, string text)
    {
        var encoded = text
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace("\r", "\n", StringComparison.Ordinal)
            .Replace("\n", "\\P", StringComparison.Ordinal);
        const int chunkSize = 250;
        var offset = 0;
        while (encoded.Length - offset > chunkSize)
        {
            WritePair(output, 3, encoded.Substring(offset, chunkSize));
            offset += chunkSize;
        }
        WritePair(output, 1, encoded[offset..]);
    }

    private static void WritePair(StringBuilder output, int code, object value)
    {
        output.AppendLine(code.ToString(Invariant));
        output.AppendLine(value switch
        {
            double number => number.ToString("0.###############", Invariant),
            float number => number.ToString("0.###############", Invariant),
            IFormattable formattable => formattable.ToString(null, Invariant),
            _ => value.ToString() ?? string.Empty
        });
    }

    private static string ToHandle(long value) => value.ToString("X", Invariant);
    private static double DegreesToRadians(double degrees) => degrees * Math.PI / 180.0;
    private static double RadiansToDegrees(double radians) => radians * 180.0 / Math.PI;
    private static double NormalizeDegrees(double degrees)
    {
        var normalized = degrees % 360.0;
        return normalized < 0 ? normalized + 360.0 : normalized;
    }
    private static bool EqualsToken(string? left, string right) => string.Equals(left, right, StringComparison.OrdinalIgnoreCase);

    private sealed record DxfPair(int Code, string Value);
    private sealed record DxfEntityRecord(string Type, IReadOnlyList<DxfPair> Data);
}

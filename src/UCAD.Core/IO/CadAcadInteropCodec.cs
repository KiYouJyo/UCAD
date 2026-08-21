using System.Globalization;
using System.Text;
using ACadSharp;
using ACadSharp.IO;
using AcadDocument = ACadSharp.CadDocument;
using AcadDimension = ACadSharp.Entities.Dimension;
using AcadDimensionAligned = ACadSharp.Entities.DimensionAligned;
using AcadDimensionAngular3Pt = ACadSharp.Entities.DimensionAngular3Pt;
using AcadDimensionRadius = ACadSharp.Entities.DimensionRadius;
using AcadDimensionDiameter = ACadSharp.Entities.DimensionDiameter;
using AcadDimensionStyle = ACadSharp.Tables.DimensionStyle;
using IxDxfFile = IxMilia.Dxf.DxfFile;
using UcadDocument = UCAD.Core.CadDocument;

namespace UCAD.Core.IO;

/// <summary>
/// AutoCAD transport bridge. ACadSharp owns DWG transport and provides a native
/// semantic recovery pass for both DWG and DXF; IxMilia.Dxf owns text/binary DXF
/// normalization; UCAD's full semantic bridge remains the authoritative conversion
/// into the editable document model.
/// </summary>
public static class CadAcadInteropCodec
{
    private static readonly UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);

    static CadAcadInteropCodec() => Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

    public static CadAcadImportResult ImportDwg(ReadOnlyMemory<byte> content, string sourceExtension = ".dwg")
    {
        if (content.IsEmpty) throw new ArgumentException("DWG content cannot be empty.", nameof(content));
        var warnings = new List<string>();
        using var input = new MemoryStream(content.ToArray(), writable: false);
        using var reader = new DwgReader(input);
        reader.OnNotification += (_, args) => AddNotification(warnings, "DWG read", args);
        var acadDocument = reader.Read();

        var bridgeText = WriteAsciiDxfBridge(acadDocument, warnings);
        var imported = CadDxfFullInteropCodec.Import(bridgeText);
        AppendWarnings(warnings, "UCAD DXF bridge", imported.Warnings);
        CadAcadDwgSemanticRepair.Apply(acadDocument, imported.Document, warnings);
        CadAcadDimensionDisplayRepair.Apply(acadDocument, imported.Document, warnings);
        CadAcadMLineDisplayRepair.Apply(acadDocument, imported.Document, warnings);
        CadAcadToleranceDisplayRepair.Apply(acadDocument, imported.Document, warnings);
        CadAcadWipeoutDisplayRepair.Apply(acadDocument, imported.Document, warnings);
        CadAcadInsertDisplayRepair.Apply(acadDocument, imported.Document, warnings);
        CadAcadLayoutInterop.Import(acadDocument, imported.Document, warnings);
        CadDxfSourceOrderRepair.Apply(bridgeText, imported.Document);
        imported.Document.ResetHistory();
        return new CadAcadImportResult(imported.Document, warnings, NormalizeExtension(sourceExtension), acadDocument.Header.Version.ToString());
    }

    public static CadAcadImportResult ImportDxf(ReadOnlyMemory<byte> content, string sourceExtension = ".dxf")
    {
        if (content.IsEmpty) throw new ArgumentException("DXF content cannot be empty.", nameof(content));
        var warnings = new List<string>();

        // Keep a second, native semantic view of the original DXF. IxMilia remains the
        // normalization transport because it is robust across text/binary DXF, while
        // ACadSharp can recover richer AutoCAD-native entities such as MLEADER and
        // dimension contexts that a normalized generic bridge may otherwise downgrade.
        AcadDocument? acadDocument = null;
        try
        {
            using var semanticInput = new MemoryStream(content.ToArray(), writable: false);
            using var semanticReader = CreateDxfReaderWithDefaults(semanticInput, warnings, "DXF native read");
            acadDocument = semanticReader.Read();
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or ArgumentException or InvalidOperationException or NotSupportedException or ACadSharp.Exceptions.DxfException)
        {
            warnings.Add($"DXF native semantic recovery was unavailable; normalized UCAD import remains active. {ex.Message}");
        }

        using var input = new MemoryStream(content.ToArray(), writable: false);
        var dxfFile = IxDxfFile.Load(input);
        using var normalized = new MemoryStream();
        dxfFile.Save(normalized, asText: true);
        var bridgeText = Utf8NoBom.GetString(normalized.ToArray());
        var imported = CadDxfFullInteropCodec.Import(bridgeText);
        AppendWarnings(warnings, "UCAD DXF bridge", imported.Warnings);

        if (acadDocument is not null)
        {
            CadAcadDwgSemanticRepair.Apply(acadDocument, imported.Document, warnings);
            CadAcadDimensionDisplayRepair.Apply(acadDocument, imported.Document, warnings);
            CadAcadMLineDisplayRepair.Apply(acadDocument, imported.Document, warnings);
            CadAcadToleranceDisplayRepair.Apply(acadDocument, imported.Document, warnings);
            CadAcadWipeoutDisplayRepair.Apply(acadDocument, imported.Document, warnings);
            CadAcadInsertDisplayRepair.Apply(acadDocument, imported.Document, warnings);
            CadAcadLayoutInterop.Import(acadDocument, imported.Document, warnings);
        }

        CadDxfSourceOrderRepair.Apply(bridgeText, imported.Document);
        imported.Document.ResetHistory();
        return new CadAcadImportResult(imported.Document, warnings, NormalizeExtension(sourceExtension), dxfFile.Header.Version.ToString());
    }

    public static CadAcadBinaryExportResult ExportDwg(UcadDocument document, string targetExtension = ".dwg")
    {
        ArgumentNullException.ThrowIfNull(document);
        var extension = NormalizeExtension(targetExtension);
        if (extension is not ".dwg" and not ".dwt") throw new NotSupportedException($"UCAD does not export DWG transport bytes as '{extension}'.");

        var warnings = new List<string>();
        var acadDocument = BuildAcadDocument(document, warnings);
        CadAcadLayoutInterop.Export(document, acadDocument, warnings);
        InjectNativeDimensions(document, acadDocument);
        PrepareAcadSharpDimensionsForDwgWriter(acadDocument, warnings);
        NormalizeAcadSharpSequencesForDwgWriter(acadDocument);

        using var output = new MemoryStream();
        using (var writer = new DwgWriter(output, acadDocument))
        {
            writer.OnNotification += (_, args) => AddNotification(warnings, "DWG write", args);
            writer.Write();
        }
        return new CadAcadBinaryExportResult(output.ToArray(), warnings, extension, acadDocument.Header.Version.ToString());
    }

    public static CadAcadBinaryExportResult ExportBinaryDxf(UcadDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        var warnings = new List<string>();
        var dxf = CadDxfFullInteropCodec.Export(document);
        AppendWarnings(warnings, "UCAD DXF export", dxf.Warnings);
        using var textInput = new MemoryStream(Utf8NoBom.GetBytes(dxf.Content), writable: false);
        var dxfFile = IxDxfFile.Load(textInput, Utf8NoBom);
        using var output = new MemoryStream();
        dxfFile.Save(output, asText: false);
        return new CadAcadBinaryExportResult(output.ToArray(), warnings, ".dxf", dxfFile.Header.Version.ToString());
    }

    private static AcadDocument BuildAcadDocument(UcadDocument document, List<string> warnings)
    {
        var dxf = CadDxfFullInteropCodec.Export(document);
        AppendWarnings(warnings, "UCAD DXF export", dxf.Warnings);
        using var input = new MemoryStream(Utf8NoBom.GetBytes(dxf.Content), writable: false);
        using var reader = CreateDxfReaderWithDefaults(input, warnings, "DXF bridge read");
        return reader.Read();
    }

    private static void InjectNativeDimensions(UcadDocument source, AcadDocument target)
    {
        foreach (var existing in target.Entities.OfType<AcadDimension>().ToArray()) target.Entities.Remove(existing);
        foreach (var entity in source.Entities)
        {
            AcadDimension? dimension = entity switch
            {
                UCAD.Core.Entities.LinearDimensionEntity linear => new AcadDimensionAligned
                {
                    FirstPoint = ToAcadPoint(linear.FirstExtensionPoint),
                    SecondPoint = ToAcadPoint(linear.SecondExtensionPoint),
                    DefinitionPoint = ToAcadPoint(linear.DimensionLinePoint),
                    Text = linear.TextOverride,
                    Style = ResolveAcadDimensionStyle(target, linear.StyleName)
                },
                UCAD.Core.Entities.AngularDimensionEntity angular => new AcadDimensionAngular3Pt
                {
                    AngleVertex = ToAcadPoint(angular.Vertex),
                    FirstPoint = ToAcadPoint(angular.FirstRayPoint),
                    SecondPoint = ToAcadPoint(angular.SecondRayPoint),
                    DefinitionPoint = ToAcadPoint(angular.ArcPoint),
                    Text = angular.TextOverride,
                    Style = ResolveAcadDimensionStyle(target, angular.StyleName)
                },
                UCAD.Core.Entities.RadialDimensionEntity radial when !radial.Diameter => new AcadDimensionRadius
                {
                    DefinitionPoint = ToAcadPoint(radial.Center),
                    AngleVertex = ToAcadPoint(radial.PointOnCircle),
                    TextMiddlePoint = ToAcadPoint(radial.TextPoint),
                    LeaderLength = (radial.PointOnCircle - radial.Center).Length,
                    Text = radial.TextOverride,
                    Style = ResolveAcadDimensionStyle(target, radial.StyleName)
                },
                UCAD.Core.Entities.RadialDimensionEntity radial => CreateDiameterDimension(radial, target),
                _ => null
            };
            if (dimension is null) continue;
            var properties = source.GetEntityProperties(entity.Id);
            if (target.Layers.FirstOrDefault(layer => string.Equals(layer.Name, properties.LayerName, StringComparison.OrdinalIgnoreCase)) is { } layer) dimension.Layer = layer;
            target.Entities.Add(dimension);
        }
    }

    private static AcadDimensionDiameter CreateDiameterDimension(UCAD.Core.Entities.RadialDimensionEntity radial, AcadDocument target)
    {
        var dx = radial.PointOnCircle.X - radial.Center.X;
        var dy = radial.PointOnCircle.Y - radial.Center.Y;
        return new AcadDimensionDiameter
        {
            DefinitionPoint = new CSMath.XYZ(radial.Center.X - dx, radial.Center.Y - dy, 0),
            AngleVertex = ToAcadPoint(radial.PointOnCircle),
            TextMiddlePoint = ToAcadPoint(radial.TextPoint),
            LeaderLength = Math.Sqrt(dx * dx + dy * dy) * 2,
            Text = radial.TextOverride,
            Style = ResolveAcadDimensionStyle(target, radial.StyleName)
        };
    }

    private static AcadDimensionStyle ResolveAcadDimensionStyle(AcadDocument document, string styleName)
    {
        var normalized = string.IsNullOrWhiteSpace(styleName) ? AcadDimensionStyle.DefaultName : styleName.Trim();
        var existing = document.DimensionStyles.FirstOrDefault(style => string.Equals(style.Name, normalized, StringComparison.OrdinalIgnoreCase));
        if (existing is not null) return existing;
        var created = new AcadDimensionStyle(normalized);
        document.DimensionStyles.Add(created);
        return created;
    }

    private static CSMath.XYZ ToAcadPoint(UCAD.Core.Geometry.CadPoint point) => new(point.X, point.Y, 0.0);

    private static void PrepareAcadSharpDimensionsForDwgWriter(AcadDocument document, List<string> warnings)
    {
        foreach (var dimension in document.Entities.OfType<AcadDimension>())
        {
            try { dimension.UpdateBlock(); }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or NotSupportedException)
            { warnings.Add($"DWG dimension block generation for {dimension.GetType().Name} failed: {ex.Message}"); }
        }
    }

    private static void NormalizeAcadSharpSequencesForDwgWriter(AcadDocument document)
    {
        foreach (var block in document.BlockRecords)
        {
            var standaloneSequenceEnds = block.Entities.OfType<ACadSharp.Entities.Seqend>().ToArray();
            foreach (var sequenceEnd in standaloneSequenceEnds) block.Entities.Remove(sequenceEnd);
        }
    }

    private static DxfReader CreateDxfReaderWithDefaults(Stream input, List<string> warnings, string phase)
    {
        var reader = new DxfReader(input);
        reader.Configuration.CreateDefaults = true;
        reader.OnNotification += (_, args) => AddNotification(warnings, phase, args);
        return reader;
    }

    private static string WriteAsciiDxfBridge(AcadDocument acadDocument, List<string> warnings)
    {
        using var output = new MemoryStream();
        using (var writer = new DxfWriter(output, acadDocument, binary: false))
        {
            writer.OnNotification += (_, args) => AddNotification(warnings, "DXF bridge write", args);
            writer.Write();
        }
        return GetDxfTextEncoding(acadDocument, warnings).GetString(output.ToArray());
    }

    private static Encoding GetDxfTextEncoding(AcadDocument document, List<string> warnings)
    {
        if (document.Header.Version >= ACadVersion.AC1021) return Utf8NoBom;
        try { return Encoding.GetEncoding(ResolveCodePage(document.Header.CodePage)); }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException)
        {
            warnings.Add($"DXF bridge: code page '{document.Header.CodePage}' is unavailable; Windows-1252 fallback was used. {ex.Message}");
            return Encoding.GetEncoding(1252);
        }
    }

    private static int ResolveCodePage(string? codePage)
    {
        if (string.IsNullOrWhiteSpace(codePage)) return 1252;
        var normalized = codePage.Trim().ToLowerInvariant().Replace('-', '_');
        return normalized switch
        {
            "utf_8" or "utf8" => 65001, "ascii" or "us_ascii" => 20127,
            "gb2312" or "ansi_936" or "ansi936" => 936, "big5" or "ansi_950" or "ansi950" => 950,
            "kcs5601" or "ks_c_5601_1987" or "ansi_949" or "ansi949" => 949, "johab" or "ansi_1361" or "ansi1361" => 1361,
            "shift_jis" or "sjis" or "ansi_932" or "ansi932" or "dos932" => 932,
            "ansi_874" or "ansi874" => 874, "ansi_1250" or "ansi1250" => 1250, "ansi_1251" or "ansi1251" => 1251,
            "ansi_1252" or "ansi1252" => 1252, "ansi_1253" or "ansi1253" => 1253, "ansi_1254" or "ansi1254" => 1254,
            "ansi_1255" or "ansi1255" => 1255, "ansi_1256" or "ansi1256" => 1256, "ansi_1257" or "ansi1257" => 1257,
            "ansi_1258" or "ansi1258" => 1258, "dos437" => 437, "dos720" => 720, "dos737" => 737, "dos775" => 775,
            "dos850" => 850, "dos852" => 852, "dos855" => 855, "dos857" => 857, "dos858" => 858, "dos860" => 860,
            "dos861" => 861, "dos862" => 862, "dos863" => 863, "dos864" => 864, "dos865" => 865, "dos866" => 866, "dos869" => 869,
            _ => TryResolveNumericCodePage(normalized)
        };
    }

    private static int TryResolveNumericCodePage(string normalized)
    {
        var digits = new string(normalized.Where(char.IsDigit).ToArray());
        if (digits.Length > 0 && int.TryParse(digits, NumberStyles.None, CultureInfo.InvariantCulture, out var value) && value > 0) return value;
        throw new NotSupportedException($"Unknown AutoCAD code page '{normalized}'.");
    }

    private static void AddNotification(List<string> warnings, string phase, NotificationEventArgs args)
    {
        if (string.IsNullOrWhiteSpace(args.Message)) return;
        var message = $"{phase}: {args.Message}";
        if (!warnings.Contains(message, StringComparer.Ordinal)) warnings.Add(message);
    }

    private static void AppendWarnings(List<string> destination, string phase, IEnumerable<string> warnings)
    {
        foreach (var warning in warnings)
        {
            var message = $"{phase}: {warning}";
            if (!destination.Contains(message, StringComparer.Ordinal)) destination.Add(message);
        }
    }

    private static string NormalizeExtension(string extension)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(extension);
        return extension.StartsWith('.') ? extension.ToLowerInvariant() : "." + extension.ToLowerInvariant();
    }
}

public sealed record CadAcadImportResult(UcadDocument Document, IReadOnlyList<string> Warnings, string SourceExtension, string SourceCadVersion)
{
    public bool HasWarnings => Warnings.Count > 0;
}
public sealed record CadAcadBinaryExportResult(byte[] Content, IReadOnlyList<string> Warnings, string TargetExtension, string TargetCadVersion)
{
    public bool HasWarnings => Warnings.Count > 0;
}

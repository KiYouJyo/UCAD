namespace UCAD.Core.IO;

[Flags]
public enum CadFileFormatCapabilities
{
    None = 0,
    Recognized = 1 << 0,
    Open = 1 << 1,
    Import = 1 << 2,
    Export = 1 << 3,
    Template = 1 << 4,
    Recovery = 1 << 5,
    Published = 1 << 6,
    Resource = 1 << 7,
    Automation = 1 << 8,
    SheetSet = 1 << 9,
    Plugin = 1 << 10,
    Exchange = 1 << 11
}

public enum CadFileFormatFamily
{
    UcadNative,
    AutoCadDrawing,
    AutoCadPublished,
    AutoCadSheetSet,
    AutoCadResource,
    AutoCadAutomation,
    AutoCadPlugin,
    AutoCadExchange
}

public sealed record CadFileFormatDescriptor(
    string Extension,
    string DisplayName,
    CadFileFormatFamily Family,
    CadFileFormatCapabilities Capabilities,
    string Transport,
    string SupportNote)
{
    public bool CanOpen => Capabilities.HasFlag(CadFileFormatCapabilities.Open);

    public bool CanImport => Capabilities.HasFlag(CadFileFormatCapabilities.Import);

    public bool CanExport => Capabilities.HasFlag(CadFileFormatCapabilities.Export);
}

/// <summary>
/// Central file-format registry for UCAD's AutoCAD interoperability work.
/// A format is only marked Open/Import/Export after a real transport exists;
/// formats that are merely recognized stay explicit so the UI never implies
/// compatibility that the Core cannot provide yet.
/// </summary>
public static class CadAcadFileFormatRegistry
{
    private static readonly IReadOnlyList<CadFileFormatDescriptor> FormatsInternal =
    [
        new(CadNativeDocumentCodec.FileExtension, "UCAD Drawing", CadFileFormatFamily.UcadNative,
            CadFileFormatCapabilities.Recognized | CadFileFormatCapabilities.Open | CadFileFormatCapabilities.Import | CadFileFormatCapabilities.Export,
            "UCAD native JSON", "Full-fidelity UCAD authoring document."),

        // AutoCAD drawing containers.
        new(".dwg", "AutoCAD Drawing", CadFileFormatFamily.AutoCadDrawing,
            CadFileFormatCapabilities.Recognized | CadFileFormatCapabilities.Open | CadFileFormatCapabilities.Import | CadFileFormatCapabilities.Export,
            "ACadSharp DWG ↔ DXF bridge", "Read/write transport is available; UCAD entity fidelity is bounded by the current DXF bridge."),
        new(".dxf", "Drawing Exchange Format", CadFileFormatFamily.AutoCadDrawing,
            CadFileFormatCapabilities.Recognized | CadFileFormatCapabilities.Open | CadFileFormatCapabilities.Import | CadFileFormatCapabilities.Export,
            "UCAD ASCII DXF + ACadSharp DXF", "ASCII DXF is native to UCAD; ACadSharp normalizes binary and legacy DXF before import."),
        new(".dwt", "AutoCAD Drawing Template", CadFileFormatFamily.AutoCadDrawing,
            CadFileFormatCapabilities.Recognized | CadFileFormatCapabilities.Open | CadFileFormatCapabilities.Import | CadFileFormatCapabilities.Export | CadFileFormatCapabilities.Template,
            "DWG-compatible container", "Opened as a drawing template source; template-only AutoCAD metadata may not round-trip yet."),
        new(".dws", "AutoCAD Standards File", CadFileFormatFamily.AutoCadDrawing,
            CadFileFormatCapabilities.Recognized | CadFileFormatCapabilities.Open | CadFileFormatCapabilities.Import,
            "DWG-compatible container", "Geometry/table content can be imported; CAD-standards semantics are not yet authored by UCAD."),
        new(".bak", "AutoCAD Drawing Backup", CadFileFormatFamily.AutoCadDrawing,
            CadFileFormatCapabilities.Recognized | CadFileFormatCapabilities.Open | CadFileFormatCapabilities.Import | CadFileFormatCapabilities.Recovery,
            "DWG-compatible recovery container", "Opened as a recovery source and never overwritten automatically."),
        new(".sv$", "AutoCAD Autosave", CadFileFormatFamily.AutoCadDrawing,
            CadFileFormatCapabilities.Recognized | CadFileFormatCapabilities.Open | CadFileFormatCapabilities.Import | CadFileFormatCapabilities.Recovery,
            "DWG-compatible recovery container", "Opened as a recovery source and never overwritten automatically."),
        new(".dxb", "Drawing Interchange Binary", CadFileFormatFamily.AutoCadDrawing,
            CadFileFormatCapabilities.Recognized | CadFileFormatCapabilities.Open | CadFileFormatCapabilities.Import | CadFileFormatCapabilities.Export,
            "IxMilia DXB 1.0 geometry codec", "Legacy DXB 1.0 2D geometry is supported with explicit warnings for annotation, modern entities, 3D flattening and unsupported properties."),

        // Sheet sets and publishing data.
        new(".dst", "AutoCAD Sheet Set Data", CadFileFormatFamily.AutoCadSheetSet,
            CadFileFormatCapabilities.Recognized | CadFileFormatCapabilities.SheetSet,
            "Pending sheet-set adapter", "Recognized as AutoCAD Sheet Set Manager data; sheet-set editing is not enabled yet."),
        new(".dsd", "Drawing Set Description", CadFileFormatFamily.AutoCadSheetSet,
            CadFileFormatCapabilities.Recognized | CadFileFormatCapabilities.SheetSet | CadFileFormatCapabilities.Published,
            "Pending publish-set adapter", "Recognized as a saved PUBLISH drawing list."),
        new(".bp3", "Batch Plot List", CadFileFormatFamily.AutoCadSheetSet,
            CadFileFormatCapabilities.Recognized | CadFileFormatCapabilities.SheetSet | CadFileFormatCapabilities.Published,
            "Pending legacy batch-plot adapter", "Recognized as a legacy batch-plot sheet list."),
        new(".dwf", "Design Web Format", CadFileFormatFamily.AutoCadPublished,
            CadFileFormatCapabilities.Recognized | CadFileFormatCapabilities.Published,
            "Pending published-format adapter", "Recognized, but DWF import/export is not yet enabled."),
        new(".dwfx", "Design Web Format XPS", CadFileFormatFamily.AutoCadPublished,
            CadFileFormatCapabilities.Recognized | CadFileFormatCapabilities.Published,
            "Pending published-format adapter", "Recognized, but DWFx import/export is not yet enabled."),
        new(".ctb", "Color-dependent Plot Style", CadFileFormatFamily.AutoCadPublished,
            CadFileFormatCapabilities.Recognized | CadFileFormatCapabilities.Resource | CadFileFormatCapabilities.Published,
            "Pending plot-style adapter", "Reserved for plot style import/export."),
        new(".stb", "Named Plot Style", CadFileFormatFamily.AutoCadPublished,
            CadFileFormatCapabilities.Recognized | CadFileFormatCapabilities.Resource | CadFileFormatCapabilities.Published,
            "Pending plot-style adapter", "Reserved for plot style import/export."),
        new(".pc3", "Plotter Configuration", CadFileFormatFamily.AutoCadPublished,
            CadFileFormatCapabilities.Recognized | CadFileFormatCapabilities.Resource | CadFileFormatCapabilities.Published,
            "Pending plot configuration adapter", "Reserved for plot configuration import."),
        new(".pmp", "Plot Model Parameter", CadFileFormatFamily.AutoCadPublished,
            CadFileFormatCapabilities.Recognized | CadFileFormatCapabilities.Resource | CadFileFormatCapabilities.Published,
            "Pending plot configuration adapter", "Reserved for plot calibration and paper-size import."),
        new(".psf", "PostScript Font Substitution", CadFileFormatFamily.AutoCadPublished,
            CadFileFormatCapabilities.Recognized | CadFileFormatCapabilities.Resource | CadFileFormatCapabilities.Published,
            "Pending PostScript resource adapter", "Recognized as an AutoCAD plotting support file."),
        new(".pss", "PostScript Support File", CadFileFormatFamily.AutoCadPublished,
            CadFileFormatCapabilities.Recognized | CadFileFormatCapabilities.Resource | CadFileFormatCapabilities.Published,
            "Pending PostScript resource adapter", "Recognized as an AutoCAD plotting support file."),

        // AutoCAD support/customization resources.
        new(".pat", "Hatch Pattern", CadFileFormatFamily.AutoCadResource,
            CadFileFormatCapabilities.Recognized | CadFileFormatCapabilities.Import | CadFileFormatCapabilities.Export | CadFileFormatCapabilities.Resource,
            "UCAD PAT text codec", "Pattern geometry can be parsed and serialized safely; palette/document integration is a later step."),
        new(".lin", "Linetype Definition", CadFileFormatFamily.AutoCadResource,
            CadFileFormatCapabilities.Recognized | CadFileFormatCapabilities.Import | CadFileFormatCapabilities.Export | CadFileFormatCapabilities.Resource,
            "UCAD LIN text codec", "Simple and complex definition text can be migrated without partial reinterpretation."),
        new(".shx", "Compiled Shape/Font", CadFileFormatFamily.AutoCadResource,
            CadFileFormatCapabilities.Recognized | CadFileFormatCapabilities.Resource,
            "Pending font/shape adapter", "Recognized as an external resource; direct SHX execution/rendering is not enabled yet."),
        new(".fmp", "Font Mapping File", CadFileFormatFamily.AutoCadResource,
            CadFileFormatCapabilities.Recognized | CadFileFormatCapabilities.Resource,
            "Pending font-map adapter", "Reserved for AutoCAD font substitution mapping."),
        new(".dcl", "Dialog Control Language", CadFileFormatFamily.AutoCadResource,
            CadFileFormatCapabilities.Recognized | CadFileFormatCapabilities.Resource,
            "Pending DCL migration adapter", "Recognized for future dialog/customization migration; DCL UI execution is not claimed."),
        new(".pgp", "Program Parameters / Command Aliases", CadFileFormatFamily.AutoCadResource,
            CadFileFormatCapabilities.Recognized | CadFileFormatCapabilities.Import | CadFileFormatCapabilities.Export | CadFileFormatCapabilities.Resource,
            "UCAD safe PGP alias codec", "Only command-alias records are imported/exported; external-process definitions are intentionally ignored."),
        new(".unt", "Unit Definition", CadFileFormatFamily.AutoCadResource,
            CadFileFormatCapabilities.Recognized | CadFileFormatCapabilities.Resource,
            "Pending unit adapter", "Recognized as an AutoCAD support-path unit definition file."),
        new(".cfg", "AutoCAD Configuration", CadFileFormatFamily.AutoCadResource,
            CadFileFormatCapabilities.Recognized | CadFileFormatCapabilities.Resource,
            "Pending configuration adapter", "Recognized as a legacy/current support configuration resource."),
        new(".cuix", "Customization UI", CadFileFormatFamily.AutoCadResource,
            CadFileFormatCapabilities.Recognized | CadFileFormatCapabilities.Resource,
            "Pending customization adapter", "Recognized for future command/UI migration tooling."),
        new(".cui", "Legacy Customization UI", CadFileFormatFamily.AutoCadResource,
            CadFileFormatCapabilities.Recognized | CadFileFormatCapabilities.Resource,
            "Pending legacy customization adapter", "Recognized for migration of older AutoCAD customization files."),
        new(".mnu", "Legacy Menu Template", CadFileFormatFamily.AutoCadResource,
            CadFileFormatCapabilities.Recognized | CadFileFormatCapabilities.Resource,
            "Pending legacy menu adapter", "Recognized for migration of legacy menu definitions."),
        new(".mns", "Legacy Menu Source", CadFileFormatFamily.AutoCadResource,
            CadFileFormatCapabilities.Recognized | CadFileFormatCapabilities.Resource,
            "Pending legacy menu adapter", "Recognized for migration of legacy menu definitions."),
        new(".mnc", "Legacy Compiled Menu", CadFileFormatFamily.AutoCadResource,
            CadFileFormatCapabilities.Recognized | CadFileFormatCapabilities.Resource,
            "No source-level migration yet", "Recognized as a legacy compiled customization artifact."),
        new(".atc", "Tool Catalog / Tool Palette", CadFileFormatFamily.AutoCadResource,
            CadFileFormatCapabilities.Recognized | CadFileFormatCapabilities.Resource,
            "Pending tool-palette adapter", "Recognized for future tool-palette migration."),
        new(".arg", "AutoCAD Profile", CadFileFormatFamily.AutoCadResource,
            CadFileFormatCapabilities.Recognized | CadFileFormatCapabilities.Resource,
            "Pending profile adapter", "Recognized for future settings/profile migration."),
        new(".sld", "AutoCAD Slide", CadFileFormatFamily.AutoCadResource,
            CadFileFormatCapabilities.Recognized | CadFileFormatCapabilities.Resource,
            "Pending slide adapter", "Recognized as a legacy AutoCAD slide resource."),
        new(".slb", "AutoCAD Slide Library", CadFileFormatFamily.AutoCadResource,
            CadFileFormatCapabilities.Recognized | CadFileFormatCapabilities.Resource,
            "Pending slide-library adapter", "Recognized as a legacy AutoCAD slide library."),

        // Scripts and source-level automation.
        new(".scr", "AutoCAD Script", CadFileFormatFamily.AutoCadAutomation,
            CadFileFormatCapabilities.Recognized | CadFileFormatCapabilities.Automation,
            "Pending safe script adapter", "Recognized only; UCAD does not execute AutoCAD scripts yet."),
        new(".lsp", "AutoLISP Source", CadFileFormatFamily.AutoCadAutomation,
            CadFileFormatCapabilities.Recognized | CadFileFormatCapabilities.Automation,
            "Pending compatibility layer", "Recognized only; AutoLISP execution is outside the current runtime."),
        new(".mnl", "Menu AutoLISP Source", CadFileFormatFamily.AutoCadAutomation,
            CadFileFormatCapabilities.Recognized | CadFileFormatCapabilities.Automation,
            "Pending compatibility layer", "Recognized as AutoLISP loaded alongside a customization file."),
        new(".fas", "Compiled AutoLISP", CadFileFormatFamily.AutoCadAutomation,
            CadFileFormatCapabilities.Recognized | CadFileFormatCapabilities.Automation,
            "No compatible runtime", "Recognized only; compiled AutoLISP is not executable in UCAD."),
        new(".vlx", "Visual LISP Application", CadFileFormatFamily.AutoCadAutomation,
            CadFileFormatCapabilities.Recognized | CadFileFormatCapabilities.Automation,
            "No compatible runtime", "Recognized only; Visual LISP applications are not executable in UCAD."),
        new(".dvb", "VBA Project", CadFileFormatFamily.AutoCadAutomation,
            CadFileFormatCapabilities.Recognized | CadFileFormatCapabilities.Automation,
            "No embedded VBA runtime", "Recognized only; VBA project execution is not claimed."),
        new(".js", "AutoCAD JavaScript", CadFileFormatFamily.AutoCadAutomation,
            CadFileFormatCapabilities.Recognized | CadFileFormatCapabilities.Automation,
            "No AutoCAD JavaScript host", "Recognized only; execution requires a compatibility/security design."),
        new(".rx", "AutoCAD Application List", CadFileFormatFamily.AutoCadAutomation,
            CadFileFormatCapabilities.Recognized | CadFileFormatCapabilities.Automation,
            "Pending migration parser", "Recognized as an AutoCAD application-load list."),

        // Binary plug-in/runtime artifacts. These are identified, not executed.
        new(".arx", "ObjectARX Application", CadFileFormatFamily.AutoCadPlugin,
            CadFileFormatCapabilities.Recognized | CadFileFormatCapabilities.Plugin,
            "No AutoCAD binary ABI", "Recognized only; UCAD cannot load AutoCAD ObjectARX binaries."),
        new(".crx", "AutoCAD CRX Application", CadFileFormatFamily.AutoCadPlugin,
            CadFileFormatCapabilities.Recognized | CadFileFormatCapabilities.Plugin,
            "No AutoCAD binary ABI", "Recognized only; UCAD cannot load AutoCAD CRX binaries."),
        new(".dbx", "ObjectDBX Module", CadFileFormatFamily.AutoCadPlugin,
            CadFileFormatCapabilities.Recognized | CadFileFormatCapabilities.Plugin,
            "No AutoCAD binary ABI", "Recognized only; UCAD cannot load AutoCAD ObjectDBX modules."),
        new(".hdi", "AutoCAD Graphics Driver", CadFileFormatFamily.AutoCadPlugin,
            CadFileFormatCapabilities.Recognized | CadFileFormatCapabilities.Plugin,
            "No AutoCAD graphics-driver ABI", "Recognized only; HDI drivers are not loadable by UCAD."),
        new(".dll", "AutoCAD .NET / Native Module", CadFileFormatFamily.AutoCadPlugin,
            CadFileFormatCapabilities.Recognized | CadFileFormatCapabilities.Plugin,
            "No arbitrary module loading", "Generic DLLs are recognized only in the AutoCAD migration inventory and are never auto-executed."),

        // Interchange formats exposed by AutoCAD import/export workflows. These stay inventory-only here
        // until routed into a UCAD exchange adapter; several already have separate UCAD feature paths.
        new(".pdf", "PDF", CadFileFormatFamily.AutoCadExchange,
            CadFileFormatCapabilities.Recognized | CadFileFormatCapabilities.Exchange,
            "Existing UCAD PDF path; generic AutoCAD exchange routing pending", "Tracked because AutoCAD imports PDF geometry and publishes PDF."),
        new(".dgn", "MicroStation DGN", CadFileFormatFamily.AutoCadExchange,
            CadFileFormatCapabilities.Recognized | CadFileFormatCapabilities.Exchange,
            "Pending DGN adapter", "Tracked because AutoCAD imports/exports DGN."),
        new(".sat", "ACIS SAT", CadFileFormatFamily.AutoCadExchange,
            CadFileFormatCapabilities.Recognized | CadFileFormatCapabilities.Exchange,
            "Out of 2D authoring scope", "Tracked for AutoCAD exchange compatibility; ACIS solids are outside UCAD 1.x 2D authoring scope."),
        new(".igs", "IGES", CadFileFormatFamily.AutoCadExchange,
            CadFileFormatCapabilities.Recognized | CadFileFormatCapabilities.Exchange,
            "Out of 2D authoring scope", "Tracked for AutoCAD exchange inventory."),
        new(".iges", "IGES", CadFileFormatFamily.AutoCadExchange,
            CadFileFormatCapabilities.Recognized | CadFileFormatCapabilities.Exchange,
            "Out of 2D authoring scope", "Tracked for AutoCAD exchange inventory."),
        new(".stl", "STL", CadFileFormatFamily.AutoCadExchange,
            CadFileFormatCapabilities.Recognized | CadFileFormatCapabilities.Exchange,
            "Out of 2D authoring scope", "Tracked for AutoCAD export inventory."),
        new(".wmf", "Windows Metafile", CadFileFormatFamily.AutoCadExchange,
            CadFileFormatCapabilities.Recognized | CadFileFormatCapabilities.Exchange,
            "Pending vector exchange adapter", "Tracked because AutoCAD supports WMF import/export."),
        new(".bmp", "Bitmap", CadFileFormatFamily.AutoCadExchange,
            CadFileFormatCapabilities.Recognized | CadFileFormatCapabilities.Exchange,
            "Pending raster exchange routing", "Tracked because AutoCAD can export bitmap data."),
        new(".dxx", "Attribute Extract DXF", CadFileFormatFamily.AutoCadExchange,
            CadFileFormatCapabilities.Recognized | CadFileFormatCapabilities.Exchange,
            "Pending attribute-extract adapter", "Tracked as AutoCAD attribute-extract output.")
    ];

    private static readonly IReadOnlyDictionary<string, CadFileFormatDescriptor> ByExtension =
        FormatsInternal.ToDictionary(format => format.Extension, StringComparer.OrdinalIgnoreCase);

    public static IReadOnlyList<CadFileFormatDescriptor> Formats => FormatsInternal;

    public static IReadOnlyList<CadFileFormatDescriptor> OpenableDrawingFormats =>
        FormatsInternal
            .Where(format => format.Family is CadFileFormatFamily.UcadNative or CadFileFormatFamily.AutoCadDrawing)
            .Where(format => format.CanOpen)
            .ToArray();

    public static IReadOnlyList<CadFileFormatDescriptor> ExportableAutoCadDrawingFormats =>
        FormatsInternal
            .Where(format => format.Family == CadFileFormatFamily.AutoCadDrawing && format.CanExport)
            .ToArray();

    public static IReadOnlyList<CadFileFormatDescriptor> PendingFormats =>
        FormatsInternal
            .Where(format => format.Capabilities.HasFlag(CadFileFormatCapabilities.Recognized))
            .Where(format => !format.CanOpen && !format.CanImport && !format.CanExport)
            .ToArray();

    public static bool TryGetByPath(string path, out CadFileFormatDescriptor descriptor) =>
        TryGetByExtension(Path.GetExtension(path), out descriptor);

    public static bool TryGetByExtension(string? extension, out CadFileFormatDescriptor descriptor)
    {
        if (string.IsNullOrWhiteSpace(extension))
        {
            descriptor = null!;
            return false;
        }

        var normalized = extension.StartsWith('.') ? extension : "." + extension;
        return ByExtension.TryGetValue(normalized, out descriptor!);
    }

    public static CadFileFormatDescriptor GetRequiredByPath(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        if (TryGetByPath(path, out var descriptor)) return descriptor;
        throw new NotSupportedException($"File extension '{Path.GetExtension(path)}' is not registered by UCAD.");
    }
}

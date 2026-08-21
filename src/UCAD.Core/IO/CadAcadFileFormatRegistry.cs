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
/// Central capability registry. Open means a file can become an editable drawing;
/// Import/Export on resource/customization families means a real bounded migration
/// adapter exists and must not be confused with an executable AutoCAD runtime.
/// </summary>
public static class CadAcadFileFormatRegistry
{
    private const CadFileFormatCapabilities R = CadFileFormatCapabilities.Recognized;
    private const CadFileFormatCapabilities I = CadFileFormatCapabilities.Import;
    private const CadFileFormatCapabilities E = CadFileFormatCapabilities.Export;
    private const CadFileFormatCapabilities O = CadFileFormatCapabilities.Open;

    private static readonly IReadOnlyList<CadFileFormatDescriptor> FormatsInternal =
    [
        F(CadNativeDocumentCodec.FileExtension, "UCAD Drawing", CadFileFormatFamily.UcadNative, R|O|I|E, "UCAD native JSON", "Full-fidelity UCAD authoring document."),

        // Drawing containers.
        F(".dwg", "AutoCAD Drawing", CadFileFormatFamily.AutoCadDrawing, R|O|I|E, "ACadSharp DWG + UCAD semantic bridge", "Editable high-value 2D semantics with opaque original-source preservation."),
        F(".dxf", "Drawing Exchange Format", CadFileFormatFamily.AutoCadDrawing, R|O|I|E, "IxMilia DXF + UCAD semantic bridge", "ASCII/binary DXF entity semantics; advanced paper-layout objects remain bounded."),
        F(".dwt", "AutoCAD Drawing Template", CadFileFormatFamily.AutoCadDrawing, R|O|I|E|CadFileFormatCapabilities.Template, "DWG-compatible container", "DWG semantic/layout transport plus exact source envelope."),
        F(".dws", "AutoCAD Standards File", CadFileFormatFamily.AutoCadDrawing, R|O|I, "DWG-compatible container", "Geometry/tables are importable; standards-rule authoring is not claimed."),
        F(".bak", "AutoCAD Drawing Backup", CadFileFormatFamily.AutoCadDrawing, R|O|I|CadFileFormatCapabilities.Recovery, "DWG-compatible recovery", "Recovery source; never overwritten automatically."),
        F(".sv$", "AutoCAD Autosave", CadFileFormatFamily.AutoCadDrawing, R|O|I|CadFileFormatCapabilities.Recovery, "DWG-compatible recovery", "Recovery source; never overwritten automatically."),
        F(".dxb", "Drawing Interchange Binary", CadFileFormatFamily.AutoCadDrawing, R|O|I|E, "IxMilia DXB 1.0", "Bounded legacy 2D geometry with explicit downgrade warnings."),

        // Sheet sets / publication lists. Import/export means migration package support,
        // not native AutoCAD Sheet Set Manager editing.
        F(".dst", "AutoCAD Sheet Set Data", CadFileFormatFamily.AutoCadSheetSet, R|I|E|CadFileFormatCapabilities.SheetSet, "Exact opaque package adapter", "Lossless byte preservation/inventory; proprietary sheet-set database semantics are not rewritten."),
        F(".dsd", "Drawing Set Description", CadFileFormatFamily.AutoCadSheetSet, R|I|E|CadFileFormatCapabilities.SheetSet|CadFileFormatCapabilities.Published, "UCAD text migration adapter", "Section/key-value publication-list metadata can be inventoried and preserved."),
        F(".bp3", "Batch Plot List", CadFileFormatFamily.AutoCadSheetSet, R|I|E|CadFileFormatCapabilities.SheetSet|CadFileFormatCapabilities.Published, "UCAD text migration adapter", "Legacy batch-plot list text can be inventoried and preserved."),

        // Published / plotting formats.
        F(".dwf", "Design Web Format", CadFileFormatFamily.AutoCadPublished, R|I|E|CadFileFormatCapabilities.Published, "Exact opaque published-package adapter", "Classic DWF can be preserved/re-exported exactly; editable geometry extraction is not claimed without a compatible DWF parser."),
        F(".dwfx", "Design Web Format XPS", CadFileFormatFamily.AutoCadPublished, R|I|E|CadFileFormatCapabilities.Published, "UCAD DWFx/XPS FixedPage adapter", "Editable M/L/Z fixed-page vector subset; richer Autodesk metadata/text/raster content is reported explicitly."),
        F(".ctb", "Color-dependent Plot Style", CadFileFormatFamily.AutoCadPublished, R|I|E|CadFileFormatCapabilities.Resource|CadFileFormatCapabilities.Published, "Exact plot-resource adapter", "Lossless migration/re-export; semantic plot-style table editing is not claimed."),
        F(".stb", "Named Plot Style", CadFileFormatFamily.AutoCadPublished, R|I|E|CadFileFormatCapabilities.Resource|CadFileFormatCapabilities.Published, "Exact plot-resource adapter", "Lossless migration/re-export; semantic plot-style table editing is not claimed."),
        F(".pc3", "Plotter Configuration", CadFileFormatFamily.AutoCadPublished, R|I|E|CadFileFormatCapabilities.Resource|CadFileFormatCapabilities.Published, "Exact configuration adapter", "Configuration is preserved without loading device drivers."),
        F(".pmp", "Plot Model Parameter", CadFileFormatFamily.AutoCadPublished, R|I|E|CadFileFormatCapabilities.Resource|CadFileFormatCapabilities.Published, "Exact configuration adapter", "Calibration/paper resource is preserved without device execution."),
        F(".psf", "PostScript Font Substitution", CadFileFormatFamily.AutoCadPublished, R|I|E|CadFileFormatCapabilities.Resource|CadFileFormatCapabilities.Published, "Exact support-resource adapter", "Lossless migration package."),
        F(".pss", "PostScript Support File", CadFileFormatFamily.AutoCadPublished, R|I|E|CadFileFormatCapabilities.Resource|CadFileFormatCapabilities.Published, "Exact support-resource adapter", "Lossless migration package."),

        // Text/binary support and customization resources.
        F(".pat", "Hatch Pattern", CadFileFormatFamily.AutoCadResource, R|I|E|CadFileFormatCapabilities.Resource, "UCAD PAT codec", "Pattern geometry parse/serialize."),
        F(".lin", "Linetype Definition", CadFileFormatFamily.AutoCadResource, R|I|E|CadFileFormatCapabilities.Resource, "UCAD LIN codec", "Simple/complex definition text preserved."),
        F(".pgp", "Program Parameters / Command Aliases", CadFileFormatFamily.AutoCadResource, R|I|E|CadFileFormatCapabilities.Resource, "UCAD safe PGP codec", "Command aliases only; external-process definitions are never executed."),
        F(".shx", "Compiled Shape/Font", CadFileFormatFamily.AutoCadResource, R|I|E|CadFileFormatCapabilities.Resource, "SHX inventory + fallback adapter", "Exact bytes preserved and a safe UI-font fallback is supplied; SHX bytecode is not executed."),
        F(".fmp", "Font Mapping File", CadFileFormatFamily.AutoCadResource, R|I|E|CadFileFormatCapabilities.Resource, "UCAD text migration adapter", "Font substitution mapping metadata preserved."),
        F(".dcl", "Dialog Control Language", CadFileFormatFamily.AutoCadResource, R|I|E|CadFileFormatCapabilities.Resource, "UCAD text migration adapter", "Source is inventoried/preserved; DCL dialogs are not executed."),
        F(".unt", "Unit Definition", CadFileFormatFamily.AutoCadResource, R|I|E|CadFileFormatCapabilities.Resource, "UCAD text migration adapter", "Unit-definition source metadata preserved."),
        F(".cfg", "AutoCAD Configuration", CadFileFormatFamily.AutoCadResource, R|I|E|CadFileFormatCapabilities.Resource, "UCAD text migration adapter", "Configuration text metadata preserved."),
        F(".cuix", "Customization UI", CadFileFormatFamily.AutoCadResource, R|I|E|CadFileFormatCapabilities.Resource, "CUIX ZIP/XML inventory adapter", "XML command/UI metadata inventoried; embedded binaries/macros are never auto-executed."),
        F(".cui", "Legacy Customization UI", CadFileFormatFamily.AutoCadResource, R|I|E|CadFileFormatCapabilities.Resource, "UCAD text migration adapter", "Legacy customization source preserved without macro execution."),
        F(".mnu", "Legacy Menu Template", CadFileFormatFamily.AutoCadResource, R|I|E|CadFileFormatCapabilities.Resource, "UCAD text migration adapter", "Legacy menu source preserved."),
        F(".mns", "Legacy Menu Source", CadFileFormatFamily.AutoCadResource, R|I|E|CadFileFormatCapabilities.Resource, "UCAD text migration adapter", "Legacy menu source preserved."),
        F(".mnc", "Legacy Compiled Menu", CadFileFormatFamily.AutoCadResource, R|I|E|CadFileFormatCapabilities.Resource, "Exact opaque adapter", "Compiled menu is preserved/inventoried, not executed."),
        F(".atc", "Tool Catalog / Tool Palette", CadFileFormatFamily.AutoCadResource, R|I|E|CadFileFormatCapabilities.Resource, "UCAD text/XML migration adapter", "Palette metadata preserved; commands are not auto-executed."),
        F(".arg", "AutoCAD Profile", CadFileFormatFamily.AutoCadResource, R|I|E|CadFileFormatCapabilities.Resource, "UCAD profile migration adapter", "Profile sections/settings can be inventoried and preserved."),
        F(".sld", "AutoCAD Slide", CadFileFormatFamily.AutoCadResource, R|I|E|CadFileFormatCapabilities.Resource, "Exact opaque adapter", "Legacy slide bytes preserved."),
        F(".slb", "AutoCAD Slide Library", CadFileFormatFamily.AutoCadResource, R|I|E|CadFileFormatCapabilities.Resource, "Exact opaque adapter", "Legacy slide-library bytes preserved."),

        // Automation compatibility. No source/compiled format is silently evaluated.
        F(".scr", "AutoCAD Script", CadFileFormatFamily.AutoCadAutomation, R|I|E|CadFileFormatCapabilities.Automation, "UCAD safe script parser/runner", "Commands are parsed; only registered UCAD command paths are dispatched and unsupported interactive statements are reported."),
        F(".lsp", "AutoLISP Source", CadFileFormatFamily.AutoCadAutomation, R|I|CadFileFormatCapabilities.Automation, "Source-level compatibility analyzer", "DEFUN and command invocations are inventoried; Lisp is not evaluated."),
        F(".mnl", "Menu AutoLISP Source", CadFileFormatFamily.AutoCadAutomation, R|I|CadFileFormatCapabilities.Automation, "Source-level compatibility analyzer", "Menu Lisp is analyzed, never evaluated."),
        F(".fas", "Compiled AutoLISP", CadFileFormatFamily.AutoCadAutomation, R|I|E|CadFileFormatCapabilities.Automation, "Exact non-executable migration adapter", "Compiled code can be preserved/inventoried but not executed without a compatible runtime."),
        F(".vlx", "Visual LISP Application", CadFileFormatFamily.AutoCadAutomation, R|I|E|CadFileFormatCapabilities.Automation, "Exact non-executable migration adapter", "VLX can be preserved/inventoried but not executed."),
        F(".dvb", "VBA Project", CadFileFormatFamily.AutoCadAutomation, R|I|E|CadFileFormatCapabilities.Automation, "Exact non-executable migration adapter", "VBA project can be preserved/inventoried; UCAD has no embedded VBA runtime."),
        F(".js", "AutoCAD JavaScript", CadFileFormatFamily.AutoCadAutomation, R|CadFileFormatCapabilities.Automation, "No AutoCAD JavaScript host", "Recognition only; arbitrary JavaScript execution is intentionally unavailable."),
        F(".rx", "AutoCAD Application List", CadFileFormatFamily.AutoCadAutomation, R|I|E|CadFileFormatCapabilities.Automation, "UCAD text migration adapter", "Application-load list metadata is preserved; referenced binaries are not auto-loaded."),

        // Binary plug-in/runtime artifacts remain inventory-only because UCAD has no AutoCAD ABI.
        F(".arx", "ObjectARX Application", CadFileFormatFamily.AutoCadPlugin, R|CadFileFormatCapabilities.Plugin, "No AutoCAD binary ABI", "Recognized only; not loadable."),
        F(".crx", "AutoCAD CRX Application", CadFileFormatFamily.AutoCadPlugin, R|CadFileFormatCapabilities.Plugin, "No AutoCAD binary ABI", "Recognized only; not loadable."),
        F(".dbx", "ObjectDBX Module", CadFileFormatFamily.AutoCadPlugin, R|CadFileFormatCapabilities.Plugin, "No AutoCAD binary ABI", "Recognized only; not loadable."),
        F(".hdi", "AutoCAD Graphics Driver", CadFileFormatFamily.AutoCadPlugin, R|CadFileFormatCapabilities.Plugin, "No AutoCAD graphics-driver ABI", "Recognized only; not loadable."),
        F(".dll", "AutoCAD .NET / Native Module", CadFileFormatFamily.AutoCadPlugin, R|CadFileFormatCapabilities.Plugin, "No arbitrary module loading", "Generic DLLs are never auto-executed."),

        // Other AutoCAD exchange formats. Existing UCAD PDF/GIS paths are separate from this inventory.
        F(".pdf", "PDF", CadFileFormatFamily.AutoCadExchange, R|CadFileFormatCapabilities.Exchange, "Existing UCAD PDF publish path", "Tracked for AutoCAD exchange inventory; generic PDF geometry import is not claimed here."),
        F(".dgn", "MicroStation DGN", CadFileFormatFamily.AutoCadExchange, R|CadFileFormatCapabilities.Exchange, "Pending DGN adapter", "No DGN geometry adapter yet."),
        F(".sat", "ACIS SAT", CadFileFormatFamily.AutoCadExchange, R|CadFileFormatCapabilities.Exchange, "Out of 2D scope", "3D solid exchange is outside UCAD 1.x."),
        F(".igs", "IGES", CadFileFormatFamily.AutoCadExchange, R|CadFileFormatCapabilities.Exchange, "Out of 2D scope", "3D exchange is outside UCAD 1.x."),
        F(".iges", "IGES", CadFileFormatFamily.AutoCadExchange, R|CadFileFormatCapabilities.Exchange, "Out of 2D scope", "3D exchange is outside UCAD 1.x."),
        F(".stl", "STL", CadFileFormatFamily.AutoCadExchange, R|CadFileFormatCapabilities.Exchange, "Out of 2D scope", "Mesh exchange is outside UCAD 1.x."),
        F(".wmf", "Windows Metafile", CadFileFormatFamily.AutoCadExchange, R|CadFileFormatCapabilities.Exchange, "Pending vector exchange adapter", "Tracked for AutoCAD exchange inventory."),
        F(".bmp", "Bitmap", CadFileFormatFamily.AutoCadExchange, R|CadFileFormatCapabilities.Exchange, "Pending raster exchange routing", "Tracked for AutoCAD exchange inventory."),
        F(".dxx", "Attribute Extract DXF", CadFileFormatFamily.AutoCadExchange, R|CadFileFormatCapabilities.Exchange, "Pending attribute-extract adapter", "Tracked for AutoCAD attribute extraction."),
    ];

    private static CadFileFormatDescriptor F(string extension, string name, CadFileFormatFamily family, CadFileFormatCapabilities capabilities, string transport, string note) =>
        new(extension, name, family, capabilities, transport, note);

    private static readonly IReadOnlyDictionary<string, CadFileFormatDescriptor> ByExtension =
        FormatsInternal.ToDictionary(format => format.Extension, StringComparer.OrdinalIgnoreCase);

    public static IReadOnlyList<CadFileFormatDescriptor> Formats => FormatsInternal;

    public static IReadOnlyList<CadFileFormatDescriptor> OpenableDrawingFormats =>
        FormatsInternal.Where(format => format.Family is CadFileFormatFamily.UcadNative or CadFileFormatFamily.AutoCadDrawing)
            .Where(format => format.CanOpen).ToArray();

    public static IReadOnlyList<CadFileFormatDescriptor> ExportableAutoCadDrawingFormats =>
        FormatsInternal.Where(format => format.Family == CadFileFormatFamily.AutoCadDrawing && format.CanExport).ToArray();

    public static IReadOnlyList<CadFileFormatDescriptor> MigratableAutoCadFormats =>
        FormatsInternal.Where(format => format.Family is not CadFileFormatFamily.UcadNative and not CadFileFormatFamily.AutoCadDrawing)
            .Where(format => format.CanImport).ToArray();

    public static IReadOnlyList<CadFileFormatDescriptor> PendingFormats =>
        FormatsInternal.Where(format => format.Capabilities.HasFlag(CadFileFormatCapabilities.Recognized))
            .Where(format => !format.CanOpen && !format.CanImport && !format.CanExport).ToArray();

    public static bool TryGetByPath(string path, out CadFileFormatDescriptor descriptor) => TryGetByExtension(Path.GetExtension(path), out descriptor);

    public static bool TryGetByExtension(string? extension, out CadFileFormatDescriptor descriptor)
    {
        if (string.IsNullOrWhiteSpace(extension)) { descriptor = null!; return false; }
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

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
    Automation = 1 << 8
}

public enum CadFileFormatFamily
{
    UcadNative,
    AutoCadDrawing,
    AutoCadPublished,
    AutoCadResource,
    AutoCadAutomation
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
        new(
            CadNativeDocumentCodec.FileExtension,
            "UCAD Drawing",
            CadFileFormatFamily.UcadNative,
            CadFileFormatCapabilities.Recognized | CadFileFormatCapabilities.Open | CadFileFormatCapabilities.Import | CadFileFormatCapabilities.Export,
            "UCAD native JSON",
            "Full-fidelity UCAD authoring document."),
        new(
            ".dwg",
            "AutoCAD Drawing",
            CadFileFormatFamily.AutoCadDrawing,
            CadFileFormatCapabilities.Recognized | CadFileFormatCapabilities.Open | CadFileFormatCapabilities.Import | CadFileFormatCapabilities.Export,
            "ACadSharp DWG ↔ DXF bridge",
            "Read/write transport is available; UCAD entity fidelity is bounded by the current DXF bridge."),
        new(
            ".dxf",
            "Drawing Exchange Format",
            CadFileFormatFamily.AutoCadDrawing,
            CadFileFormatCapabilities.Recognized | CadFileFormatCapabilities.Open | CadFileFormatCapabilities.Import | CadFileFormatCapabilities.Export,
            "UCAD ASCII DXF + ACadSharp DXF",
            "ASCII DXF is native to UCAD; ACadSharp normalizes binary and legacy DXF before import."),
        new(
            ".dwt",
            "AutoCAD Drawing Template",
            CadFileFormatFamily.AutoCadDrawing,
            CadFileFormatCapabilities.Recognized | CadFileFormatCapabilities.Open | CadFileFormatCapabilities.Import | CadFileFormatCapabilities.Export | CadFileFormatCapabilities.Template,
            "DWG-compatible container",
            "Opened as a drawing template source; template-only AutoCAD metadata may not round-trip yet."),
        new(
            ".dws",
            "AutoCAD Standards File",
            CadFileFormatFamily.AutoCadDrawing,
            CadFileFormatCapabilities.Recognized | CadFileFormatCapabilities.Open | CadFileFormatCapabilities.Import,
            "DWG-compatible container",
            "Geometry/table content can be imported; CAD-standards semantics are not yet authored by UCAD."),
        new(
            ".bak",
            "AutoCAD Drawing Backup",
            CadFileFormatFamily.AutoCadDrawing,
            CadFileFormatCapabilities.Recognized | CadFileFormatCapabilities.Open | CadFileFormatCapabilities.Import | CadFileFormatCapabilities.Recovery,
            "DWG-compatible recovery container",
            "Opened as a recovery source and never overwritten automatically."),
        new(
            ".sv$",
            "AutoCAD Autosave",
            CadFileFormatFamily.AutoCadDrawing,
            CadFileFormatCapabilities.Recognized | CadFileFormatCapabilities.Open | CadFileFormatCapabilities.Import | CadFileFormatCapabilities.Recovery,
            "DWG-compatible recovery container",
            "Opened as a recovery source and never overwritten automatically."),
        new(
            ".dwf",
            "Design Web Format",
            CadFileFormatFamily.AutoCadPublished,
            CadFileFormatCapabilities.Recognized | CadFileFormatCapabilities.Published,
            "Pending published-format adapter",
            "Recognized, but DWF import/export is not yet enabled."),
        new(
            ".dwfx",
            "Design Web Format XPS",
            CadFileFormatFamily.AutoCadPublished,
            CadFileFormatCapabilities.Recognized | CadFileFormatCapabilities.Published,
            "Pending published-format adapter",
            "Recognized, but DWFx import/export is not yet enabled."),
        new(
            ".dxb",
            "Drawing Interchange Binary",
            CadFileFormatFamily.AutoCadDrawing,
            CadFileFormatCapabilities.Recognized,
            "Pending DXB adapter",
            "Legacy DXB is distinct from binary DXF and is not claimed as supported yet."),
        new(
            ".pat",
            "Hatch Pattern",
            CadFileFormatFamily.AutoCadResource,
            CadFileFormatCapabilities.Recognized | CadFileFormatCapabilities.Resource,
            "Pending resource adapter",
            "Reserved for hatch pattern import/export."),
        new(
            ".lin",
            "Linetype Definition",
            CadFileFormatFamily.AutoCadResource,
            CadFileFormatCapabilities.Recognized | CadFileFormatCapabilities.Resource,
            "Pending resource adapter",
            "Reserved for linetype import/export."),
        new(
            ".shx",
            "Compiled Shape/Font",
            CadFileFormatFamily.AutoCadResource,
            CadFileFormatCapabilities.Recognized | CadFileFormatCapabilities.Resource,
            "Pending font/shape adapter",
            "Recognized as an external resource; direct SHX execution/rendering is not enabled yet."),
        new(
            ".ctb",
            "Color-dependent Plot Style",
            CadFileFormatFamily.AutoCadResource,
            CadFileFormatCapabilities.Recognized | CadFileFormatCapabilities.Resource,
            "Pending plot-style adapter",
            "Reserved for plot style import/export."),
        new(
            ".stb",
            "Named Plot Style",
            CadFileFormatFamily.AutoCadResource,
            CadFileFormatCapabilities.Recognized | CadFileFormatCapabilities.Resource,
            "Pending plot-style adapter",
            "Reserved for plot style import/export."),
        new(
            ".pc3",
            "Plotter Configuration",
            CadFileFormatFamily.AutoCadResource,
            CadFileFormatCapabilities.Recognized | CadFileFormatCapabilities.Resource,
            "Pending plot configuration adapter",
            "Reserved for plot configuration import."),
        new(
            ".pmp",
            "Plot Model Parameter",
            CadFileFormatFamily.AutoCadResource,
            CadFileFormatCapabilities.Recognized | CadFileFormatCapabilities.Resource,
            "Pending plot configuration adapter",
            "Reserved for plot configuration import."),
        new(
            ".cuix",
            "Customization UI",
            CadFileFormatFamily.AutoCadResource,
            CadFileFormatCapabilities.Recognized | CadFileFormatCapabilities.Resource,
            "Pending customization adapter",
            "Recognized for future command/UI migration tooling."),
        new(
            ".arg",
            "AutoCAD Profile",
            CadFileFormatFamily.AutoCadResource,
            CadFileFormatCapabilities.Recognized | CadFileFormatCapabilities.Resource,
            "Pending profile adapter",
            "Recognized for future settings/profile migration."),
        new(
            ".scr",
            "AutoCAD Script",
            CadFileFormatFamily.AutoCadAutomation,
            CadFileFormatCapabilities.Recognized | CadFileFormatCapabilities.Automation,
            "Pending safe script adapter",
            "Recognized only; UCAD does not execute AutoCAD scripts yet."),
        new(
            ".lsp",
            "AutoLISP Source",
            CadFileFormatFamily.AutoCadAutomation,
            CadFileFormatCapabilities.Recognized | CadFileFormatCapabilities.Automation,
            "Pending compatibility layer",
            "Recognized only; AutoLISP execution is outside the current runtime."),
        new(
            ".fas",
            "Compiled AutoLISP",
            CadFileFormatFamily.AutoCadAutomation,
            CadFileFormatCapabilities.Recognized | CadFileFormatCapabilities.Automation,
            "No compatible runtime",
            "Recognized only; compiled AutoLISP is not executable in UCAD."),
        new(
            ".vlx",
            "Visual LISP Application",
            CadFileFormatFamily.AutoCadAutomation,
            CadFileFormatCapabilities.Recognized | CadFileFormatCapabilities.Automation,
            "No compatible runtime",
            "Recognized only; Visual LISP applications are not executable in UCAD.")
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

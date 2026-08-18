using System.Text;
using ACadSharp.IO;
using UcadDocument = UCAD.Core.CadDocument;

namespace UCAD.Core.IO;

/// <summary>
/// Reads DXF OBJECTS/layout metadata as a sidecar without making ACadSharp authoritative for
/// DXF entity semantics. IxMilia + UCAD's advanced DXF bridge remain the primary entity path;
/// this adapter only supplements paper layouts, plot settings and paper-space viewports.
/// </summary>
internal static class CadAcadDxfLayoutSidecar
{
    private static readonly UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);

    public static void Import(string normalizedTextDxf, UcadDocument target, List<string> warnings)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(normalizedTextDxf);
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(warnings);

        try
        {
            using var stream = new MemoryStream(Utf8NoBom.GetBytes(normalizedTextDxf), writable: false);
            using var reader = new DxfReader(stream);
            reader.Configuration.CreateDefaults = true;
            reader.OnNotification += (_, args) =>
            {
                if (string.IsNullOrWhiteSpace(args.Message)) return;
                var message = $"DXF layout sidecar: {args.Message}";
                if (!warnings.Contains(message, StringComparer.Ordinal)) warnings.Add(message);
            };
            var document = reader.Read();
            CadAcadLayoutInterop.Import(document, target, warnings);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or FormatException or NotSupportedException)
        {
            warnings.Add($"DXF layout sidecar could not read paper-layout metadata; drawing entities were still imported. {ex.Message}");
        }
    }
}

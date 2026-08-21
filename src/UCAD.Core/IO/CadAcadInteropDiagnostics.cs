namespace UCAD.Core.IO;

/// <summary>
/// Classifies AutoCAD transport diagnostics that describe source-only, non-graphical metadata.
/// UCAD keeps the exact DWG-compatible source container in the opaque source envelope, so these
/// notifications do not need to interrupt opening the editable drawing. Graphical/entity or
/// semantic-loss diagnostics are deliberately not suppressed by this classifier.
/// </summary>
public static class CadAcadInteropDiagnostics
{
    private static readonly string[] KnownPhasePrefixes =
    [
        "DWG read: ",
        "DXF bridge write: ",
        "DXF bridge read: "
    ];

    public static bool IsOpaqueMetadataNotification(string? message)
    {
        if (string.IsNullOrWhiteSpace(message)) return false;
        var value = StripKnownPhasePrefix(message.Trim());

        if (value.StartsWith("Unlisted object with DXF name ", StringComparison.OrdinalIgnoreCase) &&
            value.Contains("UnknownNonGraphicalObject", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (value.StartsWith(
                "NonGraphicalObject not supported read as an UnknownNonGraphicalObject",
                StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (value.StartsWith("UnknownNonGraphicalObject not supported:", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (value.StartsWith("Entry not found ", StringComparison.OrdinalIgnoreCase) &&
            value.Contains(" for dictionary ", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (value.StartsWith("Section not implemented THUMBNAILIMAGE", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return false;
    }

    public static IReadOnlyList<string> KeepActionableWarnings(IEnumerable<string> warnings)
    {
        ArgumentNullException.ThrowIfNull(warnings);
        return warnings
            .Where(warning => !IsOpaqueMetadataNotification(warning))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    private static string StripKnownPhasePrefix(string value)
    {
        foreach (var prefix in KnownPhasePrefixes)
        {
            if (value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                return value[prefix.Length..].TrimStart();
        }

        return value;
    }
}

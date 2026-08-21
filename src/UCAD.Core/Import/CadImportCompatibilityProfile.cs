namespace UCAD.Core.Import;

/// <summary>
/// Defines the compatibility targets for broad 2D CAD document support.
/// The profile is intentionally independent from UI commands.
/// </summary>
public sealed record CadImportCompatibilityProfile
{
    public bool PreserveUnknownEntities { get; init; } = true;

    public bool EnableExtendedPrecisionCoordinates { get; init; } = true;

    public bool EnableFontFallback { get; init; } = true;

    public static CadImportCompatibilityProfile Default { get; } = new();
}

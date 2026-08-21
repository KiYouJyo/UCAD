namespace UCAD.Core.Import;

/// <summary>
/// Coordinate handling policy for large engineering drawings.
/// </summary>
public sealed class CadCoordinatePolicy
{
    public bool UseDoublePrecision { get; init; } = true;

    public double MergeTolerance { get; init; } = 0.000001d;
}

namespace UCAD.Core.Entities;

/// <summary>
/// Preserves unsupported CAD entities during import so that the document can still be displayed
/// and round-tripped without silently dropping source data.
/// </summary>
public sealed class UnsupportedCadEntity : ICadEntity
{
    public Guid Id { get; } = Guid.NewGuid();

    public string EntityType { get; init; } = string.Empty;

    public string? RawData { get; init; }
}

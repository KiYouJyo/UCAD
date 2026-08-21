namespace UCAD.Core.Import;

using UCAD.Core.Entities;

/// <summary>
/// Preserves unsupported CAD objects so they are not silently discarded.
/// </summary>
public sealed class UnsupportedCadEntity : ICadEntity
{
    public Guid Id { get; } = Guid.NewGuid();

    public string EntityType { get; }

    public UnsupportedCadEntity(string entityType)
    {
        EntityType = entityType;
    }
}

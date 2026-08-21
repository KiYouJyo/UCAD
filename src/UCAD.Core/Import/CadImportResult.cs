using UCAD.Core.Entities;

namespace UCAD.Core.Import;

public sealed class CadImportResult
{
    public List<ICadEntity> Entities { get; } = [];

    public List<string> Warnings { get; } = [];

    public bool Success => Warnings.Count == 0;
}

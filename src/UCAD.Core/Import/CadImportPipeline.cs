namespace UCAD.Core.Import;

using UCAD.Core.Entities;

/// <summary>
/// Central entry point for CAD file compatibility processing.
/// This layer intentionally keeps format readers independent from the document model.
/// </summary>
public sealed class CadImportPipeline
{
    private readonly CadImportCompatibilityProfile _profile;

    public CadImportPipeline(CadImportCompatibilityProfile? profile = null)
    {
        _profile = profile ?? new CadImportCompatibilityProfile();
    }

    public CadImportCompatibilityProfile Profile => _profile;

    public ICadEntity ConvertUnsupported(string typeName)
    {
        return new UnsupportedCadEntity(typeName);
    }
}

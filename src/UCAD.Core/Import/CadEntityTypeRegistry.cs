namespace UCAD.Core.Import;

/// <summary>
/// Registry for mapping DWG/DXF entity names to UCAD entities.
/// Expanded incrementally as compatibility coverage grows.
/// </summary>
public sealed class CadEntityTypeRegistry
{
    private readonly HashSet<string> _supportedTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "LINE",
        "ARC",
        "CIRCLE",
        "LWPOLYLINE",
        "POLYLINE",
        "TEXT",
        "BLOCK",
        "INSERT",
        "HATCH",
        "DIMENSION"
    };

    public bool IsKnown(string typeName)
    {
        return _supportedTypes.Contains(typeName);
    }

    public void Register(string typeName)
    {
        if (!string.IsNullOrWhiteSpace(typeName))
        {
            _supportedTypes.Add(typeName);
        }
    }
}

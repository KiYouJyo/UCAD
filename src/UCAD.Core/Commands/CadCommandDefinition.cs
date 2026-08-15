namespace UCAD.Core.Commands;

public sealed class CadCommandDefinition
{
    public CadCommandDefinition(string name, params string[] aliases)
        : this(name, CadCommandCategory.General, null, aliases)
    {
    }

    public CadCommandDefinition(string name, CadCommandCategory category, params string[] aliases)
        : this(name, category, null, aliases)
    {
    }

    public CadCommandDefinition(string name, CadCommandCategory category, DrawingCommandKind drawingKind, params string[] aliases)
        : this(name, category, (DrawingCommandKind?)drawingKind, aliases)
    {
    }

    private CadCommandDefinition(
        string name,
        CadCommandCategory category,
        DrawingCommandKind? drawingKind,
        string[] aliases)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Command name is required.", nameof(name));
        }

        Name = Normalize(name);
        Category = category;
        DrawingKind = drawingKind;
        Aliases = aliases
            .Where(alias => !string.IsNullOrWhiteSpace(alias))
            .Select(Normalize)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Where(alias => !alias.Equals(Name, StringComparison.OrdinalIgnoreCase))
            .ToArray();
    }

    public string Name { get; }

    public CadCommandCategory Category { get; }

    public DrawingCommandKind? DrawingKind { get; }

    public IReadOnlyList<string> Aliases { get; }

    public string? PrimaryAlias => Aliases.FirstOrDefault();

    public IEnumerable<string> Tokens
    {
        get
        {
            yield return Name;
            foreach (var alias in Aliases)
            {
                yield return alias;
            }
        }
    }

    internal static string Normalize(string value) => value.Trim().ToUpperInvariant();
}

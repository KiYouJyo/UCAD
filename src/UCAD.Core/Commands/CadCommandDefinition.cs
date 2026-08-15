namespace UCAD.Core.Commands;

public sealed class CadCommandDefinition
{
    public CadCommandDefinition(string name, params string[] aliases)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Command name is required.", nameof(name));
        }

        Name = Normalize(name);
        Aliases = aliases
            .Where(alias => !string.IsNullOrWhiteSpace(alias))
            .Select(Normalize)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Where(alias => !alias.Equals(Name, StringComparison.OrdinalIgnoreCase))
            .ToArray();
    }

    public string Name { get; }

    public IReadOnlyList<string> Aliases { get; }

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

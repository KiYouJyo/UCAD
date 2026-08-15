namespace UCAD.Core.Commands;

public sealed class CommandRegistry
{
    private readonly Dictionary<string, CadCommandDefinition> _lookup = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<CadCommandDefinition> _commands = [];

    public IReadOnlyList<CadCommandDefinition> Commands => _commands;

    public void Register(CadCommandDefinition command)
    {
        ArgumentNullException.ThrowIfNull(command);

        foreach (var token in command.Tokens)
        {
            if (_lookup.ContainsKey(token))
            {
                throw new InvalidOperationException($"Command token '{token}' is already registered.");
            }
        }

        _commands.Add(command);
        foreach (var token in command.Tokens)
        {
            _lookup[token] = command;
        }
    }

    public bool TryResolve(string? token, out CadCommandDefinition? command)
    {
        command = null;
        if (string.IsNullOrWhiteSpace(token))
        {
            return false;
        }

        return _lookup.TryGetValue(CadCommandDefinition.Normalize(token), out command);
    }

    public static CommandRegistry CreateDefault()
    {
        var registry = new CommandRegistry();
        registry.Register(new CadCommandDefinition("LINE", "L"));
        registry.Register(new CadCommandDefinition("CLEAR"));
        registry.Register(new CadCommandDefinition("RESETVIEW", "RV"));
        return registry;
    }
}

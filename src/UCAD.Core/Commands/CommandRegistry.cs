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
        registry.Register(new CadCommandDefinition("LINE", CadCommandCategory.Draw, DrawingCommandKind.Line, "L"));
        registry.Register(new CadCommandDefinition("PLINE", CadCommandCategory.Draw, DrawingCommandKind.Polyline, "PL"));
        registry.Register(new CadCommandDefinition("RECTANGLE", CadCommandCategory.Draw, DrawingCommandKind.Rectangle, "REC"));
        registry.Register(new CadCommandDefinition("CIRCLE", CadCommandCategory.Draw, DrawingCommandKind.Circle, "C"));
        registry.Register(new CadCommandDefinition("ARC", CadCommandCategory.Draw, DrawingCommandKind.Arc, "A"));
        registry.Register(new CadCommandDefinition("UNDO", CadCommandCategory.Edit, "U"));
        registry.Register(new CadCommandDefinition("REDO", CadCommandCategory.Edit));
        registry.Register(new CadCommandDefinition("ERASE", CadCommandCategory.Edit, "E", "DELETE"));
        registry.Register(new CadCommandDefinition("CLEAR", CadCommandCategory.Edit));
        registry.Register(new CadCommandDefinition("RESETVIEW", CadCommandCategory.View, "RV"));
        return registry;
    }
}

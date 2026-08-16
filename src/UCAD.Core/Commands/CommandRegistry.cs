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
            if (_lookup.ContainsKey(token)) throw new InvalidOperationException($"Command token '{token}' is already registered.");
        _commands.Add(command);
        foreach (var token in command.Tokens) _lookup[token] = command;
    }

    public bool TryResolve(string? token, out CadCommandDefinition? command)
    {
        command = null;
        if (string.IsNullOrWhiteSpace(token)) return false;
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
        registry.Register(new CadCommandDefinition("HATCH", CadCommandCategory.Draw, "H"));

        registry.Register(new CadCommandDefinition("MOVE", CadCommandCategory.Modify, "M"));
        registry.Register(new CadCommandDefinition("COPY", CadCommandCategory.Modify, "CO", "CP"));
        registry.Register(new CadCommandDefinition("ROTATE", CadCommandCategory.Modify, "RO"));
        registry.Register(new CadCommandDefinition("SCALE", CadCommandCategory.Modify, "SC"));
        registry.Register(new CadCommandDefinition("MIRROR", CadCommandCategory.Modify, "MI"));
        registry.Register(new CadCommandDefinition("OFFSET", CadCommandCategory.Modify, "O"));
        registry.Register(new CadCommandDefinition("TRIM", CadCommandCategory.Modify, "TR"));
        registry.Register(new CadCommandDefinition("EXTEND", CadCommandCategory.Modify, "EX"));
        registry.Register(new CadCommandDefinition("EXPLODE", CadCommandCategory.Modify, "X"));

        registry.Register(new CadCommandDefinition("TEXT", CadCommandCategory.Annotate, "T"));
        registry.Register(new CadCommandDefinition("DIM", CadCommandCategory.Annotate, "DLI", "DIMLINEAR"));

        registry.Register(new CadCommandDefinition("LAYER", CadCommandCategory.Layer, "LA"));
        registry.Register(new CadCommandDefinition("CHPROP", CadCommandCategory.Layer, "CH"));

        registry.Register(new CadCommandDefinition("BLOCK", CadCommandCategory.Block, "B"));
        registry.Register(new CadCommandDefinition("INSERT", CadCommandCategory.Block, "I"));

        registry.Register(new CadCommandDefinition("UNDO", CadCommandCategory.Edit, "U"));
        registry.Register(new CadCommandDefinition("REDO", CadCommandCategory.Edit));
        registry.Register(new CadCommandDefinition("ERASE", CadCommandCategory.Edit, "E", "DELETE"));
        registry.Register(new CadCommandDefinition("CLEAR", CadCommandCategory.Edit));
        registry.Register(new CadCommandDefinition("RESETVIEW", CadCommandCategory.View, "RV"));
        return registry;
    }
}

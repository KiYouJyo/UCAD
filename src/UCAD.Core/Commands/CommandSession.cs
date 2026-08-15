namespace UCAD.Core.Commands;

public enum CommandStartStatus
{
    Started,
    Unknown,
    NoPreviousCommand
}

public readonly record struct CommandStartResult(CommandStartStatus Status, CadCommandDefinition? Command, string? Token);

public sealed class CommandSession
{
    private readonly CommandRegistry _registry;

    public CommandSession(CommandRegistry registry)
    {
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
    }

    public CadCommandDefinition? ActiveCommand { get; private set; }

    public CadCommandDefinition? PreviousCommand { get; private set; }

    public bool IsActive => ActiveCommand is not null;

    public CommandStartResult Start(string? token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return RepeatPrevious();
        }

        if (!_registry.TryResolve(token, out var command) || command is null)
        {
            return new CommandStartResult(CommandStartStatus.Unknown, null, token.Trim());
        }

        ActiveCommand = command;
        PreviousCommand = command;
        return new CommandStartResult(CommandStartStatus.Started, command, token.Trim());
    }

    public CommandStartResult RepeatPrevious()
    {
        if (PreviousCommand is null)
        {
            return new CommandStartResult(CommandStartStatus.NoPreviousCommand, null, null);
        }

        ActiveCommand = PreviousCommand;
        return new CommandStartResult(CommandStartStatus.Started, PreviousCommand, PreviousCommand.Name);
    }

    public void Complete() => ActiveCommand = null;

    public bool Cancel()
    {
        if (ActiveCommand is null)
        {
            return false;
        }

        ActiveCommand = null;
        return true;
    }
}

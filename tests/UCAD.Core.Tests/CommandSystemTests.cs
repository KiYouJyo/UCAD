using UCAD.Core.Commands;
using UCAD.Core.Geometry;
using Xunit;

namespace UCAD.Core.Tests;

public sealed class CommandSystemTests
{
    [Fact]
    public void RegistryResolvesCanonicalNameAndAliasCaseInsensitively()
    {
        var registry = CommandRegistry.CreateDefault();

        Assert.True(registry.TryResolve("line", out var byName));
        Assert.True(registry.TryResolve("l", out var byAlias));
        Assert.NotNull(byName);
        Assert.Same(byName, byAlias);
        Assert.Equal("LINE", byName!.Name);
    }

    [Fact]
    public void RegistryRejectsDuplicateTokens()
    {
        var registry = new CommandRegistry();
        registry.Register(new CadCommandDefinition("LINE", "L"));

        Assert.Throws<InvalidOperationException>(() => registry.Register(new CadCommandDefinition("LAYER", "L")));
    }

    [Fact]
    public void SessionRepeatsPreviousCommandAfterCompletion()
    {
        var session = new CommandSession(CommandRegistry.CreateDefault());
        Assert.Equal(CommandStartStatus.Started, session.Start("L").Status);
        session.Complete();

        var repeated = session.Start(string.Empty);

        Assert.Equal(CommandStartStatus.Started, repeated.Status);
        Assert.Equal("LINE", session.ActiveCommand?.Name);
    }

    [Fact]
    public void SessionCancelClearsActiveCommand()
    {
        var session = new CommandSession(CommandRegistry.CreateDefault());
        session.Start("LINE");

        Assert.True(session.Cancel());
        Assert.False(session.IsActive);
        Assert.False(session.Cancel());
    }

    [Fact]
    public void EmptySessionHasNoPreviousCommand()
    {
        var session = new CommandSession(CommandRegistry.CreateDefault());

        Assert.Equal(CommandStartStatus.NoPreviousCommand, session.Start(" ").Status);
    }

    [Theory]
    [InlineData("12.5", 12.5)]
    [InlineData("-3", -3)]
    public void ParserAcceptsInvariantNumbers(string text, double expected)
    {
        Assert.True(CommandInputParser.TryParseNumber(text, out var value));
        Assert.Equal(expected, value, 10);
    }

    [Fact]
    public void ParserAcceptsAbsoluteCoordinates()
    {
        Assert.True(CommandInputParser.TryParsePoint("10.5,-4", null, out var point));
        Assert.Equal(new CadPoint(10.5, -4), point);
    }

    [Fact]
    public void ParserAcceptsRelativeCoordinates()
    {
        Assert.True(CommandInputParser.TryParsePoint("@5,-2", new CadPoint(10, 20), out var point));
        Assert.Equal(new CadPoint(15, 18), point);
    }

    [Fact]
    public void RelativeCoordinateRequiresBasePoint()
    {
        Assert.False(CommandInputParser.TryParsePoint("@5,2", null, out _));
    }
}

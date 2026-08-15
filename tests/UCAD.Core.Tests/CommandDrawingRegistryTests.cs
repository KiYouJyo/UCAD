using UCAD.Core.Commands;
using Xunit;

namespace UCAD.Core.Tests;

public sealed class CommandDrawingRegistryTests
{
    [Theory]
    [InlineData("L", "LINE")]
    [InlineData("PL", "PLINE")]
    [InlineData("REC", "RECTANGLE")]
    [InlineData("C", "CIRCLE")]
    [InlineData("A", "ARC")]
    [InlineData("U", "UNDO")]
    [InlineData("REDO", "REDO")]
    public void DefaultRegistryResolvesDrawingAndHistoryAliases(string token, string expected)
    {
        var registry = CommandRegistry.CreateDefault();

        Assert.True(registry.TryResolve(token, out var command));
        Assert.Equal(expected, command!.Name);
    }
}

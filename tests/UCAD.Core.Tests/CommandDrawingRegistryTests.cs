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

    [Theory]
    [InlineData("LINE", DrawingCommandKind.Line)]
    [InlineData("PLINE", DrawingCommandKind.Polyline)]
    [InlineData("RECTANGLE", DrawingCommandKind.Rectangle)]
    [InlineData("CIRCLE", DrawingCommandKind.Circle)]
    [InlineData("ARC", DrawingCommandKind.Arc)]
    public void DrawingCommandsExposeStableUiMetadata(string token, DrawingCommandKind expectedKind)
    {
        var registry = CommandRegistry.CreateDefault();

        Assert.True(registry.TryResolve(token, out var command));
        Assert.Equal(CadCommandCategory.Draw, command!.Category);
        Assert.Equal(expectedKind, command.DrawingKind);
    }

    [Theory]
    [InlineData("UNDO", CadCommandCategory.Edit)]
    [InlineData("REDO", CadCommandCategory.Edit)]
    [InlineData("CLEAR", CadCommandCategory.Edit)]
    [InlineData("RESETVIEW", CadCommandCategory.View)]
    public void UtilityCommandsExposeStableUiCategory(string token, CadCommandCategory expectedCategory)
    {
        var registry = CommandRegistry.CreateDefault();

        Assert.True(registry.TryResolve(token, out var command));
        Assert.Equal(expectedCategory, command!.Category);
        Assert.Null(command.DrawingKind);
    }
}

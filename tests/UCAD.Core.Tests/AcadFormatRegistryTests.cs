using UCAD.Core.IO;
using Xunit;

namespace UCAD.Core.Tests;

public sealed class AcadFormatRegistryTests
{
    [Theory]
    [InlineData("plan.dwg", true, true)]
    [InlineData("plan.DXF", true, true)]
    [InlineData("template.dwt", true, true)]
    [InlineData("standards.dws", true, false)]
    [InlineData("drawing.bak", true, false)]
    [InlineData("autosave.sv$", true, false)]
    public void DrawingContainersExposeOnlyImplementedCapabilities(string path, bool canOpen, bool canExport)
    {
        Assert.True(CadAcadFileFormatRegistry.TryGetByPath(path, out var format));
        Assert.Equal(canOpen, format.CanOpen);
        Assert.Equal(canExport, format.CanExport);
        Assert.Equal(CadFileFormatFamily.AutoCadDrawing, format.Family);
    }

    [Theory]
    [InlineData("published.dwf")]
    [InlineData("published.dwfx")]
    [InlineData("plot.ctb")]
    [InlineData("plot.stb")]
    [InlineData("font.shx")]
    [InlineData("hatch.pat")]
    [InlineData("linetype.lin")]
    [InlineData("profile.arg")]
    [InlineData("script.scr")]
    [InlineData("routine.lsp")]
    public void PendingAutoCadFormatsAreRecognizedWithoutFalseOpenOrExportClaims(string path)
    {
        Assert.True(CadAcadFileFormatRegistry.TryGetByPath(path, out var format));
        Assert.True(format.Capabilities.HasFlag(CadFileFormatCapabilities.Recognized));
        Assert.False(format.CanOpen);
        Assert.False(format.CanExport);
        Assert.Contains("Pending", format.Transport, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void OpenableDrawingFormatsContainNativeAndRecoveryContainers()
    {
        var extensions = CadAcadFileFormatRegistry.OpenableDrawingFormats
            .Select(format => format.Extension)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        Assert.Contains(".ucad", extensions);
        Assert.Contains(".dwg", extensions);
        Assert.Contains(".dxf", extensions);
        Assert.Contains(".dwt", extensions);
        Assert.Contains(".dws", extensions);
        Assert.Contains(".bak", extensions);
        Assert.Contains(".sv$", extensions);
        Assert.DoesNotContain(".dwf", extensions);
    }
}

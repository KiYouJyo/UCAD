using UCAD.Core.IO;
using Xunit;

namespace UCAD.Core.Tests;

public sealed class AcadFormatRegistryTests
{
    [Theory]
    [InlineData("plan.dwg", true, true)]
    [InlineData("plan.DXF", true, true)]
    [InlineData("legacy.dxb", true, true)]
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
    [InlineData("profile.arg")]
    [InlineData("script.scr")]
    [InlineData("routine.lsp")]
    [InlineData("sheetset.dst")]
    [InlineData("plugin.arx")]
    public void PendingAutoCadFormatsAreRecognizedWithoutFalseCapabilities(string path)
    {
        Assert.True(CadAcadFileFormatRegistry.TryGetByPath(path, out var format));
        Assert.True(format.Capabilities.HasFlag(CadFileFormatCapabilities.Recognized));
        Assert.False(format.CanOpen);
        Assert.False(format.CanImport);
        Assert.False(format.CanExport);
        Assert.False(string.IsNullOrWhiteSpace(format.Transport));
        Assert.False(string.IsNullOrWhiteSpace(format.SupportNote));
    }

    [Theory]
    [InlineData("pattern.pat")]
    [InlineData("linetype.lin")]
    [InlineData("aliases.pgp")]
    public void TextResourcesExposeRealImportExportCapabilities(string path)
    {
        Assert.True(CadAcadFileFormatRegistry.TryGetByPath(path, out var format));
        Assert.Equal(CadFileFormatFamily.AutoCadResource, format.Family);
        Assert.True(format.CanImport);
        Assert.True(format.CanExport);
        Assert.True(format.Capabilities.HasFlag(CadFileFormatCapabilities.Resource));
        Assert.Contains("UCAD", format.Transport, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AutoCadEcosystemInventoryIncludesNativeSupportAndExecutableFamilies()
    {
        var extensions = CadAcadFileFormatRegistry.Formats
            .Select(format => format.Extension)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        Assert.Contains(".dst", extensions);
        Assert.Contains(".dsd", extensions);
        Assert.Contains(".dcl", extensions);
        Assert.Contains(".fmp", extensions);
        Assert.Contains(".cuix", extensions);
        Assert.Contains(".mnl", extensions);
        Assert.Contains(".dvb", extensions);
        Assert.Contains(".arx", extensions);
        Assert.Contains(".crx", extensions);
        Assert.Contains(".dbx", extensions);
        Assert.Contains(".pdf", extensions);
        Assert.Contains(".dgn", extensions);
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
        Assert.Contains(".dxb", extensions);
        Assert.Contains(".dwt", extensions);
        Assert.Contains(".dws", extensions);
        Assert.Contains(".bak", extensions);
        Assert.Contains(".sv$", extensions);
        Assert.DoesNotContain(".dwf", extensions);
        Assert.DoesNotContain(".dst", extensions);
    }
}

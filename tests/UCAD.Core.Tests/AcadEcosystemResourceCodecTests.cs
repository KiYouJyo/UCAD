using System.IO.Compression;
using System.Text;
using UCAD.Core.IO;
using Xunit;

namespace UCAD.Core.Tests;

public sealed class AcadEcosystemResourceCodecTests
{
    [Theory]
    [InlineData(".ctb")]
    [InlineData(".stb")]
    [InlineData(".pc3")]
    [InlineData(".pmp")]
    [InlineData(".shx")]
    [InlineData(".fas")]
    [InlineData(".vlx")]
    [InlineData(".dvb")]
    public void BinaryResourcesRoundTripExactlyWithoutExecution(string extension)
    {
        var bytes = Encoding.ASCII.GetBytes("AutoCAD-resource\0payload\u0001\u0002");
        var imported = CadAcadEcosystemResourceCodec.ImportLosslessBinary(bytes, extension);
        var exported = CadAcadEcosystemResourceCodec.ExportLosslessBinary(imported);

        Assert.Equal(extension, imported.Extension);
        Assert.Equal(bytes, exported);
        Assert.True(imported.Metadata.ContainsKey("sha256"));
    }

    [Fact]
    public void TextProfilePreservesOriginalAndExtractsSections()
    {
        const string arg = "[Profiles\\UCAD]\n\"Template\"=\"acad.dwt\"\nSupportPath=C:\\CAD\\Support\n";
        var imported = CadAcadEcosystemResourceCodec.ImportTextResource(arg, ".arg");

        Assert.Equal("acad.dwt", imported.Sections["Profiles\\UCAD"]["Template"]);
        Assert.Equal("C:\\CAD\\Support", imported.Sections["Profiles\\UCAD"]["SupportPath"]);
        Assert.Equal(arg, CadAcadEcosystemResourceCodec.ExportTextResource(imported));
    }

    [Fact]
    public void CuixInventoryReadsXmlButNeverExecutesMacros()
    {
        using var output = new MemoryStream();
        using (var archive = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true))
        {
            var entry = archive.CreateEntry("Customization.xml");
            using var writer = new StreamWriter(entry.Open(), new UTF8Encoding(false));
            writer.Write("<Root><Macro Name=\"Draw parcel\" Command=\"^C^C_PLINE\" /></Root>");
        }

        var migration = CadAcadEcosystemResourceCodec.ImportCuix(output.ToArray());
        Assert.Contains("Customization.xml", migration.Entries);
        Assert.Contains("Draw parcel", migration.CommandMetadata);
        Assert.Contains("^C^C_PLINE", migration.CommandMetadata);
        Assert.Equal(output.ToArray(), CadAcadEcosystemResourceCodec.ExportCuix(migration));
    }

    [Fact]
    public void ScriptParserProducesCommandAndArgumentStatements()
    {
        var script = CadAcadEcosystemResourceCodec.ParseScript("; comment\nLINE 0,0 10,0\nZOOM E\n");
        Assert.Equal(2, script.Statements.Count);
        Assert.Equal("LINE", script.Statements[0].Command);
        Assert.Equal(new[] { "0,0", "10,0" }, script.Statements[0].Arguments);
        Assert.Equal("ZOOM", script.Statements[1].Command);
    }

    [Fact]
    public void LispAnalyzerExtractsOnlySourceLevelMigrationMetadata()
    {
        const string source = "(defun c:parcel () (command \"PLINE\" \"0,0\" \"10,0\") (princ))";
        var report = CadAcadEcosystemResourceCodec.AnalyzeLispSource(source);
        Assert.Contains("c:parcel", report.DefinedFunctions, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("PLINE", report.CommandInvocations, StringComparer.OrdinalIgnoreCase);
        Assert.NotEmpty(report.Warnings);
    }
}

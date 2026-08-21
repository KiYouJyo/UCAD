using UCAD.Core.IO;
using Xunit;

namespace UCAD.Core.Tests;

public sealed class AcadTextResourceCodecTests
{
    [Fact]
    public void PatRoundTripPreservesPatternGeometry()
    {
        const string source = """
; ANSI-style sample
*UCAD_GRID,UCAD grid pattern
0,0,0,0,10,5,-2.5
90,0,0,10,0
""";

        var parsed = CadAcadTextResourceCodec.ParsePat(source);

        var pattern = Assert.Single(parsed);
        Assert.Equal("UCAD_GRID", pattern.Name);
        Assert.Equal("UCAD grid pattern", pattern.Description);
        Assert.Equal(2, pattern.Lines.Count);
        Assert.Equal(5, pattern.Lines[0].DashLengths[0], 8);
        Assert.Equal(-2.5, pattern.Lines[0].DashLengths[1], 8);

        var reparsed = Assert.Single(CadAcadTextResourceCodec.ParsePat(CadAcadTextResourceCodec.WritePat(parsed)));
        Assert.Equal(pattern.Name, reparsed.Name);
        Assert.Equal(pattern.Description, reparsed.Description);
        Assert.Equal(pattern.Lines.Count, reparsed.Lines.Count);
        for (var i = 0; i < pattern.Lines.Count; i++)
        {
            Assert.Equal(pattern.Lines[i].AngleDegrees, reparsed.Lines[i].AngleDegrees, 8);
            Assert.Equal(pattern.Lines[i].BaseX, reparsed.Lines[i].BaseX, 8);
            Assert.Equal(pattern.Lines[i].BaseY, reparsed.Lines[i].BaseY, 8);
            Assert.Equal(pattern.Lines[i].OffsetX, reparsed.Lines[i].OffsetX, 8);
            Assert.Equal(pattern.Lines[i].OffsetY, reparsed.Lines[i].OffsetY, 8);
            Assert.Equal(pattern.Lines[i].DashLengths, reparsed.Lines[i].DashLengths);
        }
    }

    [Fact]
    public void LinRoundTripPreservesComplexDefinitionText()
    {
        const string source = """
; Complex linetype syntax is preserved instead of partially interpreted.
*UCAD_GAS,Gas line ----GAS----
A,.5,-.2,["GAS",STANDARD,S=.1,R=0.0,X=-0.1,Y=-.05],-.25
""";

        var parsed = CadAcadTextResourceCodec.ParseLin(source);

        var linetype = Assert.Single(parsed);
        Assert.Equal("UCAD_GAS", linetype.Name);
        Assert.Contains("[\"GAS\",STANDARD", linetype.Definition, StringComparison.Ordinal);

        var reparsed = CadAcadTextResourceCodec.ParseLin(CadAcadTextResourceCodec.WriteLin(parsed));
        Assert.Equal(linetype, Assert.Single(reparsed));
    }

    [Fact]
    public void PgpParserImportsAliasesWithoutImportingExternalCommands()
    {
        const string source = """
; aliases
L, *LINE
PL, *PLINE ; inline comment
MYTOOL, start mytool.exe
C, *CIRCLE
""";

        var aliases = CadAcadTextResourceCodec.ParsePgpAliases(source);

        Assert.Equal(3, aliases.Count);
        Assert.Equal(new CadAcadCommandAlias("L", "LINE"), aliases[0]);
        Assert.Equal(new CadAcadCommandAlias("PL", "PLINE"), aliases[1]);
        Assert.Equal(new CadAcadCommandAlias("C", "CIRCLE"), aliases[2]);
        Assert.DoesNotContain(aliases, alias => alias.Alias == "MYTOOL");

        var reparsed = CadAcadTextResourceCodec.ParsePgpAliases(CadAcadTextResourceCodec.WritePgpAliases(aliases));
        Assert.Equal(aliases, reparsed);
    }

    [Fact]
    public void PatRejectsDefinitionBeforeHeader()
    {
        Assert.Throws<FormatException>(() => CadAcadTextResourceCodec.ParsePat("45,0,0,1,1"));
    }
}

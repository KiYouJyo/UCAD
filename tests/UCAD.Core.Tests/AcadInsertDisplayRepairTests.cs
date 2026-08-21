using ACadSharp.Entities;
using ACadSharp.IO;
using ACadSharp.Tables;
using CSMath;
using UCAD.Core.Entities;
using UCAD.Core.IO;
using Xunit;

namespace UCAD.Core.Tests;

public sealed class AcadInsertDisplayRepairTests
{
    [Theory]
    [InlineData(2.0, 3.0, 120.0)]
    [InlineData(-1.0, 1.0, 90.0)]
    public void DirectDxfImportExplodesUnsupportedInsertScaleForDisplay(double xScale, double yScale, double expectedEndX)
    {
        var block = new BlockRecord("SCALED_BLOCK");
        block.Entities.Add(new Line(new XYZ(0, 0, 0), new XYZ(10, 0, 0)));
        var insert = new Insert(block)
        {
            InsertPoint = new XYZ(100, 50, 0),
            XScale = xScale,
            YScale = yScale
        };
        var source = new ACadSharp.CadDocument();
        source.Entities.Add(insert);

        var result = CadAcadInteropCodec.ImportDxf(WriteTextDxf(source));

        Assert.Empty(result.Document.Entities.OfType<BlockReferenceEntity>());
        var line = Assert.Single(result.Document.Entities.OfType<LineEntity>());
        Assert.Equal(100, line.Start.X, 8);
        Assert.Equal(50, line.Start.Y, 8);
        Assert.Equal(expectedEndX, line.End.X, 8);
        Assert.Equal(50, line.End.Y, 8);
    }

    [Fact]
    public void DirectDxfImportExpandsMInsertArrayInsteadOfShowingOnlyFirstCell()
    {
        var block = new BlockRecord("ARRAY_BLOCK");
        block.Entities.Add(new Line(new XYZ(0, 0, 0), new XYZ(10, 0, 0)));
        var insert = new Insert(block)
        {
            InsertPoint = new XYZ(100, 50, 0),
            XScale = 1,
            YScale = 1,
            ColumnCount = 2,
            ColumnSpacing = 20,
            RowCount = 1
        };
        var source = new ACadSharp.CadDocument();
        source.Entities.Add(insert);

        var result = CadAcadInteropCodec.ImportDxf(WriteTextDxf(source));

        Assert.Empty(result.Document.Entities.OfType<BlockReferenceEntity>());
        var lines = result.Document.Entities.OfType<LineEntity>().OrderBy(line => line.Start.X).ToArray();
        Assert.Equal(2, lines.Length);
        Assert.Equal(100, lines[0].Start.X, 8);
        Assert.Equal(110, lines[0].End.X, 8);
        Assert.Equal(120, lines[1].Start.X, 8);
        Assert.Equal(130, lines[1].End.X, 8);
    }

    [Fact]
    public void DirectDxfImportExpandsUniformNestedInsertInsteadOfDroppingInnerGeometry()
    {
        var inner = new BlockRecord("INNER_BLOCK");
        inner.Entities.Add(new Line(new XYZ(0, 0, 0), new XYZ(8, 0, 0)));

        var outer = new BlockRecord("OUTER_BLOCK");
        outer.Entities.Add(new Insert(inner)
        {
            InsertPoint = new XYZ(5, 4, 0),
            XScale = 1,
            YScale = 1
        });

        var source = new ACadSharp.CadDocument();
        source.Entities.Add(new Insert(outer)
        {
            InsertPoint = new XYZ(100, 50, 0),
            XScale = 1,
            YScale = 1
        });

        var result = CadAcadInteropCodec.ImportDxf(WriteTextDxf(source));

        Assert.Empty(result.Document.Entities.OfType<BlockReferenceEntity>());
        var line = Assert.Single(result.Document.Entities.OfType<LineEntity>());
        Assert.Equal(105, line.Start.X, 8);
        Assert.Equal(54, line.Start.Y, 8);
        Assert.Equal(113, line.End.X, 8);
        Assert.Equal(54, line.End.Y, 8);
        Assert.Contains(result.Warnings, warning => warning.Contains("nested block references", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void DirectDxfImportUsesAnonymousEvaluatedBlockGeometryAsDisplaySnapshot()
    {
        var evaluated = new BlockRecord("*U123");
        evaluated.Entities.Add(new Line(new XYZ(0, 0, 0), new XYZ(6, 0, 0)));
        var source = new ACadSharp.CadDocument();
        source.Entities.Add(new Insert(evaluated)
        {
            InsertPoint = new XYZ(25, 30, 0),
            XScale = 1,
            YScale = 1
        });

        var result = CadAcadInteropCodec.ImportDxf(WriteTextDxf(source));

        Assert.Empty(result.Document.Entities.OfType<BlockReferenceEntity>());
        var line = Assert.Single(result.Document.Entities.OfType<LineEntity>());
        Assert.Equal(25, line.Start.X, 8);
        Assert.Equal(31, line.EndXOrFallback(), 8);
        Assert.Contains(result.Warnings, warning => warning.Contains("evaluated block", StringComparison.OrdinalIgnoreCase));
    }

    private static byte[] WriteTextDxf(ACadSharp.CadDocument document)
    {
        using var stream = new MemoryStream();
        using (var writer = new DxfWriter(stream, document, binary: false)) writer.Write();
        return stream.ToArray();
    }
}

internal static class AcadInsertDisplayRepairTestExtensions
{
    public static double EndXOrFallback(this LineEntity line) => line.End.X;
}

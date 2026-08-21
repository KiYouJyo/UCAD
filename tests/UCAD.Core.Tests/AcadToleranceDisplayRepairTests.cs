using ACadSharp.Entities;
using ACadSharp.IO;
using CSMath;
using UCAD.Core.Entities;
using UCAD.Core.IO;
using Xunit;

namespace UCAD.Core.Tests;

public sealed class AcadToleranceDisplayRepairTests
{
    [Fact]
    public void DirectDxfImportRestoresToleranceAsVisibleAnnotation()
    {
        var source = new ACadSharp.CadDocument();
        source.Entities.Add(new Tolerance
        {
            InsertionPoint = new XYZ(12, 34, 0),
            Direction = new XYZ(0, 1, 0),
            Text = "⌀0.1|A"
        });

        using var stream = new MemoryStream();
        using (var writer = new DxfWriter(stream, source, binary: false)) writer.Write();
        var result = CadAcadInteropCodec.ImportDxf(stream.ToArray());

        var annotation = Assert.Single(result.Document.Entities.OfType<MTextEntity>());
        Assert.Equal("⌀0.1|A", annotation.Text);
        Assert.Equal(12, annotation.Position.X, 8);
        Assert.Equal(34, annotation.Position.Y, 8);
        Assert.Equal(Math.PI / 2, annotation.RotationRadians, 8);
        Assert.Contains(result.Warnings, warning => warning.Contains("TOLERANCE", StringComparison.OrdinalIgnoreCase));
    }
}

using UCAD.Core.Entities;
using UCAD.Core.Geometry;
using UCAD.Core.IO;
using UCAD.Core.Styles;
using Xunit;

namespace UCAD.Core.Tests;

public sealed class AnnotationStyleTests
{
    [Fact]
    public void StyleTablesAreUndoableAndCurrentStylesFollowDocumentState()
    {
        var document = new CadDocument();
        document.ResetHistory();
        document.DefineTextStyle(new CadTextStyle("Planning", "Arial", 0.9));
        document.SetCurrentTextStyle("Planning");
        document.DefineDimensionStyle(new CadDimensionStyle("Metric", 3, 2, 1, suffix: " mm"));
        document.SetCurrentDimensionStyle("Metric");

        Assert.Equal("Planning", document.CurrentTextStyleName);
        Assert.Equal("Metric", document.CurrentDimensionStyleName);
        Assert.Equal(2, document.TextStyles.Count);
        Assert.Equal(2, document.DimensionStyles.Count);
        Assert.True(document.CanUndo);

        Assert.True(document.Undo());
        Assert.Equal(CadDimensionStyle.DefaultName, document.CurrentDimensionStyleName);
    }

    [Fact]
    public void NativeRoundTripPreservesAnnotationEntitiesAndStyles()
    {
        var document = new CadDocument();
        document.DefineTextStyle(new CadTextStyle("Notes", "Segoe UI", 0.85, 8));
        document.SetCurrentTextStyle("Notes");
        document.DefineDimensionStyle(new CadDimensionStyle("Urban", 3, 2.5, 1, suffix: " m"));
        document.SetCurrentDimensionStyle("Urban");

        document.Add(new TextEntity(new CadPoint(1, 1), "A", 2.5, 0, "Notes"));
        document.Add(new MTextEntity(new CadPoint(2, 2), "line one\nline two", 2.5, 30, 0, "Notes"));
        document.Add(new LinearDimensionEntity(new CadPoint(0, 0), new CadPoint(10, 0), new CadPoint(0, 3), null, "Urban"));
        document.Add(new AngularDimensionEntity(
            new CadPoint(0, 0), new CadPoint(10, 0), new CadPoint(0, 10), new CadPoint(5, 5), null, "Urban"));
        document.Add(new RadialDimensionEntity(
            new CadPoint(20, 20), new CadPoint(25, 20), new CadPoint(28, 22), diameter: true, styleName: "Urban"));
        document.Add(new LeaderEntity(
            [new CadPoint(0, 0), new CadPoint(5, 5), new CadPoint(12, 5)], "note", 2.5, "Notes"));

        var restored = CadNativeDocumentCodec.Deserialize(CadNativeDocumentCodec.Serialize(document));

        Assert.Equal("Notes", restored.CurrentTextStyleName);
        Assert.Equal("Urban", restored.CurrentDimensionStyleName);
        Assert.Equal(2, restored.TextStyles.Count);
        Assert.Equal(2, restored.DimensionStyles.Count);
        Assert.Equal(6, restored.Entities.Count);
        Assert.Equal("Notes", Assert.IsType<TextEntity>(restored.Entities[0]).StyleName);
        Assert.Equal("Notes", Assert.IsType<MTextEntity>(restored.Entities[1]).StyleName);
        Assert.Equal("Urban", Assert.IsType<LinearDimensionEntity>(restored.Entities[2]).StyleName);
        Assert.IsType<AngularDimensionEntity>(restored.Entities[3]);
        var radial = Assert.IsType<RadialDimensionEntity>(restored.Entities[4]);
        Assert.True(radial.Diameter);
        Assert.IsType<LeaderEntity>(restored.Entities[5]);
    }

    [Fact]
    public void MTextWrapAndDimensionMeasurementsAreStable()
    {
        var mtext = new MTextEntity(new CadPoint(0, 0), "abcdefghij", textHeight: 2, width: 6);
        Assert.True(mtext.ApproximateLines().Count >= 2);

        var angle = new AngularDimensionEntity(
            new CadPoint(0, 0), new CadPoint(10, 0), new CadPoint(0, 10), new CadPoint(5, 5));
        Assert.Equal(Math.PI / 2, angle.MeasurementRadians, 8);

        var diameter = new RadialDimensionEntity(
            new CadPoint(0, 0), new CadPoint(5, 0), new CadPoint(7, 2), diameter: true);
        Assert.Equal(10, diameter.Measurement, 8);
    }
}
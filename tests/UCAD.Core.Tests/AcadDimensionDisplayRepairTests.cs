using System.Reflection;
using ACadSharp.Entities;
using CSMath;
using UCAD.Core.Entities;
using Xunit;

namespace UCAD.Core.Tests;

public sealed class AcadDimensionDisplayRepairTests
{
    [Fact]
    public void RotatedLinearDimensionIsRecoveredAsDisplayGeometry()
    {
        var source = new ACadSharp.CadDocument();
        source.Entities.Add(new DimensionLinear
        {
            FirstPoint = new XYZ(0, 0, 0),
            SecondPoint = new XYZ(12, 7, 0),
            DefinitionPoint = new XYZ(0, 16, 0),
            Rotation = 0
        });

        var target = new UCAD.Core.CadDocument();
        var warnings = new List<string>();
        InvokeSemanticRepair(source, target, warnings);
        Assert.Single(target.Entities.OfType<LinearDimensionEntity>());

        InvokeDisplayRepair(source, target, warnings);

        Assert.Empty(target.Entities.OfType<LinearDimensionEntity>());
        Assert.True(target.Entities.Count >= 3);
        Assert.Contains(target.Entities, entity => entity is LineEntity or PolylineEntity or MTextEntity or UCAD.Core.Entities.TextEntity);
        Assert.Contains(warnings, warning => warning.Contains("DimensionLinear", StringComparison.Ordinal));
    }

    [Fact]
    public void OrdinateDimensionIsRecoveredInsteadOfDisappearing()
    {
        var source = new ACadSharp.CadDocument();
        source.Entities.Add(new DimensionOrdinate
        {
            DefinitionPoint = new XYZ(0, 0, 0),
            FeatureLocation = new XYZ(8, 5, 0),
            LeaderEndpoint = new XYZ(18, 12, 0),
            IsOrdinateTypeX = true
        });

        var target = new UCAD.Core.CadDocument();
        var warnings = new List<string>();
        InvokeSemanticRepair(source, target, warnings);
        Assert.Empty(target.Entities);

        InvokeDisplayRepair(source, target, warnings);

        Assert.NotEmpty(target.Entities);
        Assert.Contains(target.Entities, entity => entity is LineEntity or PolylineEntity or MTextEntity or UCAD.Core.Entities.TextEntity);
        Assert.Contains(warnings, warning => warning.Contains("DimensionOrdinate", StringComparison.Ordinal));
    }

    private static void InvokeSemanticRepair(ACadSharp.CadDocument source, UCAD.Core.CadDocument target, List<string> warnings) =>
        Invoke("UCAD.Core.IO.CadAcadDwgSemanticRepair", source, target, warnings);

    private static void InvokeDisplayRepair(ACadSharp.CadDocument source, UCAD.Core.CadDocument target, List<string> warnings) =>
        Invoke("UCAD.Core.IO.CadAcadDimensionDisplayRepair", source, target, warnings);

    private static void Invoke(string typeName, ACadSharp.CadDocument source, UCAD.Core.CadDocument target, List<string> warnings)
    {
        var repairType = typeof(UCAD.Core.IO.CadAcadInteropCodec).Assembly.GetType(typeName, throwOnError: true)!;
        var apply = repairType.GetMethod("Apply", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        Assert.NotNull(apply);
        apply!.Invoke(null, [source, target, warnings]);
    }
}

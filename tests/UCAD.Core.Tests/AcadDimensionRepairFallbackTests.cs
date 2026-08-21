using System.Reflection;
using ACadSharp.Entities;
using CSMath;
using UCAD.Core.Entities;
using UCAD.Core.Geometry;
using UCAD.Core.IO;
using Xunit;

namespace UCAD.Core.Tests;

public sealed class AcadDimensionRepairFallbackTests
{
    [Fact]
    public void NativeDimensionRepairRetainsNormalizedFallbackWhenAnyDimensionIsDegenerate()
    {
        var source = new ACadSharp.CadDocument();
        source.Entities.Add(new DimensionAligned
        {
            FirstPoint = new XYZ(10, 10, 0),
            SecondPoint = new XYZ(10, 10, 0),
            DefinitionPoint = new XYZ(10, 20, 0)
        });

        var target = new UCAD.Core.CadDocument();
        var fallback = new LinearDimensionEntity(
            new CadPoint(0, 0),
            new CadPoint(10, 0),
            new CadPoint(0, 5));
        target.Add(fallback);
        var warnings = new List<string>();

        InvokeSemanticRepair(source, target, warnings);

        Assert.Same(fallback, Assert.Single(target.Entities.OfType<LinearDimensionEntity>()));
        Assert.Contains(warnings, warning => warning.Contains("could not be upgraded safely", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(warnings, warning => warning.Contains("skipped atomically", StringComparison.OrdinalIgnoreCase));
    }

    private static void InvokeSemanticRepair(ACadSharp.CadDocument source, UCAD.Core.CadDocument target, List<string> warnings)
    {
        var repairType = typeof(CadAcadInteropCodec).Assembly.GetType("UCAD.Core.IO.CadAcadDwgSemanticRepair", throwOnError: true)!;
        var apply = repairType.GetMethod("Apply", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        Assert.NotNull(apply);
        apply!.Invoke(null, [source, target, warnings]);
    }
}

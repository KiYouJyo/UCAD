using System.Reflection;
using ACadSharp.Entities;
using CSMath;
using UCAD.Core.Entities;
using UCAD.Core.Geometry;
using Xunit;

namespace UCAD.Core.Tests;

public sealed class AcadSourceDrawOrderTests
{
    [Fact]
    public void DxfSourceOrderKeepsGapForUnsupportedMiddleEntity()
    {
        const string dxf = "0\nSECTION\n2\nENTITIES\n" +
                           "0\nLINE\n5\nA\n10\n0\n20\n0\n11\n10\n21\n0\n" +
                           "0\nUNSUPPORTED_VISUAL\n5\nB\n" +
                           "0\nLINE\n5\nC\n10\n0\n20\n5\n11\n10\n21\n5\n" +
                           "0\nENDSEC\n0\nEOF\n";
        var target = new UCAD.Core.CadDocument();
        var first = new LineEntity(new CadPoint(0, 0), new CadPoint(10, 0));
        var second = new LineEntity(new CadPoint(0, 5), new CadPoint(10, 5));
        target.Add(first);
        target.Add(second);

        InvokeInternal("UCAD.Core.IO.CadDxfSourceOrderRepair", "Apply", dxf, target);

        var firstProperties = target.GetEntityProperties(first.Id);
        var secondProperties = target.GetEntityProperties(second.Id);
        Assert.Equal(0, firstProperties.SourceOrder);
        Assert.Equal("A", firstProperties.SourceHandle);
        Assert.Equal(2, secondProperties.SourceOrder);
        Assert.Equal("C", secondProperties.SourceHandle);
    }

    [Fact]
    public void NativeWipeoutCarriesItsMiddleSourceOrderInsteadOfBeingAppendedOnTop()
    {
        var source = new ACadSharp.CadDocument();
        source.Entities.Add(new Line(new XYZ(0, 0, 0), new XYZ(10, 0, 0)));
        source.Entities.Add(new Wipeout
        {
            InsertPoint = new XYZ(2, 2, 0),
            UVector = new XYZ(1, 0, 0),
            VVector = new XYZ(0, 1, 0),
            Size = new XY(5, 4)
        });
        source.Entities.Add(new Line(new XYZ(0, 8, 0), new XYZ(10, 8, 0)));

        var target = new UCAD.Core.CadDocument();
        var warnings = new List<string>();
        InvokeInternal("UCAD.Core.IO.CadAcadWipeoutDisplayRepair", "Apply", source, target, warnings);

        var wipeout = Assert.Single(target.Entities.OfType<WipeoutEntity>());
        Assert.Equal(1, target.GetEntityProperties(wipeout.Id).SourceOrder);
        Assert.True(wipeout.MaskEnabled);
        Assert.Equal(4, wipeout.Boundary.Count);
    }

    private static void InvokeInternal(string typeName, string methodName, params object[] args)
    {
        var type = typeof(UCAD.Core.IO.CadAcadInteropCodec).Assembly.GetType(typeName, throwOnError: true)!;
        var method = type.GetMethod(methodName, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        Assert.NotNull(method);
        method!.Invoke(null, args);
    }
}

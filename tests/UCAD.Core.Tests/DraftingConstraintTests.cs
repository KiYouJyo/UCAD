using UCAD.Core.Geometry;
using UCAD.Core.Interaction;
using Xunit;

namespace UCAD.Core.Tests;

public sealed class DraftingConstraintTests
{
    [Fact]
    public void GridSnapRoundsToNearestWorldSpacing()
    {
        Assert.Equal(new CadPoint(20, -10), DraftingConstraints.SnapToGrid(new CadPoint(16.2, -6.1), 10));
        Assert.Equal(new CadPoint(-10, 10), DraftingConstraints.SnapToGrid(new CadPoint(-5.1, 5.1), 10));
    }

    [Fact]
    public void PolarTrackingPreservesRadiusAndSnapsAngle()
    {
        var origin = new CadPoint(0, 0);
        var raw = new CadPoint(9, 4);
        var result = DraftingConstraints.ApplyPolar(origin, raw, 45);

        Assert.Equal((raw - origin).Length, (result - origin).Length, 8);
        Assert.Equal(result.X, result.Y, 8);
    }

    [Fact]
    public void ObjectTrackingAlignsToStoredSnapAnchor()
    {
        var anchor = new CadPoint(100, 50);

        Assert.True(DraftingConstraints.TryApplyObjectTracking(
            anchor,
            new CadPoint(102, 80),
            3,
            out var vertical,
            out var verticalAxis));
        Assert.Equal(new CadPoint(100, 80), vertical);
        Assert.Equal(ObjectTrackingAxis.Vertical, verticalAxis);

        Assert.True(DraftingConstraints.TryApplyObjectTracking(
            anchor,
            new CadPoint(140, 48),
            3,
            out var horizontal,
            out var horizontalAxis));
        Assert.Equal(new CadPoint(140, 50), horizontal);
        Assert.Equal(ObjectTrackingAxis.Horizontal, horizontalAxis);
    }

    [Fact]
    public void ObjectTrackingIgnoresPointersOutsideAperture()
    {
        Assert.False(DraftingConstraints.TryApplyObjectTracking(
            new CadPoint(0, 0),
            new CadPoint(10, 10),
            2,
            out var result,
            out var axis));
        Assert.Equal(new CadPoint(10, 10), result);
        Assert.Equal(ObjectTrackingAxis.None, axis);
    }
}
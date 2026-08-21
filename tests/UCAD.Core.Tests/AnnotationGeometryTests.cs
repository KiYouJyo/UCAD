using UCAD.Core.Entities;
using UCAD.Core.Geometry;
using UCAD.Core.Interaction;
using Xunit;

namespace UCAD.Core.Tests;

public sealed class AnnotationGeometryTests
{
    [Fact]
    public void MTextCanBeHitSelectedAndWindowSelected()
    {
        var text = new MTextEntity(new CadPoint(10, 10), "planning note", textHeight: 2, width: 20);

        var hit = CadSelectionQuery.HitTestNearest([text], new CadPoint(12, 11), 1);
        Assert.NotNull(hit);
        Assert.Equal(text.Id, hit!.Id);

        var window = CadRect.FromPoints(new CadPoint(8, 8), new CadPoint(35, 20));
        Assert.Contains(text.Id, CadSelectionQuery.QueryWindow([text], window, crossing: false));
    }

    [Fact]
    public void AngularDimensionExposesArcMidpointAndCenterSnaps()
    {
        var dimension = new AngularDimensionEntity(
            new CadPoint(0, 0),
            new CadPoint(10, 0),
            new CadPoint(0, 10),
            new CadPoint(5, 5));
        var midpoint = dimension.GetArcMidpoint();

        var midSnap = ObjectSnapResolver.Resolve([dimension], midpoint, 0.5, ObjectSnapMode.Midpoint);
        Assert.NotNull(midSnap);
        Assert.Equal(ObjectSnapKind.Midpoint, midSnap!.Kind);

        var centerSnap = ObjectSnapResolver.Resolve([dimension], new CadPoint(0.1, 0.1), 0.5, ObjectSnapMode.Center);
        Assert.NotNull(centerSnap);
        Assert.Equal(dimension.Vertex, centerSnap!.Point);
    }

    [Fact]
    public void RadialDimensionAndLeaderParticipateInCrossingSelection()
    {
        var radial = new RadialDimensionEntity(
            new CadPoint(20, 20),
            new CadPoint(25, 20),
            new CadPoint(30, 22));
        var leader = new LeaderEntity(
            [new CadPoint(0, 0), new CadPoint(5, 5), new CadPoint(12, 5)],
            "note");

        var radialWindow = CadRect.FromPoints(new CadPoint(24, 19), new CadPoint(27, 21));
        Assert.Contains(radial.Id, CadSelectionQuery.QueryWindow([radial], radialWindow, crossing: true));

        var leaderWindow = CadRect.FromPoints(new CadPoint(4, 4), new CadPoint(7, 6));
        Assert.Contains(leader.Id, CadSelectionQuery.QueryWindow([leader], leaderWindow, crossing: true));
    }

    [Fact]
    public void AnnotationGripSetsExposeEditableControlPoints()
    {
        var text = new MTextEntity(new CadPoint(0, 0), "abc", 2.5, 20);
        var radial = new RadialDimensionEntity(new CadPoint(0, 0), new CadPoint(5, 0), new CadPoint(8, 2));
        var leader = new LeaderEntity([new CadPoint(1, 1), new CadPoint(3, 4)], "A");

        Assert.Equal(2, CadAnnotationEntityGeometry.GetGripPoints(text).Count);
        Assert.Equal(3, CadAnnotationEntityGeometry.GetGripPoints(radial).Count);
        Assert.Equal(2, CadAnnotationEntityGeometry.GetGripPoints(leader).Count);
    }
}
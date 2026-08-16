using UCAD.Core.Entities;
using UCAD.Core.Geometry;
using UCAD.Core.Interaction;
using Xunit;

namespace UCAD.Core.Tests;

public sealed class InteractionTests
{
    [Fact]
    public void SelectionSetPrunesEntitiesRemovedFromDocument()
    {
        var document = new CadDocument();
        var line = new LineEntity(new CadPoint(0, 0), new CadPoint(10, 0));
        document.Add(line);
        var selection = new SelectionSet(document);

        Assert.True(selection.Replace(line.Id));
        Assert.Single(selection.SelectedEntities);

        document.Remove(line.Id);

        Assert.True(selection.IsEmpty);
    }

    [Fact]
    public void SelectionSetSupportsAdditiveMultiSelection()
    {
        var document = new CadDocument();
        var first = new LineEntity(new CadPoint(0, 0), new CadPoint(10, 0));
        var second = new CircleEntity(new CadPoint(20, 0), 5);
        document.Add(first);
        document.Add(second);
        var selection = new SelectionSet(document);

        Assert.True(selection.Add(first.Id));
        Assert.True(selection.Add(second.Id));
        Assert.Equal(2, selection.Count);
        Assert.Contains(selection.SelectedEntities, entity => entity.Id == first.Id);
        Assert.Contains(selection.SelectedEntities, entity => entity.Id == second.Id);
    }

    [Fact]
    public void SelectionSetSupportsShiftStyleBatchRemovalWithSingleChange()
    {
        var document = new CadDocument();
        var first = new LineEntity(new CadPoint(0, 0), new CadPoint(10, 0));
        var second = new CircleEntity(new CadPoint(20, 0), 5);
        var third = new LineEntity(new CadPoint(30, 0), new CadPoint(40, 0));
        document.Add(first);
        document.Add(second);
        document.Add(third);
        var selection = new SelectionSet(document);
        selection.Add([first.Id, second.Id, third.Id]);
        var changedCount = 0;
        selection.Changed += (_, _) => changedCount++;

        Assert.True(selection.Remove([first.Id, third.Id]));

        Assert.Equal(1, changedCount);
        Assert.Single(selection.SelectedIds);
        Assert.Contains(second.Id, selection.SelectedIds);
    }

    [Fact]
    public void PointHitTestReturnsNearestVisibleEntity()
    {
        var line = new LineEntity(new CadPoint(0, 0), new CadPoint(10, 0));
        var circle = new CircleEntity(new CadPoint(5, 5), 2);

        var hit = CadSelectionQuery.HitTestNearest([line, circle], new CadPoint(4.8, 0.2), 0.5);

        Assert.Same(line, hit);
    }

    [Fact]
    public void HitTestSupportsCircleAndArcCurves()
    {
        var circle = new CircleEntity(new CadPoint(0, 0), 5);
        Assert.Same(circle, CadSelectionQuery.HitTestNearest([circle], new CadPoint(5.1, 0), 0.2));

        Assert.True(ArcEntity.TryCreateFromThreePoints(
            new CadPoint(5, 0),
            new CadPoint(0, 5),
            new CadPoint(-5, 0),
            out var arc));
        Assert.NotNull(arc);
        Assert.Same(arc, CadSelectionQuery.HitTestNearest([arc!], new CadPoint(0, 5.1), 0.2));
        Assert.Null(CadSelectionQuery.HitTestNearest([arc!], new CadPoint(0, -5), 0.2));
    }

    [Fact]
    public void CrossingWindowSelectsIntersectingLineButWindowDoesNot()
    {
        var line = new LineEntity(new CadPoint(-10, 0), new CadPoint(10, 0));
        var rectangle = CadRect.FromPoints(new CadPoint(-2, -2), new CadPoint(2, 2));

        Assert.Empty(CadSelectionQuery.QueryWindow([line], rectangle, crossing: false));
        Assert.Equal(line.Id, Assert.Single(CadSelectionQuery.QueryWindow([line], rectangle, crossing: true)));
    }

    [Fact]
    public void WindowSelectionRequiresWholeCircleToBeContained()
    {
        var circle = new CircleEntity(new CadPoint(0, 0), 5);
        var small = CadRect.FromPoints(new CadPoint(-2, -2), new CadPoint(2, 2));
        var large = CadRect.FromPoints(new CadPoint(-6, -6), new CadPoint(6, 6));

        Assert.Empty(CadSelectionQuery.QueryWindow([circle], small, crossing: false));
        Assert.Equal(circle.Id, Assert.Single(CadSelectionQuery.QueryWindow([circle], large, crossing: false)));
    }

    [Fact]
    public void ObjectSnapResolvesEndpointMidpointAndIntersection()
    {
        var horizontal = new LineEntity(new CadPoint(0, 0), new CadPoint(10, 0));
        var vertical = new LineEntity(new CadPoint(5, -5), new CadPoint(5, 5));
        var modes = ObjectSnapMode.Endpoint | ObjectSnapMode.Midpoint | ObjectSnapMode.Intersection;

        var endpoint = ObjectSnapResolver.Resolve([horizontal], new CadPoint(0.1, 0.1), 0.5, modes);
        Assert.NotNull(endpoint);
        Assert.Equal(ObjectSnapKind.Endpoint, endpoint!.Kind);
        Assert.Equal(new CadPoint(0, 0), endpoint.Point);

        var midpoint = ObjectSnapResolver.Resolve([horizontal], new CadPoint(5.1, 0.1), 0.5, modes);
        Assert.NotNull(midpoint);
        Assert.Equal(ObjectSnapKind.Midpoint, midpoint!.Kind);
        Assert.Equal(new CadPoint(5, 0), midpoint.Point);

        var intersection = ObjectSnapResolver.Resolve([horizontal, vertical], new CadPoint(5.05, 0.05), 0.5, modes);
        Assert.NotNull(intersection);
        Assert.Equal(ObjectSnapKind.Intersection, intersection!.Kind);
        Assert.Equal(new CadPoint(5, 0), intersection.Point);
    }

    [Fact]
    public void ObjectSnapCenterIsAvailableWhenExplicitlyEnabled()
    {
        var circle = new CircleEntity(new CadPoint(12, 7), 5);

        Assert.Null(ObjectSnapResolver.Resolve(
            [circle],
            new CadPoint(12.1, 7.1),
            0.5,
            ObjectSnapMode.Endpoint | ObjectSnapMode.Midpoint));

        var center = ObjectSnapResolver.Resolve(
            [circle],
            new CadPoint(12.1, 7.1),
            0.5,
            ObjectSnapMode.Center);

        Assert.NotNull(center);
        Assert.Equal(ObjectSnapKind.Center, center!.Kind);
        Assert.Equal(circle.Center, center.Point);
    }

    [Fact]
    public void LineCircleIntersectionIsAvailableToObjectSnap()
    {
        var line = new LineEntity(new CadPoint(-10, 0), new CadPoint(10, 0));
        var circle = new CircleEntity(new CadPoint(0, 0), 5);

        var intersections = CadEntityGeometry.Intersections(line, circle);

        Assert.Equal(2, intersections.Count);
        Assert.Contains(intersections, point => Math.Abs(point.X - 5) < 1e-8 && Math.Abs(point.Y) < 1e-8);
        Assert.Contains(intersections, point => Math.Abs(point.X + 5) < 1e-8 && Math.Abs(point.Y) < 1e-8);
    }

    [Fact]
    public void CircleCircleIntersectionIsAvailableToObjectSnap()
    {
        var first = new CircleEntity(new CadPoint(0, 0), 5);
        var second = new CircleEntity(new CadPoint(8, 0), 5);

        var intersections = CadEntityGeometry.Intersections(first, second);

        Assert.Equal(2, intersections.Count);
        Assert.All(intersections, point => Assert.Equal(4, point.X, 8));
        Assert.Contains(intersections, point => Math.Abs(point.Y - 3) < 1e-8);
        Assert.Contains(intersections, point => Math.Abs(point.Y + 3) < 1e-8);
    }

    [Theory]
    [InlineData(8, 2, 8, 0)]
    [InlineData(2, 8, 0, 8)]
    [InlineData(-8, 2, -8, 0)]
    [InlineData(2, -8, 0, -8)]
    public void OrthoConstrainsToDominantAxis(double x, double y, double expectedX, double expectedY)
    {
        var result = OrthoConstraint.Apply(new CadPoint(0, 0), new CadPoint(x, y));
        Assert.Equal(new CadPoint(expectedX, expectedY), result);
    }
}

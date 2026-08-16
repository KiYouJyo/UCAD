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
    public void PointHitTestReturnsNearestVisibleEntity()
    {
        var line = new LineEntity(new CadPoint(0, 0), new CadPoint(10, 0));
        var circle = new CircleEntity(new CadPoint(5, 5), 2);

        var hit = CadSelectionQuery.HitTestNearest([line, circle], new CadPoint(4.8, 0.2), 0.5);

        Assert.Same(line, hit);
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
    public void LineCircleIntersectionIsAvailableToObjectSnap()
    {
        var line = new LineEntity(new CadPoint(-10, 0), new CadPoint(10, 0));
        var circle = new CircleEntity(new CadPoint(0, 0), 5);

        var intersections = CadEntityGeometry.Intersections(line, circle);

        Assert.Equal(2, intersections.Count);
        Assert.Contains(intersections, point => Math.Abs(point.X - 5) < 1e-8 && Math.Abs(point.Y) < 1e-8);
        Assert.Contains(intersections, point => Math.Abs(point.X + 5) < 1e-8 && Math.Abs(point.Y) < 1e-8);
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

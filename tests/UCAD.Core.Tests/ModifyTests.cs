using UCAD.Core.Entities;
using UCAD.Core.Geometry;
using UCAD.Core.Interaction;
using UCAD.Core.Modify;
using Xunit;

namespace UCAD.Core.Tests;

public sealed class ModifyTests
{
    [Fact]
    public void MovePreservesIdentityAndSelectionAcrossOneReplaceTransaction()
    {
        var document = new CadDocument();
        var line = new LineEntity(new CadPoint(0, 0), new CadPoint(10, 0));
        document.Add(line);
        var selection = new SelectionSet(document);
        selection.Add(line.Id);
        var revisionBefore = document.Revision;

        var moved = CadEntityTransform.Translate(line, new CadVector(5, 3));
        Assert.Equal(1, document.ReplaceRange([moved]));

        var result = Assert.IsType<LineEntity>(Assert.Single(document.Entities));
        Assert.Equal(line.Id, result.Id);
        Assert.Equal(new CadPoint(5, 3), result.Start);
        Assert.Equal(new CadPoint(15, 3), result.End);
        Assert.Contains(line.Id, selection.SelectedIds);
        Assert.Equal(revisionBefore + 1, document.Revision);

        Assert.True(document.Undo());
        var restored = Assert.IsType<LineEntity>(Assert.Single(document.Entities));
        Assert.Equal(new CadPoint(0, 0), restored.Start);
    }

    [Fact]
    public void CopyCreatesFreshIdentity()
    {
        var source = new CircleEntity(new CadPoint(2, 3), 4);
        var copy = Assert.IsType<CircleEntity>(
            CadEntityTransform.Translate(source, new CadVector(10, -2), preserveIdentity: false));

        Assert.NotEqual(source.Id, copy.Id);
        Assert.Equal(new CadPoint(12, 1), copy.Center);
        Assert.Equal(4, copy.Radius);
    }

    [Fact]
    public void RotateScaleAndMirrorShareImmutableTransformPipeline()
    {
        var line = new LineEntity(new CadPoint(1, 0), new CadPoint(2, 0));

        var rotated = Assert.IsType<LineEntity>(CadEntityTransform.Rotate(line, new CadPoint(0, 0), Math.PI / 2));
        AssertClose(new CadPoint(0, 1), rotated.Start);
        AssertClose(new CadPoint(0, 2), rotated.End);
        Assert.Equal(line.Id, rotated.Id);

        var scaled = Assert.IsType<LineEntity>(CadEntityTransform.Scale(line, new CadPoint(0, 0), 2));
        AssertClose(new CadPoint(2, 0), scaled.Start);
        AssertClose(new CadPoint(4, 0), scaled.End);

        var mirrored = Assert.IsType<LineEntity>(CadEntityTransform.Mirror(
            line,
            new CadPoint(0, 0),
            new CadPoint(0, 10)));
        AssertClose(new CadPoint(-1, 0), mirrored.Start);
        AssertClose(new CadPoint(-2, 0), mirrored.End);
    }

    [Fact]
    public void ScaleCircleAndArcPreservesCenterTopologyAndIdentity()
    {
        var circle = new CircleEntity(new CadPoint(5, 5), 2);
        var scaledCircle = Assert.IsType<CircleEntity>(CadEntityTransform.Scale(circle, new CadPoint(0, 0), 3));
        Assert.Equal(circle.Id, scaledCircle.Id);
        AssertClose(new CadPoint(15, 15), scaledCircle.Center);
        Assert.Equal(6, scaledCircle.Radius, 8);

        Assert.True(ArcEntity.TryCreateFromThreePoints(
            new CadPoint(1, 0), new CadPoint(0, 1), new CadPoint(-1, 0), out var arc));
        var scaledArc = Assert.IsType<ArcEntity>(CadEntityTransform.Scale(arc!, new CadPoint(0, 0), 4));
        Assert.Equal(arc!.Id, scaledArc.Id);
        Assert.Equal(4, scaledArc.Radius, 8);
        Assert.Equal(arc.SweepAngleRadians, scaledArc.SweepAngleRadians, 8);
    }

    [Fact]
    public void MirrorArcReversesSweepAndPreservesRadius()
    {
        Assert.True(ArcEntity.TryCreateFromThreePoints(
            new CadPoint(1, 0),
            new CadPoint(0, 1),
            new CadPoint(-1, 0),
            out var arc));
        Assert.NotNull(arc);

        var mirrored = Assert.IsType<ArcEntity>(CadEntityTransform.Mirror(
            arc!,
            new CadPoint(0, -10),
            new CadPoint(0, 10)));

        Assert.Equal(arc!.Id, mirrored.Id);
        Assert.Equal(arc.Radius, mirrored.Radius, 8);
        Assert.Equal(-Math.Sign(arc.SweepAngleRadians), Math.Sign(mirrored.SweepAngleRadians));
        AssertClose(new CadPoint(-1, 0), mirrored.StartPoint);
    }

    [Fact]
    public void OffsetLineUsesPickedSide()
    {
        var line = new LineEntity(new CadPoint(0, 0), new CadPoint(10, 0));

        Assert.True(CadOffset.TryCreate(line, 2, new CadPoint(5, 10), out var result));
        var offset = Assert.IsType<LineEntity>(result);

        AssertClose(new CadPoint(0, 2), offset.Start);
        AssertClose(new CadPoint(10, 2), offset.End);
        Assert.NotEqual(line.Id, offset.Id);
    }

    [Fact]
    public void OffsetCircleSupportsInsideAndOutside()
    {
        var circle = new CircleEntity(new CadPoint(0, 0), 10);

        Assert.True(CadOffset.TryCreate(circle, 2, new CadPoint(20, 0), out var outside));
        Assert.Equal(12, Assert.IsType<CircleEntity>(outside).Radius, 8);

        Assert.True(CadOffset.TryCreate(circle, 2, new CadPoint(1, 0), out var inside));
        Assert.Equal(8, Assert.IsType<CircleEntity>(inside).Radius, 8);
        Assert.False(CadOffset.TryCreate(circle, 10, new CadPoint(1, 0), out _));
    }

    [Fact]
    public void OffsetOpenPolylineUsesMiterJoin()
    {
        var polyline = new PolylineEntity([
            new CadPoint(0, 0),
            new CadPoint(10, 0),
            new CadPoint(10, 10)
        ]);

        Assert.True(CadOffset.TryCreate(polyline, 2, new CadPoint(8, 2), out var result));
        var offset = Assert.IsType<PolylineEntity>(result);
        Assert.False(offset.Closed);
        Assert.Equal(3, offset.Points.Count);
        AssertClose(new CadPoint(0, 2), offset.Points[0]);
        AssertClose(new CadPoint(8, 2), offset.Points[1]);
        AssertClose(new CadPoint(8, 10), offset.Points[2]);
    }

    [Fact]
    public void QuickTrimLineRemovesPickedIntervalBetweenCuttingEdges()
    {
        var target = new LineEntity(new CadPoint(0, 0), new CadPoint(10, 0));
        var leftBoundary = new LineEntity(new CadPoint(3, -5), new CadPoint(3, 5));
        var rightBoundary = new LineEntity(new CadPoint(7, -5), new CadPoint(7, 5));

        Assert.True(CadTrimExtend.TryTrim(
            target,
            [leftBoundary, rightBoundary],
            new CadPoint(5, 0),
            out var replacements));

        Assert.Equal(2, replacements.Count);
        var left = Assert.IsType<LineEntity>(replacements[0]);
        var right = Assert.IsType<LineEntity>(replacements[1]);
        Assert.Equal(target.Id, left.Id);
        AssertClose(new CadPoint(0, 0), left.Start);
        AssertClose(new CadPoint(3, 0), left.End);
        AssertClose(new CadPoint(7, 0), right.Start);
        AssertClose(new CadPoint(10, 0), right.End);
    }

    [Fact]
    public void QuickTrimCircleCreatesArcPiecesAndPreservesOneIdentity()
    {
        var circle = new CircleEntity(new CadPoint(0, 0), 10);
        var left = new LineEntity(new CadPoint(-5, -20), new CadPoint(-5, 20));
        var right = new LineEntity(new CadPoint(5, -20), new CadPoint(5, 20));

        Assert.True(CadTrimExtend.TryTrim(circle, [left, right], new CadPoint(0, 10), out var replacements));
        Assert.NotEmpty(replacements);
        Assert.All(replacements, replacement => Assert.IsType<ArcEntity>(replacement));
        Assert.Contains(replacements, replacement => replacement.Id == circle.Id);
        Assert.DoesNotContain(replacements.OfType<ArcEntity>(), arc => CadEntityGeometry.IsPointOnArc(arc, new CadPoint(0, 10)));
    }

    [Fact]
    public void QuickExtendLineUsesNearestForwardBoundary()
    {
        var target = new LineEntity(new CadPoint(0, 0), new CadPoint(5, 0));
        var nearBoundary = new LineEntity(new CadPoint(10, -5), new CadPoint(10, 5));
        var farBoundary = new LineEntity(new CadPoint(20, -5), new CadPoint(20, 5));

        Assert.True(CadTrimExtend.TryExtend(
            target,
            [farBoundary, nearBoundary],
            new CadPoint(5, 0),
            out var replacement));

        var extended = Assert.IsType<LineEntity>(replacement);
        Assert.Equal(target.Id, extended.Id);
        AssertClose(new CadPoint(0, 0), extended.Start);
        AssertClose(new CadPoint(10, 0), extended.End);
    }

    [Fact]
    public void QuickExtendOpenPolylineExtendsPickedEndOnly()
    {
        var target = new PolylineEntity([
            new CadPoint(0, 0), new CadPoint(5, 0), new CadPoint(5, 5)
        ]);
        var boundary = new LineEntity(new CadPoint(0, 10), new CadPoint(10, 10));

        Assert.True(CadTrimExtend.TryExtend(target, [boundary], new CadPoint(5, 5), out var replacement));
        var extended = Assert.IsType<PolylineEntity>(replacement);
        Assert.Equal(target.Id, extended.Id);
        AssertClose(new CadPoint(5, 10), extended.Points[^1]);
        AssertClose(new CadPoint(0, 0), extended.Points[0]);
    }

    [Fact]
    public void TrimReplaceCanSplitOneEntityInSingleUndoStep()
    {
        var document = new CadDocument();
        var target = new LineEntity(new CadPoint(0, 0), new CadPoint(10, 0));
        var leftBoundary = new LineEntity(new CadPoint(3, -5), new CadPoint(3, 5));
        var rightBoundary = new LineEntity(new CadPoint(7, -5), new CadPoint(7, 5));
        document.Add(target);
        document.Add(leftBoundary);
        document.Add(rightBoundary);

        Assert.True(CadTrimExtend.TryTrim(target, [leftBoundary, rightBoundary], new CadPoint(5, 0), out var replacements));
        Assert.True(document.Replace(target.Id, replacements));
        Assert.Equal(4, document.Entities.Count);

        Assert.True(document.Undo());
        Assert.Equal(3, document.Entities.Count);
        Assert.Contains(document.Entities, entity => entity.Id == target.Id);
    }

    [Fact]
    public void ReplaceRangeRejectsIdentityCollisionsWithoutMutatingDocument()
    {
        var document = new CadDocument();
        var first = new LineEntity(new CadPoint(0, 0), new CadPoint(1, 0));
        var second = new LineEntity(new CadPoint(0, 1), new CadPoint(1, 1));
        document.Add(first);
        document.Add(second);
        var revision = document.Revision;

        Assert.Throws<ArgumentException>(() => document.ReplaceRange([
            CadEntityTransform.Translate(first, new CadVector(1, 0)),
            CadEntityTransform.Translate(first, new CadVector(2, 0))
        ]));
        Assert.Equal(revision, document.Revision);
        Assert.Equal(2, document.Entities.Count);
    }

    private static void AssertClose(CadPoint expected, CadPoint actual, double tolerance = 1e-7)
    {
        Assert.InRange(Math.Abs(expected.X - actual.X), 0, tolerance);
        Assert.InRange(Math.Abs(expected.Y - actual.Y), 0, tolerance);
    }
}

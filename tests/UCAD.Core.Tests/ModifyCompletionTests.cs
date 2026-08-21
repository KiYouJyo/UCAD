using UCAD.Core.Entities;
using UCAD.Core.Geometry;
using UCAD.Core.Modify;
using Xunit;

namespace UCAD.Core.Tests;

public sealed class ModifyCompletionTests
{
    [Fact]
    public void FilletTrimsTwoLinesAndCreatesTangentArc()
    {
        var horizontal = new LineEntity(new CadPoint(-10, 0), new CadPoint(10, 0));
        var vertical = new LineEntity(new CadPoint(0, -10), new CadPoint(0, 10));

        Assert.True(CadFilletChamfer.TryFillet(
            horizontal,
            new CadPoint(8, 0),
            vertical,
            new CadPoint(0, 8),
            2,
            out var first,
            out var second,
            out var arc));

        Assert.NotNull(first);
        Assert.NotNull(second);
        Assert.NotNull(arc);
        Assert.Equal(horizontal.Id, first!.Id);
        Assert.Equal(vertical.Id, second!.Id);
        Assert.Contains(new[] { first.Start, first.End }, point => Math.Abs(point.X - 2) < 1e-8 && Math.Abs(point.Y) < 1e-8);
        Assert.Contains(new[] { second.Start, second.End }, point => Math.Abs(point.X) < 1e-8 && Math.Abs(point.Y - 2) < 1e-8);
        Assert.Equal(2, arc!.Center.X, 8);
        Assert.Equal(2, arc.Center.Y, 8);
        Assert.Equal(2, arc.Radius, 8);
    }

    [Fact]
    public void ChamferUsesIndependentDistances()
    {
        var horizontal = new LineEntity(new CadPoint(-10, 0), new CadPoint(10, 0));
        var vertical = new LineEntity(new CadPoint(0, -10), new CadPoint(0, 10));

        Assert.True(CadFilletChamfer.TryChamfer(
            horizontal,
            new CadPoint(8, 0),
            vertical,
            new CadPoint(0, 8),
            3,
            4,
            out _,
            out _,
            out var chamfer));

        Assert.NotNull(chamfer);
        Assert.Equal(new CadPoint(3, 0), chamfer!.Start);
        Assert.Equal(new CadPoint(0, 4), chamfer.End);
        Assert.Equal(5, chamfer.Length, 8);
    }

    [Fact]
    public void JoinConnectsLinesWithinToleranceAndPreservesSeedIdentity()
    {
        var first = new LineEntity(new CadPoint(0, 0), new CadPoint(10, 0));
        var second = new LineEntity(new CadPoint(10.001, 0), new CadPoint(20, 0));

        Assert.True(CadJoinBreak.TryJoin(first, second, 0.01, out var joined));
        Assert.NotNull(joined);
        Assert.Equal(first.Id, joined!.Id);
        Assert.Equal(3, joined.Points.Count);
        Assert.Equal(20, joined.Points[^1].X, 8);
    }

    [Fact]
    public void BreakLineRemovesSpanBetweenBreakPoints()
    {
        var line = new LineEntity(new CadPoint(0, 0), new CadPoint(10, 0));

        Assert.True(CadJoinBreak.TryBreak(
            line,
            new CadPoint(3, 0),
            new CadPoint(7, 0),
            out var replacements));

        Assert.Equal(2, replacements.Count);
        var first = Assert.IsType<LineEntity>(replacements[0]);
        var second = Assert.IsType<LineEntity>(replacements[1]);
        Assert.Equal(new CadPoint(0, 0), first.Start);
        Assert.Equal(new CadPoint(3, 0), first.End);
        Assert.Equal(new CadPoint(7, 0), second.Start);
        Assert.Equal(new CadPoint(10, 0), second.End);
    }

    [Fact]
    public void RectangularArrayCreatesExpectedCopies()
    {
        var source = new LineEntity(new CadPoint(0, 0), new CadPoint(1, 0));
        var copies = CadArray.CreateRectangular([source], rows: 2, columns: 3, rowSpacing: 10, columnSpacing: 20);

        Assert.Equal(5, copies.Count);
        Assert.All(copies, copy => Assert.NotEqual(source.Id, copy.Id));
        var last = Assert.IsType<LineEntity>(copies[^1]);
        Assert.Equal(new CadPoint(40, 10), last.Start);
    }

    [Fact]
    public void PolarArrayDistributesCopiesAroundCenter()
    {
        var source = new PointEntity(new CadPoint(10, 0));
        var copies = CadArray.CreatePolar([source], new CadPoint(0, 0), itemCount: 4);

        Assert.Equal(3, copies.Count);
        var firstCopy = Assert.IsType<PointEntity>(copies[0]);
        Assert.Equal(0, firstCopy.Position.X, 8);
        Assert.Equal(10, firstCopy.Position.Y, 8);
    }

    [Fact]
    public void StretchMovesOnlyGripsInsideCrossingWindow()
    {
        var line = new LineEntity(new CadPoint(0, 0), new CadPoint(10, 0));
        var window = CadRect.FromPoints(new CadPoint(8, -2), new CadPoint(12, 2));

        Assert.True(CadStretch.TryStretch(line, window, new CadVector(0, 5), out var stretched));
        var result = Assert.IsType<LineEntity>(stretched);
        Assert.Equal(line.Id, result.Id);
        Assert.Equal(new CadPoint(0, 0), result.Start);
        Assert.Equal(new CadPoint(10, 5), result.End);
    }

    [Fact]
    public void PeditCanCloseReverseAndJoinPolyline()
    {
        var seed = new PolylineEntity([new CadPoint(0, 0), new CadPoint(5, 0)]);
        var continuation = new LineEntity(new CadPoint(5, 0), new CadPoint(5, 5));

        Assert.True(CadPolylineEdit.TryJoinMany(seed, [continuation], 0.001, out var joined, out var consumed));
        Assert.Single(consumed);
        Assert.Equal(3, joined.Points.Count);

        var reversed = CadPolylineEdit.Reverse(joined);
        Assert.Equal(joined.Points[^1], reversed.Points[0]);
        var closed = CadPolylineEdit.SetClosed(reversed, true);
        Assert.True(closed.Closed);
        Assert.Equal(seed.Id, closed.Id);
    }
}

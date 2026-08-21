using UCAD.Core.Entities;

namespace UCAD.Core.Modify;

public static class CadPolylineEdit
{
    public static PolylineEntity SetClosed(PolylineEntity polyline, bool closed)
    {
        ArgumentNullException.ThrowIfNull(polyline);
        if (closed && polyline.Points.Count < 3)
            throw new InvalidOperationException("Closing a polyline requires at least three vertices.");
        return polyline.Closed == closed
            ? polyline
            : new PolylineEntity(polyline.Points, closed, polyline.Id);
    }

    public static PolylineEntity Reverse(PolylineEntity polyline)
    {
        ArgumentNullException.ThrowIfNull(polyline);
        return new PolylineEntity(polyline.Points.Reverse(), polyline.Closed, polyline.Id);
    }

    public static bool TryJoinMany(
        PolylineEntity seed,
        IEnumerable<ICadEntity> candidates,
        double tolerance,
        out PolylineEntity result,
        out IReadOnlyList<Guid> consumedIds)
    {
        ArgumentNullException.ThrowIfNull(seed);
        ArgumentNullException.ThrowIfNull(candidates);
        if (!double.IsFinite(tolerance) || tolerance < 0) throw new ArgumentOutOfRangeException(nameof(tolerance));

        result = seed;
        var consumed = new List<Guid>();
        var remaining = candidates
            .Where(candidate => candidate.Id != seed.Id)
            .ToList();

        var changed = true;
        while (changed && remaining.Count > 0 && !result.Closed)
        {
            changed = false;
            for (var i = 0; i < remaining.Count; i++)
            {
                if (!CadJoinBreak.TryJoin(result, remaining[i], tolerance, out var joined) || joined is null) continue;
                result = joined;
                consumed.Add(remaining[i].Id);
                remaining.RemoveAt(i);
                changed = true;
                break;
            }
        }

        consumedIds = consumed;
        return consumed.Count > 0;
    }
}
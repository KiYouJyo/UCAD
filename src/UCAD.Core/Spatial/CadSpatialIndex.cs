using UCAD.Core.Geometry;

namespace UCAD.Core.Spatial;

public readonly record struct CadSpatialIndexEntry<T>(T Item, CadRect Bounds);

/// <summary>
/// Read-only bulk-built bounding-volume index for large CAD/GIS datasets. The tree uses
/// median splits along the widest center axis, keeps leaves small, and supports rectangle
/// intersection plus nearest-candidate queries without scanning every entity.
/// </summary>
public sealed class CadSpatialIndex<T>
{
    private const int DefaultLeafCapacity = 16;
    private readonly Node? _root;

    private CadSpatialIndex(Node? root, int count)
    {
        _root = root;
        Count = count;
    }

    public int Count { get; }

    public static CadSpatialIndex<T> Build(
        IEnumerable<CadSpatialIndexEntry<T>> entries,
        int leafCapacity = DefaultLeafCapacity)
    {
        ArgumentNullException.ThrowIfNull(entries);
        if (leafCapacity is < 2 or > 256) throw new ArgumentOutOfRangeException(nameof(leafCapacity));
        var snapshot = entries.ToArray();
        foreach (var entry in snapshot)
        {
            if (!IsFinite(entry.Bounds)) throw new ArgumentException("Spatial-index bounds must be finite.", nameof(entries));
        }
        return new CadSpatialIndex<T>(BuildNode(snapshot, leafCapacity), snapshot.Length);
    }

    public IReadOnlyList<T> Query(CadRect rectangle)
    {
        if (!IsFinite(rectangle)) throw new ArgumentException("Query rectangle must be finite.", nameof(rectangle));
        if (_root is null) return [];
        var result = new List<T>();
        QueryNode(_root, rectangle, result);
        return result;
    }

    public T? FindNearest(CadPoint point, double maximumDistance, Func<T, CadPoint, double> exactDistance)
    {
        if (!double.IsFinite(point.X) || !double.IsFinite(point.Y)) throw new ArgumentException("Query point must be finite.", nameof(point));
        if (!double.IsFinite(maximumDistance) || maximumDistance < 0) throw new ArgumentOutOfRangeException(nameof(maximumDistance));
        ArgumentNullException.ThrowIfNull(exactDistance);
        if (_root is null) return default;

        var queue = new PriorityQueue<Node, double>();
        queue.Enqueue(_root, DistanceToBounds(point, _root.Bounds));
        var bestDistance = maximumDistance;
        var hasBest = false;
        T? best = default;
        while (queue.TryDequeue(out var node, out var lowerBound))
        {
            if (lowerBound > bestDistance) break;
            if (node.Entries is not null)
            {
                foreach (var entry in node.Entries)
                {
                    if (DistanceToBounds(point, entry.Bounds) > bestDistance) continue;
                    var distance = exactDistance(entry.Item, point);
                    if (!double.IsFinite(distance) || distance < 0 || distance > bestDistance) continue;
                    bestDistance = distance;
                    best = entry.Item;
                    hasBest = true;
                }
                continue;
            }
            if (node.First is not null)
            {
                var distance = DistanceToBounds(point, node.First.Bounds);
                if (distance <= bestDistance) queue.Enqueue(node.First, distance);
            }
            if (node.Second is not null)
            {
                var distance = DistanceToBounds(point, node.Second.Bounds);
                if (distance <= bestDistance) queue.Enqueue(node.Second, distance);
            }
        }
        return hasBest ? best : default;
    }

    private static Node? BuildNode(CadSpatialIndexEntry<T>[] entries, int leafCapacity)
    {
        if (entries.Length == 0) return null;
        var bounds = Union(entries.Select(entry => entry.Bounds));
        if (entries.Length <= leafCapacity) return new Node(bounds, Array.AsReadOnly(entries), null, null);

        var minCenterX = entries.Min(entry => CenterX(entry.Bounds));
        var maxCenterX = entries.Max(entry => CenterX(entry.Bounds));
        var minCenterY = entries.Min(entry => CenterY(entry.Bounds));
        var maxCenterY = entries.Max(entry => CenterY(entry.Bounds));
        var sortByX = maxCenterX - minCenterX >= maxCenterY - minCenterY;
        var ordered = sortByX
            ? entries.OrderBy(entry => CenterX(entry.Bounds)).ToArray()
            : entries.OrderBy(entry => CenterY(entry.Bounds)).ToArray();
        var middle = ordered.Length / 2;
        var first = BuildNode(ordered[..middle], leafCapacity)!;
        var second = BuildNode(ordered[middle..], leafCapacity)!;
        return new Node(bounds, null, first, second);
    }

    private static void QueryNode(Node node, CadRect rectangle, List<T> result)
    {
        if (!Intersects(node.Bounds, rectangle)) return;
        if (node.Entries is not null)
        {
            foreach (var entry in node.Entries)
                if (Intersects(entry.Bounds, rectangle)) result.Add(entry.Item);
            return;
        }
        if (node.First is not null) QueryNode(node.First, rectangle, result);
        if (node.Second is not null) QueryNode(node.Second, rectangle, result);
    }

    private static CadRect Union(IEnumerable<CadRect> rectangles)
    {
        using var iterator = rectangles.GetEnumerator();
        if (!iterator.MoveNext()) return default;
        var first = iterator.Current;
        var left = first.Left;
        var bottom = first.Bottom;
        var right = first.Right;
        var top = first.Top;
        while (iterator.MoveNext())
        {
            var current = iterator.Current;
            left = Math.Min(left, current.Left);
            bottom = Math.Min(bottom, current.Bottom);
            right = Math.Max(right, current.Right);
            top = Math.Max(top, current.Top);
        }
        return new CadRect(left, bottom, right, top);
    }

    private static bool Intersects(CadRect first, CadRect second) =>
        first.Right >= second.Left && first.Left <= second.Right &&
        first.Top >= second.Bottom && first.Bottom <= second.Top;

    private static double DistanceToBounds(CadPoint point, CadRect bounds)
    {
        var dx = point.X < bounds.Left ? bounds.Left - point.X : point.X > bounds.Right ? point.X - bounds.Right : 0;
        var dy = point.Y < bounds.Bottom ? bounds.Bottom - point.Y : point.Y > bounds.Top ? point.Y - bounds.Top : 0;
        return Math.Sqrt((dx * dx) + (dy * dy));
    }

    private static double CenterX(CadRect bounds) => (bounds.Left + bounds.Right) / 2.0;
    private static double CenterY(CadRect bounds) => (bounds.Bottom + bounds.Top) / 2.0;

    private static bool IsFinite(CadRect bounds) =>
        double.IsFinite(bounds.Left) && double.IsFinite(bounds.Bottom) &&
        double.IsFinite(bounds.Right) && double.IsFinite(bounds.Top);

    private sealed record Node(
        CadRect Bounds,
        IReadOnlyList<CadSpatialIndexEntry<T>>? Entries,
        Node? First,
        Node? Second);
}

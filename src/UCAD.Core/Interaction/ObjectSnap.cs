using UCAD.Core.Entities;
using UCAD.Core.Geometry;

namespace UCAD.Core.Interaction;

[Flags]
public enum ObjectSnapMode
{
    None = 0,
    Endpoint = 1 << 0,
    Midpoint = 1 << 1,
    Intersection = 1 << 2,
    Center = 1 << 3
}

public enum ObjectSnapKind
{
    Endpoint,
    Midpoint,
    Intersection,
    Center
}

public sealed record ObjectSnapResult(
    CadPoint Point,
    ObjectSnapKind Kind,
    Guid PrimaryEntityId,
    Guid? SecondaryEntityId,
    double Distance);

public static class ObjectSnapResolver
{
    public static ObjectSnapResult? Resolve(
        IEnumerable<ICadEntity> entities,
        CadPoint cursor,
        double aperture,
        ObjectSnapMode modes)
    {
        ArgumentNullException.ThrowIfNull(entities);
        if (!double.IsFinite(aperture) || aperture < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(aperture));
        }
        if (modes == ObjectSnapMode.None)
        {
            return null;
        }

        var snapshot = entities.ToArray();
        ObjectSnapResult? best = null;

        foreach (var entity in snapshot)
        {
            if (modes.HasFlag(ObjectSnapMode.Endpoint))
            {
                foreach (var point in CadEntityGeometry.GetEndpoints(entity))
                {
                    best = Choose(best, Candidate(point, ObjectSnapKind.Endpoint, entity.Id, null, cursor), aperture);
                }
            }

            if (modes.HasFlag(ObjectSnapMode.Midpoint))
            {
                foreach (var point in CadEntityGeometry.GetMidpoints(entity))
                {
                    best = Choose(best, Candidate(point, ObjectSnapKind.Midpoint, entity.Id, null, cursor), aperture);
                }
            }

            if (modes.HasFlag(ObjectSnapMode.Center))
            {
                var center = entity switch
                {
                    CircleEntity circle => circle.Center,
                    ArcEntity arc => arc.Center,
                    _ => (CadPoint?)null
                };
                if (center is CadPoint point)
                {
                    best = Choose(best, Candidate(point, ObjectSnapKind.Center, entity.Id, null, cursor), aperture);
                }
            }
        }

        if (modes.HasFlag(ObjectSnapMode.Intersection) && snapshot.Length > 1)
        {
            var apertureRect = new CadRect(
                cursor.X - aperture,
                cursor.Y - aperture,
                cursor.X + aperture,
                cursor.Y + aperture);
            var nearby = snapshot
                .Where(entity => CadEntityGeometry.GetBounds(entity).Intersects(apertureRect))
                .ToArray();

            for (var firstIndex = 0; firstIndex < nearby.Length - 1; firstIndex++)
            for (var secondIndex = firstIndex + 1; secondIndex < nearby.Length; secondIndex++)
            {
                var first = nearby[firstIndex];
                var second = nearby[secondIndex];
                foreach (var point in CadEntityGeometry.Intersections(first, second))
                {
                    best = Choose(
                        best,
                        Candidate(point, ObjectSnapKind.Intersection, first.Id, second.Id, cursor),
                        aperture);
                }
            }
        }

        return best;
    }

    private static ObjectSnapResult Candidate(
        CadPoint point,
        ObjectSnapKind kind,
        Guid primary,
        Guid? secondary,
        CadPoint cursor) =>
        new(point, kind, primary, secondary, (point - cursor).Length);

    private static ObjectSnapResult? Choose(
        ObjectSnapResult? current,
        ObjectSnapResult candidate,
        double aperture)
    {
        if (candidate.Distance > aperture)
        {
            return current;
        }
        if (current is null || candidate.Distance < current.Distance - 1e-9)
        {
            return candidate;
        }
        if (Math.Abs(candidate.Distance - current.Distance) <= 1e-9 && Priority(candidate.Kind) < Priority(current.Kind))
        {
            return candidate;
        }
        return current;
    }

    private static int Priority(ObjectSnapKind kind) => kind switch
    {
        ObjectSnapKind.Intersection => 0,
        ObjectSnapKind.Endpoint => 1,
        ObjectSnapKind.Midpoint => 2,
        ObjectSnapKind.Center => 3,
        _ => 10
    };
}

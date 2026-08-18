using UCAD.Core.Entities;
using UCAD.Core.Geometry;

namespace UCAD.Core.Modify;

public static class CadArray
{
    public static IReadOnlyList<ICadEntity> CreateRectangular(
        IEnumerable<ICadEntity> sourceEntities,
        int rows,
        int columns,
        double rowSpacing,
        double columnSpacing,
        bool includeSource = false)
    {
        ArgumentNullException.ThrowIfNull(sourceEntities);
        if (rows < 1) throw new ArgumentOutOfRangeException(nameof(rows));
        if (columns < 1) throw new ArgumentOutOfRangeException(nameof(columns));
        if (!double.IsFinite(rowSpacing)) throw new ArgumentOutOfRangeException(nameof(rowSpacing));
        if (!double.IsFinite(columnSpacing)) throw new ArgumentOutOfRangeException(nameof(columnSpacing));

        var sources = sourceEntities.ToArray();
        var result = new List<ICadEntity>(sources.Length * rows * columns);
        for (var row = 0; row < rows; row++)
        for (var column = 0; column < columns; column++)
        {
            if (!includeSource && row == 0 && column == 0) continue;
            var displacement = new CadVector(column * columnSpacing, row * rowSpacing);
            foreach (var source in sources)
                result.Add(CadEntityTransform.Translate(source, displacement, preserveIdentity: false));
        }
        return result;
    }

    public static IReadOnlyList<ICadEntity> CreatePolar(
        IEnumerable<ICadEntity> sourceEntities,
        CadPoint center,
        int itemCount,
        double fillAngleRadians = Math.Tau,
        bool rotateItems = true,
        bool includeSource = false)
    {
        ArgumentNullException.ThrowIfNull(sourceEntities);
        if (itemCount < 2) throw new ArgumentOutOfRangeException(nameof(itemCount));
        if (!double.IsFinite(fillAngleRadians) || Math.Abs(fillAngleRadians) <= 1e-9)
            throw new ArgumentOutOfRangeException(nameof(fillAngleRadians));

        var sources = sourceEntities.ToArray();
        var result = new List<ICadEntity>(sources.Length * itemCount);
        var fullCircle = Math.Abs(Math.Abs(fillAngleRadians) - Math.Tau) <= 1e-9;
        var denominator = fullCircle ? itemCount : itemCount - 1;

        for (var index = 0; index < itemCount; index++)
        {
            if (!includeSource && index == 0) continue;
            var angle = fillAngleRadians * index / denominator;
            foreach (var source in sources)
            {
                if (rotateItems)
                {
                    result.Add(CadEntityTransform.Rotate(source, center, angle, preserveIdentity: false));
                    continue;
                }

                var anchor = EntityAnchor(source);
                var rotatedAnchor = CadEntityTransform.RotatePoint(anchor, center, angle);
                var displacement = rotatedAnchor - anchor;
                result.Add(CadEntityTransform.Translate(source, displacement, preserveIdentity: false));
            }
        }
        return result;
    }

    private static CadPoint EntityAnchor(ICadEntity entity) => entity switch
    {
        LineEntity line => line.Start,
        PolylineEntity polyline => polyline.Points[0],
        CircleEntity circle => circle.Center,
        ArcEntity arc => arc.Center,
        PointEntity point => point.Position,
        EllipseEntity ellipse => ellipse.Center,
        SplineEntity spline => spline.FitPoints[0],
        RayEntity ray => ray.Origin,
        XLineEntity xline => xline.Point,
        TextEntity text => text.Position,
        LinearDimensionEntity dimension => dimension.FirstExtensionPoint,
        HatchEntity hatch => hatch.Boundary[0],
        BlockReferenceEntity block => block.InsertionPoint,
        _ => new CadPoint(0, 0)
    };
}
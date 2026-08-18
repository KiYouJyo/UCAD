using UCAD.Core.Geometry;

namespace UCAD.Core.Interaction;

/// <summary>
/// Pure drafting-aid geometry used by both pointer preview and committed points.
/// Screen-space aperture decisions stay in the viewport; these helpers only calculate
/// deterministic world-space constrained points.
/// </summary>
public static class DraftingConstraints
{
    private const double Epsilon = 1e-9;

    public static CadPoint SnapToGrid(CadPoint point, double spacing)
    {
        if (!double.IsFinite(spacing) || spacing <= 0) throw new ArgumentOutOfRangeException(nameof(spacing));
        return new CadPoint(
            Math.Round(point.X / spacing, MidpointRounding.AwayFromZero) * spacing,
            Math.Round(point.Y / spacing, MidpointRounding.AwayFromZero) * spacing);
    }

    public static CadPoint ApplyPolar(CadPoint basePoint, CadPoint rawPoint, double incrementDegrees)
    {
        if (!double.IsFinite(incrementDegrees) || incrementDegrees <= 0 || incrementDegrees > 180)
            throw new ArgumentOutOfRangeException(nameof(incrementDegrees));

        var vector = rawPoint - basePoint;
        var radius = vector.Length;
        if (radius <= Epsilon) return basePoint;

        var incrementRadians = incrementDegrees * Math.PI / 180.0;
        var rawAngle = Math.Atan2(vector.Y, vector.X);
        var snappedAngle = Math.Round(rawAngle / incrementRadians, MidpointRounding.AwayFromZero) * incrementRadians;
        return new CadPoint(
            basePoint.X + (Math.Cos(snappedAngle) * radius),
            basePoint.Y + (Math.Sin(snappedAngle) * radius));
    }

    public static bool TryApplyObjectTracking(
        CadPoint trackingAnchor,
        CadPoint rawPoint,
        double tolerance,
        out CadPoint constrained,
        out ObjectTrackingAxis axis)
    {
        if (!double.IsFinite(tolerance) || tolerance < 0) throw new ArgumentOutOfRangeException(nameof(tolerance));

        var dx = Math.Abs(rawPoint.X - trackingAnchor.X);
        var dy = Math.Abs(rawPoint.Y - trackingAnchor.Y);
        var vertical = dx <= tolerance;
        var horizontal = dy <= tolerance;

        if (!vertical && !horizontal)
        {
            constrained = rawPoint;
            axis = ObjectTrackingAxis.None;
            return false;
        }

        if (vertical && horizontal)
        {
            if (dx <= dy)
            {
                constrained = new CadPoint(trackingAnchor.X, rawPoint.Y);
                axis = ObjectTrackingAxis.Vertical;
            }
            else
            {
                constrained = new CadPoint(rawPoint.X, trackingAnchor.Y);
                axis = ObjectTrackingAxis.Horizontal;
            }
            return true;
        }

        if (vertical)
        {
            constrained = new CadPoint(trackingAnchor.X, rawPoint.Y);
            axis = ObjectTrackingAxis.Vertical;
            return true;
        }

        constrained = new CadPoint(rawPoint.X, trackingAnchor.Y);
        axis = ObjectTrackingAxis.Horizontal;
        return true;
    }
}

public enum ObjectTrackingAxis
{
    None,
    Horizontal,
    Vertical
}
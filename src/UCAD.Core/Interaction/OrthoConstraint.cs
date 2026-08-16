using UCAD.Core.Geometry;

namespace UCAD.Core.Interaction;

public static class OrthoConstraint
{
    public static CadPoint Apply(CadPoint basePoint, CadPoint candidate)
    {
        var delta = candidate - basePoint;
        if (Math.Abs(delta.X) >= Math.Abs(delta.Y))
        {
            return new CadPoint(candidate.X, basePoint.Y);
        }
        return new CadPoint(basePoint.X, candidate.Y);
    }
}

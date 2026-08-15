namespace UCAD.Core.Geometry;

public readonly record struct CadPoint(double X, double Y)
{
    public static CadPoint operator +(CadPoint point, CadVector vector) =>
        new(point.X + vector.X, point.Y + vector.Y);

    public static CadVector operator -(CadPoint end, CadPoint start) =>
        new(end.X - start.X, end.Y - start.Y);
}

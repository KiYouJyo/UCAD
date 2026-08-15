namespace UCAD.Core.Geometry;

public readonly record struct CadVector(double X, double Y)
{
    public double Length => Math.Sqrt((X * X) + (Y * Y));
}

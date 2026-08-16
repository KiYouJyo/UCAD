namespace UCAD.Core.Geometry;

public readonly record struct CadRect(double Left, double Bottom, double Right, double Top)
{
    public double Width => Right - Left;
    public double Height => Top - Bottom;

    public static CadRect FromPoints(CadPoint first, CadPoint second) => new(
        Math.Min(first.X, second.X),
        Math.Min(first.Y, second.Y),
        Math.Max(first.X, second.X),
        Math.Max(first.Y, second.Y));

    public bool Contains(CadPoint point, double tolerance = 0) =>
        point.X >= Left - tolerance &&
        point.X <= Right + tolerance &&
        point.Y >= Bottom - tolerance &&
        point.Y <= Top + tolerance;

    public bool Contains(CadRect other, double tolerance = 0) =>
        other.Left >= Left - tolerance &&
        other.Right <= Right + tolerance &&
        other.Bottom >= Bottom - tolerance &&
        other.Top <= Top + tolerance;

    public bool Intersects(CadRect other, double tolerance = 0) =>
        other.Right >= Left - tolerance &&
        other.Left <= Right + tolerance &&
        other.Top >= Bottom - tolerance &&
        other.Bottom <= Top + tolerance;
}

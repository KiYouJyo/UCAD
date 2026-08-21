using System.Numerics;
using UCAD.Core.Geometry;

namespace UCAD.Views;

public sealed partial class CadViewport
{
    public bool TryGetVisibleModelRect(out CadRect rectangle)
    {
        var width = Canvas.ActualWidth;
        var height = Canvas.ActualHeight;
        if (!double.IsFinite(width) || !double.IsFinite(height) || width <= 1 || height <= 1 || !double.IsFinite(_zoom) || _zoom <= 0)
        {
            rectangle = default;
            return false;
        }

        var first = ScreenToWorld(Vector2.Zero);
        var second = ScreenToWorld(new Vector2((float)width, (float)height));
        rectangle = CadRect.FromPoints(first, second);
        return rectangle.Width > 1e-9 && rectangle.Height > 1e-9;
    }
}

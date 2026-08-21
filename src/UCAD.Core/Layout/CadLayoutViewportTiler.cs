using UCAD.Core.Geometry;

namespace UCAD.Core.Layout;

public static class CadLayoutViewportTiler
{
    public static CadLayoutDefinition Tile(CadLayoutDefinition layout, double gapMm = 5)
    {
        ArgumentNullException.ThrowIfNull(layout);
        if (!double.IsFinite(gapMm) || gapMm < 0) throw new ArgumentOutOfRangeException(nameof(gapMm));
        if (layout.Viewports.Count == 0) return layout;

        var printable = layout.PageSetup.PrintablePaperRectMm;
        var columns = (int)Math.Ceiling(Math.Sqrt(layout.Viewports.Count));
        var rows = (int)Math.Ceiling((double)layout.Viewports.Count / columns);
        var cellWidth = (printable.Width - (gapMm * (columns - 1))) / columns;
        var cellHeight = (printable.Height - (gapMm * (rows - 1))) / rows;
        if (cellWidth <= 0 || cellHeight <= 0)
            throw new InvalidOperationException("Viewport tiling gap leaves no usable paper-space area.");

        var tiled = new CadLayoutViewport[layout.Viewports.Count];
        for (var index = 0; index < layout.Viewports.Count; index++)
        {
            var source = layout.Viewports[index];
            var column = index % columns;
            var rowFromTop = index / columns;
            var left = printable.Left + (column * (cellWidth + gapMm));
            var bottom = printable.Bottom + ((rows - rowFromTop - 1) * (cellHeight + gapMm));
            var paperRect = new CadRect(left, bottom, left + cellWidth, bottom + cellHeight);
            tiled[index] = new CadLayoutViewport(
                source.Name,
                paperRect,
                source.ModelCenter,
                source.ScaleDenominator,
                source.TwistAngleRadians,
                source.Locked,
                source.Id);
        }

        return new CadLayoutDefinition(layout.Name, layout.PageSetup, tiled, layout.ModelLayout);
    }
}

using UCAD.Core.Geometry;

namespace UCAD.Core.Layout;

public enum CadPlotArea
{
    Extents,
    Display,
    Window,
    Layout
}

public enum CadPlotStyleMode
{
    Color,
    Monochrome,
    Grayscale
}

public sealed record CadPageSetup
{
    public CadPageSetup(
        CadPaperSize? paperSize = null,
        bool landscape = true,
        double marginLeftMm = 10,
        double marginTopMm = 10,
        double marginRightMm = 10,
        double marginBottomMm = 10,
        double plotScaleDenominator = 100,
        CadPlotArea plotArea = CadPlotArea.Layout,
        CadPlotStyleMode plotStyle = CadPlotStyleMode.Monochrome,
        CadRect? modelWindow = null)
    {
        PaperSize = paperSize ?? CadPaperSize.A3;
        if (!double.IsFinite(marginLeftMm) || marginLeftMm < 0) throw new ArgumentOutOfRangeException(nameof(marginLeftMm));
        if (!double.IsFinite(marginTopMm) || marginTopMm < 0) throw new ArgumentOutOfRangeException(nameof(marginTopMm));
        if (!double.IsFinite(marginRightMm) || marginRightMm < 0) throw new ArgumentOutOfRangeException(nameof(marginRightMm));
        if (!double.IsFinite(marginBottomMm) || marginBottomMm < 0) throw new ArgumentOutOfRangeException(nameof(marginBottomMm));
        if (!double.IsFinite(plotScaleDenominator) || plotScaleDenominator <= 0) throw new ArgumentOutOfRangeException(nameof(plotScaleDenominator));

        Landscape = landscape;
        MarginLeftMm = marginLeftMm;
        MarginTopMm = marginTopMm;
        MarginRightMm = marginRightMm;
        MarginBottomMm = marginBottomMm;
        PlotScaleDenominator = plotScaleDenominator;
        PlotArea = plotArea;
        PlotStyle = plotStyle;
        ModelWindow = modelWindow;

        var (width, height) = PaperSize.Oriented(Landscape);
        if (MarginLeftMm + MarginRightMm >= width || MarginTopMm + MarginBottomMm >= height)
            throw new ArgumentException("Page margins leave no printable area.");
        if (PlotArea == CadPlotArea.Window && ModelWindow is null)
            throw new ArgumentException("Window plot area requires a model window.", nameof(modelWindow));
    }

    public CadPaperSize PaperSize { get; }
    public bool Landscape { get; }
    public double MarginLeftMm { get; }
    public double MarginTopMm { get; }
    public double MarginRightMm { get; }
    public double MarginBottomMm { get; }
    public double PlotScaleDenominator { get; }
    public CadPlotArea PlotArea { get; }
    public CadPlotStyleMode PlotStyle { get; }
    public CadRect? ModelWindow { get; }

    public double PaperWidthMm => PaperSize.Oriented(Landscape).WidthMm;
    public double PaperHeightMm => PaperSize.Oriented(Landscape).HeightMm;

    public CadRect PrintablePaperRectMm => new(
        MarginLeftMm,
        MarginBottomMm,
        PaperWidthMm - MarginRightMm,
        PaperHeightMm - MarginTopMm);
}
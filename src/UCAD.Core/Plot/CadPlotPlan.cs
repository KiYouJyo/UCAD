using UCAD.Core.Geometry;
using UCAD.Core.Layout;

namespace UCAD.Core.Plot;

public sealed record CadPlotPlan
{
    public CadPlotPlan(
        CadPageSetup pageSetup,
        CadPoint modelCenter,
        double scaleDenominator,
        CadRect paperRectMm,
        CadRect? modelWindow = null,
        double twistAngleRadians = 0)
    {
        PageSetup = pageSetup ?? throw new ArgumentNullException(nameof(pageSetup));
        if (!double.IsFinite(scaleDenominator) || scaleDenominator <= 0) throw new ArgumentOutOfRangeException(nameof(scaleDenominator));
        if (paperRectMm.Width <= 0 || paperRectMm.Height <= 0) throw new ArgumentException("Plot paper rectangle must have positive area.", nameof(paperRectMm));
        if (!double.IsFinite(twistAngleRadians)) throw new ArgumentOutOfRangeException(nameof(twistAngleRadians));
        ModelCenter = modelCenter;
        ScaleDenominator = scaleDenominator;
        PaperRectMm = paperRectMm;
        ModelWindow = modelWindow;
        TwistAngleRadians = twistAngleRadians;
    }

    public CadPageSetup PageSetup { get; }
    public CadPoint ModelCenter { get; }
    public double ScaleDenominator { get; }
    public CadRect PaperRectMm { get; }
    public CadRect? ModelWindow { get; }
    public double TwistAngleRadians { get; }

    public CadPoint ModelToPaper(CadPoint modelPoint)
    {
        var dx = modelPoint.X - ModelCenter.X;
        var dy = modelPoint.Y - ModelCenter.Y;
        var cosine = Math.Cos(-TwistAngleRadians);
        var sine = Math.Sin(-TwistAngleRadians);
        var rotatedX = (dx * cosine) - (dy * sine);
        var rotatedY = (dx * sine) + (dy * cosine);
        return new CadPoint(
            PaperCenter.X + (rotatedX / ScaleDenominator),
            PaperCenter.Y + (rotatedY / ScaleDenominator));
    }

    public CadPoint PaperToModel(CadPoint paperPoint)
    {
        var dx = (paperPoint.X - PaperCenter.X) * ScaleDenominator;
        var dy = (paperPoint.Y - PaperCenter.Y) * ScaleDenominator;
        var cosine = Math.Cos(TwistAngleRadians);
        var sine = Math.Sin(TwistAngleRadians);
        return new CadPoint(
            ModelCenter.X + (dx * cosine) - (dy * sine),
            ModelCenter.Y + (dx * sine) + (dy * cosine));
    }

    public CadPoint PaperCenter => new(
        (PaperRectMm.Left + PaperRectMm.Right) / 2,
        (PaperRectMm.Bottom + PaperRectMm.Top) / 2);

    public static CadPlotPlan FromViewport(CadPageSetup pageSetup, CadLayoutViewport viewport) =>
        new(
            pageSetup,
            viewport.ModelCenter,
            viewport.ScaleDenominator,
            viewport.PaperRectMm,
            modelWindow: null,
            twistAngleRadians: viewport.TwistAngleRadians);

    public static CadPlotPlan FitExtents(CadPageSetup pageSetup, CadRect modelExtents, double paddingFactor = 0.95)
    {
        ArgumentNullException.ThrowIfNull(pageSetup);
        if (!double.IsFinite(paddingFactor) || paddingFactor <= 0 || paddingFactor > 1) throw new ArgumentOutOfRangeException(nameof(paddingFactor));
        var printable = pageSetup.PrintablePaperRectMm;
        if (modelExtents.Width <= 1e-9 || modelExtents.Height <= 1e-9)
            throw new ArgumentException("Model extents must have positive area.", nameof(modelExtents));
        var scaleX = modelExtents.Width / (printable.Width * paddingFactor);
        var scaleY = modelExtents.Height / (printable.Height * paddingFactor);
        var denominator = Math.Max(scaleX, scaleY);
        return new CadPlotPlan(
            pageSetup,
            new CadPoint((modelExtents.Left + modelExtents.Right) / 2, (modelExtents.Bottom + modelExtents.Top) / 2),
            denominator,
            printable,
            modelExtents);
    }
}

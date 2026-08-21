using UCAD.Core.Geometry;

namespace UCAD.Core.Layout;

/// <summary>
/// Paper-space viewport window. ScaleDenominator follows CAD notation: 100 means 1:100
/// when model units are millimetres. PaperRectMm is always expressed in physical page mm.
/// </summary>
public sealed record CadLayoutViewport
{
    public CadLayoutViewport(
        string name,
        CadRect paperRectMm,
        CadPoint modelCenter,
        double scaleDenominator = 100,
        double twistAngleRadians = 0,
        bool locked = true,
        Guid? id = null)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Viewport name cannot be empty.", nameof(name));
        if (paperRectMm.Width <= 0 || paperRectMm.Height <= 0) throw new ArgumentException("Viewport paper rectangle must have positive area.", nameof(paperRectMm));
        if (!double.IsFinite(scaleDenominator) || scaleDenominator <= 0) throw new ArgumentOutOfRangeException(nameof(scaleDenominator));
        if (!double.IsFinite(twistAngleRadians)) throw new ArgumentOutOfRangeException(nameof(twistAngleRadians));
        Name = name.Trim();
        PaperRectMm = paperRectMm;
        ModelCenter = modelCenter;
        ScaleDenominator = scaleDenominator;
        TwistAngleRadians = twistAngleRadians;
        Locked = locked;
        Id = id ?? Guid.NewGuid();
    }

    public Guid Id { get; }
    public string Name { get; }
    public CadRect PaperRectMm { get; }
    public CadPoint ModelCenter { get; }
    public double ScaleDenominator { get; }
    public double TwistAngleRadians { get; }
    public bool Locked { get; }

    public CadPoint PaperCenterMm => new(
        (PaperRectMm.Left + PaperRectMm.Right) / 2,
        (PaperRectMm.Bottom + PaperRectMm.Top) / 2);

    public CadPoint ModelToPaper(CadPoint modelPoint)
    {
        var offset = modelPoint - ModelCenter;
        var cosine = Math.Cos(-TwistAngleRadians);
        var sine = Math.Sin(-TwistAngleRadians);
        var rotatedX = (offset.X * cosine) - (offset.Y * sine);
        var rotatedY = (offset.X * sine) + (offset.Y * cosine);
        return new CadPoint(
            PaperCenterMm.X + (rotatedX / ScaleDenominator),
            PaperCenterMm.Y + (rotatedY / ScaleDenominator));
    }

    public CadPoint PaperToModel(CadPoint paperPoint)
    {
        var dx = (paperPoint.X - PaperCenterMm.X) * ScaleDenominator;
        var dy = (paperPoint.Y - PaperCenterMm.Y) * ScaleDenominator;
        var cosine = Math.Cos(TwistAngleRadians);
        var sine = Math.Sin(TwistAngleRadians);
        return new CadPoint(
            ModelCenter.X + (dx * cosine) - (dy * sine),
            ModelCenter.Y + (dx * sine) + (dy * cosine));
    }
}
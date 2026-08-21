using UCAD.Core.Geometry;
using UCAD.Core.Styles;

namespace UCAD.Core.Plot;

public sealed record CadPdfTextOutlineFigure(IReadOnlyList<CadPoint> Points, bool Closed)
{
    public CadPdfTextOutlineFigure(IEnumerable<CadPoint> points, bool closed)
        : this(Array.AsReadOnly((points ?? throw new ArgumentNullException(nameof(points))).ToArray()), closed)
    {
        if (Points.Count < 2) throw new ArgumentException("A text outline figure requires at least two points.", nameof(points));
    }
}

/// <summary>
/// Text geometry normalized so one font em equals one local outline unit. Positive Y is
/// downward to match DirectWrite/Win2D layout coordinates; the PDF writer flips Y while
/// placing the outline in paper space.
/// </summary>
public sealed record CadPdfTextOutline(IReadOnlyList<CadPdfTextOutlineFigure> Figures)
{
    public CadPdfTextOutline(IEnumerable<CadPdfTextOutlineFigure> figures)
        : this(Array.AsReadOnly((figures ?? throw new ArgumentNullException(nameof(figures))).ToArray()))
    {
        if (Figures.Count == 0) throw new ArgumentException("A text outline requires at least one figure.", nameof(figures));
    }
}

/// <summary>
/// Platform adapter used by the pure Core PDF writer. The WinUI application implements
/// this with the installed Windows font stack; Core tests can provide deterministic fake
/// outlines without depending on Win2D or font files.
/// </summary>
public interface ICadPdfTextOutlineProvider
{
    bool TryCreateOutline(
        string text,
        CadTextStyle style,
        out CadPdfTextOutline? outline,
        out string? warning);
}

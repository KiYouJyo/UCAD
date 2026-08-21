using UCAD.Core.Entities;
using UCAD.Core.Geometry;

namespace UCAD.Core.Hatching;

public readonly record struct CadHatchPatternSegment(CadPoint Start, CadPoint End);

public sealed record CadHatchPatternResult(
    IReadOnlyList<CadHatchPatternSegment> Segments,
    double RequestedSpacing,
    double EffectiveSpacing,
    bool DensityReduced);

/// <summary>
/// Generates model-space pattern strokes clipped by hatch loops. ANSI31 is the first
/// production pattern. It uses a 45-degree base line family plus the entity pattern
/// angle, with metric acadiso-style 3.175 drawing-unit base spacing.
/// </summary>
public static class CadHatchPatternGenerator
{
    public const double Ansi31BaseSpacing = 3.175;
    public const int DefaultMaxScanLines = 12_000;
    private const double Epsilon = 1e-9;

    public static CadHatchPatternResult Generate(HatchEntity hatch, int maxScanLines = DefaultMaxScanLines)
    {
        ArgumentNullException.ThrowIfNull(hatch);
        if (maxScanLines < 2) throw new ArgumentOutOfRangeException(nameof(maxScanLines));
        if (!string.Equals(hatch.Pattern, "ANSI31", StringComparison.OrdinalIgnoreCase))
            throw new NotSupportedException($"Hatch pattern '{hatch.Pattern}' is not supported by the line-pattern generator.");

        var angle = (Math.PI / 4.0) + hatch.PatternAngleRadians;
        var cosine = Math.Cos(angle);
        var sine = Math.Sin(angle);
        var loops = new List<IReadOnlyList<PatternPoint>>
        {
            hatch.Boundary.Select(point => ToPattern(point, cosine, sine)).ToArray()
        };
        loops.AddRange(hatch.EffectiveIslandLoops.Select(loop =>
            (IReadOnlyList<PatternPoint>)loop.Select(point => ToPattern(point, cosine, sine)).ToArray()));

        var minV = loops.SelectMany(loop => loop).Min(point => point.V);
        var maxV = loops.SelectMany(loop => loop).Max(point => point.V);
        var requestedSpacing = Ansi31BaseSpacing * hatch.PatternScale;
        var span = Math.Max(0, maxV - minV);
        var estimatedLines = span <= Epsilon ? 1 : (int)Math.Min(int.MaxValue, Math.Ceiling(span / requestedSpacing) + 1);
        var densityReduced = estimatedLines > maxScanLines;
        var effectiveSpacing = densityReduced && span > Epsilon
            ? span / (maxScanLines - 1)
            : requestedSpacing;

        var firstV = Math.Floor(minV / effectiveSpacing) * effectiveSpacing;
        while (firstV < minV - Epsilon) firstV += effectiveSpacing;

        var segments = new List<CadHatchPatternSegment>();
        var scanCount = 0;
        for (var v = firstV; v <= maxV + Epsilon && scanCount < maxScanLines; v += effectiveSpacing, scanCount++)
        {
            var intersections = new List<double>();
            foreach (var loop in loops) AddScanlineIntersections(loop, v, intersections);
            if (intersections.Count < 2) continue;
            intersections.Sort();
            for (var index = 0; index + 1 < intersections.Count; index += 2)
            {
                var firstU = intersections[index];
                var secondU = intersections[index + 1];
                if (secondU - firstU <= Epsilon) continue;
                segments.Add(new CadHatchPatternSegment(
                    FromPattern(firstU, v, cosine, sine),
                    FromPattern(secondU, v, cosine, sine)));
            }
        }

        return new CadHatchPatternResult(
            segments.AsReadOnly(),
            requestedSpacing,
            effectiveSpacing,
            densityReduced);
    }

    private static void AddScanlineIntersections(
        IReadOnlyList<PatternPoint> loop,
        double scanV,
        List<double> intersections)
    {
        for (var index = 0; index < loop.Count; index++)
        {
            var first = loop[index];
            var second = loop[(index + 1) % loop.Count];
            if ((first.V > scanV) == (second.V > scanV)) continue;
            var denominator = second.V - first.V;
            if (Math.Abs(denominator) <= Epsilon) continue;
            var fraction = (scanV - first.V) / denominator;
            intersections.Add(first.U + ((second.U - first.U) * fraction));
        }
    }

    private static PatternPoint ToPattern(CadPoint point, double cosine, double sine) => new(
        (point.X * cosine) + (point.Y * sine),
        (-point.X * sine) + (point.Y * cosine));

    private static CadPoint FromPattern(double u, double v, double cosine, double sine) => new(
        (u * cosine) - (v * sine),
        (u * sine) + (v * cosine));

    private readonly record struct PatternPoint(double U, double V);
}

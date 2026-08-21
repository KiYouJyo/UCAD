using ACadSharp.Entities;
using ACadSharp.Objects;
using UCAD.Core.Entities;
using UCAD.Core.Geometry;
using UCAD.Core.Layers;
using AcadMLine = ACadSharp.Entities.MLine;
using UcadDocument = UCAD.Core.CadDocument;

namespace UCAD.Core.IO;

/// <summary>
/// Display-first MLINE recovery. AutoCAD stores the rendered element-path geometry in
/// each vertex's group-41 parameter list. The first value is the miter intersection
/// offset, the second is the path-start offset, and later values alternate break stop /
/// break restart distances. Reconstructing those paths preserves ordinary multiline
/// drawings even though UCAD does not yet expose an editable MLINE entity.
/// </summary>
internal static class CadAcadMLineDisplayRepair
{
    private const double Tolerance = 1e-8;

    public static void Apply(ACadSharp.CadDocument source, UcadDocument target, List<string> warnings)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(warnings);

        foreach (var mline in source.Entities.OfType<AcadMLine>())
        {
            try
            {
                var additions = BuildGeometry(mline, target, out var lossyFillOrCaps);
                if (additions.Count == 0)
                {
                    warnings.Add("AutoCAD MLINE contained no recoverable 2D element paths and was left to the normalized fallback.");
                    continue;
                }

                target.AddRange(additions);
                warnings.RemoveAll(warning =>
                    warning.Contains("MLINE", StringComparison.OrdinalIgnoreCase) &&
                    (warning.Contains("not supported", StringComparison.OrdinalIgnoreCase) ||
                     warning.Contains("could not be imported", StringComparison.OrdinalIgnoreCase)));

                if (lossyFillOrCaps)
                {
                    const string notice = "AutoCAD MLINE element paths were recovered; complex area-fill breaks or curved cap semantics remain approximated.";
                    if (!warnings.Contains(notice, StringComparer.Ordinal)) warnings.Add(notice);
                }
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or OverflowException)
            {
                warnings.Add($"AutoCAD MLINE display recovery failed; normalized fallback was retained. {ex.Message}");
            }
        }
    }

    private static List<(ICadEntity Entity, CadEntityProperties Properties)> BuildGeometry(
        AcadMLine source,
        UcadDocument target,
        out bool lossyFillOrCaps)
    {
        lossyFillOrCaps = false;
        var additions = new List<(ICadEntity Entity, CadEntityProperties Properties)>();
        if (source.Vertices.Count < 2) return additions;

        var layerName = string.IsNullOrWhiteSpace(source.Layer?.Name) ? CadLayer.DefaultLayerName : source.Layer.Name;
        if (!target.TryGetLayer(layerName, out _)) target.CreateLayer(new CadLayer(layerName));

        var styleElements = source.Style?.Elements?.ToArray() ?? [];
        var elementCount = Math.Max(
            styleElements.Length,
            source.Vertices.Count == 0 ? 0 : source.Vertices.Max(vertex => vertex.Segments.Count));
        if (elementCount == 0) return additions;

        var closed = source.Flags.HasFlag(MLineFlags.Closed);
        var segmentCount = closed ? source.Vertices.Count : source.Vertices.Count - 1;
        for (var segmentIndex = 0; segmentIndex < segmentCount; segmentIndex++)
        {
            var current = source.Vertices[segmentIndex];
            var next = source.Vertices[(segmentIndex + 1) % source.Vertices.Count];
            var segmentIntersections = new List<(CadPoint Start, CadPoint End)>();
            var segmentHasFillBreaks = false;

            for (var elementIndex = 0; elementIndex < elementCount; elementIndex++)
            {
                var styleElement = elementIndex < styleElements.Length ? styleElements[elementIndex] : null;
                var currentSegment = elementIndex < current.Segments.Count ? current.Segments[elementIndex] : null;
                var nextSegment = elementIndex < next.Segments.Count ? next.Segments[elementIndex] : null;

                var fallbackOffset = (styleElement?.Offset ?? 0d) * source.ScaleFactor;
                var currentOffset = FirstParameter(currentSegment, fallbackOffset);
                var nextOffset = FirstParameter(nextSegment, fallbackOffset);
                var baseStart = OffsetAlongMiter(current, currentOffset);
                var baseEnd = OffsetAlongMiter(next, nextOffset);
                segmentIntersections.Add((baseStart, baseEnd));

                var direction = ResolveDirection(current, baseStart, baseEnd);
                var startShift = ParameterAt(currentSegment, 1, 0d);
                var actualStart = new CadPoint(baseStart.X + direction.X * startShift, baseStart.Y + direction.Y * startShift);
                var projectedLength = ((baseEnd.X - actualStart.X) * direction.X) + ((baseEnd.Y - actualStart.Y) * direction.Y);

                var lineType = !string.IsNullOrWhiteSpace(styleElement?.LineType?.Name)
                    ? styleElement!.LineType.Name
                    : (!string.IsNullOrWhiteSpace(source.LineType?.Name) ? source.LineType.Name : "ByLayer");
                var properties = new CadEntityProperties(layerName, lineType: lineType);

                if (projectedLength <= Tolerance)
                {
                    if (DistanceSquared(actualStart, baseEnd) > Tolerance * Tolerance)
                        additions.Add((new LineEntity(actualStart, baseEnd), properties));
                }
                else
                {
                    AppendVisibleSpans(additions, properties, actualStart, direction, projectedLength, currentSegment?.Parameters);
                }

                if ((currentSegment?.AreaFillParameters.Count ?? 0) > 0) segmentHasFillBreaks = true;
            }

            if (source.Style is not null && source.Style.Flags.HasFlag(MLineStyleFlags.FillOn))
            {
                if (!segmentHasFillBreaks && TryBuildFillPolygon(segmentIntersections, out var polygon))
                {
                    additions.Add((new HatchEntity(polygon, "Solid"), new CadEntityProperties(layerName)));
                }
                else
                {
                    lossyFillOrCaps = true;
                }
            }
        }

        if (!closed)
        {
            AddEndpointCaps(source, styleElements, additions, layerName, ref lossyFillOrCaps);
        }
        AddDisplayedJoints(source, styleElements, additions, layerName);
        return additions;
    }

    private static void AppendVisibleSpans(
        List<(ICadEntity Entity, CadEntityProperties Properties)> additions,
        CadEntityProperties properties,
        CadPoint start,
        (double X, double Y) direction,
        double totalLength,
        IReadOnlyList<double>? parameters)
    {
        if (parameters is null || parameters.Count <= 2)
        {
            additions.Add((new LineEntity(start, PointAt(start, direction, totalLength)), properties));
            return;
        }

        var cursor = 0d;
        var visible = true;
        for (var i = 2; i < parameters.Count; i++)
        {
            var boundary = Math.Clamp(parameters[i], 0d, totalLength);
            if (visible && boundary - cursor > Tolerance)
                additions.Add((new LineEntity(PointAt(start, direction, cursor), PointAt(start, direction, boundary)), properties));
            cursor = Math.Max(cursor, boundary);
            visible = !visible;
        }

        if (visible && totalLength - cursor > Tolerance)
            additions.Add((new LineEntity(PointAt(start, direction, cursor), PointAt(start, direction, totalLength)), properties));
    }

    private static void AddEndpointCaps(
        AcadMLine source,
        IReadOnlyList<MLineStyle.Element> styleElements,
        List<(ICadEntity Entity, CadEntityProperties Properties)> additions,
        string layerName,
        ref bool lossyFillOrCaps)
    {
        if (source.Vertices.Count == 0) return;
        var style = source.Style;
        if (style is null) return;

        var startCapRequested = !source.Flags.HasFlag(MLineFlags.NoStartCaps) &&
            (style.Flags.HasFlag(MLineStyleFlags.StartSquareCap) || style.Flags.HasFlag(MLineStyleFlags.StartInnerArcsCap) || style.Flags.HasFlag(MLineStyleFlags.StartRoundCap));
        var endCapRequested = !source.Flags.HasFlag(MLineFlags.NoEndCaps) &&
            (style.Flags.HasFlag(MLineStyleFlags.EndSquareCap) || style.Flags.HasFlag(MLineStyleFlags.EndInnerArcsCap) || style.Flags.HasFlag(MLineStyleFlags.EndRoundCap));

        if (startCapRequested && TryGetOuterPoints(source.Vertices[0], styleElements, source.ScaleFactor, out var startA, out var startB))
        {
            additions.Add((new LineEntity(startA, startB), new CadEntityProperties(layerName)));
            if (style.Flags.HasFlag(MLineStyleFlags.StartInnerArcsCap) || style.Flags.HasFlag(MLineStyleFlags.StartRoundCap)) lossyFillOrCaps = true;
        }
        if (endCapRequested && TryGetOuterPoints(source.Vertices[^1], styleElements, source.ScaleFactor, out var endA, out var endB))
        {
            additions.Add((new LineEntity(endA, endB), new CadEntityProperties(layerName)));
            if (style.Flags.HasFlag(MLineStyleFlags.EndInnerArcsCap) || style.Flags.HasFlag(MLineStyleFlags.EndRoundCap)) lossyFillOrCaps = true;
        }
    }

    private static void AddDisplayedJoints(
        AcadMLine source,
        IReadOnlyList<MLineStyle.Element> styleElements,
        List<(ICadEntity Entity, CadEntityProperties Properties)> additions,
        string layerName)
    {
        if (source.Style is null || !source.Style.Flags.HasFlag(MLineStyleFlags.DisplayJoints)) return;
        var first = source.Flags.HasFlag(MLineFlags.Closed) ? 0 : 1;
        var lastExclusive = source.Flags.HasFlag(MLineFlags.Closed) ? source.Vertices.Count : source.Vertices.Count - 1;
        for (var i = first; i < lastExclusive; i++)
        {
            if (TryGetOuterPoints(source.Vertices[i], styleElements, source.ScaleFactor, out var a, out var b))
                additions.Add((new LineEntity(a, b), new CadEntityProperties(layerName)));
        }
    }

    private static bool TryBuildFillPolygon(IReadOnlyList<(CadPoint Start, CadPoint End)> paths, out CadPoint[] polygon)
    {
        polygon = [];
        if (paths.Count < 2) return false;
        var orderedStart = paths.OrderBy(path => path.Start.Y).ThenBy(path => path.Start.X).ToArray();
        var first = orderedStart[0];
        var last = orderedStart[^1];
        var candidate = new[] { first.Start, last.Start, last.End, first.End };
        if (candidate.Distinct().Count() < 3) return false;
        polygon = candidate;
        return true;
    }

    private static bool TryGetOuterPoints(
        MLine.Vertex vertex,
        IReadOnlyList<MLineStyle.Element> styleElements,
        double scale,
        out CadPoint first,
        out CadPoint second)
    {
        var points = new List<(double Offset, CadPoint Point)>();
        var count = Math.Max(styleElements.Count, vertex.Segments.Count);
        for (var i = 0; i < count; i++)
        {
            var fallback = i < styleElements.Count ? styleElements[i].Offset * scale : 0d;
            var segment = i < vertex.Segments.Count ? vertex.Segments[i] : null;
            var offset = FirstParameter(segment, fallback);
            points.Add((offset, OffsetAlongMiter(vertex, offset)));
        }
        if (points.Count < 2)
        {
            first = default;
            second = default;
            return false;
        }
        first = points.MinBy(item => item.Offset).Point;
        second = points.MaxBy(item => item.Offset).Point;
        return DistanceSquared(first, second) > Tolerance * Tolerance;
    }

    private static CadPoint OffsetAlongMiter(MLine.Vertex vertex, double offset) =>
        new(vertex.Position.X + vertex.Miter.X * offset, vertex.Position.Y + vertex.Miter.Y * offset);

    private static double FirstParameter(MLine.Vertex.Segment? segment, double fallback) =>
        segment is not null && segment.Parameters.Count > 0 && double.IsFinite(segment.Parameters[0]) ? segment.Parameters[0] : fallback;

    private static double ParameterAt(MLine.Vertex.Segment? segment, int index, double fallback) =>
        segment is not null && segment.Parameters.Count > index && double.IsFinite(segment.Parameters[index]) ? segment.Parameters[index] : fallback;

    private static (double X, double Y) ResolveDirection(MLine.Vertex vertex, CadPoint start, CadPoint end)
    {
        var x = vertex.Direction.X;
        var y = vertex.Direction.Y;
        var length = Math.Sqrt(x * x + y * y);
        if (length <= Tolerance)
        {
            x = end.X - start.X;
            y = end.Y - start.Y;
            length = Math.Sqrt(x * x + y * y);
        }
        return length <= Tolerance ? (1d, 0d) : (x / length, y / length);
    }

    private static CadPoint PointAt(CadPoint origin, (double X, double Y) direction, double distance) =>
        new(origin.X + direction.X * distance, origin.Y + direction.Y * distance);

    private static double DistanceSquared(CadPoint first, CadPoint second)
    {
        var dx = second.X - first.X;
        var dy = second.Y - first.Y;
        return dx * dx + dy * dy;
    }
}

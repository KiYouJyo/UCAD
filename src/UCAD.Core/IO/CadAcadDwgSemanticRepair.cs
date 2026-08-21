using ACadSharp.Entities;
using UCAD.Core.Entities;
using UCAD.Core.Geometry;
using UCAD.Core.Layers;
using UCAD.Core.Styles;
using AcadDimension = ACadSharp.Entities.Dimension;
using AcadDocument = ACadSharp.CadDocument;
using AcadLeader = ACadSharp.Entities.Leader;
using AcadMText = ACadSharp.Entities.MText;
using AcadMultiLeader = ACadSharp.Entities.MultiLeader;
using UcadDocument = UCAD.Core.CadDocument;
using UcadLeader = UCAD.Core.Entities.LeaderEntity;

namespace UCAD.Core.IO;

internal static class CadAcadDwgSemanticRepair
{
    private const double GeometryTolerance = 1e-7;

    public static void Apply(AcadDocument source, UcadDocument target, List<string> warnings)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(warnings);
        RestoreDimensions(source, target, warnings);
        RestoreLeaders(source, target, warnings);
        RestoreMultiLeaders(source, target, warnings);
    }

    private static void RestoreDimensions(AcadDocument source, UcadDocument target, List<string> warnings)
    {
        var sourceDimensions = source.Entities.OfType<AcadDimension>().ToArray();
        if (sourceDimensions.Length == 0) return;

        // Treat native dimension replacement as an atomic enhancement. The normalized DXF
        // bridge may already have produced a usable visual representation, so a malformed,
        // degenerate, or not-yet-modeled native DIMENSION must never make the whole import fail
        // or cause other dimensions to disappear.
        var replacements = new List<(ICadEntity Entity, CadEntityProperties Properties)>(sourceDimensions.Length);
        var conversionWarnings = new List<string>();

        foreach (var sourceDimension in sourceDimensions)
        {
            try
            {
                var styleName = EnsureDimensionStyle(target, sourceDimension.Style?.Name);
                var textOverride = NormalizeDimensionText(sourceDimension.Text);
                ICadEntity? converted = sourceDimension switch
                {
                    DimensionAligned aligned => new LinearDimensionEntity(
                        ToCadPoint(aligned.FirstPoint), ToCadPoint(aligned.SecondPoint), ToCadPoint(aligned.DefinitionPoint), textOverride, styleName),
                    DimensionAngular3Pt angular3 => new AngularDimensionEntity(
                        ToCadPoint(angular3.AngleVertex), ToCadPoint(angular3.FirstPoint), ToCadPoint(angular3.SecondPoint), ToCadPoint(angular3.DefinitionPoint), textOverride, styleName),
                    DimensionAngular2Line angular2 => ConvertAngular2Line(angular2, textOverride, styleName, conversionWarnings),
                    DimensionRadius radius => new RadialDimensionEntity(
                        ToCadPoint(radius.DefinitionPoint),
                        ToCadPoint(radius.AngleVertex),
                        ToCadPoint(radius.TextMiddlePoint),
                        false,
                        textOverride,
                        styleName),
                    DimensionDiameter diameter => new RadialDimensionEntity(
                        ToCadPoint(diameter.Center),
                        ToCadPoint(diameter.AngleVertex),
                        ToCadPoint(diameter.TextMiddlePoint),
                        true,
                        textOverride,
                        styleName),
                    _ => null
                };

                if (converted is null)
                {
                    conversionWarnings.Add($"AutoCAD native semantic repair: dimension type {sourceDimension.GetType().Name} has no lossless UCAD 2D semantic equivalent yet.");
                    AppendDimensionFallbackWarnings(warnings, conversionWarnings);
                    return;
                }

                replacements.Add((converted, ToEntityProperties(sourceDimension, target)));
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or FormatException or OverflowException)
            {
                conversionWarnings.Add($"AutoCAD native semantic repair: dimension type {sourceDimension.GetType().Name} could not be upgraded safely: {ex.Message}");
                AppendDimensionFallbackWarnings(warnings, conversionWarnings);
                return;
            }
        }

        var existingDimensionIds = target.Entities
            .Where(entity => entity is LinearDimensionEntity or AngularDimensionEntity or RadialDimensionEntity)
            .Select(entity => entity.Id)
            .ToArray();
        if (existingDimensionIds.Length > 0) target.RemoveRange(existingDimensionIds);
        if (replacements.Count > 0) target.AddRange(replacements);
    }

    private static void AppendDimensionFallbackWarnings(List<string> warnings, IEnumerable<string> conversionWarnings)
    {
        foreach (var warning in conversionWarnings)
            if (!warnings.Contains(warning, StringComparer.Ordinal)) warnings.Add(warning);

        const string fallback = "AutoCAD native semantic repair: native DIMENSION replacement was skipped atomically; the normalized DXF display fallback was retained.";
        if (!warnings.Contains(fallback, StringComparer.Ordinal)) warnings.Add(fallback);
    }

    private static AngularDimensionEntity? ConvertAngular2Line(DimensionAngular2Line source, string? textOverride, string styleName, List<string> warnings)
    {
        var center = source.Center;
        if (!double.IsFinite(center.X) || !double.IsFinite(center.Y))
        {
            warnings.Add("AutoCAD native semantic repair: two-line angular dimension has no finite line intersection and was skipped.");
            return null;
        }
        var vertex = ToCadPoint(center);
        var firstRay = FartherPoint(vertex, ToCadPoint(source.FirstPoint), ToCadPoint(source.SecondPoint));
        var secondRay = FartherPoint(vertex, ToCadPoint(source.AngleVertex), ToCadPoint(source.DefinitionPoint));
        return new AngularDimensionEntity(vertex, firstRay, secondRay, ToCadPoint(source.DimensionArc), textOverride, styleName);
    }

    private static void RestoreLeaders(AcadDocument source, UcadDocument target, List<string> warnings)
    {
        var sourceLeaders = source.Entities.OfType<AcadLeader>().ToArray();
        if (sourceLeaders.Length == 0) return;
        var sourceMTexts = source.Entities.OfType<AcadMText>().ToArray();
        var usedMTexts = new HashSet<ulong>();

        foreach (var sourceLeader in sourceLeaders)
        {
            if (sourceLeader.Vertices.Count < 2) continue;
            if (sourceLeader.CreationType != LeaderCreationType.CreatedWithTextAnnotation && sourceLeader.AssociatedAnnotation is not AcadMText) continue;
            var annotation = sourceLeader.AssociatedAnnotation as AcadMText;
            if (annotation is null)
            {
                var endpoint = sourceLeader.Vertices[^1];
                annotation = sourceMTexts.Where(candidate => !usedMTexts.Contains(candidate.Handle))
                    .OrderBy(candidate => DistanceSquared(candidate.InsertPoint, endpoint))
                    .FirstOrDefault(candidate => DistanceSquared(candidate.InsertPoint, endpoint) <= GeometryTolerance * GeometryTolerance);
            }
            if (annotation is null)
            {
                warnings.Add("DWG native semantic repair: text LEADER had no recoverable MTEXT annotation; its DXF fallback geometry was retained.");
                continue;
            }
            usedMTexts.Add(annotation.Handle);
            var text = NormalizeMText(annotation.PlainText);
            if (string.IsNullOrWhiteSpace(text))
            {
                warnings.Add("DWG native semantic repair: text LEADER annotation was empty; its DXF fallback geometry was retained.");
                continue;
            }
            var points = sourceLeader.Vertices.Select(ToCadPoint).ToArray();
            RemoveLeaderFallback(target, points, annotation, text);
            var styleName = EnsureDimensionStyle(target, sourceLeader.Style?.Name);
            var textHeight = sourceLeader.TextHeight > 0 ? sourceLeader.TextHeight : Math.Max(annotation.Height, 2.5);
            target.Add(new UcadLeader(points, text, textHeight, styleName), ToEntityProperties(sourceLeader, target));
        }
    }

    private static void RestoreMultiLeaders(AcadDocument source, UcadDocument target, List<string> warnings)
    {
        var sourceMultiLeaders = source.Entities.OfType<AcadMultiLeader>().ToArray();
        if (sourceMultiLeaders.Length == 0) return;

        foreach (var sourceMultiLeader in sourceMultiLeaders)
        {
            var context = sourceMultiLeader.ContextData;
            if (context is null) continue;

            var text = NormalizeMText(context.TextLabel);
            var textHeight = context.TextHeight > GeometryTolerance ? context.TextHeight : 2.5;
            var properties = ToEntityProperties(sourceMultiLeader, target);
            var recoveredAny = false;
            var emittedTextLeader = false;

            foreach (var root in context.LeaderRoots)
            {
                foreach (var line in root.Lines)
                {
                    var points = new List<CadPoint>();
                    foreach (var point in line.Points)
                    {
                        if (IsFinite(point)) AppendDistinct(points, ToCadPoint(point));
                    }

                    if (IsFinite(root.ConnectionPoint)) AppendDistinct(points, ToCadPoint(root.ConnectionPoint));
                    if (!string.IsNullOrWhiteSpace(text) && IsFinite(context.ContentBasePoint))
                        AppendDistinct(points, ToCadPoint(context.ContentBasePoint));

                    if (points.Count < 2) continue;
                    if (!emittedTextLeader && !string.IsNullOrWhiteSpace(text))
                    {
                        target.Add(new UcadLeader(points, text, textHeight, CadTextStyle.DefaultName), properties);
                        emittedTextLeader = true;
                    }
                    else
                    {
                        target.Add(new PolylineEntity(points, closed: false), properties);
                    }
                    recoveredAny = true;
                }
            }

            if (!recoveredAny) continue;

            // The ASCII bridge does not understand MLEADER today. Once native DWG context data
            // has produced visible leader geometry, replace the generic unsupported-record warning
            // with a narrower warning only when content semantics are still lossy.
            warnings.RemoveAll(warning => warning.Contains("MLEADER", StringComparison.OrdinalIgnoreCase));
            if (string.IsNullOrWhiteSpace(text))
            {
                warnings.Add("DWG native semantic repair: MLEADER leader geometry was recovered, but non-text/block content has no editable UCAD equivalent yet.");
            }
        }
    }

    private static void RemoveLeaderFallback(UcadDocument target, IReadOnlyList<CadPoint> leaderPoints, AcadMText annotation, string text)
    {
        var removals = new List<Guid>();
        var fallbackPolyline = target.Entities.OfType<PolylineEntity>().FirstOrDefault(polyline => !polyline.Closed && PointsMatch(polyline.Points, leaderPoints));
        if (fallbackPolyline is not null) removals.Add(fallbackPolyline.Id);
        var annotationPoint = ToCadPoint(annotation.InsertPoint);
        var fallbackMText = target.Entities.OfType<MTextEntity>().FirstOrDefault(mtext => PointsNear(mtext.Position, annotationPoint) && string.Equals(NormalizeMText(mtext.Text), text, StringComparison.Ordinal));
        if (fallbackMText is not null) removals.Add(fallbackMText.Id);
        if (removals.Count > 0) target.RemoveRange(removals);
    }

    private static CadEntityProperties ToEntityProperties(ACadSharp.Entities.Entity source, UcadDocument target)
    {
        var layerName = string.IsNullOrWhiteSpace(source.Layer?.Name) ? CadLayer.DefaultLayerName : source.Layer.Name;
        if (!target.TryGetLayer(layerName, out _)) target.CreateLayer(new CadLayer(layerName));
        var lineType = string.IsNullOrWhiteSpace(source.LineType?.Name) ? "ByLayer" : source.LineType.Name;
        return new CadEntityProperties(layerName, lineType: lineType);
    }

    private static string EnsureDimensionStyle(UcadDocument target, string? sourceName)
    {
        var name = string.IsNullOrWhiteSpace(sourceName) ? CadDimensionStyle.DefaultName : sourceName.Trim();
        if (!target.TryGetDimensionStyle(name, out _)) target.DefineDimensionStyle(new CadDimensionStyle(name));
        return target.GetDimensionStyle(name).Name;
    }

    private static string? NormalizeDimensionText(string? value) => string.IsNullOrEmpty(value) || string.Equals(value, "<>", StringComparison.Ordinal) ? null : value;
    private static string NormalizeMText(string? value) => (value ?? string.Empty).Replace("\r\n", "\n", StringComparison.Ordinal).Replace("\r", "\n", StringComparison.Ordinal).Replace("\\P", "\n", StringComparison.OrdinalIgnoreCase);

    private static bool PointsMatch(IReadOnlyList<CadPoint> first, IReadOnlyList<CadPoint> second)
    {
        if (first.Count != second.Count) return false;
        for (var i = 0; i < first.Count; i++) if (!PointsNear(first[i], second[i])) return false;
        return true;
    }

    private static void AppendDistinct(List<CadPoint> points, CadPoint point)
    {
        if (points.Count == 0 || !PointsNear(points[^1], point)) points.Add(point);
    }

    private static bool IsFinite(CSMath.XYZ point) => double.IsFinite(point.X) && double.IsFinite(point.Y);
    private static bool PointsNear(CadPoint first, CadPoint second) => Math.Abs(first.X - second.X) <= GeometryTolerance && Math.Abs(first.Y - second.Y) <= GeometryTolerance;
    private static double DistanceSquared(CSMath.XYZ first, CSMath.XYZ second) { var dx = first.X - second.X; var dy = first.Y - second.Y; return dx * dx + dy * dy; }
    private static CadPoint FartherPoint(CadPoint origin, CadPoint first, CadPoint second) => DistanceSquared(origin, first) >= DistanceSquared(origin, second) ? first : second;
    private static double DistanceSquared(CadPoint first, CadPoint second) { var dx = first.X - second.X; var dy = first.Y - second.Y; return dx * dx + dy * dy; }
    private static CadPoint ToCadPoint(CSMath.XYZ point) => new(point.X, point.Y);
}

using ACadSharp;
using ACadSharp.Entities;
using UCAD.Core.Entities;
using UCAD.Core.Geometry;
using UCAD.Core.Layers;
using UCAD.Core.Styles;
using AcadDimension = ACadSharp.Entities.Dimension;
using AcadLeader = ACadSharp.Entities.Leader;
using AcadMText = ACadSharp.Entities.MText;
using UcadDocument = UCAD.Core.CadDocument;
using UcadLeader = UCAD.Core.Entities.LeaderEntity;

namespace UCAD.Core.IO;

/// <summary>
/// Repairs high-value semantics directly from the parsed DWG object graph when ACadSharp's
/// DWG-to-DXF serializer cannot faithfully reproduce the relationship. This is deliberately
/// narrow: the shared DXF bridge remains authoritative for everything it can preserve, while
/// native DWG objects fill only verified gaps such as dimensions and LEADER annotation links.
/// </summary>
internal static class CadAcadDwgSemanticRepair
{
    private const double GeometryTolerance = 1e-7;

    public static void Apply(CadDocument source, UcadDocument target, List<string> warnings)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(warnings);

        RestoreDimensions(source, target, warnings);
        RestoreLeaders(source, target, warnings);
    }

    private static void RestoreDimensions(CadDocument source, UcadDocument target, List<string> warnings)
    {
        var sourceDimensions = source.Entities.OfType<AcadDimension>().ToArray();
        if (sourceDimensions.Length == 0) return;

        var existingDimensionIds = target.Entities
            .Where(entity => entity is LinearDimensionEntity or AngularDimensionEntity)
            .Select(entity => entity.Id)
            .ToArray();
        if (existingDimensionIds.Length > 0) target.RemoveRange(existingDimensionIds);

        foreach (var sourceDimension in sourceDimensions)
        {
            var styleName = EnsureDimensionStyle(target, sourceDimension.Style?.Name);
            var textOverride = NormalizeDimensionText(sourceDimension.Text);
            ICadEntity? converted = sourceDimension switch
            {
                DimensionAligned aligned => new LinearDimensionEntity(
                    ToCadPoint(aligned.FirstPoint),
                    ToCadPoint(aligned.SecondPoint),
                    ToCadPoint(aligned.DefinitionPoint),
                    textOverride,
                    styleName),
                DimensionAngular3Pt angular3 => new AngularDimensionEntity(
                    ToCadPoint(angular3.AngleVertex),
                    ToCadPoint(angular3.FirstPoint),
                    ToCadPoint(angular3.SecondPoint),
                    ToCadPoint(angular3.DefinitionPoint),
                    textOverride,
                    styleName),
                DimensionAngular2Line angular2 => ConvertAngular2Line(angular2, textOverride, styleName, warnings),
                _ => null
            };

            if (converted is null)
            {
                warnings.Add($"DWG native semantic repair: dimension type {sourceDimension.GetType().Name} is recognized but has no matching UCAD 2D dimension entity yet.");
                continue;
            }

            target.Add(converted, ToEntityProperties(sourceDimension, target));
        }
    }

    private static AngularDimensionEntity? ConvertAngular2Line(
        DimensionAngular2Line source,
        string? textOverride,
        string styleName,
        List<string> warnings)
    {
        var center = source.Center;
        if (!double.IsFinite(center.X) || !double.IsFinite(center.Y))
        {
            warnings.Add("DWG native semantic repair: two-line angular dimension has no finite line intersection and was skipped.");
            return null;
        }

        var vertex = ToCadPoint(center);
        var firstA = ToCadPoint(source.FirstPoint);
        var firstB = ToCadPoint(source.SecondPoint);
        var secondA = ToCadPoint(source.AngleVertex);
        var secondB = ToCadPoint(source.DefinitionPoint);
        var firstRay = FartherPoint(vertex, firstA, firstB);
        var secondRay = FartherPoint(vertex, secondA, secondB);
        var arcPoint = ToCadPoint(source.DimensionArc);
        return new AngularDimensionEntity(vertex, firstRay, secondRay, arcPoint, textOverride, styleName);
    }

    private static void RestoreLeaders(CadDocument source, UcadDocument target, List<string> warnings)
    {
        var sourceLeaders = source.Entities.OfType<AcadLeader>().ToArray();
        if (sourceLeaders.Length == 0) return;

        var sourceMTexts = source.Entities.OfType<AcadMText>().ToArray();
        var usedMTexts = new HashSet<ulong>();

        foreach (var sourceLeader in sourceLeaders)
        {
            if (sourceLeader.Vertices.Count < 2) continue;
            if (sourceLeader.CreationType != LeaderCreationType.CreatedWithTextAnnotation && sourceLeader.AssociatedAnnotation is not AcadMText)
                continue;

        var annotation = sourceLeader.AssociatedAnnotation as AcadMText;
            if (annotation is null)
            {
                var endpoint = sourceLeader.Vertices[^1];
                annotation = sourceMTexts
                    .Where(candidate => !usedMTexts.Contains(candidate.Handle))
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

    private static void RemoveLeaderFallback(UcadDocument target, IReadOnlyList<CadPoint> leaderPoints, AcadMText annotation, string text)
    {
        var removals = new List<Guid>();
        var fallbackPolyline = target.Entities
            .OfType<PolylineEntity>()
            .FirstOrDefault(polyline => !polyline.Closed && PointsMatch(polyline.Points, leaderPoints));
        if (fallbackPolyline is not null) removals.Add(fallbackPolyline.Id);

        var annotationPoint = ToCadPoint(annotation.InsertPoint);
        var fallbackMText = target.Entities
            .OfType<MTextEntity>()
            .FirstOrDefault(mtext =>
                PointsNear(mtext.Position, annotationPoint) &&
                string.Equals(NormalizeMText(mtext.Text), text, StringComparison.Ordinal));
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

    private static string? NormalizeDimensionText(string? value) =>
        string.IsNullOrEmpty(value) || string.Equals(value, "<>", StringComparison.Ordinal) ? null : value;

    private static string NormalizeMText(string value) =>
        (value ?? string.Empty)
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace("\r", "\n", StringComparison.Ordinal)
            .Replace("\\P", "\n", StringComparison.OrdinalIgnoreCase);

    private static bool PointsMatch(IReadOnlyList<CadPoint> first, IReadOnlyList<CadPoint> second)
    {
        if (first.Count != second.Count) return false;
        for (var i = 0; i < first.Count; i++)
            if (!PointsNear(first[i], second[i])) return false;
        return true;
    }

    private static bool PointsNear(CadPoint first, CadPoint second) =>
        Math.Abs(first.X - second.X) <= GeometryTolerance && Math.Abs(first.Y - second.Y) <= GeometryTolerance;

    private static double DistanceSquared(CSMath.XYZ first, CSMath.XYZ second)
    {
        var dx = first.X - second.X;
        var dy = first.Y - second.Y;
        return (dx * dx) + (dy * dy);
    }

    private static CadPoint FartherPoint(CadPoint origin, CadPoint first, CadPoint second)
    {
        var firstDistance = DistanceSquared(origin, first);
        var secondDistance = DistanceSquared(origin, second);
        return firstDistance >= secondDistance ? first : second;
    }

    private static double DistanceSquared(CadPoint first, CadPoint second)
    {
        var dx = first.X - second.X;
        var dy = first.Y - second.Y;
        return (dx * dx) + (dy * dy);
    }

    private static CadPoint ToCadPoint(CSMath.XYZ point) => new(point.X, point.Y);
}

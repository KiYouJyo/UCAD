using System.Globalization;
using ACadSharp.Entities.ProxyGraphics;
using UCAD.Core.Entities;
using UCAD.Core.Geometry;
using UCAD.Core.Layers;
using UCAD.Core.Styles;
using AcadDocument = ACadSharp.CadDocument;
using AcadEntity = ACadSharp.Entities.Entity;
using UcadDocument = UCAD.Core.CadDocument;

namespace UCAD.Core.IO;

/// <summary>
/// Recovers AutoCAD proxy graphics only when the source entity has no normal semantic/display
/// representation in UCAD. ACadSharp has already decoded the embedded proxy stream into primitives;
/// this pass intentionally consumes those primitives instead of inventing a generic bounding box.
/// </summary>
internal static class CadAcadProxyGraphicsDisplayRepair
{
    private const double Epsilon = 1e-9;
    private const double MaxArcStep = Math.PI / 18.0;

    public static void Apply(AcadDocument source, UcadDocument target, List<string> warnings)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(warnings);

        var alreadyRepresented = target.Entities
            .Select(entity => target.GetEntityProperties(entity.Id).SourceOrder)
            .Where(order => order.HasValue)
            .Select(order => order!.Value)
            .ToHashSet();

        var sourceEntities = source.Entities.ToArray();
        for (var sourceOrder = 0; sourceOrder < sourceEntities.Length; sourceOrder++)
        {
            if (alreadyRepresented.Contains(sourceOrder)) continue;
            if (sourceEntities[sourceOrder] is not AcadEntity sourceEntity || sourceEntity.ProxyGeometries.Count == 0) continue;

            var recovered = new List<ICadEntity>();
            ProxyExtents? extents = null;
            foreach (var proxy in sourceEntity.ProxyGeometries)
            {
                if (proxy is ProxyExtents proxyExtents)
                {
                    extents = proxyExtents;
                    continue;
                }

                try
                {
                    ConvertPrimitive(proxy, recovered);
                }
                catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or OverflowException)
                {
                    warnings.Add($"AutoCAD proxy primitive '{proxy.GraphicsType}' could not be recovered for source order {sourceOrder}: {ex.Message}");
                }
            }

            // Extents are a last-resort display hint, never preferred over decoded proxy primitives.
            if (recovered.Count == 0 && extents is not null) AddExtents(extents, recovered);
            if (recovered.Count == 0) continue;

            var properties = ToProperties(sourceEntity, target, sourceOrder);
            target.AddRange(recovered.Select(entity => (entity, properties)));
            warnings.RemoveAll(warning =>
                warning.Contains("proxy", StringComparison.OrdinalIgnoreCase) &&
                (warning.Contains("not supported", StringComparison.OrdinalIgnoreCase) ||
                 warning.Contains("could not be imported", StringComparison.OrdinalIgnoreCase)));
        }
    }

    private static void ConvertPrimitive(IProxyGeometry proxy, List<ICadEntity> target)
    {
        switch (proxy)
        {
            case ProxyPolyline polyline:
                AddPolyline(polyline.Points.Select(ToCadPoint), closed: false, target);
                break;
            case ProxyPolygon polygon when polygon.Points is { Count: >= 2 }:
                AddPolyline(polygon.Points.Select(ToCadPoint), closed: true, target);
                break;
            case ProxyCircle circle when double.IsFinite(circle.Radius) && circle.Radius > Epsilon:
                target.Add(new CircleEntity(ToCadPoint(circle.Center), circle.Radius));
                break;
            case ProxyCircularArc arc:
                AddCircularArc(arc, target);
                break;
            case ProxyCircularArc3Pt arc3:
                if (ArcEntity.TryCreateFromThreePoints(ToCadPoint(arc3.Point1), ToCadPoint(arc3.Point2), ToCadPoint(arc3.Point3), out var converted) && converted is not null)
                    target.Add(converted);
                break;
            case ProxyText text when !string.IsNullOrEmpty(text.Text) && double.IsFinite(text.Height) && text.Height > Epsilon:
                target.Add(new TextEntity(ToCadPoint(text.StartPoint), text.Text, text.Height, DirectionAngle(text.TextDirection), CadTextStyle.DefaultName));
                break;
            case ProxyUnicodeText text when !string.IsNullOrEmpty(text.Text) && double.IsFinite(text.Height) && text.Height > Epsilon:
                target.Add(new TextEntity(ToCadPoint(text.StartPoint), text.Text, text.Height, DirectionAngle(text.TextDirection), CadTextStyle.DefaultName));
                break;
        }
    }

    private static void AddCircularArc(ProxyCircularArc source, List<ICadEntity> target)
    {
        if (!double.IsFinite(source.Radius) || source.Radius <= Epsilon ||
            !double.IsFinite(source.SweepAngle) || Math.Abs(source.SweepAngle) <= Epsilon) return;

        var start = DirectionAngle(source.StartVectorDirection);
        var sweep = Math.Clamp(source.SweepAngle, -Math.Tau, Math.Tau);
        var segments = Math.Max(2, (int)Math.Ceiling(Math.Abs(sweep) / MaxArcStep));
        var points = new CadPoint[segments + 1];
        for (var i = 0; i <= segments; i++)
        {
            var angle = start + sweep * i / segments;
            points[i] = new CadPoint(
                source.Center.X + Math.Cos(angle) * source.Radius,
                source.Center.Y + Math.Sin(angle) * source.Radius);
        }
        AddPolyline(points, closed: false, target);
    }

    private static void AddExtents(ProxyExtents source, List<ICadEntity> target)
    {
        if (!double.IsFinite(source.Min.X) || !double.IsFinite(source.Min.Y) ||
            !double.IsFinite(source.Max.X) || !double.IsFinite(source.Max.Y)) return;
        if (source.Max.X - source.Min.X <= Epsilon || source.Max.Y - source.Min.Y <= Epsilon) return;
        AddPolyline(
        [
            new CadPoint(source.Min.X, source.Min.Y),
            new CadPoint(source.Max.X, source.Min.Y),
            new CadPoint(source.Max.X, source.Max.Y),
            new CadPoint(source.Min.X, source.Max.Y)
        ], closed: true, target);
    }

    private static void AddPolyline(IEnumerable<CadPoint> points, bool closed, List<ICadEntity> target)
    {
        var cleaned = new List<CadPoint>();
        foreach (var point in points)
        {
            if (!double.IsFinite(point.X) || !double.IsFinite(point.Y)) continue;
            if (cleaned.Count == 0 || (point - cleaned[^1]).Length > Epsilon) cleaned.Add(point);
        }
        if (closed && cleaned.Count > 1 && (cleaned[0] - cleaned[^1]).Length <= Epsilon) cleaned.RemoveAt(cleaned.Count - 1);
        if (cleaned.Count >= 2) target.Add(new PolylineEntity(cleaned, closed));
    }

    private static CadEntityProperties ToProperties(AcadEntity source, UcadDocument target, int sourceOrder)
    {
        var layer = string.IsNullOrWhiteSpace(source.Layer?.Name) ? CadLayer.DefaultLayerName : source.Layer.Name;
        if (!target.TryGetLayer(layer, out _)) target.CreateLayer(new CadLayer(layer));
        var lineType = string.IsNullOrWhiteSpace(source.LineType?.Name) ? "ByLayer" : source.LineType.Name;
        var handle = source.Handle == 0 ? null : source.Handle.ToString("X", CultureInfo.InvariantCulture);
        return new CadEntityProperties(layer, lineType: lineType, sourceOrder: sourceOrder, sourceHandle: handle);
    }

    private static CadPoint ToCadPoint(CSMath.XYZ point) => new(point.X, point.Y);

    private static double DirectionAngle(CSMath.XYZ direction)
    {
        if (!double.IsFinite(direction.X) || !double.IsFinite(direction.Y) ||
            Math.Abs(direction.X) + Math.Abs(direction.Y) <= Epsilon) return 0d;
        return Math.Atan2(direction.Y, direction.X);
    }
}

using IxMilia.Dxf;
using IxMilia.Dxf.Entities;
using UCAD.Core.Entities;
using UCAD.Core.Geometry;
using UCAD.Core.Layers;
using UcadDocument = UCAD.Core.CadDocument;

namespace UCAD.Core.IO;

/// <summary>
/// Legacy AutoCAD DXB 1.0 transport. DXB is a compact geometry interchange format rather than
/// a full drawing database, so the codec intentionally exposes a bounded 2D geometry subset and
/// reports every downgrade instead of silently pretending to preserve annotation or modern CAD data.
/// </summary>
public static class CadDxbCodec
{
    public static CadAcadImportResult Import(ReadOnlyMemory<byte> content)
    {
        if (content.IsEmpty) throw new ArgumentException("DXB content cannot be empty.", nameof(content));

        var warnings = new List<string>();
        using var input = new MemoryStream(content.ToArray(), writable: false);
        var file = DxfFile.Load(input);
        var document = new UcadDocument();

        foreach (var source in file.Entities)
        {
            ImportEntity(source, document, warnings);
        }

        document.ResetHistory();
        return new CadAcadImportResult(document, warnings, ".dxb", "DXB 1.0");
    }

    public static CadAcadBinaryExportResult Export(UcadDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        var warnings = new List<string>();
        var file = new DxfFile();
        file.Entities.Clear();

        foreach (var entity in document.Entities)
        {
            var properties = document.GetEntityProperties(entity.Id);
            var layer = GetDxbLayerName(properties.LayerName, entity.Id, warnings);
            WarnUnsupportedProperties(properties, entity.Id, warnings);

            DxfEntity? converted = entity switch
            {
                LineEntity line => new DxfLine(ToDxfPoint(line.Start), ToDxfPoint(line.End)),
                PointEntity point => new DxfModelPoint(ToDxfPoint(point.Position)),
                CircleEntity circle => new DxfCircle(ToDxfPoint(circle.Center), circle.Radius),
                ArcEntity arc => CreateDxfArc(arc),
                PolylineEntity polyline => CreateDxfPolyline(polyline),
                _ => null
            };

            if (converted is null)
            {
                warnings.Add($"DXB export skipped unsupported UCAD entity {entity.GetType().Name} ({entity.Id}).");
                continue;
            }

            converted.Layer = layer;
            file.Entities.Add(converted);
        }

        using var output = new MemoryStream();
        file.SaveDxb(output);
        return new CadAcadBinaryExportResult(output.ToArray(), warnings, ".dxb", "DXB 1.0");
    }

    private static void ImportEntity(DxfEntity source, UcadDocument document, List<string> warnings)
    {
        var layerName = EnsureLayer(document, source.Layer);
        var properties = new CadEntityProperties(layerName);

        switch (source)
        {
            case DxfLine line:
                WarnIf3D(line.P1.Z, line.P2.Z, "LINE", warnings);
                document.Add(new LineEntity(ToCadPoint(line.P1), ToCadPoint(line.P2)), properties);
                return;

            case DxfModelPoint point:
                WarnIf3D(point.Location.Z, "POINT", warnings);
                document.Add(new PointEntity(ToCadPoint(point.Location)), properties);
                return;

            case DxfCircle circle:
                WarnIf3D(circle.Center.Z, "CIRCLE", warnings);
                document.Add(new CircleEntity(ToCadPoint(circle.Center), circle.Radius), properties);
                return;

            case DxfArc arc:
                WarnIf3D(arc.Center.Z, "ARC", warnings);
                document.Add(CreateCadArc(arc), properties);
                return;

            case DxfPolyline polyline:
                ImportPolyline(polyline, document, properties, warnings);
                return;

            case DxfTrace trace:
                document.Add(new PolylineEntity(
                    [
                        ToCadPoint(trace.FirstCorner),
                        ToCadPoint(trace.SecondCorner),
                        ToCadPoint(trace.FourthCorner),
                        ToCadPoint(trace.ThirdCorner)
                    ],
                    closed: true), properties);
                warnings.Add("DXB TRACE was imported as a closed UCAD polyline.");
                return;

            case DxfSolid solid:
                document.Add(new PolylineEntity(
                    [
                        ToCadPoint(solid.FirstCorner),
                        ToCadPoint(solid.SecondCorner),
                        ToCadPoint(solid.FourthCorner),
                        ToCadPoint(solid.ThirdCorner)
                    ],
                    closed: true), properties);
                warnings.Add("DXB SOLID was imported as its closed 2D boundary; fill semantics were not preserved.");
                return;

            case Dxf3DFace face:
                if (!IsPlanar2D(face.FirstCorner, face.SecondCorner, face.ThirdCorner, face.FourthCorner))
                {
                    warnings.Add("DXB 3DFACE with non-zero Z coordinates was skipped because UCAD 1.x is 2D-first.");
                    return;
                }
                document.Add(new PolylineEntity(
                    [
                        ToCadPoint(face.FirstCorner),
                        ToCadPoint(face.SecondCorner),
                        ToCadPoint(face.ThirdCorner),
                        ToCadPoint(face.FourthCorner)
                    ],
                    closed: true), properties);
                warnings.Add("DXB planar 3DFACE was imported as a closed UCAD polyline.");
                return;

            default:
                warnings.Add($"DXB entity '{source.EntityType}' is not supported by the UCAD 2D DXB bridge and was skipped.");
                return;
        }
    }

    private static void ImportPolyline(
        DxfPolyline polyline,
        UcadDocument document,
        CadEntityProperties properties,
        List<string> warnings)
    {
        if (polyline.Vertices.Any(vertex => Math.Abs(vertex.Location.Z) > 1e-9))
            warnings.Add("DXB POLYLINE Z coordinates were flattened to the UCAD 2D plane.");

        if (polyline.Vertices.Any(vertex => Math.Abs(vertex.Bulge) > 1e-10))
        {
            foreach (var simple in polyline.AsSimpleEntities()) ImportEntityWithProperties(simple, document, properties, warnings);
            warnings.Add("DXB POLYLINE bulge segments were expanded to independent LINE/ARC entities for exact 2D geometry.");
            return;
        }

        if (polyline.Vertices.Count < 2)
        {
            warnings.Add("DXB POLYLINE with fewer than two vertices was skipped.");
            return;
        }

        document.Add(
            new PolylineEntity(polyline.Vertices.Select(vertex => ToCadPoint(vertex.Location)), polyline.IsClosed),
            properties);
    }

    private static void ImportEntityWithProperties(
        DxfEntity source,
        UcadDocument document,
        CadEntityProperties properties,
        List<string> warnings)
    {
        switch (source)
        {
            case DxfLine line:
                document.Add(new LineEntity(ToCadPoint(line.P1), ToCadPoint(line.P2)), properties);
                break;
            case DxfArc arc:
                document.Add(CreateCadArc(arc), properties);
                break;
            default:
                warnings.Add($"Expanded DXB polyline segment '{source.EntityType}' could not be mapped and was skipped.");
                break;
        }
    }

    private static DxfArc CreateDxfArc(ArcEntity arc)
    {
        var start = NormalizeDegrees(arc.StartAngleRadians * 180.0 / Math.PI);
        var end = NormalizeDegrees((arc.StartAngleRadians + arc.SweepAngleRadians) * 180.0 / Math.PI);
        if (arc.SweepAngleRadians < 0) (start, end) = (end, start);
        return new DxfArc(ToDxfPoint(arc.Center), arc.Radius, start, end);
    }

    private static ArcEntity CreateCadArc(DxfArc arc)
    {
        var start = arc.StartAngle * Math.PI / 180.0;
        var end = arc.EndAngle * Math.PI / 180.0;
        var sweep = (end - start) % Math.Tau;
        if (sweep < 0) sweep += Math.Tau;
        if (sweep <= 1e-12) sweep = Math.Tau;
        return ArcEntity.Create(ToCadPoint(arc.Center), arc.Radius, start, sweep);
    }

    private static DxfPolyline CreateDxfPolyline(PolylineEntity polyline)
    {
        var vertices = polyline.Points.Select(point => new DxfVertex(ToDxfPoint(point))).ToArray();
        return new DxfPolyline(vertices) { IsClosed = polyline.Closed };
    }

    private static string EnsureLayer(UcadDocument document, string? layerName)
    {
        var normalized = string.IsNullOrWhiteSpace(layerName) ? CadLayer.DefaultLayerName : layerName.Trim();
        if (!document.TryGetLayer(normalized, out _)) document.CreateLayer(new CadLayer(normalized));
        return normalized;
    }

    private static string GetDxbLayerName(string layerName, Guid entityId, List<string> warnings)
    {
        if (layerName.All(ch => ch <= byte.MaxValue)) return layerName;
        warnings.Add($"DXB export moved entity {entityId} from layer '{layerName}' to layer 0 because DXB 1.0 layer names are byte-oriented.");
        return CadLayer.DefaultLayerName;
    }

    private static void WarnUnsupportedProperties(CadEntityProperties properties, Guid entityId, List<string> warnings)
    {
        if (properties.ColorHex is not null || properties.LineWeight is not null || !string.Equals(properties.LineType, "ByLayer", StringComparison.OrdinalIgnoreCase))
            warnings.Add($"DXB export for entity {entityId} preserves its layer but not UCAD true-color, lineweight, or non-ByLayer linetype overrides.");
    }

    private static CadPoint ToCadPoint(DxfPoint point) => new(point.X, point.Y);

    private static DxfPoint ToDxfPoint(CadPoint point) => new(point.X, point.Y, 0.0);

    private static bool IsPlanar2D(params DxfPoint[] points) => points.All(point => Math.Abs(point.Z) <= 1e-9);

    private static void WarnIf3D(double z, string entityType, List<string> warnings)
    {
        if (Math.Abs(z) > 1e-9) warnings.Add($"DXB {entityType} Z coordinate was flattened to the UCAD 2D plane.");
    }

    private static void WarnIf3D(double z1, double z2, string entityType, List<string> warnings)
    {
        if (Math.Abs(z1) > 1e-9 || Math.Abs(z2) > 1e-9)
            warnings.Add($"DXB {entityType} Z coordinates were flattened to the UCAD 2D plane.");
    }

    private static double NormalizeDegrees(double degrees)
    {
        var normalized = degrees % 360.0;
        return normalized < 0 ? normalized + 360.0 : normalized;
    }
}

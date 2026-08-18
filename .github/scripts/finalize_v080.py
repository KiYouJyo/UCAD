from __future__ import annotations

import json
import re
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]


def read(path: str) -> str:
    return (ROOT / path).read_text(encoding="utf-8")


def write(path: str, text: str) -> None:
    target = ROOT / path
    target.parent.mkdir(parents=True, exist_ok=True)
    target.write_text(text, encoding="utf-8", newline="\n")


def replace_once(path: str, old: str, new: str) -> None:
    text = read(path)
    count = text.count(old)
    if count != 1:
        raise RuntimeError(f"Expected exactly one marker in {path}, found {count}: {old[:120]!r}")
    write(path, text.replace(old, new, 1))


# Core closure: advanced blocks.
write(
    "src/UCAD.Core/Blocks/CadBlockFactory.cs",
    """using UCAD.Core.Entities;
using UCAD.Core.Geometry;
using UCAD.Core.Modify;

namespace UCAD.Core.Blocks;

public static class CadBlockFactory
{
    private static readonly CadPoint Origin = new(0, 0);

    public static BlockReferenceEntity CreateReference(
        CadBlockDefinition definition,
        CadPoint insertionPoint,
        double scale = 1,
        double rotationRadians = 0) =>
        CreateReferenceCore(definition, insertionPoint, scale, rotationRadians, attributeValues: null, preserveId: null);

    public static BlockReferenceEntity CreateReference(
        CadBlockDefinition definition,
        CadPoint insertionPoint,
        IReadOnlyDictionary<string, string>? attributeValues,
        double scale = 1,
        double rotationRadians = 0) =>
        CreateReferenceCore(definition, insertionPoint, scale, rotationRadians, attributeValues, preserveId: null);

    public static BlockReferenceEntity CreateReference(
        CadBlockDefinition definition,
        CadPoint insertionPoint,
        double scale,
        double rotationRadians,
        IReadOnlyDictionary<string, string>? attributeValues) =>
        CreateReferenceCore(definition, insertionPoint, scale, rotationRadians, attributeValues, preserveId: null);

    public static BlockReferenceEntity RefreshReference(CadBlockDefinition definition, BlockReferenceEntity reference)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(reference);
        if (!string.Equals(definition.Name, reference.DefinitionName, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Block definition name does not match the reference.", nameof(definition));

        return CreateReferenceCore(
            definition,
            reference.InsertionPoint,
            reference.Scale,
            reference.RotationRadians,
            reference.AttributeValues,
            reference.Id);
    }

    public static IReadOnlyList<ICadEntity> Explode(BlockReferenceEntity reference) =>
        reference.Contents
            .Select(entity => CadEntityTransform.Translate(entity, new CadVector(0, 0), preserveIdentity: false))
            .ToArray();

    private static BlockReferenceEntity CreateReferenceCore(
        CadBlockDefinition definition,
        CadPoint insertionPoint,
        double scale,
        double rotationRadians,
        IReadOnlyDictionary<string, string>? attributeValues,
        Guid? preserveId)
    {
        ArgumentNullException.ThrowIfNull(definition);
        if (!double.IsFinite(scale) || scale <= 1e-9) throw new ArgumentOutOfRangeException(nameof(scale));
        if (!double.IsFinite(rotationRadians)) throw new ArgumentOutOfRangeException(nameof(rotationRadians));

        var toOrigin = new CadVector(-definition.BasePoint.X, -definition.BasePoint.Y);
        var toInsertion = new CadVector(insertionPoint.X, insertionPoint.Y);
        var contents = definition.Entities.Select(entity =>
        {
            var local = CadEntityTransform.Translate(entity, toOrigin, preserveIdentity: false);
            var scaled = CadEntityTransform.Scale(local, Origin, scale);
            var rotated = CadEntityTransform.Rotate(scaled, Origin, rotationRadians);
            return CadEntityTransform.Translate(rotated, toInsertion);
        }).ToArray();

        var values = ResolveAttributes(definition, attributeValues);
        return preserveId is Guid id
            ? new BlockReferenceEntity(definition.Name, insertionPoint, contents, scale, rotationRadians, values, id)
            : new BlockReferenceEntity(definition.Name, insertionPoint, contents, scale, rotationRadians, values);
    }

    private static IReadOnlyDictionary<string, string> ResolveAttributes(
        CadBlockDefinition definition,
        IReadOnlyDictionary<string, string>? requested)
    {
        var validTags = definition.AttributeDefinitions
            .Select(attribute => attribute.Tag)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (requested is not null && requested.Keys.Any(tag => !validTags.Contains(tag)))
            throw new ArgumentException("Attribute values contain a tag not defined by the block.", nameof(requested));

        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var attribute in definition.AttributeDefinitions)
        {
            if (attribute.Constant)
            {
                values[attribute.Tag] = attribute.DefaultValue;
                continue;
            }
            values[attribute.Tag] = requested is not null && requested.TryGetValue(attribute.Tag, out var value)
                ? value ?? string.Empty
                : attribute.DefaultValue;
        }
        return values;
    }
}
""",
)

# Core closure: spatial index, including infinite RAY/XLINE fallback.
write(
    "src/UCAD.Core/Spatial/CadEntitySpatialIndex.cs",
    """using UCAD.Core.Entities;
using UCAD.Core.Geometry;
using UCAD.Core.Interaction;

namespace UCAD.Core.Spatial;

public sealed class CadEntitySpatialIndex
{
    private readonly CadSpatialIndex<ICadEntity> _index;
    private readonly IReadOnlyList<ICadEntity> _unbounded;

    private CadEntitySpatialIndex(CadSpatialIndex<ICadEntity> index, IReadOnlyList<ICadEntity> unbounded)
    {
        _index = index;
        _unbounded = unbounded;
    }

    public int Count => _index.Count + _unbounded.Count;

    public static CadEntitySpatialIndex Build(IEnumerable<ICadEntity> entities)
    {
        ArgumentNullException.ThrowIfNull(entities);
        var entries = new List<CadSpatialIndexEntry<ICadEntity>>();
        var unbounded = new List<ICadEntity>();
        foreach (var entity in entities)
        {
            if (TryBounds(entity, out var bounds)) entries.Add(new CadSpatialIndexEntry<ICadEntity>(entity, bounds));
            else unbounded.Add(entity);
        }
        return new CadEntitySpatialIndex(CadSpatialIndex<ICadEntity>.Build(entries), unbounded.AsReadOnly());
    }

    public IReadOnlyList<ICadEntity> Query(CadRect rectangle)
    {
        if (_unbounded.Count == 0) return _index.Query(rectangle);
        var result = _index.Query(rectangle).ToList();
        foreach (var entity in _unbounded)
            if (IntersectsRectangle(entity, rectangle)) result.Add(entity);
        return result;
    }

    public ICadEntity? FindNearest(CadPoint point, double maximumDistance)
    {
        var best = _index.FindNearest(point, maximumDistance, DistanceTo);
        var bestDistance = best is null ? maximumDistance : DistanceTo(best, point);
        foreach (var entity in _unbounded)
        {
            var distance = DistanceTo(entity, point);
            if (distance <= bestDistance)
            {
                best = entity;
                bestDistance = distance;
            }
        }
        return bestDistance <= maximumDistance ? best : null;
    }

    private static bool TryBounds(ICadEntity entity, out CadRect bounds)
    {
        if (CadAnnotationEntityGeometry.TryGetBounds(entity, out bounds)) return true;
        if (CadExtendedEntityGeometry.TryGetBounds(entity, out bounds)) return true;
        try
        {
            bounds = CadEntityGeometry.GetBounds(entity);
            return true;
        }
        catch (NotSupportedException)
        {
            bounds = default;
            return false;
        }
    }

    private static bool IntersectsRectangle(ICadEntity entity, CadRect rectangle)
    {
        if (CadAnnotationEntityGeometry.Supports(entity)) return CadAnnotationEntityGeometry.IntersectsRectangle(entity, rectangle);
        if (CadExtendedEntityGeometry.Supports(entity)) return CadExtendedEntityGeometry.IntersectsRectangle(entity, rectangle);
        return CadEntityGeometry.IntersectsRectangle(entity, rectangle);
    }

    private static double DistanceTo(ICadEntity entity, CadPoint point)
    {
        if (CadAnnotationEntityGeometry.Supports(entity)) return CadAnnotationEntityGeometry.DistanceTo(entity, point);
        if (CadExtendedEntityGeometry.Supports(entity)) return CadExtendedEntityGeometry.DistanceTo(entity, point);
        return CadEntityGeometry.DistanceTo(entity, point);
    }
}
""",
)

# Canonical geometry delegates to the new adapters for bounded extended/annotation entities.
replace_once(
    "src/UCAD.Core/Interaction/CadEntityGeometry.cs",
    """    public static CadRect GetBounds(ICadEntity entity)
    {
        ArgumentNullException.ThrowIfNull(entity);
        return entity switch
""",
    """    public static CadRect GetBounds(ICadEntity entity)
    {
        ArgumentNullException.ThrowIfNull(entity);
        if (CadAnnotationEntityGeometry.TryGetBounds(entity, out var annotationBounds)) return annotationBounds;
        if (CadExtendedEntityGeometry.TryGetBounds(entity, out var extendedBounds)) return extendedBounds;
        return entity switch
""",
)
replace_once(
    "src/UCAD.Core/Interaction/CadEntityGeometry.cs",
    """    public static double DistanceTo(ICadEntity entity, CadPoint point)
    {
        ArgumentNullException.ThrowIfNull(entity);
        return entity switch
""",
    """    public static double DistanceTo(ICadEntity entity, CadPoint point)
    {
        ArgumentNullException.ThrowIfNull(entity);
        if (CadAnnotationEntityGeometry.Supports(entity)) return CadAnnotationEntityGeometry.DistanceTo(entity, point);
        if (CadExtendedEntityGeometry.Supports(entity)) return CadExtendedEntityGeometry.DistanceTo(entity, point);
        return entity switch
""",
)
replace_once(
    "src/UCAD.Core/Interaction/CadEntityGeometry.cs",
    """    public static bool IsContainedBy(ICadEntity entity, CadRect rectangle, double tolerance = 0) =>
        rectangle.Contains(GetBounds(entity), tolerance);
""",
    """    public static bool IsContainedBy(ICadEntity entity, CadRect rectangle, double tolerance = 0)
    {
        if (CadAnnotationEntityGeometry.Supports(entity)) return CadAnnotationEntityGeometry.IsContainedBy(entity, rectangle, tolerance);
        if (CadExtendedEntityGeometry.Supports(entity)) return CadExtendedEntityGeometry.IsContainedBy(entity, rectangle, tolerance);
        return rectangle.Contains(GetBounds(entity), tolerance);
    }
""",
)
replace_once(
    "src/UCAD.Core/Interaction/CadEntityGeometry.cs",
    """    public static bool IntersectsRectangle(ICadEntity entity, CadRect rectangle, double tolerance = 0)
    {
        ArgumentNullException.ThrowIfNull(entity);
        if (!GetBounds(entity).Intersects(rectangle, tolerance)) return false;
""",
    """    public static bool IntersectsRectangle(ICadEntity entity, CadRect rectangle, double tolerance = 0)
    {
        ArgumentNullException.ThrowIfNull(entity);
        if (CadAnnotationEntityGeometry.Supports(entity)) return CadAnnotationEntityGeometry.IntersectsRectangle(entity, rectangle, tolerance);
        if (CadExtendedEntityGeometry.Supports(entity)) return CadExtendedEntityGeometry.IntersectsRectangle(entity, rectangle, tolerance);
        if (!GetBounds(entity).Intersects(rectangle, tolerance)) return false;
""",
)

# Compiler/analyzer fixes discovered by branch validation.
replace_once(
    "src/UCAD.Core/Plot/CadPdfExporter.cs",
    """    {
        if (textOutlineProvider is not null &&
            textOutlineProvider.TryCreateOutline(value, textStyle, out var outline, out var outlineWarning) &&
""",
    """    {
        string? outlineWarning = null;
        if (textOutlineProvider is not null &&
            textOutlineProvider.TryCreateOutline(value, textStyle, out var outline, out outlineWarning) &&
""",
)
replace_once(
    "src/UCAD.Core/Gis/CadGeoJsonCodec.cs",
    "return PolygonGeometry(hatch.Boundary, hatch.EffectiveIslandLoops);",
    "return PolygonGeometry(hatch.Boundary, hatch.EffectiveIslandLoops.ToArray());",
)
replace_once(
    "src/UCAD.Core/IO/CadNativeDocumentCodecV11.cs",
    "root[ExtensionsProperty] = new JsonObject { [ExtensionName] = extension };",
    "root[ExtensionsProperty] = new JsonObject { [ExtensionName] = JsonSerializer.SerializeToNode(extension, JsonOptions) };",
)
replace_once(
    "src/UCAD.Core/IO/CadNativeDocumentCodecV11.cs",
    "Islands = hatch.Islands.Select(loop => loop.Select(ToDto).ToList()).ToList(),",
    "Islands = hatch.Islands.Select(loop => (List<PointDto>?)loop.Select(ToDto).ToList()).ToList(),",
)
replace_once(
    "src/UCAD.Core/IO/CadNativeDocumentCodecCurrent.cs",
    "Islands = hatch.Islands.Select(loop => loop.Select(ToDto).ToList()).ToList(),",
    "Islands = hatch.Islands.Select(loop => (List<PointDto>?)loop.Select(ToDto).ToList()).ToList(),",
)
replace_once(
    "src/UCAD.Core/Gis/CadDbfCodec.cs",
    """        foreach (var field in fields)
        {
            Span<byte> descriptor = stackalloc byte[32];
            descriptor.Clear();
""",
    """        Span<byte> descriptor = stackalloc byte[32];
        foreach (var field in fields)
        {
            descriptor.Clear();
""",
)

# Version SSOT closure.
write("VERSION", "0.8.0\n")
release_path = ROOT / "release/release.json"
release = json.loads(release_path.read_text(encoding="utf-8"))
release["product"]["version"] = "0.8.0"
release["product"]["packageVersion"] = "0.8.0.0"
release["product"]["releaseTitle"] = "Document & Exchange Foundation"
release_path.write_text(json.dumps(release, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
replace_once("src/UCAD.App/Package.appxmanifest", 'Version="0.7.0.0"', 'Version="0.8.0.0"')
replace_once(
    ".github/scripts/Validate-UcadUi.ps1",
    "if ($version -ne '0.7.0') { throw \"Expected VERSION 0.7.0, got $version\" }",
    "if ($version -ne '0.8.0') { throw \"Expected VERSION 0.8.0, got $version\" }",
)

# Trilingual release notes.
notes_zh = """# UCAD v0.8.0 — 文档与交换基础

UCAD v0.8.0 将 v0.7 的基础二维 CAD 创作闭环扩展为可保存、可交换、可打印的文档工作流，并冻结为本轮验收候选。

## 主要更新
- 原生 `.ucad` 文档读写、最近文件、文件关联启动、自动保存与恢复基础。
- DXF 导入/导出基础及扩展二维实体交换。
- POINT、ELLIPSE、SPLINE、RAY、XLINE 等二维实体。
- STRETCH、ARRAY、FILLET、CHAMFER、JOIN、BREAK 与 Polyline Edit。
- MTEXT、Leader、角度/半径标注、Hatch/Block 管理扩展。
- Layout、Page Setup、多视口、打印预览与矢量 PDF 输出。
- 建筑墙线/房间标注、规划地块指标/表格等专业基础。
- GeoJSON、CSV Point、Shapefile/DBF/PRJ 与 CRS 交换基础。
- 空间索引与大图交互查询基础。

## 验收边界
本版本仍不声称完整 DWG 兼容、3D/BIM、动态块或完整 AutoCAD API 兼容。
"""
notes_ja = """# UCAD v0.8.0 — ドキュメント／交換基盤

UCAD v0.8.0 は v0.7 の 2D CAD 作図ループを、保存・交換・印刷まで含むドキュメントワークフローへ拡張した受け入れ候補です。

## 主な更新
- `.ucad` 保存/読込、最近使ったファイル、関連付け起動、自動保存/復旧基盤。
- DXF 入出力と拡張 2D エンティティ交換。
- POINT / ELLIPSE / SPLINE / RAY / XLINE。
- STRETCH / ARRAY / FILLET / CHAMFER / JOIN / BREAK / Polyline Edit。
- MTEXT、Leader、角度/半径寸法、Hatch/Block 管理。
- Layout、Page Setup、複数 Viewport、印刷プレビュー、ベクター PDF。
- 建築・都市計画向け基礎ヘルパー。
- GeoJSON、CSV Point、Shapefile/DBF/PRJ、CRS 交換。
- 空間インデックス基盤。

## 対象外
完全な DWG 互換、3D/BIM、Dynamic Block、AutoCAD API 完全互換は対象外です。
"""
notes_en = """# UCAD v0.8.0 — Document & Exchange Foundation

UCAD v0.8.0 extends the v0.7 2D authoring loop into a document workflow that can save, exchange and plot drawings, and freezes that work as this acceptance candidate.

## Highlights
- Native `.ucad` open/save, recent files, file activation, autosave and recovery foundations.
- DXF import/export and extended 2D entity exchange.
- POINT, ELLIPSE, SPLINE, RAY and XLINE entities.
- STRETCH, ARRAY, FILLET, CHAMFER, JOIN, BREAK and polyline editing.
- MTEXT, leaders, angular/radial dimensions, hatch and block-management enhancements.
- Layouts, page setup, multiple viewports, plot preview and vector PDF export.
- Foundational architecture and urban-planning helpers.
- GeoJSON, CSV point, Shapefile/DBF/PRJ and CRS exchange foundations.
- Spatial indexing for larger-drawing interaction queries.

## Scope boundary
Full DWG compatibility, 3D/BIM, dynamic blocks and full AutoCAD API compatibility remain outside this release.
"""
write("docs/RELEASE-NOTES-v0.8.0.md", notes_zh)
write("docs/RELEASE-NOTES-v0.8.0.ja.md", notes_ja)
write("docs/RELEASE-NOTES-v0.8.0.en.md", notes_en)
write("RELEASE_NOTES.md", notes_zh)

changelog = read("CHANGELOG.md")
if "## 0.8.0 — 2026-08-18" not in changelog:
    entry = """## 0.8.0 — 2026-08-18

### Document & Exchange Foundation
- Froze the post-v0.7 branch into one acceptance candidate covering native document I/O, DXF, plotting/PDF, extended drawing/modify/annotation, layouts, architecture/planning helpers, GIS exchange, autosave/recovery, and spatial-index foundations.
- Fixed block-reference refresh, extended-entity spatial indexing, PDF outline fallback, GeoJSON hatch islands, native-codec JSON/nullability, and DBF analyzer regressions found by branch validation.

### Validation
- Core tests, WinUI x64 Release build, UI/release contracts, release-certificate signing, one-click package validation and SHA-256 manifest are required before the v0.8.0 candidate is committed.

"""
    changelog = changelog.replace("# Changelog\n\n", "# Changelog\n\n" + entry, 1)
    write("CHANGELOG.md", changelog)

roadmap = read("ROADMAP.md")
roadmap_block = """## v0.8 — Document & Exchange Foundation

- [x] native `.ucad` document open/save foundation
- [x] recent files, file activation, autosave and recovery foundation
- [x] DXF-first import/export foundation
- [x] extended 2D entities and Modify/Annotation completion foundation
- [x] layouts, page setup, multiple viewports and plot preview
- [x] vector PDF export foundation
- [x] architecture and planning helper foundation
- [x] GeoJSON / CSV Point / Shapefile / DBF / PRJ / CRS exchange foundation
- [x] spatial index and larger-drawing query foundation
- [x] release-signed acceptance package gate

## v0.9–v1.0

- advanced interoperability and round-trip fidelity
- performance profiling and large-drawing regression suite
- professional architecture/planning workflow refinement
- UI/interaction polish against the Figma visual SSOT

"""
roadmap, count = re.subn(
    r"## v0\.8–v1\.0\n.*?(?=## Explicitly out of scope for 1\.x)",
    roadmap_block,
    roadmap,
    count=1,
    flags=re.S,
)
if count != 1:
    raise RuntimeError(f"Expected one v0.8–v1.0 roadmap block, found {count}")
write("ROADMAP.md", roadmap)

print("v0.8.0 finalization patches applied")

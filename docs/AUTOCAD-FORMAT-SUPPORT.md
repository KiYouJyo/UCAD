# AutoCAD Format Support Matrix

UCAD distinguishes **container transport**, **resource parsing**, and **semantic round-trip fidelity**. A file extension is not advertised as open/import/export capable until a real adapter exists, and successful container parsing does not imply that every AutoCAD object type is editable by UCAD.

## Drawing containers

| Format | Open / Import | Export | Current transport | Current fidelity boundary |
| --- | --- | --- | --- | --- |
| `.dwg` | Yes | Yes | ACadSharp DWG ↔ shared DXF bridge | UCAD-supported DXF entities/tables |
| `.dxf` | Yes | Yes | UCAD ASCII DXF + ACadSharp binary/legacy normalization | UCAD-supported DXF entities/tables |
| `.dxb` | Yes | Yes | IxMilia DXB 1.0 geometry codec | Legacy 2D geometry only; unsupported/3D/property downgrades are explicit warnings |
| `.dwt` | Yes | Yes | DWG-compatible template container | Template-specific metadata may be reduced |
| `.dws` | Yes | No | DWG-compatible standards container | Geometry/tables only; standards rules are not authored |
| `.bak` | Yes | No | DWG-compatible recovery source | Recovery import only |
| `.sv$` | Yes | No | DWG-compatible recovery source | Recovery import only |

Imported AutoCAD files are intentionally opened without a native UCAD save path. Normal **Save** therefore asks for a `.ucad` destination instead of silently overwriting a source file that may contain unsupported objects.

DXB is intentionally treated differently from DWG/DXF. UCAD supports the legacy DXB 1.0 geometry stream for line, point, circle, arc, polyline and planar boundary data. Bulged polylines are expanded to exact LINE/ARC geometry, non-zero Z values are flattened only with warnings, and modern annotation/property data that DXB cannot carry is reported instead of silently dropped.

## Text support resources

| Format | Parse / Import | Serialize / Export | Safety / fidelity behavior |
| --- | --- | --- | --- |
| `.pat` | Yes | Yes | Parses hatch angle, origin, offset and dash sequences |
| `.lin` | Yes | Yes | Preserves complex linetype definition text rather than partially reinterpreting it |
| `.pgp` | Yes | Yes | Imports only `ALIAS, *COMMAND` records; external-process definitions are ignored |

These codecs are Core resource adapters. Integration into the hatch palette, linetype manager and command-alias settings will be handled separately from file parsing.

## AutoCAD ecosystem inventory

The capability registry also identifies the surrounding AutoCAD file ecosystem so UCAD can report a truthful migration status instead of treating unknown extensions as generic files.

| Category | Registered formats | Current policy |
| --- | --- | --- |
| Sheet sets / publish lists | `.dst`, `.dsd`, `.bp3` | Recognized; manager/publish adapters pending |
| Published / plotting | `.dwf`, `.dwfx`, `.ctb`, `.stb`, `.pc3`, `.pmp`, `.psf`, `.pss` | Recognized; no false open/export claim |
| Support / customization | `.shx`, `.fmp`, `.dcl`, `.unt`, `.cfg`, `.cuix`, `.cui`, `.mnu`, `.mns`, `.mnc`, `.atc`, `.arg`, `.sld`, `.slb` | Recognized; migration adapters staged separately |
| Source automation | `.scr`, `.lsp`, `.mnl`, `.fas`, `.vlx`, `.dvb`, `.js`, `.rx` | Recognized; execution is not enabled by recognition alone |
| Binary plug-ins | `.arx`, `.crx`, `.dbx`, `.hdi`, `.dll` | Inventory only; UCAD does not claim the AutoCAD binary ABI |
| AutoCAD exchange formats | `.pdf`, `.dgn`, `.sat`, `.igs`, `.iges`, `.stl`, `.wmf`, `.bmp`, `.dxx` | Registered for routing; 3D formats stay outside UCAD 1.x 2D authoring scope unless a bounded exchange use case is added |

## DWG/DXF semantic fidelity

The shared DXF bridge currently preserves the foundational 2D geometry plus `TEXT`, `MTEXT`, and closed-polyline `HATCH` data. MTEXT content/style/width/rotation and hatch outer/island loops, pattern name, scale, angle and island detection are round-tripped. Associative hatch handles are deliberately downgraded with a warning until AutoCAD handles can be mapped to stable UCAD identities.

Still pending for stronger DWG/DXF round-trip fidelity:

1. Dimensions, leaders and annotation styles.
2. Blocks, attributes and dynamic/custom block metadata.
3. Edge-based/bulged hatch boundaries and full pattern/style tables.
4. Layouts, viewports, page setup and advanced object tables.
5. Explicit proxy preservation for unsupported/custom DWG objects.
6. Multi-version fixtures and large-drawing regression.

## DWG/DXF version transport

The current ACadSharp bridge targets the AC1032 generation for UCAD exports. Reader/writer version coverage is constrained by the upstream transport library and UCAD's own semantic mapping. Version-specific regression fixtures will be added before v0.9 acceptance.

## Validation gate

The v0.9 foundation is not considered accepted solely because an extension appears in this table. Core round-trip tests, the Release WinUI build, startup smoke, package validation, and later fixture-based AutoCAD interoperability checks must pass before a capability is promoted beyond preview status.

`CadAcadFileFormatRegistry` is the source of truth for capability routing. A recognized format can remain intentionally non-openable/non-exportable when safe or faithful compatibility does not yet exist.

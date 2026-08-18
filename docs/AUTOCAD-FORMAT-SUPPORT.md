# AutoCAD Format Support Matrix

UCAD distinguishes **container transport**, **resource parsing**, and **semantic round-trip fidelity**. A file extension is not advertised as open/import/export capable until a real adapter exists, and successful container parsing does not imply that every AutoCAD object type is editable by UCAD.

## Drawing containers

| Format | Open / Import | Export | Current transport | Current fidelity boundary |
| --- | --- | --- | --- | --- |
| `.dwg` | Yes | Yes | ACadSharp DWG + UCAD semantic repair | High-value 2D entities, annotation, blocks and paper layouts; unsupported/custom objects are reported rather than claimed as preserved |
| `.dxf` | Yes | Yes | IxMilia text/binary DXF + UCAD advanced DXF bridge | Entity semantics are shared with DWG; paper-layout import is best-effort and DXF layout export is not yet claimed |
| `.dxb` | Yes | Yes | IxMilia DXB 1.0 geometry codec | Legacy 2D geometry only; unsupported/3D/property downgrades are explicit warnings |
| `.dwt` | Yes | Yes | DWG-compatible template container | Uses the DWG semantic/layout transport; template-only/custom metadata may still be reduced |
| `.dws` | Yes | No | DWG-compatible standards container | Geometry/tables can be imported; standards rules are not authored |
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

These codecs are Core resource adapters. Integration into the hatch palette, linetype manager and command-alias settings is separate from file parsing.

## AutoCAD ecosystem inventory

The capability registry also identifies the surrounding AutoCAD file ecosystem so UCAD can report a truthful migration status instead of treating unknown extensions as generic files.

| Category | Registered formats | Current policy |
| --- | --- | --- |
| Sheet sets / publish lists | `.dst`, `.dsd`, `.bp3` | Recognized; manager/publish adapters pending |
| Published / plotting | `.dwf`, `.dwfx`, `.ctb`, `.stb`, `.pc3`, `.pmp`, `.psf`, `.pss` | Recognized; no false open/export claim |
| Support / customization | `.shx`, `.fmp`, `.dcl`, `.unt`, `.cfg`, `.cuix`, `.cui`, `.mnu`, `.mns`, `.mnc`, `.atc`, `.arg`, `.sld`, `.slb` | Recognized; migration adapters staged separately |
| Source automation | `.scr`, `.lsp`, `.mnl`, `.fas`, `.vlx`, `.dvb`, `.js`, `.rx` | Recognized; execution is not enabled by recognition alone |
| Binary plug-ins | `.arx`, `.crx`, `.dbx`, `.hdi`, `.dll` | Inventory only; UCAD does not implement the AutoCAD binary ABI |
| AutoCAD exchange formats | `.pdf`, `.dgn`, `.sat`, `.igs`, `.iges`, `.stl`, `.wmf`, `.bmp`, `.dxx` | Registered for routing; 3D formats stay outside UCAD 1.x 2D authoring scope unless a bounded exchange use case is added |

## DWG/DXF semantic fidelity

The shared semantic bridge preserves UCAD's foundational 2D geometry plus the following higher-value AutoCAD authoring data:

- `TEXT` and `MTEXT`, including MTEXT content, width, height, rotation and style.
- Closed-polyline `HATCH` with outer/island loops, pattern name, scale, angle and island detection. Associative source handles are downgraded with an explicit warning until AutoCAD handles can be mapped to stable UCAD identities.
- `DIMSTYLE` plus aligned linear and three-point angular dimensions. Two-line angular dimensions can be imported when a valid 2D intersection exists. AutoCAD dimension picture blocks are generated before DWG write so semantic dimensions survive DWG round-trip.
- `LEADER` with linked MTEXT annotation. DWG import repairs the relationship from the native object graph when the upstream DWG-to-DXF serializer drops the annotation pointer.
- `BLOCK` / `INSERT` / `ATTDEF` / `ATTRIB`, including block base point, positive uniform scale, rotation and attribute values. Mirrored/non-uniform INSERT transforms are rejected with a warning instead of silently distorting geometry.
- DWG/DWT paper layouts, page setup and rectangular paper-space viewports: paper size/orientation, printable margins, plot area, plot scale, basic CTB style classification, viewport paper rectangle, model target, scale, twist and lock state.
- DXF paper layout/viewports are imported through a non-authoritative sidecar parser. This path is intentionally **best-effort** because producer-specific PlotSettings can normalize or omit fields such as unprintable margins. Use DWG/DWT when page-setup fidelity is required.

### Explicit fidelity boundaries

Still pending for stronger AutoCAD compatibility:

1. Radius/diameter/ordinate dimensions, richer leader/MLEADER and annotation-style semantics.
2. Dynamic/custom block metadata and mirrored/non-uniform block references.
3. Edge-based/bulged hatch boundaries and full hatch/style tables.
4. Non-rectangular viewport clipping, advanced page setup dictionaries and **DXF layout export**.
5. Explicit opaque proxy/raw-payload preservation for unsupported/custom ObjectARX objects.
6. Multi-version real-world DWG/DXF fixture corpus and large-drawing regression.
7. DWF/DWFx, plot-style/configuration, sheet-set and customization adapters listed above.

## Container architecture

UCAD does not force every AutoCAD format through one third-party object model:

- **DWG/DWT/DWS/recovery containers:** ACadSharp provides DWG transport. UCAD supplements it with native-object semantic repair for known serializer gaps.
- **Text/binary DXF:** IxMilia.Dxf provides container normalization so DIMENSION/BLOCK group codes are not lost by an unnecessary object-model conversion; UCAD's advanced DXF codec remains authoritative for entities.
- **DXF layout sidecar:** ACadSharp is used only to inspect layout/PlotSettings/viewport OBJECTS metadata after the primary DXF entity import has already succeeded. Sidecar failure is non-fatal.
- **DXB:** IxMilia.Dxf handles the bounded DXB 1.0 geometry stream directly.

This split keeps interoperability truthful and minimizes cross-format regressions.

## DWG/DXF version transport

UCAD exports the current DWG/DXF generation through the AC1032-era bridge. Reader/writer version coverage is constrained by the upstream transport libraries and UCAD's own semantic mapping. Version-specific real-world regression fixtures remain an acceptance item before claiming broad historical-version fidelity.

## Validation gate

A capability is not considered accepted solely because an extension appears in this table. Core round-trip tests, the Release WinUI build, startup smoke, package validation/signing, and runtime Authoring/Interaction/Modify/Localization smoke tests must pass before handoff.

`CadAcadFileFormatRegistry` is the source of truth for capability routing. A recognized format can remain intentionally non-openable/non-exportable when safe or faithful compatibility does not exist.

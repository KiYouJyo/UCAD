# AutoCAD Format Support Matrix

UCAD distinguishes **container transport**, **resource parsing**, **editable semantic round-trip fidelity**, and **opaque source recovery**. A file extension is not advertised as open/import/export capable until a real adapter exists, and successful container parsing does not imply that every AutoCAD object type is editable by UCAD.

## Drawing containers

| Format | Open / Import | Export | Current transport | Current fidelity boundary |
| --- | --- | --- | --- | --- |
| `.dwg` | Yes | Yes | ACadSharp DWG + UCAD semantic repair + opaque source envelope | High-value 2D entities, annotation, blocks and paper layouts are editable; the exact original container is retained for source-only/custom data recovery |
| `.dxf` | Yes | Yes | IxMilia text/binary DXF + UCAD advanced DXF bridge | High-value entity/annotation/block semantics; paper-layout metadata is not claimed in the current DXF path |
| `.dxb` | Yes | Yes | IxMilia DXB 1.0 geometry codec | Legacy 2D geometry only; unsupported/3D/property downgrades are explicit warnings |
| `.dwt` | Yes | Yes | DWG-compatible template container + opaque source envelope | Uses the DWG semantic/layout transport; exact untouched source can be passed through, while edited template-only/custom metadata may still be reduced |
| `.dws` | Yes | No | DWG-compatible standards container + opaque source envelope | Geometry/tables can be imported; standards rules are not authored, but the original source remains recoverable |
| `.bak` | Yes | No | DWG-compatible recovery source + opaque source envelope | Recovery import only; original recovery container is retained |
| `.sv$` | Yes | No | DWG-compatible recovery source + opaque source envelope | Recovery import only; original recovery container is retained |

Imported AutoCAD files are intentionally opened without a native UCAD save path. Normal **Save** therefore asks for a `.ucad` destination instead of silently overwriting a source file that may contain unsupported objects. Native `.ucad` persistence stores both the editable UCAD model and, for DWG-compatible imports, the original AutoCAD container in a namespaced recovery extension.

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

## Opaque ObjectARX / proxy source preservation

ACadSharp's DWG writer does not implement every custom/proxy entity family, so UCAD does **not** claim that unknown ObjectARX objects become editable native UCAD objects. Instead, the application uses a source-envelope boundary:

1. Every DWG-compatible import retains an immutable copy of the exact original container alongside the editable UCAD semantic model.
2. If the document has not changed and is exported back to the same DWG/DWT format, UCAD reuses the original bytes directly rather than rewriting them through a lossy object model.
3. If the user edits the drawing, UCAD writes the supported semantic model and emits an explicit warning that source-only ObjectARX/proxy/custom data cannot yet be merged into the rebuilt DWG/DWT.
4. The untouched original remains recoverable after edits through the source envelope and is persisted inside native `.ucad` files with a SHA-256 integrity check.

This is **opaque source preservation**, not editable proxy-object support. Reinjection/merge of unknown objects into an edited DWG remains pending.

## Multi-version real-world regression corpus

The dedicated `AutoCAD Interoperability` workflow downloads a pinned, MIT-licensed ACadSharp fixture corpus from commit `d7dc111023477d8a9fffc2153139459c95b4f345`. Files are not trusted by URL alone: the fixture manifest locks both byte length and Git blob SHA-1, and CI verifies them before import.

Current pinned coverage:

- DWG: AC1014 (R14), AC1015 (2000/2002), AC1018 (2004-2006), AC1021 (2007-2009), AC1024 (2010-2012), AC1027 (2013-2017), AC1032 (2018+).
- DXF: AC1009 R12 ASCII, AC1015 binary, AC1032 binary.

The corpus is intentionally a compatibility gate, not a claim that every historical AutoCAD object in every producer-specific drawing is semantically editable.

## Large-drawing regression

A deterministic planning-drawing stress fixture generates **12,000 semantic entities** in one document: 4,000 lines, 3,500 parcel polylines, 2,000 text labels, 1,000 circles, 400 dimensions, 250 leaders, 600 attributed block references and 250 hatches, plus layers, dimension style and A1 paper layout/viewport metadata. The regression performs a full UCAD → DWG → UCAD round-trip and verifies that the major semantic populations, attributed block definition, dimension style and paper layout survive without collapse.

The test intentionally uses structural thresholds rather than wall-clock timing so CI remains stable across runner hardware. Larger performance-only benchmarks can be added separately without weakening the semantic gate.

### Explicit fidelity boundaries

Still pending for stronger AutoCAD compatibility:

1. Radius/diameter/ordinate dimensions, richer leader/MLEADER and annotation-style semantics.
2. Dynamic/custom block metadata and mirrored/non-uniform block references.
3. Edge-based/bulged hatch boundaries and full hatch/style tables.
4. DXF paper-layout/PageSetup/viewport import-export, non-rectangular viewport clipping and advanced page setup dictionaries.
5. Reinjection/merge of opaque ObjectARX/proxy/custom payloads into an **edited** DWG/DWT; the exact original source is already retained and recoverable.
6. A broader independently sourced customer/production fixture corpus beyond the pinned upstream regression set, plus optional 100k+ entity performance benchmarks.
7. DWF/DWFx, plot-style/configuration, sheet-set and customization adapters listed above.

## Container architecture

UCAD does not force every AutoCAD format through one third-party object model:

- **DWG/DWT/DWS/recovery containers:** ACadSharp provides DWG transport. UCAD supplements it with native-object semantic repair for known serializer gaps, a native paper-layout adapter, and an opaque original-source envelope for data outside the editable model.
- **Text/binary DXF:** IxMilia.Dxf provides container normalization so DIMENSION/BLOCK group codes are not lost by an unnecessary object-model conversion; UCAD's advanced DXF codec remains authoritative for entities. An ACadSharp layout-sidecar experiment was explicitly rejected because it did not reliably reconstruct DXF paper-layout OBJECTS.
- **DXB:** IxMilia.Dxf handles the bounded DXB 1.0 geometry stream directly.

This split keeps interoperability truthful and minimizes cross-format regressions.

## DWG/DXF version transport

UCAD semantic DWG export currently targets the AC1032-era bridge. Untouched DWG/DWT imports can be exported byte-for-byte from their original source envelope, preserving the original container generation. Reader coverage is continuously exercised by the pinned AC1014-through-AC1032 fixture matrix described above.

## Acceptance package versioning

The v0.9 interoperability acceptance build uses application version **0.9.0** and MSIX package version **0.9.0.0** while retaining the existing UCAD package identity and publisher. This makes the signed acceptance package a normal in-place upgrade from the v0.8.1 acceptance build rather than a same-version replacement.

## Validation gate

A capability is not considered accepted solely because an extension appears in this table. Core round-trip tests, the dedicated pinned-fixture/stress interoperability workflow, the Release WinUI build, startup smoke, package validation/signing, and runtime Authoring/Interaction/Modify/Localization smoke tests must pass before handoff.

`CadAcadFileFormatRegistry` is the source of truth for capability routing. A recognized format can remain intentionally non-openable/non-exportable when safe or faithful compatibility does not exist.

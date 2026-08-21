# AutoCAD Format Support Matrix

UCAD distinguishes four different promises: **drawing Open**, **bounded migration Import/Export**, **editable semantic fidelity**, and **exact opaque source recovery**. A capability flag therefore means only what its concrete adapter implements; recognition never implies executable AutoCAD runtime compatibility.

## Drawing containers

| Format | Open / Import | Export | Current transport | Current fidelity boundary |
| --- | --- | --- | --- | --- |
| `.dwg` | Yes | Yes | ACadSharp DWG + UCAD semantic repair + source envelope | High-value 2D geometry/annotation/blocks and DWG/DWT paper layouts; exact untouched source retained |
| `.dxf` | Yes | Yes | IxMilia text/binary DXF + UCAD full semantic bridge + source envelope | Geometry, TEXT/MTEXT, DIMSTYLE, aligned/angular/radius/diameter dimensions, hatch, blocks/attributes; original OBJECTS/XDATA/custom payloads retained exactly |
| `.dxb` | Yes | Yes | IxMilia DXB 1.0 geometry codec | Legacy 2D geometry only; 3D/property downgrades are explicit |
| `.dwt` | Yes | Yes | DWG-compatible template container | Same semantic/layout path as DWG plus exact untouched source |
| `.dws` | Yes | No | DWG-compatible standards container | Geometry/tables import; standards-rule authoring not claimed |
| `.bak` | Yes | No | DWG-compatible recovery source | Recovery import only |
| `.sv$` | Yes | No | DWG-compatible recovery source | Recovery import only |

Imported AutoCAD drawings intentionally do not become a native save path. Normal **Save** requests `.ucad`; explicit AutoCAD export is a separate action. The native `.ucad` extension stores the editable model plus the original AutoCAD source envelope and verifies its SHA-256 on load.

### Exact source preservation

DWG-compatible **and DXF** imports retain the exact original bytes. If the document is untouched and exported to the same format, UCAD can reuse those bytes rather than rewriting unknown dictionaries/proxy/custom/XDATA payloads. After an edit, UCAD emits its supported semantic model and explicitly warns that arbitrary source-only payloads are not blindly reinjected; the untouched original remains recoverable.

This is a deliberate integrity boundary. Blindly splicing unknown handle graphs into an edited DWG/DXF would risk producing corrupt files, so edited-container proxy reinjection remains a v1.0 research item rather than a false v0.9 claim.

## DWG/DXF semantic fidelity

Verified high-value semantics include:

- foundational 2D geometry plus TEXT and MTEXT;
- closed-polyline HATCH with pattern/scale/angle and island loops;
- DIMSTYLE;
- aligned linear, 3-point angular, valid 2-line angular import, **radius and diameter dimensions**;
- LEADER + linked MTEXT repair on DWG import;
- BLOCK / INSERT / ATTDEF / ATTRIB with base point, positive uniform scale, rotation and attributes;
- DWG/DWT paper size/orientation, margins, plot area/scale and rectangular paper-space viewports.

Still bounded: ordinate dimensions, richer MLEADER/annotation-style semantics, dynamic blocks, mirrored/non-uniform block references, edge/spline hatch loops, standard DXF paper-layout export and non-rectangular viewport clipping.

## Published / plotting migration

| Format | Import | Export | v0.9.2 behavior |
| --- | --- | --- | --- |
| `.dwf` | Yes | Yes | Exact classic DWF package preservation/re-export; no false editable WHIP claim |
| `.dwfx` | Yes | Yes | XPS FixedPage M/L/Z vector subset becomes editable/publishable; text/raster/VisualBrush gaps are warned |
| `.ctb` / `.stb` | Yes | Yes | Exact plot-style bytes + SHA-256 integrity; semantic table editing not claimed |
| `.pc3` / `.pmp` | Yes | Yes | Exact plot configuration preservation; no device-driver loading |
| `.psf` / `.pss` | Yes | Yes | Exact support-resource migration |
| `.dst` | Yes | Yes | Exact sheet-set package preservation; proprietary database semantics not rewritten |
| `.dsd` / `.bp3` | Yes | Yes | Text publication-list metadata inventory/preservation |

## Resource / customization migration

| Format | Import | Export | Safety / fidelity behavior |
| --- | --- | --- | --- |
| `.pat` | Yes | Yes | Hatch pattern geometry parse/serialize |
| `.lin` | Yes | Yes | Linetype definitions; complex text preserved |
| `.pgp` | Yes | Yes | Safe `ALIAS, *COMMAND` migration; external-process records ignored |
| `.shx` | Yes | Yes | Exact bytes + resource inventory + safe UI-font fallback; SHX bytecode not executed |
| `.cuix` | Yes | Yes | ZIP/XML package inventory and exact re-export; embedded code/macros not auto-executed |
| `.arg` | Yes | Yes | Profile section/settings migration |
| `.fmp`, `.dcl`, `.unt`, `.cfg`, `.cui`, `.mnu`, `.mns`, `.atc` | Yes | Yes | Bounded text/customization metadata migration |
| `.mnc`, `.sld`, `.slb` | Yes | Yes | Exact non-executing binary preservation |

The File menu exposes these through **Migrate AutoCAD resource/customization…** rather than pretending a plot profile or menu file is a drawing.

## Automation compatibility

| Format | v0.9.2 behavior |
| --- | --- |
| `.scr` | Parses statements and dispatches only registered bounded UCAD command paths; unsupported/incomplete interactive statements are skipped and reported |
| `.lsp` / `.mnl` | Source analyzer extracts DEFUN and command-invocation inventory; Lisp expressions are never evaluated |
| `.fas` / `.vlx` | Exact migration/re-export only; no compatible Lisp runtime means no execution |
| `.dvb` | Exact migration/re-export only; UCAD has no embedded VBA runtime |
| `.rx` | Application-list metadata migration; referenced binaries are not auto-loaded |
| `.js` | Recognition only; no arbitrary AutoCAD JavaScript host |
| `.arx`, `.crx`, `.dbx`, `.hdi`, `.dll` | Inventory recognition only; UCAD does not implement AutoCAD's binary ABI/runtime |

## Physical Delete / ERASE reliability

v0.9.2 adds a root `KeyboardAccelerator` for the physical Delete key in addition to routed KeyDown handling. It feeds the same `ERASE` transaction used by the visible ERASE button and `ERASE` / `E` / `DELETE` commands. Delete remains untouched when a text-editing control owns focus. Runtime Modify smoke now requires the accelerator to exist and actually delete/undo selected geometry through the production helper.

## Multi-version regression

The dedicated AutoCAD workflow cryptographically verifies a pinned MIT-licensed fixture corpus and imports:

- DWG AC1014, AC1015, AC1018, AC1021, AC1024, AC1027 and AC1032;
- DXF R12 ASCII, AC1015 binary and AC1032 binary.

A separate deterministic planning fixture performs a **12,000-entity UCAD → DWG → UCAD** regression covering geometry, annotation, blocks/attributes, hatches, dimensions and paper-layout metadata.

## Licensing

UCAD remains GPL-2.0-only. ACadSharp and IxMilia.Dxf are MIT-licensed. GPLv3-only LibreDWG is intentionally not introduced. Classic DWF is therefore kept as an exact published-package migration boundary rather than pulling in an unreviewed Autodesk-specific runtime/toolkit merely to claim semantic extraction.

## Explicit remaining boundaries

The following are not advertised as completed adapters:

1. standards-based DXF paper-layout/PageSetup/viewport **export** and richer advanced OBJECTS editing;
2. reinjection of arbitrary proxy/ObjectARX/custom handle graphs into an **edited** DWG/DXF;
3. ordinate dimensions and richer MLEADER/annotation styles;
4. dynamic/custom block metadata and mirrored/non-uniform block references;
5. edge/spline/bulged hatch boundaries;
6. AutoCAD JavaScript, VBA/Lisp compiled execution, or ARX/CRX/DBX binary ABI compatibility;
7. DGN/WMF/BMP/DXX bounded exchange adapters; SAT/IGES/STL 3D authoring remains outside UCAD 1.x scope.

## Validation gate

A format is accepted only after its Core tests and relevant real-process WinUI smoke pass together with Release build, startup launch, package signing/validation and the dedicated AutoCAD fixture/stress workflow. `CadAcadFileFormatRegistry` remains the routing source of truth.

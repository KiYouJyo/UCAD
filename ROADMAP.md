# UCAD Roadmap

## Product target

UCAD aims to become a lightweight Windows-native 2D CAD focused on architecture and urban planning, with familiar command-driven interaction and a deliberately bounded feature set.

## P0 / v0.1 — Foundation Preview

- [x] Separate core and UI projects
- [x] WinUI 3 shell
- [x] Win2D viewport
- [x] world ↔ screen transforms
- [x] adaptive grid
- [x] crosshair
- [x] zoom and pan
- [x] line entity model
- [x] interactive two-point line creation
- [x] core unit tests
- [x] CI and GitHub release workflows

## v0.2 — Command Foundation

- [x] command line UI
- [x] command registry and aliases
- [x] Enter / Space confirmation
- [x] Esc cancellation
- [x] repeat previous command
- [x] numeric and coordinate input
- [x] relative coordinates (`@x,y`)

## v0.3 — Drawing

- [x] LINE
- [x] PLINE
- [x] RECTANGLE
- [x] CIRCLE
- [x] ARC
- [x] Undo / Redo

## v0.3.5 — Workspace Shell Foundation

- [x] Fluent CAD workspace shell based on the approved Figma layout
- [x] browser-style in-memory document tabs
- [x] independent `CadDocument` / `CadViewport` / `CommandSession` per tab
- [x] persistent category tool shelf and compact high-frequency tool rail
- [x] command search backed by `CommandRegistry`
- [x] document change/revision notifications from Core
- [x] command category and drawing-kind metadata from Core
- [x] inspector boundary wired to real document/history state
- [x] future command surfaces visible but disabled until their Core capability exists
- [x] zh-CN / ja-JP / en-US shell localization

**Transition gate:** v0.3.5 freezes the primary information architecture. v0.4 connects interaction capabilities to these existing shell surfaces rather than redesigning the workspace.

## v0.4 — Interaction

- [x] click selection
- [x] window selection
- [x] crossing selection
- [x] multi-selection
- [x] delete / ERASE as one undoable multi-entity mutation
- [x] endpoint / midpoint / center / intersection OSNAP
- [x] ortho mode
- [x] selection-backed inspector properties
- [x] enable the corresponding existing status/tool surfaces from Core capability state

## v0.4.1 — CAD Selection & Cursor Refinement

- [x] AutoCAD-style two-point selection window: click first corner, move, click opposite corner
- [x] retain press-drag-release selection as an alternate gesture
- [x] Window / Crossing direction semantics shared by both gesture styles
- [x] PICKADD-style additive object/window selection
- [x] Shift-click and Shift-window removal from the current selection set
- [x] Esc cancels an unfinished selection gesture before clearing completed selection
- [x] empty completed window clears the current selection set
- [x] CAD crosshair with central pickbox on the drawing surface
- [x] live-adjustable crosshair size, pickbox size, and OSNAP aperture

**Interaction gate:** v0.4.x freezes document-scoped selection and drafting-aid ownership. Pixel-level UI tuning is intentionally not part of this milestone. Modify commands should reuse `SelectionSet`, `CadEntityGeometry`, `CadInteractionState`, and the existing `CommandRegistry → CommandSession` path instead of creating parallel selection/edit models.

## v0.5 — Modify

- [x] MOVE
- [x] COPY
- [x] ROTATE
- [x] SCALE
- [x] MIRROR
- [x] OFFSET
- [x] TRIM
- [x] EXTEND
- [x] shared identity-preserving transform pipeline
- [x] undoable `Replace` / `ReplaceRange` edit transactions
- [x] command-first and preselection workflows
- [x] OSNAP-aware Modify point input and transient previews
- [x] dedicated real-process Modify Smoke coverage

## v0.6 — Layers & Entity Properties

- [x] document layer table with protected layer `0`
- [x] current-layer inheritance for newly created entities
- [x] create / rename / delete / set-current layer workflow
- [x] layer visibility and lock state
- [x] layer color, lineweight, and linetype metadata
- [x] per-entity layer / color / lineweight / linetype overrides with ByLayer inheritance
- [x] layer-aware drawing visibility, selection, OSNAP, and Modify picking
- [x] Layer Manager surfaced from the existing Layers category / Inspector
- [x] `LAYER` / `LA` and `CHPROP` / `CH` command registration
- [x] layer/property state participates in the document Undo/Redo snapshot

## v0.7 — Annotation, Hatch & Blocks

- [x] `TEXT` / `T` single-line text entity and insertion workflow
- [x] `DIM` / `DLI` / `DIMLINEAR` foundational aligned linear dimensions
- [x] `HATCH` / `H` solid hatch for a selected closed polyline or circle
- [x] text / dimension / hatch rendering, selection geometry, grips, and shared transforms
- [x] document block-definition table
- [x] `BLOCK` / `B` definition workflow from selected entities
- [x] `INSERT` / `I` block reference with insertion point, scale, and rotation
- [x] `EXPLODE` / `X` block-reference explosion as one undoable replace mutation
- [x] shared geometry queries and Modify transforms for the new v0.7 entity types
- [x] restart-free zh-CN / ja-JP / en-US authoring prompts and dialogs
- [x] dedicated real-process Authoring Smoke for v0.6 + v0.7 capabilities

**Combined acceptance gate:** the v0.7.0 candidate intentionally carries v0.5, v0.6, and v0.7 together so Modify, Layers/Properties, annotation, hatch, and blocks can be accepted as one coherent CAD authoring loop before release.

## v0.8 — Document & Exchange Foundation

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

## v0.9 — AutoCAD Format Interoperability

### Drawing containers

- [x] central AutoCAD format capability registry with truthful Open / Import / Export flags
- [x] MIT-licensed ACadSharp transport dependency compatible with UCAD's GPL-2.0-only licensing
- [x] `.dwg` read/write transport through the shared UCAD DXF semantic bridge
- [x] `.dxf` ASCII foundation retained and binary/legacy DXF normalization added
- [x] `.dwt` drawing-template read/write transport
- [x] `.dws` standards-file container import without claiming standards-rule authoring
- [x] `.bak` and `.sv$` recovery-source import
- [x] WinUI file picker, file activation and AutoCAD export UI for implemented drawing containers
- [x] DWG / DWT / binary-DXF round-trip regression tests
- [x] DXF/DWG semantic fidelity for TEXT/MTEXT, DIMSTYLE, aligned/angular/radius/diameter dimensions, closed-polyline hatch islands/patterns, blocks/attributes and DWG/DWT paper layouts
- [x] preserve exact original DWG/DWT/DWS/BAK/SV$/DXF source containers for proxy/custom/XDATA/unknown payload recovery and byte-for-byte untouched re-export
- [x] multi-version DWG/DXF fixture corpus plus deterministic 12,000-entity regression suite
- [x] v0.9.1 filter non-graphical custom-dictionary warning noise without suppressing visible/semantic failures
- [x] v0.9.2 focus-independent physical Delete `KeyboardAccelerator`, sharing the same ERASE transaction while preserving Delete inside text editors

### Published / plotting formats

- [x] `.dwf` exact package migration/re-export adapter; classic DWF remains a published opaque package rather than falsely claiming editable WHIP semantics
- [x] `.dwfx` XPS FixedPage vector import/export for the verified M/L/Z vector subset with explicit text/raster metadata warnings
- [x] `.ctb` / `.stb` lossless plot-style migration/re-export with SHA-256 integrity; semantic plot-table editing is not falsely claimed
- [x] `.pc3` / `.pmp` lossless plot-configuration migration/re-export without loading device drivers
- [x] `.psf` / `.pss` support-resource migration/re-export
- [x] `.dst` exact sheet-set package preservation plus `.dsd` / `.bp3` publication-list text migration

### Resource / customization formats

- [x] `.pat` hatch-pattern import/export
- [x] `.lin` linetype-definition import/export
- [x] `.pgp` safe command-alias migration while ignoring external-process definitions
- [x] `.shx` exact resource preservation/inventory with safe UI-font fallback; SHX bytecode is not executed
- [x] `.cuix` ZIP/XML customization inventory plus exact re-export; embedded binaries/macros are not auto-executed
- [x] `.arg` profile/settings migration tooling
- [x] `.fmp`, `.dcl`, `.unt`, `.cfg`, `.cui`, `.mnu`, `.mns`, `.atc` bounded text/customization migration
- [x] `.mnc`, `.sld`, `.slb` exact non-executing binary migration
- [x] `.dxb` legacy interchange adapter
- [x] File-menu migration route that exposes implemented resource/customization adapters without pretending they are drawing Open operations

### Automation compatibility

- [x] `.scr` parser plus safe registered-command dispatch for bounded noninteractive statements; unsupported/interactively incomplete statements are reported instead of guessed
- [x] `.lsp` / `.mnl` source-level compatibility analyzer for DEFUN and command-invocation inventory; no Lisp evaluation
- [x] `.fas` / `.vlx` exact migration inventory/re-export while remaining deliberately non-executable without a compatible AutoLISP/Visual LISP runtime
- [x] `.dvb` exact migration inventory/re-export while remaining non-executable without an embedded VBA runtime
- [x] `.rx` application-list metadata migration without auto-loading referenced binaries

### Deliberate compatibility boundaries moved to v1.0 or kept out of scope

- [ ] standards-based DXF paper-layout/PageSetup/viewport **export** and richer advanced OBJECTS table editing; original DXF OBJECTS are already protected by the exact source envelope
- [ ] reinject/merge arbitrary ObjectARX/proxy/custom payloads into an **edited** DWG/DXF; exact original recovery/untouched re-export is implemented, but blind handle-graph reinjection would corrupt files
- [ ] `.js` AutoCAD JavaScript runtime compatibility; arbitrary JavaScript is not executed
- [ ] `.arx` / `.crx` / `.dbx` / `.hdi` / generic `.dll` AutoCAD binary ABI compatibility; inventory recognition only
- [ ] `.dgn` / `.wmf` / `.bmp` / `.dxx` bounded 2D exchange adapters where a compatible parser and concrete planning workflow are justified
- [ ] `.sat` / `.igs` / `.iges` / `.stl` 3D exchange authoring, intentionally outside UCAD 1.x 2D scope

**Interoperability gate:** UCAD distinguishes drawing-open support, bounded migration, exact opaque preservation, and editable semantic fidelity. A format is never advertised beyond the adapter that actually exists. Executable/customization formats are never auto-run merely because they are recognized or migratable.

## v1.0

- standards-based DXF paper-layout/advanced OBJECTS editing
- edited-container proxy/custom-object reinjection only where handle/reference integrity can be proven
- broader production/customer fixture corpus and performance profiling
- professional architecture/planning workflow refinement
- UI/interaction polish against the Figma visual SSOT

## Explicitly out of scope for 1.x

- 3D solid/surface/mesh modeling
- BIM authoring
- rendering
- point clouds
- mechanical/electrical toolsets
- full AutoCAD plug-in binary compatibility
- arbitrary executable AutoLISP/VBA/JavaScript/ObjectARX compatibility without a sandboxed compatible runtime
- lossless semantic editing of every proprietary/custom DWG object when no safe writer exists; exact source recovery remains the safety boundary
- cloud collaboration as a core dependency

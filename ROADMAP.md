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

**Project gate:** UCAD should not expand into architecture/GIS convenience features until the v0.5 drawing-editing loop feels coherent and reliable. v0.5.0 satisfies the implementation side of this gate; real-desktop acceptance of the eight Modify commands remains required before release.

## v0.6–v1.0

- layers and entity properties
- text / dimensions / hatch / blocks
- DXF-first import/export
- print/PDF workflow
- architectural helpers
- planning parcels and indicators
- GIS exchange workflows
- performance and large-drawing regression suite

## Explicitly out of scope for 1.x

- 3D solid/surface/mesh modeling
- BIM authoring
- rendering
- point clouds
- mechanical/electrical toolsets
- full AutoCAD plug-in compatibility
- full DWG editing compatibility
- cloud collaboration as a core dependency

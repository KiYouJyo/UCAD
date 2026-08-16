# UCAD v0.5.0 — Modify Foundation

v0.5.0 builds the first practical CAD Modify command family on top of the Selection / OSNAP / Ortho foundation established in v0.4.x. The focus is not UI expansion; it is a shared geometry-transform, edit-transaction, and input pipeline that remains inside the existing `CommandRegistry → CommandSession → CadWorkspaceSession → CadDocument` architecture.

## Highlights

- **MOVE / M**: supports preselection or command-first selection, then a base point and second point. Entity identity is preserved so selection can remain coherent, and one operation is one Undo step.
- **COPY / CO / CP**: creates new-identity copies from a base point and second point.
- **ROTATE / RO**: rotates selected geometry around a base point using either a canvas point or a numeric command-line angle in degrees, with transient preview.
- **SCALE / SC**: scales about a base point using a positive numeric factor or a picked point, with live preview.
- **MIRROR / MI**: defines the mirror axis with two points. Source objects are kept by default and can optionally be erased.
- **OFFSET / O**: accepts distance, source entity, and side point. The foundation covers Line, Polyline, Circle, and Arc.
- **TRIM / TR**: uses a quick-trim workflow where the other visible entities can act as boundaries; pick target segments repeatedly and press Enter to finish.
- **EXTEND / EX**: extends the picked end of a Line, open Polyline, or Arc to the nearest valid boundary in that direction, with repeated picks until Enter.

## Shared Modify foundation

- Added `CadEntityTransform` for common immutable translate / rotate / scale / mirror geometry operations.
- Added `CadOffset` and `CadTrimExtend` in Core so geometric algorithms do not leak into WinUI event handlers.
- Added undoable `CadDocument.Replace` / `ReplaceRange` transactions. MOVE / ROTATE / SCALE / MIRROR and TRIM / EXTEND mutations can therefore be restored with one Undo.
- Identity-preserving edits retain existing entity IDs; COPY, keep-source MIRROR, OFFSET, and other generated entities receive fresh IDs.
- Modify input reuses the v0.4.x SelectionSet, OSNAP, Ortho, transparent native cursor, and Win2D CAD cursor architecture.
- Both common CAD workflows are supported: select first, then run a command; or start the command and select objects afterward.

## Interaction and shell

- The Modify category is promoted from reserved placeholders to real commands, with all eight foundational commands registered in the shared command registry.
- Point-based Modify input uses object snaps, while MOVE / COPY displacement input can also use F8 Ortho.
- Transform commands and OFFSET provide transient canvas previews.
- New phase prompts are localized in Simplified Chinese / Japanese / English and continue to switch without restart.
- v0.4.1 two-click Window/Crossing, Shift removal, and adjustable Crosshair / Pickbox / OSNAP aperture behavior remains intact.

## Validation

- Core tests cover identity preservation, one-step Undo, translate / rotate / scale / mirror, representative Line / Polyline / Circle / Arc offset cases, and quick TRIM / EXTEND geometry.
- Added a dedicated **Modify Smoke** workflow that launches a real UCAD process and executes MOVE + COPY + ROTATE + SCALE + MIRROR + OFFSET + TRIM + EXTEND in the same process; the run only passes after the success marker is written.
- Existing Core tests, app-build, startup-smoke, Interaction Smoke, Localization Smoke, MSIX / one-click package validation, PerMonitorV2, version SSOT, and trilingual resource parity remain required.

## Scope

v0.5.0 is the first Modify Foundation milestone. Advanced chamfer / fillet, arrays, stretch, grip editing, complex entities, and DWG compatibility remain outside this release and can build on the shared edit transactions and geometry services introduced here.

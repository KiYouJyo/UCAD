# UCAD v0.7.0 — CAD Authoring Foundation

This candidate combines the originally planned v0.5, v0.6, and v0.7 milestones into one acceptance build. On top of the accepted v0.4.1 Selection / OSNAP / Ortho / CAD-cursor foundation, it adds Modify, Layers/Properties, Text/Dimension/Hatch, and Blocks as one coherent authoring loop.

## v0.5: Modify

- MOVE (`M`), COPY (`CO`/`CP`), ROTATE (`RO`), SCALE (`SC`), MIRROR (`MI`)
- OFFSET (`O`), TRIM (`TR`), EXTEND (`EX`)
- both preselection → command and command → selection → Enter CAD workflows
- existing OSNAP reused for Modify point input; MOVE/COPY support F8 Ortho
- transient transform and OFFSET previews
- identity-preserving edits; generated copies receive fresh entity IDs
- unified undoable `CadDocument.Replace` / `ReplaceRange` transactions

## v0.6: Layers & Properties

- document layer table with protected layer `0`
- new entities inherit the current layer
- create, rename, delete, and set-current layer workflows
- visibility, lock, color, lineweight, and linetype metadata
- per-entity layer / color / lineweight / linetype overrides with ByLayer inheritance
- hidden layers are not drawn or used by OSNAP; hidden/locked layers cannot be selected or Modify-picked
- `LAYER` / `LA` Layer Manager and `CHPROP` / `CH` object-property editor
- layer/property state participates in document Undo/Redo snapshots

## v0.7: Annotation, Hatch & Blocks

- `TEXT` / `T`: single-line text with insertion point, height, and rotation
- `DIM` / `DLI` / `DIMLINEAR`: foundational aligned linear dimensions
- `HATCH` / `H`: Solid hatch from one selected closed Polyline or Circle
- Text / Dimension / Hatch participate in shared rendering, selection geometry, grips, and Modify transforms
- document block-definition table
- `BLOCK` / `B`: define a reusable block from the current selection and a picked base point
- `INSERT` / `I`: choose block, scale, and rotation, then pick an insertion point
- `EXPLODE` / `X`: explode one block reference as a single undoable Replace mutation

## Validation

v0.7.0 must pass Core tests, WinUI app build, startup-smoke, Interaction Smoke, Localization Smoke, Modify Smoke, Authoring Smoke, and MSIX/one-click package validation together. Authoring Smoke validates Layers + Properties + Text + Dimension + Hatch + Block + Insert + Explode inside a real running UCAD process; Modify Smoke continues to execute all eight v0.5 Modify commands in one real process.

Restart-free Simplified Chinese / Japanese / English switching remains based on the explicit MRT Core `ResourceContext`. The v0.4.1 two-point Window/Crossing workflow, Shift removal, transparent system pointer, Win2D CAD cursor, F3/F8 behavior, and multi-document isolation remain regression gates.

## Scope boundary

This is still a CAD authoring foundation, not a complete AutoCAD replacement. DXF import/export, print/PDF, advanced dimension styles, advanced hatch patterns/islands, dynamic or attributed blocks, STRETCH/ARRAY/FILLET/CHAMFER, 3D/BIM, and full DWG compatibility remain later work beginning with v0.8.

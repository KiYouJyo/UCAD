# UCAD v0.4.0 — Selection / OSNAP / Ortho Interaction Foundation

v0.4.0 shifts priority away from UI fine-tuning and toward a coherent CAD interaction loop. Rather than expanding into MOVE / COPY / TRIM yet, it establishes reusable selection, erase, object-snap, ortho, and Inspector boundaries for the v0.5 editing milestone.

## Highlights

- Added a document-scoped `SelectionSet` so selection is no longer ad-hoc XAML state. Selections automatically prune entities that disappear through Undo or deletion.
- Added idle click selection, additive selection, and AutoCAD-style directional windows: left-to-right is a containing Window selection; right-to-left is a Crossing selection.
- Added preselection highlighting, selected-entity highlighting, and grip feedback. Blank click or Esc clears the selection.
- Added `ERASE / E / DELETE`. The Delete key, command line, and command search all converge on `CommandRegistry → CommandSession`; erasing multiple selected entities is one undoable document mutation, so one Undo restores the whole selection.
- Connected the foundational OSNAP set — **Endpoint / Midpoint / Center / Intersection** — to real drafting input. The snap aperture is converted from fixed screen pixels to world units so zoom does not distort interaction feel.
- F3 or the OSNAP status button toggles object snap immediately, with independent state per drawing session.
- Added Ortho constraint to LINE / PLINE mouse input. F8 or the ORTHO status button toggles it per drawing session.
- Drafting defaults in Settings now actually initialize new Drawing sessions: default object snap, the complete foundational snap set, and default ortho.
- Inspector now reads selected Line / Polyline / Circle / Arc entities and exposes type, count, basic geometry, and entity ID; observable command-session lifecycle keeps Inspector state synchronized.
- Tool-category availability now derives from real `CommandRegistry` capabilities so unimplemented Modify / Annotate / Layer / Block / Measure categories do not imply completed Core behavior.
- Added UI-independent `CadRect`, entity bounds/distance/window-intersection queries, and line/circle/arc intersection geometry for later Modify commands and spatial indexing.
- Preserved the v0.3.10 no-restart Simplified Chinese / Japanese / English localization system and added v0.4.0 interaction status text.

## Controls

- Click an entity: select it; continue clicking entities to add to the selection.
- Click empty space or press Esc: clear selection.
- Drag left-to-right: Window selection, fully contained entities only.
- Drag right-to-left: Crossing selection, contained or intersecting entities.
- Delete: erase the current selection; `ERASE`, `E`, and `DELETE` invoke the same command path.
- F3: toggle OSNAP.
- F8: toggle ORTHO.

## Scope

Except for foundational ERASE, v0.4.0 intentionally does not include MOVE, COPY, ROTATE, SCALE, MIRROR, TRIM, EXTEND, OFFSET, or the full AutoCAD OSNAP catalog. Those remain v0.5.x work. This release freezes the Selection / Drafting Aid contracts across Core, Workspace, and Viewport first.

## Validation

Core tests cover selection sets, point hit testing, Window/Crossing selection, Line/Circle/Arc hits, Endpoint/Midpoint/Center/Intersection snaps, line-circle and circle-circle intersections, Ortho constraints, one-step multi-entity ERASE Undo, and observable CommandSession lifecycle. Real startup-smoke creates a Drawing in the running app and verifies Selection + OSNAP + Center Snap + ORTHO + Inspector + capability-derived category state. Existing app-build, MSIX/one-click package validation, trilingual key parity, version SSOT, and PerMonitorV2 checks remain required. Pixel-level UI tuning is not a v0.4.0 release gate, while existing Figma design tokens remain protected from regression by CI.

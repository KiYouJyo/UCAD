# UCAD v0.4.0 — Selection / OSNAP / Ortho Interaction Foundation

v0.4.0 shifts priority away from UI fine-tuning and toward a coherent CAD interaction loop. This release does not add Modify commands; it establishes reusable selection, object-snap, ortho, and Inspector boundaries for MOVE / COPY / TRIM / OFFSET work in v0.5.0.

## Highlights

- Added a document-scoped `SelectionSet` so selection is no longer ad-hoc XAML state. Selections automatically prune entities that disappear through Undo or deletion.
- Added idle click selection, additive selection, and AutoCAD-style directional windows: left-to-right is a containing Window selection; right-to-left is a Crossing selection.
- Added preselection highlighting, selected-entity highlighting, and grip feedback. Blank click or Esc clears the selection.
- Added foundational OSNAP modes: Endpoint, Midpoint, and Intersection. Core also exposes Center snap for circles/arcs for later UI exposure.
- Connected OSNAP to real mouse drafting input and transient previews. F3 or the OSNAP status button toggles it immediately, with independent state per drawing session.
- Added Ortho constraint to LINE / PLINE mouse input. F8 or the ORTHO status button toggles it per drawing session.
- Drafting defaults in Settings now actually initialize new Drawing sessions: default object snap, snap types, and default ortho.
- Inspector now reads selected Line / Polyline / Circle / Arc entities and exposes type, count, basic geometry, and entity ID; observable command-session lifecycle keeps Inspector state synchronized.
- Tool-category availability now derives from real `CommandRegistry` capabilities so unimplemented Modify / Annotate / Layer / Block / Measure categories do not imply completed Core behavior.
- Added UI-independent `CadRect`, entity bounds/distance/window-intersection queries, and line/circle/arc intersection geometry for later Modify commands and spatial indexing.
- Preserved the v0.3.10 no-restart Simplified Chinese / Japanese / English localization system and added trilingual v0.4.0 interaction strings.

## Controls

- Click an entity: select it; continue clicking entities to add to the selection.
- Click empty space or press Esc: clear selection.
- Drag left-to-right: Window selection, fully contained entities only.
- Drag right-to-left: Crossing selection, contained or intersecting entities.
- F3: toggle OSNAP.
- F8: toggle ORTHO.

## Scope

v0.4.0 intentionally does not include MOVE, COPY, ROTATE, TRIM, EXTEND, OFFSET, or the full AutoCAD OSNAP catalog. Those remain v0.5.x work. This release freezes the Selection / Drafting Aid contracts across Core, Workspace, and Viewport first.

## Validation

Core tests cover selection sets, point hit testing, Window/Crossing selection, Line/Circle/Arc hits, Endpoint/Midpoint/Intersection/Center snaps, line-circle and circle-circle intersections, Ortho constraints, and observable CommandSession lifecycle. Existing app-build, real startup-smoke, MSIX/one-click package validation, trilingual key parity, version SSOT, and PerMonitorV2 checks remain required. Pixel-level UI tuning is not a v0.4.0 release gate, while existing Figma design tokens remain protected from regression by CI.

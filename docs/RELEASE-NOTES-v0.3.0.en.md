# UCAD v0.3.0 — Drawing Foundation

v0.3 is the first UCAD release with a complete 2D drawing loop: command input → live geometry preview → persistent entity commit → Undo/Redo.

## Five drawing commands

- `LINE` / `L`: continuous two-point line segments; each segment commits as a Line entity.
- `PLINE` / `PL`: continuous vertices committed as one Polyline on Enter / Space.
- `RECTANGLE` / `REC`: two opposite corners create a closed Polyline rectangle.
- `CIRCLE` / `C`: center plus radius point; after the center, a radius can also be typed directly.
- `ARC` / `A`: a true three-point arc using start, second point on the arc, and end.

Every command can mix mouse picks with the v0.2 `x,y`, `@x,y`, and distance input system and includes a blue live preview. New drawings now start empty rather than with demo geometry.

## Undo / Redo

- `UNDO` / `U` reverses the previous committed drawing mutation.
- `REDO` reapplies it.
- Toolbar Undo / Redo state follows the document history automatically.
- CLEAR also participates in reversible history.

History lives in `UCAD.Core`, ready to be reused by later MOVE, COPY, TRIM, and OFFSET commands.

## Geometry model

v0.3 adds `PolylineEntity`, `CircleEntity`, and `ArcEntity`. Three-point arcs calculate their canonical center, radius, and sweep; sampling is used only for rendering, not stored as model geometry.

## Installation

Download `UCAD-v0.3.0-x64-one-click.zip`. Devices that already trust the UCAD release certificate normally do not need to import it again.

## Next

v0.4 moves into interaction: click/window/crossing selection, multi-select, Delete, endpoint/midpoint/center/intersection OSNAP, and Ortho.

# UCAD v0.4.1 — CAD Selection & Cursor Interaction Refinement

v0.4.1 refines the selection and drafting interaction foundation introduced in v0.4.0 without adding new Modify commands. The focus is a more conventional CAD mouse-selection flow plus an adjustable crosshair, pickbox, and object-snap aperture.

## Highlights

- **Two-click window selection**: click empty space to define the first corner, release the mouse, move to preview the window, and click again to define the opposite corner and commit the selection.
- **Drag selection retained**: press-drag-release window selection remains available as a shortcut; both interaction styles coexist.
- **Directional Window / Crossing behavior**: finishing to the right produces a Window selection (fully contained objects only), while finishing to the left produces a Crossing selection (contained or intersecting objects).
- **PICKADD-style additive selection**: subsequent object picks and windows add to the current selection instead of replacing it.
- **Shift to remove**: Shift-click an object or hold Shift when committing a window to remove matching objects from the current selection.
- **Layered Esc behavior**: when a two-point window is active, the first Esc cancels only that gesture; with no active gesture, Esc clears the completed selection set.
- **CAD cursor**: the drawing surface no longer uses the normal arrow. A system cross hotspot is combined with a Win2D-rendered CAD crosshair and central square pickbox.
- **Adjustable crosshair**: crosshair length can be set from 5–100% of the drawing area, defaulting to 100%.
- **Adjustable pickbox**: the central selection target can be set from 3–20 px, defaulting to 6 px; visual pickbox size and point-selection tolerance use the same size basis.
- **Adjustable OSNAP aperture**: object-snap acquisition range can be set from 3–50 px, defaulting to 10 px, and remains converted to world units at the current zoom.
- **Immediate settings**: all three controls appear under Settings → Input & Interaction → CAD cursor and apply to existing Drawing sessions without restart.
- **Trilingual behavior preserved**: the new setting labels support Simplified Chinese / Japanese / English and remain compatible with the restart-free language switching introduced in v0.3.10.

## Controls

- Click an object: add it to the selection.
- Shift + click: remove it from the selection.
- Click empty space → move → click again: two-point window selection.
- Press-drag-release: quick window selection.
- Finish to the right: Window; finish to the left: Crossing.
- Hold Shift when finishing a window: remove matching objects.
- Esc: cancel an unfinished window first; Esc again can clear the completed selection.

## Scope

v0.4.1 is an interaction refinement release. MOVE / COPY / ROTATE / SCALE / MIRROR / OFFSET / TRIM / EXTEND remain v0.5.x work. The refined selection model and CAD cursor settings become the interaction baseline for those future commands.

## Acceptance focus

Selection gestures and cursor sizing depend heavily on real mouse feel. Automated validation protects selection-set behavior, Shift removal, build/startup/package contracts, versioning, and localization; the PR acceptance build should still be manually checked for two-click selection, drag selection, Window/Crossing direction, Shift removal, Esc cancellation, and the three cursor-size controls before release.

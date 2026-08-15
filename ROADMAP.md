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

- [ ] command line UI
- [ ] command registry and aliases
- [ ] Enter / Space confirmation
- [ ] Esc cancellation
- [ ] repeat previous command
- [ ] numeric and coordinate input
- [ ] relative coordinates (`@x,y`)

## v0.3 — Drawing

- [ ] LINE
- [ ] PLINE
- [ ] RECTANGLE
- [ ] CIRCLE
- [ ] ARC
- [ ] Undo / Redo

## v0.4 — Interaction

- [ ] click selection
- [ ] window selection
- [ ] crossing selection
- [ ] multi-selection
- [ ] delete
- [ ] endpoint / midpoint / center / intersection OSNAP
- [ ] ortho mode

## v0.5 — Modify

- [ ] MOVE
- [ ] COPY
- [ ] ROTATE
- [ ] SCALE
- [ ] MIRROR
- [ ] OFFSET
- [ ] TRIM
- [ ] EXTEND

**Project gate:** UCAD should not expand into architecture/GIS convenience features until the v0.5 drawing-editing loop feels coherent and reliable.

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

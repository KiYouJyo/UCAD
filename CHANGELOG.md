# Changelog

## 0.3.0 — 2026-08-15

### Drawing foundation
- Added persistent `PolylineEntity`, `CircleEntity`, and three-point `ArcEntity` models to `UCAD.Core`.
- Added LINE (`L`), PLINE (`PL`), RECTANGLE (`REC`), CIRCLE (`C`), and ARC (`A`) drawing commands using the v0.2 command system.
- Unified mouse picks and typed coordinates for all drawing commands.
- Added live transient previews for line, polyline, rectangle, circle, and three-point arc workflows.
- Removed the startup demo geometry so new drawings begin empty.

### History
- Added document-level Undo (`U`) and Redo history with toolbar state feedback.
- Clearing a drawing and committing entities participate in the same reversible history model.

### Validation
- Added entity geometry, arc construction, history, and drawing-command alias tests.

## 0.2.0 — 2026-08-15

### Command foundation
- Added a UI-independent command registry with case-insensitive aliases and session state.
- Added AutoCAD-style Enter / Space confirmation, Esc cancellation, and repeat-previous-command behavior.
- Added absolute `x,y`, relative `@x,y`, and numeric distance parsing.
- Connected `LINE` / `L` to mouse and typed point input through the same command path.
- Added command-system unit tests and multilingual command prompts.

### Release infrastructure
- Added per-version Release titles to `release/release.json`.
- Made first-time GitHub Release asset cleanup idempotent.

## 0.1.0 — 2026-08-15

### Foundation
- Established WinUI 3 / Win2D application and independent `UCAD.Core` geometry/document layer.
- Added grid, crosshair, coordinate transforms, zoom, pan, and two-point Line drawing.

### Packaging and release
- Converted GitHub distribution to signed x64 MSIXBundle.
- Added one-time UAC certificate trust setup and normal-user MSIX installation flow.
- Added SHA-256 release manifests and release-asset validation.
- Added `release/release.json` as release metadata SSOT.

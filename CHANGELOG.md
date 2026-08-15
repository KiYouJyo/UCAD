# Changelog

## 0.3.6 — 2026-08-15

### Startup reliability
- Fixed the v0.3.5 launch crash caused by passing the XAML `x:Uid` property resource key `ToolShelfHintText.Text` to `ResourceLoader.GetString()`, which raised `0x80073B17 NamedResource Not Found` before the window appeared.
- Switched code-side lookup to the plain named `ToolShelfHint` resource and added a guarded fallback that records missing runtime resources instead of allowing one localization lookup to terminate startup.
- Deferred initial `CadWorkspaceSession` / Win2D `CadViewport` creation until the root visual tree is loaded, reducing additional early-startup initialization risk.
- Added startup diagnostics at `%LOCALAPPDATA%\UCAD\Logs\startup.log` for launch stages and unhandled exceptions.
- Preserved the v0.3.5 multi-document workspace architecture and existing CAD commands.

### Validation
- Added a Windows `startup-smoke` CI job that actually launches `UCAD.App.exe`, waits eight seconds, and fails if the process exits unexpectedly.
- Added a runtime localization contract that rejects `GetString()` calls using XAML property-style resource keys, checks all runtime keys across zh-CN / ja-JP / en-US, and fails when startup diagnostics report a missing resource.
- Startup smoke prints the startup diagnostic log on failure.
- Added a PR-only one-click acceptance package so installed-environment startup can be manually verified before release.

## 0.3.5 — 2026-08-15

### Workspace shell
- Rebuilt the WinUI shell around the approved Fluent CAD workspace with browser-style document tabs, a persistent category tool shelf, a compact high-frequency tool rail, an inspector boundary, command line, and status bar.
- Added real in-memory multi-document sessions: every tab owns an independent `CadDocument`, `CadViewport`, `CommandSession`, command base point, viewport state, and previous-command state.
- Connected LINE / PLINE / RECTANGLE / CIRCLE / ARC, Undo / Redo, Clear, and Reset View to the new shell through one command-dispatch path.
- Added command search driven directly by `CommandRegistry` tokens.
- Kept planned interaction/modify/layer surfaces visible but disabled so the UI does not imply unfinished Core capabilities.

### UI ↔ Core foundation
- Added `CadCommandCategory` and optional `DrawingCommandKind` metadata to command definitions, removing the UI string switch that previously identified drawing commands.
- Added `CadDocument.Changed`, `CadDocument.Revision`, and structured document-change events so tabs, inspector state, and history controls observe Core directly.
- Changed `CadViewport` to accept an externally owned `CadDocument` and expose zoom state, enabling independent document workspaces without duplicating the renderer.
- Added Core tests for command metadata and observable document revisions.

### Localization and release metadata
- Expanded zh-CN, ja-JP, and en-US resources for the complete workspace shell.
- Updated package and release metadata to 0.3.5 / 0.3.5.0.
- Documented v0.3.5 as the transition gate before v0.4 selection, OSNAP, and Ortho begin tighter shell/Core coupling.

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

# UCAD v0.3.7 — UI Fidelity & HiDPI Foundation

v0.3.7 is the display and workspace-foundation release before v0.4.0 begins deeper UI ↔ CAD Core coupling. It adds no new CAD Core commands. The focus is to fix the soft/blurry installed UI seen on high-DPI displays, close the gap between the running WinUI shell and the approved Figma v0.2 design, and freeze a stable shell for the next interaction layer.

## HiDPI clarity

- Restored `PerMonitorV2` DPI awareness in the custom application manifest, with the `true/pm` compatibility declaration retained.
- Added a CI contract that fails when `PerMonitorV2` is removed.
- Kept the v0.3.6 real startup smoke test that launches UCAD on a Windows runner and checks startup diagnostics.

## High-fidelity Figma v0.2 workspace

- Translated the approved 1440×900 Figma v0.2 desktop frame into the WinUI 3 workspace baseline.
- Reworked the title area as a browser-like multi-document strip: a fixed UCAD brand area followed immediately by approximately `190×34` drawing tabs.
- Preserved native WinUI title-bar behavior and system caption buttons rather than replacing them with a screenshot or fake window chrome.
- Kept the persistent top structure `File / Edit / View | Draw / Modify / Annotate / Layers / Blocks / Measure / View`.
- Category tool shelves remain open after a category is selected and collapse only when the active category is clicked again.
- The 52 px left Tool Rail remains reserved for the highest-frequency commands.
- Inspector, command line, and status bar were restyled to the Figma dimensions, hierarchy, and dark surfaces.

## Design tokens and icons

- Added `UcadDesignTokens.xaml` to centralize the Figma-derived title-bar, category-bar, shelf, inspector, canvas, status-bar, text, divider, and accent resources.
- Cursor uses the Microsoft Fluent System Icons vector geometry; common actions such as Move, Copy, Trim, and More use Fluent system icon semantics.
- CAD-specific Line, Polyline, and Offset symbols remain temporary simplified glyphs until the dedicated UCAD CAD Fluent icon set is defined rather than substituting misleading generic icons.
- Planned categories no longer inherit WinUI's washed-out disabled treatment. Categories remain legible and switchable while unfinished tools are explicitly presented as reserved/noninteractive.

## Shell behavior and version state

- Document tabs continue to own real independent `CadWorkspaceSession` instances rather than becoming visual-only tabs.
- LINE / PLINE / RECTANGLE / CIRCLE / ARC, Undo / Redo, Clear, and Reset View keep using the existing unified command path.
- The status-bar UCAD version is now derived from assembly metadata instead of a hardcoded `UCAD v0.3.5` string.
- zh-CN, ja-JP, and en-US resources now include the reserved-tool-shelf state and remove stale v0.3.5 wording where applicable.

## CI regression protection

In addition to Core tests, app-build, startup-smoke, and MSIX / one-click validation, v0.3.7 adds a shell-foundation contract:

- `PerMonitorV2` must remain present;
- the shell must not hardcode a v0.3.x display version;
- the browser-like title strip must not reintroduce a separate `AppTitleBar.Title` label;
- the core Figma-derived UCAD design tokens must exist;
- runtime resource keys must remain available in zh-CN, ja-JP, and en-US.

## Scope

This release does not implement CAD Core Selection, OSNAP, Ortho, Move, Copy, Offset, or Trim. v0.4.0 will build those interactions directly on the `CadWorkspaceSession + CadViewport + Inspector + StatusBar` boundary frozen by v0.3.7.

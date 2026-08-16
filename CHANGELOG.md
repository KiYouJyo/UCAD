# Changelog

## 0.5.0 — 2026-08-16

### Modify Foundation
- Added MOVE (`M`), COPY (`CO`, `CP`), ROTATE (`RO`), SCALE (`SC`), MIRROR (`MI`), OFFSET (`O`), TRIM (`TR`), and EXTEND (`EX`) through the existing `CommandRegistry → CommandSession` path.
- Added both CAD noun/verb and verb/noun workflows: preselected entities can enter Modify immediately, while command-first operation keeps normal click/Window/Crossing selection active until Enter confirms the selection set.
- Added shared immutable `CadEntityTransform` logic for translate, rotate, scale, and mirror across Line / Polyline / Circle / Arc, with identity-preserving edits and fresh identities for generated copies.
- Added `CadDocument.Replace` / `ReplaceRange` so identity-preserving transforms and trim/extend replacements are recorded as one undoable document mutation.
- Added foundational OFFSET geometry for Line / Polyline / Circle / Arc and quick-mode TRIM / EXTEND geometry using other visible entities as boundaries.
- Added OSNAP-aware Modify point input, F8 Ortho support for displacement input, transient transform/offset previews, and real entity-pick phases without creating a second selection model.
- Promoted the Modify category and existing MOVE / COPY / OFFSET / TRIM shell surfaces from reserved placeholders to real commands, with ROTATE / SCALE / MIRROR / EXTEND added to the Modify shelf.
- Added restart-free zh-CN / ja-JP / en-US Modify phase prompts while preserving v0.4.1 two-click selection and the transparent native cursor + Win2D CAD cursor architecture.

### Validation
- Added Core coverage for identity preservation, one-step Undo, translate / rotate / scale / mirror, offset side/radius/polyline behavior, quick trim/extend geometry, and edit-transaction identity safety.
- Added a dedicated Modify Smoke workflow that launches real UCAD and executes MOVE + COPY + ROTATE + SCALE + MIRROR + OFFSET + TRIM + EXTEND in one running process.
- Extended static contracts for the full v0.5 Modify registry, shared geometry services, viewport input bridge, undoable replacements, trilingual prompt parity, and version SSOT.
- Retained Core tests, app-build, startup-smoke, Interaction Smoke, Localization Smoke, MSIX / one-click package validation, PerMonitorV2, frozen Figma-critical design tokens, and transparent CAD cursor regression checks.

## 0.4.1 — 2026-08-16

### CAD Selection & Cursor Interaction Refinement
- Added AutoCAD-style two-point Window/Crossing selection: blank first click arms the selection rectangle, pointer movement previews it without holding the button, and the second click commits it.
- Retained press-drag-release Window/Crossing as an alternate fast gesture using the same direction semantics.
- Kept PICKADD-style additive selection and added Shift + point/window removal from the current selection set.
- Completing an empty non-Shift selection window clears the current selection set; Esc cancels an unfinished window before clearing a completed selection set.
- Replaced the drawing-surface native arrow/cross overlay with a clean Win2D CAD pointer: the Windows cursor is suppressed while inside the canvas, leaving only the adjustable crosshair and central pickbox.
- Increased the fresh-install pickbox default from 6 px to 10 px and kept the visible pickbox adjustable from 3–20 px with the same screen-space basis used by point hit testing.
- Added persisted CAD pointer settings for crosshair size, pickbox size, and OSNAP aperture, applied live to existing Drawing sessions.
- Preserved v0.4.0 document-scoped SelectionSet / OSNAP / Ortho / Inspector / ERASE foundations and v0.3.10 restart-free trilingual switching.

### Validation
- Added batch selection-removal coverage to Core tests.
- Added v0.4.1 contracts for two-click selection, Shift removal, native cursor suppression, adjustable pointer dimensions, version SSOT, PerMonitorV2, frozen Figma tokens, and localization parity.
- Interaction Smoke and Localization Smoke run on active `codex/**` development branches as well as PR/main validation.
- Same-repository PR acceptance packages are re-signed with the normal release certificate before manual mouse-feel acceptance.

## 0.4.0 — 2026-08-16

### Selection / OSNAP / Ortho Interaction Foundation
- Added document-scoped `SelectionSet` / `CadInteractionState` ownership so selection, object snap, and ortho are independent per Drawing tab and are not stored as ad-hoc XAML state.
- Added click selection, additive multi-selection, preselection highlighting, grips, left-to-right Window selection, and right-to-left Crossing selection for Line / Polyline / Circle / Arc entities.
- Added UI-independent `CadRect`, bounds, hit-distance, rectangle intersection, grip-point, and line/circle/arc intersection geometry queries in Core.
- Added Endpoint / Midpoint / Center / Intersection OSNAP with a screen-pixel aperture converted to world units; connected snap points and transient markers to real mouse drafting input.
- Added Ortho constraint for LINE / PLINE mouse drafting, with F8 and status-bar toggling. F3 and status-bar OSNAP toggling are also per-workspace and immediate.
- Made Drafting Settings defaults initialize real OSNAP / snap-mode / Ortho state for newly created Drawing sessions.
- Added selection-backed Inspector reporting for entity type, selection count, basic geometry, and entity ID; made command-session lifecycle observable so Inspector stays synchronized.
- Added `ERASE` (`E`, `DELETE`) through the shared `CommandRegistry → CommandSession` path. Keyboard Delete uses the same command implementation, and multi-entity erase is a single undoable document mutation.
- Derived tool-category enabled state from actual `CommandRegistry` capabilities so unfinished Modify / Annotate / Layer / Block / Measure categories do not imply implemented Core behavior.
- Preserved v0.3.10 restart-free zh-CN / ja-JP / en-US localization and added v0.4 interaction status/Inspector wording.

### Validation
- Expanded Core tests for SelectionSet, point/curve hit testing, Window/Crossing selection, four foundational OSNAP modes, line-circle/circle-circle intersections, Ortho, multi-entity ERASE one-step Undo, and observable CommandSession lifecycle.
- Expanded real startup smoke to create a Drawing in the running application and validate Selection + ERASE + Endpoint/Center OSNAP + Ortho + Inspector + capability-derived category state.
- Kept app-build, MSIX / one-click package validation, localization parity, version SSOT, `PerMonitorV2`, Unicode placeholder checks, and frozen Figma-critical design tokens as required CI.
- Pixel-level UI tuning remains manual/non-gating for this milestone.

## 0.3.10 — 2026-08-16

### Live trilingual localization hotfix
- Fixed Start / Settings rendering resource identifiers such as `Start_TabTitle` and `Settings_Nav_Title` instead of translated UI text.
- Replaced the broken imperative named-resource loading path with an explicit MRT Core `ResourceManager` / `ResourceContext` whose language qualifier can be changed inside the running process.
- Added a dedicated plain-ID `ShellLive.resw` map for zh-CN / ja-JP / en-US so code-side hot refresh never depends on XAML `x:Uid` property-resource identifiers such as `FileMenuButton.Content`.
- Added restart-free Simplified Chinese / Japanese / English switching for the current Window, Start, Settings, document tabs, menus, Category Bar, Inspector, command area, and status bar.
- Preserved existing `CadWorkspaceSession` objects during language changes, including geometry, Undo/Redo history, command/session ownership, and viewport state.
- Relocalized untitled drawing labels in place (`图纸 1` / `図面 1` / `Drawing 1`) and kept Follow System Language behavior.
- Updated Settings language guidance so it no longer claims a restart is required.

### Validation
- Added a dedicated Localization Smoke workflow that switches `zh-CN → ja-JP → en-US` in one running UCAD process and rejects raw resource identifiers.
- Added CI parity and representative-value checks for `Resources.resw`, `UcadV039.resw`, and the new `ShellLive.resw` in all three locales.
- Kept Core tests, app-build, startup-smoke, package-validation, PerMonitorV2, version SSOT, Figma token contracts, and Unicode placeholder-icon checks.

## 0.3.9 — 2026-08-16

### UI Completion / Figma Fidelity
- Promoted the 1440×900 UCAD Figma file to the production UI visual SSOT and centralized its critical geometry in `UcadDesignTokens.xaml`.
- Rebuilt the title strip as an explicit browser-style Brand → Document Tabs → `+` → drag region → native caption layout with approximately 190×34 tabs.
- Added explicit Drawing / Start / Settings page modes so CAD-only Tool Rail, Inspector, Command Line, and Status Bar are not shown beside Start or Settings content.
- Completed the Start Center with New/Open, Recent, Blank/Architecture/Urban Planning template information architecture, and Learn UCAD surfaces while keeping unsupported file/template behavior honest.
- Completed General, Appearance, Drafting, Input & Interaction, Files & Save, Language & Region, and About UCAD Settings pages using the Figma 228 px navigation, 940×72 cards, and 35 / 12 / 8 / 30 px vertical rhythm.
- Replaced Unicode placeholder glyphs with Fluent / WinUI icons or UCAD-style `PathIcon` geometry.

### Settings, behavior, localization, and versioning
- Added centralized `AppSettings` / `SettingsService` persistence under `%LOCALAPPDATA%\UCAD\settings.json`.
- Made the new-tab preference functional: `+` opens Start by default and creates a blank Drawing when “show Start on new tab” is disabled.
- Made App Theme and CAD Canvas Theme independently effective at runtime; the shell palette and Win2D drawing palette no longer depend on each other.
- Wired canvas background/grid, cursor-centered zoom, middle-button pan, reverse wheel zoom, coordinate precision, and decimal-format preferences to runtime behavior.
- Kept unsupported Restore Session, manual UI Scale, automatic-update checking, and recent-history clearing disabled/reserved instead of presenting no-op controls as completed features.
- Added complete Start/Settings resource maps for zh-CN, ja-JP, and en-US with CI key-parity validation; display-language preference is applied before the next shell is created to avoid partial mixed-language refreshes.
- Made root `VERSION` the product version SSOT and aligned assembly metadata, runtime UI, release metadata, and MSIX package identity to 0.3.9 / 0.3.9.0.
- Preserved `PerMonitorV2` and XAML DIP layout behavior.

### Validation
- Kept Core tests, WinUI app build, runtime startup smoke, and MSIX / one-click package validation as required CI.
- Expanded UI/behavior contracts to cover Figma dimensions/colors, page/section existence, Start/Settings/Canvas behavior, version SSOT, localization parity, PerMonitorV2, and removal of Unicode fake icons.
- Kept pixel-level 1440×900 comparison as a manual, non-gating workflow that runs only when the runner exposes a real 1440×900 interactive desktop.

## 0.3.7 — 2026-08-16

### UI fidelity and HiDPI
- Restored `PerMonitorV2` DPI awareness in the custom application manifest so Windows does not bitmap-scale the UCAD window on high-DPI displays.
- Added Figma-derived `UcadDesignTokens.xaml` resources for the title bar, category bar, tool shelf, inspector, canvas, status bar, typography hierarchy, dividers, and accent states.
- Reworked the title area into a browser-like document strip with a fixed UCAD brand area and approximately 190×34 drawing tabs while preserving native WinUI caption behavior.
- Aligned category-bar, persistent tool-shelf, 52 px high-frequency Tool Rail, 304 px Inspector, command line, and status bar geometry with the approved Figma v0.2 desktop frame.
- Replaced several generic glyph placeholders with Fluent icon semantics, including Cursor, Move, Copy, Trim, More, and Reset View; CAD-specific glyphs remain intentionally provisional pending the UCAD CAD Fluent icon set.
- Kept planned categories legible and switchable instead of applying WinUI's washed-out Disabled visual state to the entire category bar.
- Switched the status-bar version label from a hardcoded v0.3.5 string to runtime assembly metadata.

### Shell behavior and localization
- Preserved real independent `CadWorkspaceSession` instances behind browser-style tabs and retained the existing command registry / viewport command flow.
- Added persistent shelf previews for planned categories without exposing unfinished CAD Core actions as clickable commands.
- Updated zh-CN, ja-JP, and en-US resources for reserved tool shelves and removed stale v0.3.5 wording from shell notices.

### Validation
- Added a HiDPI and shell-fidelity CI contract requiring `PerMonitorV2`, runtime-derived version text, the browser-title contract, and the core Figma-derived design tokens.
- Retained Core tests, WinUI app build, runtime localization validation, real startup smoke, and MSIX / one-click package validation.
- Generalized the PR acceptance package to v0.3.7 metadata.

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
- Added core unit tests, CI, GitHub Actions release automation, and signed x64 MSIXBundle distribution.
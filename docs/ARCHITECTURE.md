# Architecture

UCAD is intentionally split into a UI-independent CAD core and a Windows-native presentation layer.

## `UCAD.Core`

Owns geometry, document state, entity models, command vocabulary, command-session state, parsers, reversible document history, and the reusable geometry/interaction contracts required by selection and drafting aids.

### Entity model at v0.4.0

- `LineEntity` — two-point line segment.
- `PolylineEntity` — ordered vertices with optional closed topology; RECTANGLE commits as a closed polyline.
- `CircleEntity` — center and positive radius.
- `ArcEntity` — center/radius/start/sweep representation constructed from three picked points. Rendering samples the canonical arc rather than storing a display-only approximation.

### Document history and observable state

`CadDocument` snapshots the immutable entity list at each committed mutation. Undo/Redo therefore lives below WinUI and can be reused by later MOVE/COPY/TRIM/OFFSET commands. A future large-drawing milestone may replace snapshots with operation deltas if profiling justifies it.

The observable boundary remains UI-agnostic:

- `CadDocument.Changed` publishes committed mutations, Undo, and Redo.
- `CadDocument.Revision` monotonically identifies document-state changes.
- `CadDocumentChangedEventArgs` exposes change kind and entity count without referencing WinUI.
- `CommandSession.Changed` publishes command lifecycle changes so Inspector/shell state does not need to poll Core internals.

Browser-style tabs, inspector state, dirty indicators, selection, and future layer services can react to document/workspace state without routing ownership through `CadViewport`.

### v0.4 geometry and selection foundation

v0.4.0 adds UI-independent geometry queries under `UCAD.Core.Interaction`:

- `CadRect` represents normalized world-coordinate selection rectangles.
- `CadEntityGeometry` supplies entity bounds, point-to-entity distance, rectangle containment/intersection, grip points, and line/polyline/circle/arc intersections.
- `CadSelectionQuery` resolves nearest point hits plus Window/Crossing queries.
- `SelectionSet` owns selected entity IDs for one `CadDocument`, supports additive selection, and automatically prunes IDs that disappear from document history.
- `ObjectSnapResolver` resolves Endpoint / Midpoint / Intersection and optional Center candidates within a world-space aperture supplied by the viewport.
- `OrthoConstraint` applies a dominant-axis 0°/90° constraint from a drawing base point.
- `CadInteractionState` groups Selection, OSNAP state/modes, and Ortho state for one workspace.

These types have no WinUI/Win2D dependency and are intended to be reused by v0.5 Modify commands and later spatial-index work.

### Command foundation

- `CadCommandDefinition` — canonical command name plus aliases, UI-neutral category metadata, and optional `DrawingCommandKind`.
- `CommandRegistry` — case-insensitive command resolution and duplicate-token protection.
- `CommandSession` — active/previous command lifecycle, repeat, complete, cancel, and observable lifecycle changes.
- `CommandInputParser` — numeric, absolute coordinate, and relative coordinate parsing.
- `DrawingCommandKind` — UI-independent drawing workflow vocabulary.
- `CadCommandCategory` — stable high-level command grouping used by the shell without encoding toolbar logic in Core.

Every command-capable UI surface resolves to the same `CommandRegistry → CommandSession → CAD Core` path. The shell must not maintain a second command implementation.

## `UCAD.App`

Owns WinUI 3 windowing, the Win2D viewport, keyboard routing, localized prompts, pointer interaction, live previews, workspace presentation, settings persistence, and MSIX integration.

### Shell and page model

Figma remains the visual SSOT and WinUI native controls remain the interaction SSOT. `WorkspacePageKind` defines three explicit tab-content types:

- **Drawing** — the CAD workspace: Category Bar, Tool Shelf, Tool Rail, `CadViewport`, Inspector, Command Line, and Status Bar.
- **Start** — the long-lived new-tab / Start Center. The title-bar `+` selects or creates Start rather than immediately creating an empty CAD document by default.
- **Settings** — one reusable settings tab with its own navigation and content. CAD-only rails and bottom bars are covered rather than duplicated beside Settings navigation.

The title strip is an explicit `[Brand][Document Tab Strip][Drag Region][native caption buttons]` layout instead of relying on `TitleBar.Content` auto-arrangement. Approximately 190×34 document tabs remain left-contiguous like a browser.

`CadWorkspaceSession` is the application-level owner for one Drawing tab. Each session has its own:

- `CadDocument`
- `CadInteractionState`
- `CadViewport`
- `CommandSession`
- typed-command base point
- pointer/status context

Tabs are therefore real independent in-memory CAD tasks, including independent Selection / OSNAP / Ortho state.

### v0.4 selection and drafting interaction

`CadViewport` coordinates screen-space pointer input with Core interaction services rather than owning selection business rules.

- Idle click uses `CadSelectionQuery.HitTestNearest`.
- Consecutive click/window results are additive by default; blank click or Esc clears selection.
- Left-to-right drag performs Window selection (fully contained entities).
- Right-to-left drag performs Crossing selection (contained or intersecting entities).
- Selection preview, selected-entity emphasis, grip feedback, selection window, and snap markers are viewport-only presentation of Core-owned results.
- OSNAP converts a fixed screen-pixel aperture to world units, then resolves candidates through `ObjectSnapResolver`.
- OSNAP has priority when a real snap candidate is present; otherwise Ortho constrains LINE/PLINE mouse input from the latest accepted base point.
- Typed coordinates remain explicit typed coordinates and converge on the same `SubmitDrawingPoint` entity-commit path without being silently rewritten by mouse-only aids.
- F3 and the OSNAP status button toggle per-workspace object snap.
- F8 and the ORTHO status button toggle per-workspace orthogonal drawing.

Grid snap, Polar and OTRACK remain reserved and non-interactive until their Core contracts exist.

### Inspector and capability boundary

Inspector reads the active workspace's `SelectionSet`. With one selected Line / Polyline / Circle / Arc it reports type, basic geometry and entity ID; multiple selections report a mixed selection/count summary. No duplicate selection model lives in XAML.

Category availability derives from `CommandRegistry` categories. Draw/View remain available because registered commands exist; Modify/Annotate/Layer/Block/Measure stay unavailable until corresponding commands are registered. This prevents a presentation-only category from implying a Core capability.

### Start Center

`StartPage` owns only Start-specific presentation and emits requests to the shell. New Drawing is the point at which the shell creates a real `CadWorkspaceSession`. File-open, recent-file, architecture-template, and urban-planning-template surfaces can exist before the corresponding file/template subsystems, but must stay honest placeholders rather than implementing parallel fake business logic.

### Settings architecture

`AppSettings` is the lightweight settings model and `SettingsService` is the single persistence boundary. Values are stored at `%LOCALAPPDATA%\UCAD\settings.json`; individual views do not scatter `ApplicationData.Current.LocalSettings.Values[...]` calls.

Settings covers General, Appearance, Drafting, Input & Interaction, Files & Save, Language & Region, and About. In v0.4.0, `DefaultObjectSnap`, `DefaultSnapTypes`, and `DefaultOrtho` are no longer future-only values: they initialize the `CadInteractionState` of newly created Drawing sessions. Existing sessions keep their current F3/F8 state rather than being overwritten by a default-setting edit.

App Theme and Canvas Theme are deliberately separate state domains. Existing viewport-backed options such as canvas background, grid visibility/opacity, cursor-centered zoom, middle-button pan, reverse wheel zoom, and selection preview flow through `SettingsService` into `CadViewport`.

### Localization

Legacy XAML resources remain in each locale's `Resources.resw`; Start/Settings use `UcadV039.resw`; imperative hot-refresh Shell strings use `ShellLive.resw`. zh-CN, ja-JP, and en-US maintain identical key sets, validated in CI.

`LocalizationService` uses an explicit MRT Core `ResourceContext` language qualifier, so switching language refreshes the existing Window/Start/Settings/Drawing surfaces without restarting the process or discarding `CadWorkspaceSession` state. v0.4 interaction/Inspector strings are part of the same hot-switchable `ShellLive` contract.

### Version SSOT

The repository root `VERSION` file is the product version SSOT. `Directory.Build.props` derives assembly/file/informational versions from it; `release/release.json` and MSIX identity are validated against the same value. Runtime UI reads assembly metadata through `AppVersionInfo` rather than hardcoding a third version string.

## Rendering and DPI

The viewport performs world/screen transforms, adaptive grid rendering, crosshair drawing, zoom/pan, persistent entity rendering, selection/snap feedback, and transient previews. Geometry and interaction calculations in `UCAD.Core` have no Win2D dependency.

UCAD declares `PerMonitorV2` and uses XAML DIP layout. The UI must not add manual bitmap scaling for 100/125/150/175/200% display scales. Figma fidelity remains a separate visual contract and is intentionally not a v0.4.0 release gate while Core interaction work is active.

## Validation boundary

CI requires:

- Core tests for command/document/entity plus v0.4 selection, Window/Crossing, curve hit tests, OSNAP/intersections, Ortho, and command lifecycle observation;
- WinUI app build;
- real startup smoke, which now creates a Drawing session and verifies Selection + OSNAP + Ortho + Inspector + capability-derived category state in the running application;
- MSIX / one-click packaging;
- language-key parity and representative translated values;
- version SSOT, PerMonitorV2, icon contracts, and frozen Figma-critical design tokens.

Pixel-level UI comparison remains manual/non-gating for this milestone.

## Milestone boundary

v0.4.0 freezes the first explicit shell/Core interaction contracts:

1. selection state belongs to `SelectionSet` / `CadInteractionState`, not ad-hoc XAML;
2. OSNAP and Ortho are reusable Core/interaction services surfaced by per-workspace status controls;
3. Inspector reads selected Core entities through the stable selection model;
4. tool/category enabled state derives from registered `CommandRegistry` capabilities;
5. all command invocations continue through `CommandRegistry` / `CommandSession`.

MOVE/COPY/ROTATE/TRIM/EXTEND/OFFSET and other Modify commands belong to v0.5.x. Architecture/GIS helpers remain out of scope until the drawing-editing loop is coherent.

# Architecture

UCAD is intentionally split into a UI-independent CAD core and a Windows-native presentation layer.

## `UCAD.Core`

Owns geometry, document state, entity models, command vocabulary, command-session state, parsers, and reversible document history.

### Entity model at v0.3.x

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

Browser-style tabs, inspector state, dirty indicators, and future selection/layer services can react to the document without routing state through `CadViewport`.

### Command foundation

- `CadCommandDefinition` — canonical command name plus aliases, UI-neutral category metadata, and optional `DrawingCommandKind`.
- `CommandRegistry` — case-insensitive command resolution and duplicate-token protection.
- `CommandSession` — active/previous command lifecycle, repeat, complete, cancel.
- `CommandInputParser` — numeric, absolute coordinate, and relative coordinate parsing.
- `DrawingCommandKind` — UI-independent drawing workflow vocabulary.
- `CadCommandCategory` — stable high-level command grouping used by the shell without encoding toolbar logic in Core.

Every command-capable UI surface resolves to the same `CommandRegistry → CommandSession → CAD Core` path. The shell must not maintain a second command implementation.

## `UCAD.App`

Owns WinUI 3 windowing, the Win2D viewport, keyboard routing, localized prompts, pointer interaction, live previews, workspace presentation, settings persistence, and MSIX integration.

### Shell and page model at v0.3.9

Figma is the visual SSOT and WinUI native controls are the interaction SSOT. `WorkspacePageKind` defines three explicit tab-content types:

- **Drawing** — the CAD workspace: Category Bar, Tool Shelf, Tool Rail, `CadViewport`, Inspector, Command Line, and Status Bar.
- **Start** — the long-lived new-tab / Start Center. The title-bar `+` selects or creates Start rather than immediately creating an empty CAD document.
- **Settings** — one reusable settings tab with its own navigation and content. CAD-only rails and bottom bars are covered rather than duplicated beside Settings navigation.

The title strip is an explicit `[Brand][Document Tab Strip][Drag Region][native caption buttons]` layout instead of relying on `TitleBar.Content` auto-arrangement. This keeps approximately 190×34 document tabs left-contiguous like a browser.

`CadWorkspaceSession` remains the application-level owner for one Drawing tab. Each session has its own:

- `CadDocument`
- `CadViewport`
- `CommandSession`
- typed-command base point
- pointer/status context

Tabs are therefore real independent in-memory CAD tasks, not presentation-only tabs. Start and Settings are shell pages, not fake `CadWorkspaceSession` objects.

### Start Center

`StartPage` owns only Start-specific presentation and emits requests to the shell. New Drawing is the point at which the shell creates a real `CadWorkspaceSession`. File-open, recent-file, architecture-template, and urban-planning-template surfaces can exist before the corresponding file/template subsystems, but must stay honest placeholders rather than implementing parallel fake business logic.

### Settings architecture

`AppSettings` is the lightweight settings model and `SettingsService` is the single persistence boundary. Values are stored at `%LOCALAPPDATA%\UCAD\settings.json`; individual views do not scatter `ApplicationData.Current.LocalSettings.Values[...]` calls.

Settings covers General, Appearance, Drafting, Input & Interaction, Files & Save, Language & Region, and About. Drafting aids that do not yet have Core support are persisted as future defaults but do not alter CAD behavior prematurely.

App Theme and Canvas Theme are deliberately separate state domains. Existing viewport-backed options such as canvas background, grid visibility/opacity, cursor-centered zoom, middle-button pan, and reverse wheel zoom flow through `SettingsService` into `CadViewport`. A Windows light theme therefore does not force the CAD canvas to become light.

The settings layout contract is centralized in `UcadDesignTokens.xaml`: 228-DIP navigation, 54-DIP content offset, 940×72 setting cards, and 35 / 12 / 8 / 30 DIP vertical rhythm. `SettingCard` is the reusable native-control composition for these rows.

### Localization

Legacy shell resources remain in each locale's `Resources.resw`; the v0.3.9 Start/Settings surface uses the parallel `UcadV039.resw` map. zh-CN, ja-JP, and en-US maintain identical key sets, validated in CI. A small resource-loader bridge lets the shell resolve legacy keys first and v0.3.9 keys second without copying strings into view code.

### Version SSOT

The repository root `VERSION` file is the product version SSOT. `Directory.Build.props` derives assembly/file/informational versions from it; `release/release.json` and MSIX identity are validated against the same value. Runtime UI reads assembly metadata through `AppVersionInfo` rather than hardcoding a third version string.

### Viewport

`CadViewport` is the drawing interaction coordinator and renderer. Mouse picks and typed coordinates converge on `SubmitDrawingPoint`, so the final entity model is independent of input method. LINE commits segments incrementally; PLINE commits one polyline when confirmed; RECTANGLE/CIRCLE/ARC auto-complete after the required points are valid.

The viewport receives a `CadDocument` rather than owning the only document in the application. It reports pointer and zoom changes, while committed document state is observed directly from Core.

## Rendering and DPI

The viewport performs world/screen transforms, adaptive grid rendering, crosshair drawing, zoom/pan, persistent entity rendering, and transient previews. Geometry code in `UCAD.Core` has no Win2D dependency.

UCAD declares `PerMonitorV2` and uses XAML DIP layout. The UI must not add manual bitmap scaling for 100/125/150/175/200% display scales. Figma fidelity is checked at a deterministic 1440×900 window while PMv2 remains a separate runtime/manifest contract.

## Validation boundary

CI validates Core tests, WinUI build, real startup, MSIX/one-click packaging, language-key parity, version SSOT, PerMonitorV2, removal of Unicode placeholder icons, and Figma-critical design tokens. A dedicated UI-fidelity workflow starts the real executable at 1440×900 and captures Drawing, Start, Settings General, Appearance, Input & Interaction, and About for visual comparison with the Figma frames.

## v0.4 coupling rule

v0.4 is the first milestone where the completed shell and CAD Core become deliberately more coupled through explicit, reusable contracts:

1. selection state belongs to a document/workspace service, not ad-hoc XAML controls;
2. OSNAP and Ortho are Core/interaction state surfaced by the existing status bar;
3. inspector content reads selected Core entities through a stable selection model;
4. tool/category enabled state is derived from registered capabilities;
5. all command invocations continue through `CommandRegistry` / `CommandSession`.

Selection/OSNAP/Ortho belongs to v0.4 and modify commands belong to v0.5. Architecture/GIS helpers remain out of scope until the v0.5 drawing-editing loop is coherent.

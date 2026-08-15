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

v0.3.5 adds a narrow observable boundary to the document:

- `CadDocument.Changed` publishes committed mutations, Undo, and Redo.
- `CadDocument.Revision` monotonically identifies document-state changes.
- `CadDocumentChangedEventArgs` exposes change kind and entity count without referencing WinUI.

This boundary is deliberately UI-agnostic. Browser-style tabs, inspector state, dirty indicators, and future selection/layer services can react to the document without routing state through `CadViewport`.

### Command foundation

- `CadCommandDefinition` — canonical command name plus aliases, UI-neutral category metadata, and optional `DrawingCommandKind`.
- `CommandRegistry` — case-insensitive command resolution and duplicate-token protection.
- `CommandSession` — active/previous command lifecycle, repeat, complete, cancel.
- `CommandInputParser` — numeric, absolute coordinate, and relative coordinate parsing.
- `DrawingCommandKind` — UI-independent drawing workflow vocabulary.
- `CadCommandCategory` — stable high-level command grouping used by the shell without encoding toolbar logic in Core.

Every UI surface (tool shelf, left rail, search, menu, and command line) resolves to the same command registry/session path. The shell must not maintain a second command implementation.

## `UCAD.App`

Owns WinUI 3 windowing, the Win2D viewport, keyboard routing, localized prompts, pointer interaction, live previews, workspace presentation, and MSIX integration.

### Workspace shell at v0.3.5

The WinUI shell follows the Figma-approved Fluent CAD workspace:

- WinUI `TitleBar` + browser-style `TabView` for document tasks.
- category bar plus persistent tool shelf.
- compact left rail for highest-frequency commands.
- central solid CAD canvas.
- right inspector boundary.
- bottom command line and status bar.

`CadWorkspaceSession` is the application-level owner for one tab. Each session has its own:

- `CadDocument`
- `CadViewport`
- `CommandSession`
- typed-command base point
- pointer/status context

Tabs are therefore real independent in-memory CAD tasks, not presentation-only tabs. Switching tabs preserves geometry, command history, transient drawing state, zoom/pan, and previous-command state.

The v0.3.5 inspector intentionally does **not** fake selection or layer features. Until v0.4 selection exists, it shows active-document state (entity count, active command, Undo/Redo availability). Future command categories remain visible but disabled so later milestones can connect Core capabilities without redesigning the shell.

### Viewport

`CadViewport` is the drawing interaction coordinator and renderer. Mouse picks and typed coordinates converge on `SubmitDrawingPoint`, so the final entity model is independent of input method. LINE commits segments incrementally; PLINE commits one polyline when confirmed; RECTANGLE/CIRCLE/ARC auto-complete after the required points are valid.

The viewport receives a `CadDocument` rather than owning the only document in the application. It reports pointer and zoom changes, while committed document state is observed directly from Core.

## Rendering

The viewport performs world/screen transforms, adaptive grid rendering, crosshair drawing, zoom/pan, persistent entity rendering, and transient previews. Geometry code in `UCAD.Core` has no Win2D dependency.

## v0.4 coupling rule

v0.4 is the first milestone where the new shell and CAD Core become deliberately more coupled through explicit, reusable contracts:

1. selection state belongs to a document/workspace service, not ad-hoc XAML controls;
2. OSNAP and Ortho are Core/interaction state surfaced by the existing status bar;
3. inspector content reads selected Core entities through a stable selection model;
4. tool/category enabled state is derived from registered capabilities;
5. all command invocations continue through `CommandRegistry` / `CommandSession`.

Selection/OSNAP/Ortho belongs to v0.4 and modify commands belong to v0.5. Architecture/GIS helpers remain out of scope until the v0.5 drawing-editing loop is coherent.

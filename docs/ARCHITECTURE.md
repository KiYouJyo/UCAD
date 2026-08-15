# Architecture

UCAD is intentionally split into a UI-independent CAD core and a Windows-native presentation layer.

## `UCAD.Core`

Owns geometry, document state, entity models, command vocabulary, command-session state, parsers, and reversible document history.

### Entity model at v0.3

- `LineEntity` — two-point line segment.
- `PolylineEntity` — ordered vertices with optional closed topology; RECTANGLE commits as a closed polyline.
- `CircleEntity` — center and positive radius.
- `ArcEntity` — center/radius/start/sweep representation constructed from three picked points. Rendering samples the canonical arc rather than storing a display-only approximation.

### Document history

`CadDocument` snapshots the immutable entity list at each committed mutation. Undo/Redo therefore lives below WinUI and can be reused by later MOVE/COPY/TRIM/OFFSET commands. A future large-drawing milestone may replace snapshots with operation deltas if profiling justifies it.

### Command foundation

- `CadCommandDefinition` — canonical command name plus aliases.
- `CommandRegistry` — case-insensitive command resolution and duplicate-token protection.
- `CommandSession` — active/previous command lifecycle, repeat, complete, cancel.
- `CommandInputParser` — numeric, absolute coordinate, and relative coordinate parsing.
- `DrawingCommandKind` — UI-independent drawing workflow vocabulary.

## `UCAD.App`

Owns WinUI 3 windowing, the Win2D viewport, keyboard routing, localized prompts, pointer interaction, live previews, and MSIX integration.

`CadViewport` is the drawing interaction coordinator. Mouse picks and typed coordinates converge on `SubmitDrawingPoint`, so the final entity model is independent of input method. LINE commits segments incrementally; PLINE commits one polyline when confirmed; RECTANGLE/CIRCLE/ARC auto-complete after the required points are valid.

## Rendering

The viewport performs world/screen transforms, adaptive grid rendering, crosshair drawing, zoom/pan, persistent entity rendering, and transient previews. Geometry code in `UCAD.Core` has no Win2D dependency.

## Boundary rule

Selection/OSNAP belongs to v0.4 and modify commands belong to v0.5. Architecture/GIS helpers remain out of scope until the v0.5 drawing-editing loop is coherent.

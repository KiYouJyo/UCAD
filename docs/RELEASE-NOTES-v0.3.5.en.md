# UCAD v0.3.5 — Workspace Shell Foundation

v0.3.5 brings the approved Fluent CAD workspace from Figma into the existing CAD Core. This release intentionally does not expand the drawing-command count; instead, it places the v0.3 capabilities inside a durable desktop workspace designed to carry v0.4 and v0.5.

## New workspace

- Browser-style multi-document tabs in the title bar.
- Every tab is a real independent in-memory CAD session with its own `CadDocument`, `CadViewport`, `CommandSession`, zoom/pan state, and command context.
- A category bar plus persistent tool shelf: choosing Draw keeps the detailed drawing shelf open until the active category is clicked again.
- A compact high-frequency tool rail on the left, Inspector boundary on the right, and command/status bars at the bottom.
- Command search is generated directly from `CommandRegistry` rather than a second UI-only command list.

## Existing capabilities now connected

- LINE / L
- PLINE / PL
- RECTANGLE / REC
- CIRCLE / C
- ARC / A
- UNDO / U
- REDO
- CLEAR
- RESETVIEW / RV
- combined mouse and typed-coordinate input
- `x,y`, `@x,y`, and distance input
- Enter / Space confirmation, Esc cancellation, repeat previous command
- adaptive grid, crosshair, zoom, and pan

## Core contracts added for v0.4

- `CadCommandDefinition` now exposes `CadCommandCategory` plus optional `DrawingCommandKind`, so UI code no longer identifies drawing workflows with command-name string switches.
- `CadDocument` now exposes `Changed`, `Revision`, and structured change events, allowing tabs, inspector state, and history controls to observe Core directly.
- `CadViewport` can receive an externally owned `CadDocument`, removing the old implicit one-window/one-document assumption.
- `CadWorkspaceSession` explicitly groups the Core document, viewport, and command context owned by one task tab.

These contracts are the intended attachment points for v0.4 selection, OSNAP, Ortho, and selection-backed properties.

## Intentionally disabled surfaces

The new shell already reserves the correct locations for selection, MOVE/COPY/OFFSET/TRIM, layers, HATCH, OSNAP, ORTHO, and other planned capabilities. v0.3.5 does not pretend these features exist: their controls remain visible but disabled until the corresponding Core capability is implemented.

## Localization

The new shell is covered in:

- Simplified Chinese (zh-CN)
- Japanese (ja-JP)
- English (en-US)

## Note

The v0.3.5 document tabs are currently in-memory workspaces. File open/save is not implemented yet, so closing a tab containing drawing content explicitly warns that the content will be discarded.

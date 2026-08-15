# UCAD v0.2.0 — Command Foundation

v0.2 moves UCAD from a drawing technology preview to an application with a real CAD-style command interaction foundation.

## Added

- Bottom command line UI.
- Command registry with aliases; `LINE` supports the familiar `L` alias.
- Enter / Space confirmation.
- Esc cancellation.
- Repeat previous command with empty Enter / Space input.
- Absolute `x,y` coordinates.
- Relative `@x,y` coordinates.
- Numeric distance input along the current cursor direction after a base point exists.
- Mouse picks and typed coordinates converge on the same LINE point-submission path.
- `CLEAR` and `RESETVIEW` are routed through the same registry.
- Core tests for alias resolution, command lifecycle, numeric parsing, and coordinate parsing.

## Example

```text
Command: L
0,0
@5000,0
@0,3600
[Enter]
```

You can also start `LINE`, pick points with the mouse, and finish with Enter, Space, or Esc.

## Installation

Download `UCAD-v0.2.0-x64-one-click.zip`. A one-time UAC prompt is used only when the public release certificate first needs to be trusted; the signed MSIX install itself continues in the normal user context.

## Next

v0.3 will build LINE, PLINE, RECTANGLE, CIRCLE, ARC, and Undo / Redo on top of this command foundation.

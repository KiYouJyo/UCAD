# UCAD v0.1.0 — Foundation Release

UCAD v0.1.0 establishes the first runnable technical foundation for a lightweight 2D CAD focused on urban planning and architecture.

## Highlights

- WinUI 3 desktop shell for Windows.
- Win2D GPU-accelerated CAD viewport.
- Adaptive drafting grid, origin axes, and full-window crosshair.
- Cursor-centered mouse-wheel zoom and middle-button pan.
- World/screen coordinate conversion.
- Independent `UCAD.Core` document and geometry layer.
- Basic `CadPoint`, `CadVector`, `LineEntity`, and `CadDocument` types.
- Interactive two-point line drawing.
- Core unit tests and Windows CI foundation.
- Automated x64 GitHub Release packaging.

## Scope

This is an early foundation release, not yet a production CAD application. DXF/DWG compatibility, command-line CAD interaction, selection, OSNAP, modification tools, layers, annotation, and planning-specific workflows are planned for later milestones.

## Next milestone

v0.2 will focus on the command foundation: command aliases and an Enter/Esc/Space-driven CAD state machine.

# UCAD v0.1.0 — Foundation Release

UCAD v0.1.0 establishes the first runnable technical foundation for a lightweight 2D CAD focused on urban planning and architecture.

## Highlights

- Native WinUI 3 Windows shell with a Win2D GPU-accelerated CAD viewport.
- Adaptive grid, origin axes, and full-window crosshair.
- Cursor-centered mouse-wheel zoom and middle-button pan.
- World/screen coordinate conversion.
- Independent `UCAD.Core` geometry and document layer.
- Basic `CadPoint`, `CadVector`, `LineEntity`, and `CadDocument` types.
- Interactive two-point line drawing.
- Simplified Chinese, Japanese, and English resource foundation.
- Windows CI, MSIXBundle packaging, signing, and GitHub Release automation.

## Installation

For most users, download `UCAD-v0.1.0-x64-one-click.zip`, extract it, and run `① 安装UCAD.cmd`. The bootstrap verifies SHA-256 and the release certificate before installing the signed `UCAD_0.1.0.0_x64.msixbundle`.

## Current scope

This is a foundation release, not yet a production CAD application. DXF/DWG compatibility, AutoCAD-style command interaction, selection, OSNAP, modification tools, layers, annotation, and planning-specific workflows are planned for later milestones.

## Next milestone

v0.2 will focus on command aliases and an Enter / Esc / Space-driven CAD command state machine.

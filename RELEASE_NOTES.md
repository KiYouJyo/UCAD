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

## Installation

For most users, download **`UCAD-v0.1.0-x64-one-click.zip`**, extract it, and double-click **`① 安装UCAD.cmd`**.

The one-click installer follows the same lightweight bootstrap pattern used by UrbanPlanToolbox: it downloads the official x64 application archive from this Release, verifies it against `SHA256SUMS.txt`, installs UCAD for the current Windows user under `%LOCALAPPDATA%\Programs\UCAD`, creates Start menu and desktop shortcuts, and starts UCAD. It does not require administrator privileges, certificate installation, or Microsoft Store.

Advanced users can still download **`UCAD-v0.1.0-win-x64.zip`** directly as the portable/self-contained application archive. Release asset hashes are published in **`SHA256SUMS.txt`**.

## Scope

This is an early foundation release, not yet a production CAD application. DXF/DWG compatibility, command-line CAD interaction, selection, OSNAP, modification tools, layers, annotation, and planning-specific workflows are planned for later milestones.

## Next milestone

v0.2 will focus on the command foundation: command aliases and an Enter/Esc/Space-driven CAD state machine.

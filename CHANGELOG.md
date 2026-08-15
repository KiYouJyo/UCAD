# Changelog

## 0.2.0 — 2026-08-15

### Command foundation
- Added a UI-independent command registry with case-insensitive aliases and session state.
- Added AutoCAD-style Enter / Space confirmation, Esc cancellation, and repeat-previous-command behavior.
- Added absolute `x,y`, relative `@x,y`, and numeric distance parsing.
- Connected `LINE` / `L` to mouse and typed point input through the same command path.
- Added command-system unit tests and multilingual command prompts.

### Release infrastructure
- Added per-version Release titles to `release/release.json` so GitHub Releases are no longer hard-coded as Foundation Release.

## 0.1.0 — 2026-08-15

### Foundation
- Established WinUI 3 / Win2D application and independent `UCAD.Core` geometry/document layer.
- Added grid, crosshair, coordinate transforms, zoom, pan, and two-point Line drawing.

### Packaging and release
- Converted GitHub distribution from unpackaged portable deployment to signed x64 MSIXBundle.
- Added one-time UAC certificate trust setup and normal-user MSIX installation flow.
- Added SHA-256 release manifests and release-asset validation.
- Added `release/release.json` as release metadata SSOT.

### Repository
- Added Simplified Chinese, Japanese, and English README / Release Notes structure.
- Added zh-CN, ja-JP, and en-US application resources.
- Added architecture, release-process, privacy, support, and third-party documentation.

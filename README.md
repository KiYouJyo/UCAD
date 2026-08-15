# UCAD

**Urban Computer-Aided Design**  
**都市計画支援CAD（アーバンCAD）**  
*Lightweight 2D CAD for Urban Planning & Architecture*

[![CI](https://github.com/KiYouJyo/UCAD/actions/workflows/ci.yml/badge.svg)](https://github.com/KiYouJyo/UCAD/actions/workflows/ci.yml)
[![Release](https://github.com/KiYouJyo/UCAD/actions/workflows/release.yml/badge.svg)](https://github.com/KiYouJyo/UCAD/actions/workflows/release.yml)
![License](https://img.shields.io/badge/license-GPL--2.0--only-blue.svg)

> **v0.1 · Foundation Preview** — UCAD is at the technical-foundation stage. It is not yet a production CAD application and does not yet claim DXF/DWG compatibility.

UCAD is an open-source Windows desktop project exploring a lightweight 2D CAD workflow for architecture and urban planning. The long-term direction is a modern WinUI 3 interface with familiar command-driven CAD interaction, a focused planning/architecture feature set, and a rendering/core architecture that can grow without turning the application into a multi-gigabyte general-purpose CAD suite.

## v0.1 scope

The first preview intentionally proves the minimum drawing stack:

- WinUI 3 desktop shell
- Win2D GPU-accelerated drawing surface
- world/screen coordinate transform
- adaptive drafting grid and origin axes
- full-window crosshair
- mouse-wheel zoom around cursor
- middle-button pan
- basic `LineEntity` document model
- two-point interactive line drawing
- independent `UCAD.Core` project and unit tests
- CI and tag-driven GitHub Release workflow

## Architecture

```text
UCAD.App (WinUI 3)
  └─ CadViewport (Win2D)
       └─ UCAD.Core
            ├─ Geometry
            ├─ Entities
            └─ CadDocument
```

The UI layer does not store CAD geometry. `UCAD.Core` owns document data so future rendering, DXF I/O, command handling, snapping, selection and geometry algorithms can evolve independently.

## Build

Requirements:

- Windows 10 1809 or newer
- Visual Studio 2026 with Windows application development workload
- .NET 10 SDK
- x64 or ARM64 target

Open `UCAD.sln`, select `x64`, and build `UCAD.App`.

The current preview uses Windows App SDK `1.8.260710003` and Win2D `1.4.0`.

## Controls in v0.1

| Input | Action |
|---|---|
| Mouse wheel | Zoom around cursor |
| Middle mouse drag | Pan |
| **Line** button | Enter/leave line mode |
| Left click in line mode | Set points / continue line chain |
| **Clear** | Clear drawing entities |
| **Reset view** | Reset zoom and pan |

## Roadmap

The next milestones are deliberately ordered around CAD interaction quality rather than feature count:

1. **v0.2 — Command Foundation:** command line, aliases, Enter/Esc/Space state machine.
2. **v0.3 — Drawing:** LINE/PLINE/RECTANGLE/CIRCLE/ARC.
3. **v0.4 — Interaction:** selection and OSNAP.
4. **v0.5 — Modify:** MOVE/COPY/OFFSET/TRIM/EXTEND.
5. Later: layers/properties, annotation, architecture helpers, planning/GIS workflows and DXF-first file exchange.

See [`ROADMAP.md`](ROADMAP.md) for details.

## Project principles

- **2D first.** No 3D/BIM scope in the 1.x line.
- **CAD muscle memory first.** Commands and pointer interaction matter more than ribbon density.
- **DXF first.** Future DWG support must not block the core product.
- **Planning & architecture focused.** Features must justify themselves against real drafting/planning workflows.
- **Performance by architecture.** CAD entities are drawn by a dedicated immediate-mode renderer, not thousands of XAML shapes.
- **Offline first.** Core drawing work should not depend on cloud services.

## License

UCAD is licensed under the GNU General Public License v2.0. See [`LICENSE`](LICENSE).

This repository currently contains original UCAD code only. If code from LibreCAD or other GPL projects is incorporated later, source provenance and attribution must be recorded explicitly.

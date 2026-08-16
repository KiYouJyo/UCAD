[简体中文](README.md) | [日本語](README.ja.md) | [English](README.en.md)

# UCAD

**Urban Computer-Aided Design · 都市計画支援CAD**  
Lightweight 2D CAD for urban planning and architectural design.

![Windows](https://img.shields.io/badge/Windows-10%2F11-blue) ![Windows App SDK](https://img.shields.io/badge/Windows%20App%20SDK-1.8-blue) ![MSIX](https://img.shields.io/badge/package-MSIX-blue) ![CI](https://github.com/KiYouJyo/UCAD/actions/workflows/ci.yml/badge.svg) ![License](https://img.shields.io/badge/license-GPL--2.0--only-blue)

## Current candidate

UCAD targets a Windows-native, AutoCAD-familiar, deliberately bounded 2D-first / DXF-first CAD for architecture and urban planning.

**Current acceptance candidate: v0.7.0 — CAD Authoring Foundation.** It combines v0.5 Modify, v0.6 Layers & Properties, and v0.7 Annotation / Hatch / Blocks into one manual acceptance cycle.

The accepted v0.4.1 baseline remains intact: two-point/drag Window-Crossing selection, Shift removal, transparent Windows pointer + Win2D CAD cursor, F3 OSNAP, F8 Ortho, ERASE, Inspector, and per-document isolation.

## v0.5 — Modify

`MOVE (M)`, `COPY (CO/CP)`, `ROTATE (RO)`, `SCALE (SC)`, `MIRROR (MI)`, `OFFSET (O)`, `TRIM (TR)`, and `EXTEND (EX)` are implemented through one command/document pipeline. Both preselection→command and command→selection→Enter workflows are supported, with OSNAP, applicable Ortho, transient previews, and unified Undo/Redo.

## v0.6 — Layers & Properties

- document layer table with protected layer `0`
- current-layer inheritance plus create / rename / delete / set-current workflows
- visibility, lock, color, lineweight, and linetype metadata
- per-entity Layer / Color / Lineweight / Linetype overrides with ByLayer inheritance
- hidden layers are excluded from drawing and OSNAP; hidden/locked layers are excluded from selection and Modify picking
- `LAYER / LA` and `CHPROP / CH`
- layer/property state participates in document Undo/Redo

## v0.7 — Annotation, Hatch & Blocks

- `TEXT / T`: single-line text
- `DIM / DLI / DIMLINEAR`: foundational aligned linear dimensions
- `HATCH / H`: Solid hatch from one selected closed Polyline or Circle
- `BLOCK / B`: define a reusable block from the current selection
- `INSERT / I`: insert a block reference with scale, rotation, and picked insertion point
- `EXPLODE / X`: explode one block reference as a single undoable mutation

Text / Dimension / Hatch / Block Reference entities participate in shared rendering, selection geometry, grips, intersection/OSNAP queries, and Modify transforms.

## Commands

| Category | Commands |
| --- | --- |
| Draw | `LINE`, `PLINE`, `RECTANGLE`, `CIRCLE`, `ARC`, `HATCH` |
| Modify | `MOVE`, `COPY`, `ROTATE`, `SCALE`, `MIRROR`, `OFFSET`, `TRIM`, `EXTEND`, `EXPLODE` |
| Annotate | `TEXT`, `DIM` |
| Layers / Properties | `LAYER`, `CHPROP` |
| Blocks | `BLOCK`, `INSERT` |
| Edit / View | `ERASE`, `UNDO`, `REDO`, `CLEAR`, `RESETVIEW` |

All working command entry points remain unified through `CommandRegistry → CommandSession → CadWorkspaceSession / CadDocument`.

## Localization / Validation

Simplified Chinese, Japanese, and English still switch **without restarting UCAD** through the explicit MRT Core `ResourceContext`; the new authoring dialogs and prompts use the same live language context.

v0.7.0 acceptance requires Core tests, app build, startup-smoke, Interaction Smoke, Localization Smoke, Modify Smoke, Authoring Smoke, and MSIX/one-click package validation together.

## Next

From v0.8 onward: DXF-first import/export, print/PDF, architectural helpers, planning parcels/indicators, GIS exchange, and large-drawing performance regression. 3D/BIM, rendering, point clouds, and full DWG/AutoCAD plug-in compatibility remain outside the 1.x target.

## Documents

- [Roadmap](ROADMAP.md)
- [Architecture](docs/ARCHITECTURE.md)
- [Release process](docs/RELEASE-PROCESS.md)
- [v0.7.0 Release Notes](docs/RELEASE-NOTES-v0.7.0.en.md)

## License

UCAD is released under **GPL-2.0-only**.

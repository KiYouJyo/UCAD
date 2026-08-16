[简体中文](README.md) | [日本語](README.ja.md) | [English](README.en.md)

# UCAD

**Urban Computer-Aided Design · 都市計画支援CAD**  
Lightweight 2D CAD for urban planning and architectural design.

![Windows](https://img.shields.io/badge/Windows-10%2F11-blue) ![Windows App SDK](https://img.shields.io/badge/Windows%20App%20SDK-1.8-blue) ![MSIX](https://img.shields.io/badge/package-MSIX-blue) ![CI](https://github.com/KiYouJyo/UCAD/actions/workflows/ci.yml/badge.svg) ![License](https://img.shields.io/badge/license-GPL--2.0--only-blue)

## Positioning

UCAD aims to be a Windows-native lightweight 2D CAD with familiar AutoCAD-style interaction and a deliberately bounded architecture/planning feature set. The project is DXF-first and 2D-first.

**Current candidate: v0.5.0 — Modify Foundation.** Rather than expanding UI, this milestone builds the first real Modify family on the v0.4.x Selection / OSNAP / Ortho foundation: MOVE / COPY / ROTATE / SCALE / MIRROR / OFFSET / TRIM / EXTEND, backed by shared geometry transforms, undoable edit transactions, and transient input previews.

## Installation

Download production builds from [GitHub Releases](https://github.com/KiYouJyo/UCAD/releases/latest): `UCAD-v<version>-x64-one-click.zip`, `UCAD_<packageVersion>_x64.msixbundle`, and `SHA256SUMS.txt`.

## Workspace pages

- **Drawing**: Category Bar, Tool Shelf, Tool Rail, CAD Canvas, Inspector, Command Line, and Status Bar.
- **Start**: the CAD new-tab / Start Center. By default `+` opens Start; a real `CadWorkspaceSession` is created by New Drawing. If “show Start on new tab” is disabled, `+` creates a blank Drawing directly.
- **Settings**: one reusable settings tab without CAD-only rails/panels.

Each Drawing owns an independent `CadDocument`, `CadInteractionState`, `CadViewport`, and `CommandSession`, so geometry, history, selection, OSNAP, Ortho, command, and viewport state are isolated per tab.

## CAD interaction

- Click entities to build an additive selection; Shift + pick/window removes from the current set.
- Click empty space for the first Window/Crossing corner, release, move to preview, and click again to commit. Press-drag-release selection remains available.
- Left-to-right is **Window** (fully contained only); right-to-left is **Crossing** (contained or intersecting).
- An empty completed non-Shift window clears the selection set. Esc cancels an unfinished window before clearing completed selection.
- The drawing canvas suppresses the native Windows pointer and renders only the Win2D CAD crosshair + central pickbox. Crosshair, Pickbox, and OSNAP aperture are live-adjustable under Settings → Input & Interaction → CAD cursor.
- **F3 / OSNAP status** toggles Endpoint / Midpoint / Center / Intersection object snap.
- **F8 / ORTHO status** toggles horizontal/vertical constraint for LINE / PLINE and applicable Modify point input.
- **Delete / ERASE / E / DELETE** erases the current selection as one undoable mutation.
- Inspector reads real selected Line / Polyline / Circle / Arc entities and reports type, count, basic geometry, and entity ID.

## v0.5.0 Modify

- Supports both preselection and command-first selection followed by Enter confirmation.
- MOVE / COPY use a base point + second point; canvas input supports OSNAP and F8 Ortho.
- ROTATE accepts a picked direction or numeric command-line angle in degrees after the base point.
- SCALE accepts a positive numeric factor or a picked point after the base point.
- MIRROR defines an axis with two points; source objects are kept by default and can optionally be erased.
- OFFSET uses distance → entity → side point and supports the foundational Line / Polyline / Circle / Arc cases.
- TRIM / EXTEND use quick-mode behavior where other visible entities act as boundaries; target picks can be repeated until Enter.
- Transform commands and OFFSET render transient previews.
- Identity-preserving edits keep existing entity IDs; COPY, keep-source MIRROR, OFFSET, and other generated entities receive fresh IDs.
- `CadDocument.Replace` / `ReplaceRange` make Modify mutations one-step Undo transactions.

## Commands

| Command | Alias | Purpose |
| --- | --- | --- |
| `LINE` | `L` | continuous line |
| `PLINE` | `PL` | polyline |
| `RECTANGLE` | `REC` | two-corner rectangle |
| `CIRCLE` | `C` | center/radius circle |
| `ARC` | `A` | three-point arc |
| `MOVE` | `M` | move selection |
| `COPY` | `CO`, `CP` | copy selection |
| `ROTATE` | `RO` | rotate around base point |
| `SCALE` | `SC` | scale around base point |
| `MIRROR` | `MI` | mirror about a two-point axis |
| `OFFSET` | `O` | offset by distance and side point |
| `TRIM` | `TR` | quick trim |
| `EXTEND` | `EX` | quick extend |
| `ERASE` | `E`, `DELETE` | erase current selection |
| `UNDO` | `U` | undo |
| `REDO` | — | redo |
| `CLEAR` | — | clear drawing |
| `RESETVIEW` | `RV` | reset view |

All working command entry points converge on `CommandRegistry → CommandSession → CAD Core`. Modify is now a real capability-derived category; Annotate / Layer / Block / Measure remain reserved until their Core capability exists.

## Settings, display, and localization

UCAD remains `PerMonitorV2`, with Figma critical design tokens protected by CI even though pixel-level UI comparison is not a release gate for the current functional milestone. App Theme and CAD Canvas Theme remain independent.

Drafting defaults — object snap, snap set, and ortho — initialize newly created Drawing sessions; existing Drawing sessions keep their current F3/F8 state. Settings persist through `SettingsService` / `AppSettings` at `%LOCALAPPDATA%\UCAD\settings.json`.

Simplified Chinese, Japanese, and English continue to switch **without restarting UCAD** through the explicit MRT Core `ResourceContext`; the current window, Start, Settings, document tabs, menus, Inspector, command area, Modify phase prompts, and status bar refresh in place without rebuilding existing drawing sessions.

## Development

```powershell
dotnet restore src/UCAD.App/UCAD.App.csproj -p:Platform=x64 -r win-x64
dotnet build src/UCAD.App/UCAD.App.csproj -c Debug -p:Platform=x64 -r win-x64
dotnet test tests/UCAD.Core.Tests/UCAD.Core.Tests.csproj -c Release
```

Required CI covers Core tests, app build, real startup smoke, MSIX/package validation, localization parity, version SSOT, PerMonitorV2, and Figma-token contracts. Interaction Smoke validates Selection + ERASE + OSNAP + Ortho + Inspector in a running UCAD process. Localization Smoke switches zh-CN → ja-JP → en-US in one running process. **Modify Smoke** executes MOVE + COPY + ROTATE + SCALE + MIRROR + OFFSET + TRIM + EXTEND in one real UCAD process.

## Documentation

- [Roadmap](ROADMAP.md)
- [Architecture](docs/ARCHITECTURE.md)
- [Release process](docs/RELEASE-PROCESS.md)
- [v0.5.0 Release Notes](docs/RELEASE-NOTES-v0.5.0.en.md)

## License

UCAD is released under **GPL-2.0-only**.
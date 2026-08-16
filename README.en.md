[简体中文](README.md) | [日本語](README.ja.md) | [English](README.en.md)

# UCAD

**Urban Computer-Aided Design · 都市計画支援CAD**  
Lightweight 2D CAD for urban planning and architectural design.

![Windows](https://img.shields.io/badge/Windows-10%2F11-blue) ![Windows App SDK](https://img.shields.io/badge/Windows%20App%20SDK-1.8-blue) ![MSIX](https://img.shields.io/badge/package-MSIX-blue) ![CI](https://github.com/KiYouJyo/UCAD/actions/workflows/ci.yml/badge.svg) ![License](https://img.shields.io/badge/license-GPL--2.0--only-blue)

## Positioning

UCAD aims to be a Windows-native lightweight 2D CAD with familiar AutoCAD-style interaction and a deliberately bounded architecture/planning feature set. The project is DXF-first and 2D-first.

**Current candidate: v0.4.1 — CAD Selection & Cursor Interaction Refinement.** Pixel-level UI tuning remains paused. This release focuses on mouse interaction: two-click and drag Window/Crossing selection, Shift removal, layered Esc behavior, and an adjustable CAD crosshair, pickbox, and OSNAP aperture.

## Installation

Download production builds from [GitHub Releases](https://github.com/KiYouJyo/UCAD/releases/latest): `UCAD-v<version>-x64-one-click.zip`, `UCAD_<packageVersion>_x64.msixbundle`, and `SHA256SUMS.txt`.

## Workspace pages

- **Drawing**: Category Bar, Tool Shelf, Tool Rail, CAD Canvas, Inspector, Command Line, and Status Bar.
- **Start**: the CAD new-tab / Start Center. By default `+` opens Start; a real `CadWorkspaceSession` is created by New Drawing. If “show Start on new tab” is disabled, `+` creates a blank Drawing directly.
- **Settings**: one reusable settings tab without CAD-only rails/panels.

Each Drawing owns an independent `CadDocument`, `CadInteractionState`, `CadViewport`, and `CommandSession`, so geometry, history, selection, OSNAP, Ortho, command, and viewport state are isolated per tab.

## v0.4.1 CAD interaction

- Click an entity to add it to the current selection; continue clicking to build an additive multi-selection.
- **Shift + click** removes an entity from the current selection.
- Click empty space to define the first corner, release the mouse, move to preview the window, then click again to define the opposite corner. Press-drag-release window selection remains available too.
- Finish to the right for **Window** selection (fully contained objects only); finish to the left for **Crossing** selection (contained or intersecting objects).
- Hold Shift while committing a window to remove matching objects. Completing an empty window clears the current selection set.
- Esc first cancels an unfinished two-point window; with no active window, Esc clears the completed selection.
- Selection preview, selected highlighting, and grips are viewport presentation of Core-owned `SelectionSet` state.
- The drawing surface uses a CAD crosshair with a central pickbox. Crosshair length, pickbox size, and OSNAP aperture can be adjusted live under Settings → Input & Interaction.
- **F3 / OSNAP status** toggles Endpoint / Midpoint / Center / Intersection object snap.
- **F8 / ORTHO status** toggles horizontal/vertical constraint for LINE / PLINE mouse input.
- **Delete / ERASE / E / DELETE** erases the current selection as one undoable mutation.
- Inspector reads real selected Line / Polyline / Circle / Arc entities and reports type, count, basic geometry, and entity ID.

## Commands

| Command | Alias | Purpose |
| --- | --- | --- |
| `LINE` | `L` | continuous line |
| `PLINE` | `PL` | polyline |
| `RECTANGLE` | `REC` | two-corner rectangle |
| `CIRCLE` | `C` | center/radius circle |
| `ARC` | `A` | three-point arc |
| `ERASE` | `E`, `DELETE` | erase current selection |
| `UNDO` | `U` | undo |
| `REDO` | — | redo |
| `CLEAR` | — | clear drawing |
| `RESETVIEW` | `RV` | reset view |

All working command entry points converge on `CommandRegistry → CommandSession → CAD Core`. Tool-category availability derives from registered Core capabilities. MOVE / COPY / ROTATE / OFFSET / TRIM and other Modify commands remain v0.5.x work.

## Settings, display, and localization

UCAD remains `PerMonitorV2`, with Figma critical design tokens protected by CI even though pixel-level UI comparison is not a v0.4.1 release gate. App Theme and CAD Canvas Theme remain independent. Selection preview and existing viewport preferences are runtime settings.

Crosshair length, pickbox size, and OSNAP aperture apply immediately to existing Drawing sessions. Drafting defaults — object snap, snap set, and ortho — initialize newly created Drawing sessions; existing Drawing sessions keep their current F3/F8 state. Settings persist through `SettingsService` / `AppSettings` at `%LOCALAPPDATA%\UCAD\settings.json`.

Simplified Chinese, Japanese, and English continue to switch **without restarting UCAD** through the explicit MRT Core `ResourceContext`; the current window, Start, Settings, document tabs, menus, Inspector, command area, and status bar refresh in place without rebuilding existing drawing sessions.

## Development

```powershell
dotnet restore src/UCAD.App/UCAD.App.csproj -p:Platform=x64 -r win-x64
dotnet build src/UCAD.App/UCAD.App.csproj -c Debug -p:Platform=x64 -r win-x64
dotnet test tests/UCAD.Core.Tests/UCAD.Core.Tests.csproj -c Release
```

Required CI covers Core tests, app build, real startup smoke, MSIX/package validation, localization parity, version SSOT, PerMonitorV2, icon/Figma-token contracts, and interaction contracts. Interaction Smoke validates Selection + ERASE + OSNAP + Ortho + Inspector in a running process. Localization Smoke still switches zh-CN → ja-JP → en-US inside one running process.

## Documentation

- [Roadmap](ROADMAP.md)
- [Architecture](docs/ARCHITECTURE.md)
- [Release process](docs/RELEASE-PROCESS.md)
- [v0.4.1 Release Notes](docs/RELEASE-NOTES-v0.4.1.en.md)

## License

UCAD is released under **GPL-2.0-only**.

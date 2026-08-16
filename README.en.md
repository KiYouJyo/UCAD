[简体中文](README.md) | [日本語](README.ja.md) | [English](README.en.md)

# UCAD

**Urban Computer-Aided Design · 都市計画支援CAD**  
Lightweight 2D CAD for urban planning and architectural design.

![Windows](https://img.shields.io/badge/Windows-10%2F11-blue) ![Windows App SDK](https://img.shields.io/badge/Windows%20App%20SDK-1.8-blue) ![MSIX](https://img.shields.io/badge/package-MSIX-blue) ![CI](https://github.com/KiYouJyo/UCAD/actions/workflows/ci.yml/badge.svg) ![License](https://img.shields.io/badge/license-GPL--2.0--only-blue)

## Positioning

UCAD aims to be a Windows-native lightweight 2D CAD with familiar AutoCAD-style interaction and a deliberately bounded architecture/planning feature set. The project is DXF-first and 2D-first.

**Current candidate: v0.4.1 — CAD Selection & Cursor Interaction Refinement.** Pixel-level UI tuning remains paused. This milestone refines v0.4.0 with two-point Window/Crossing selection, retained drag selection, Shift removal, and a clean Win2D CAD cursor without a second Windows cross layered over the pickbox.

## Installation

Download production builds from [GitHub Releases](https://github.com/KiYouJyo/UCAD/releases/latest): `UCAD-v<version>-x64-one-click.zip`, `UCAD_<packageVersion>_x64.msixbundle`, and `SHA256SUMS.txt`.

## Workspace pages

- **Drawing**: Category Bar, Tool Shelf, Tool Rail, CAD Canvas, Inspector, Command Line, and Status Bar.
- **Start**: the CAD new-tab / Start Center. By default `+` opens Start; a real `CadWorkspaceSession` is created by New Drawing. If “show Start on new tab” is disabled, `+` creates a blank Drawing directly.
- **Settings**: one reusable settings tab without CAD-only rails/panels.

Each Drawing owns an independent `CadDocument`, `CadInteractionState`, `CadViewport`, and `CommandSession`, so geometry, history, selection, OSNAP, Ortho, command, and viewport state are isolated per tab.

## v0.4.1 interaction

- Click entities to build an additive selection; Shift + pick/window removes from the current set.
- Click empty space for the first Window/Crossing corner, release, move to preview, and click again to commit. Press-drag-release selection remains available as a fast alternative.
- Left-to-right is **Window** (fully contained only); right-to-left is **Crossing** (contained or intersecting).
- An empty completed non-Shift window clears the selection set. Esc cancels an unfinished window before clearing completed selection.
- The drawing canvas suppresses the native Windows pointer and renders only the Win2D CAD crosshair + central pickbox. Crosshair size is 5–100%, pickbox size is 3–20 px (10 px default), and OSNAP aperture is 3–50 px (10 px default), all live-adjustable under Settings → Input & Interaction → CAD cursor.
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

UCAD remains `PerMonitorV2`, with Figma critical design tokens protected by CI even though pixel-level UI comparison is not a v0.4.x release gate. App Theme and CAD Canvas Theme remain independent. Selection preview and viewport preferences are runtime settings.

Drafting defaults — object snap, snap set, and ortho — initialize newly created Drawing sessions; existing Drawing sessions keep their current F3/F8 state. Settings persist through `SettingsService` / `AppSettings` at `%LOCALAPPDATA%\UCAD\settings.json`.

Simplified Chinese, Japanese, and English continue to switch **without restarting UCAD** through the explicit MRT Core `ResourceContext`; the current window, Start, Settings, document tabs, menus, Inspector, command area, and status bar refresh in place without rebuilding existing drawing sessions.

## Development

```powershell
dotnet restore src/UCAD.App/UCAD.App.csproj -p:Platform=x64 -r win-x64
dotnet build src/UCAD.App/UCAD.App.csproj -c Debug -p:Platform=x64 -r win-x64
dotnet test tests/UCAD.Core.Tests/UCAD.Core.Tests.csproj -c Release
```

Required CI covers Core tests, app build, real startup smoke, MSIX/package validation, localization parity, version SSOT, PerMonitorV2, icon/Figma-token contracts, and v0.4 interaction contracts. Interaction Smoke validates Selection + ERASE + OSNAP + Ortho + Inspector in a running UCAD process. Localization Smoke still switches zh-CN → ja-JP → en-US inside one running process.

## Documentation

- [Roadmap](ROADMAP.md)
- [Architecture](docs/ARCHITECTURE.md)
- [Release process](docs/RELEASE-PROCESS.md)
- [v0.4.1 Release Notes](docs/RELEASE-NOTES-v0.4.1.en.md)

## License

UCAD is released under **GPL-2.0-only**.

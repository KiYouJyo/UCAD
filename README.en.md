[简体中文](README.md) | [日本語](README.ja.md) | [English](README.en.md)

# UCAD

**Urban Computer-Aided Design · 都市計画支援CAD**  
Lightweight 2D CAD for urban planning and architectural design.

![Windows](https://img.shields.io/badge/Windows-10%2F11-blue) ![Windows App SDK](https://img.shields.io/badge/Windows%20App%20SDK-1.8-blue) ![MSIX](https://img.shields.io/badge/package-MSIX-blue) ![CI](https://github.com/KiYouJyo/UCAD/actions/workflows/ci.yml/badge.svg) ![License](https://img.shields.io/badge/license-GPL--2.0--only-blue)

## Positioning

UCAD aims to be a Windows-native lightweight 2D CAD with familiar AutoCAD-style interaction and a deliberately bounded architecture/planning feature set. The project is DXF-first and 2D-first.

**Current candidate: v0.3.10 — Live Trilingual Localization Hotfix.** This release fixes the v0.3.9 regressions where Start / Settings could display resource identifiers and changing display language required a restart. An explicit MRT Core `ResourceContext` now switches Simplified Chinese, Japanese, and English inside the running process while preserving the v0.3.9 UI foundation and current CAD Core behavior.

## Installation

Download production builds from [GitHub Releases](https://github.com/KiYouJyo/UCAD/releases/latest):

- `UCAD-v<version>-x64-one-click.zip`: recommended.
- `UCAD_<packageVersion>_x64.msixbundle`: direct sideload package.
- `SHA256SUMS.txt`: integrity manifest.

## Workspace pages

UCAD has three explicit tab-content types:

- **Drawing**: Category Bar, Tool Shelf, Tool Rail, CAD Canvas, Inspector, Command Line, and Status Bar.
- **Start**: the CAD new-tab / Start Center. By default the title-bar `+` opens Start and a real `CadWorkspaceSession` is created only after New Drawing is chosen. If “show Start on new tab” is disabled, `+` creates a blank Drawing directly.
- **Settings**: a single reusable settings tab, without the CAD Tool Rail, Inspector, Command Line, or Status Bar.

Start includes New/Open entry points, an honest empty Recent state, Blank / Architecture / Urban Planning template information architecture, and Learn UCAD. Unsupported file I/O, recent-file storage, and template features are disabled or explicitly reported as unavailable rather than fabricated.

## Commands

Each Drawing tab is an independent in-memory CAD session with its own geometry, Undo/Redo history, command state, and viewport state. Available commands are `LINE/L`, `PLINE/PL`, `RECTANGLE/REC`, `CIRCLE/C`, `ARC/A`, `UNDO/U`, `REDO`, `CLEAR`, and `RESETVIEW/RV`.

All working UI entry points route through `CommandRegistry → CommandSession → CAD Core`. Selection, MOVE/COPY/OFFSET/TRIM, layers, OSNAP, ORTHO, and related planned capabilities retain UI slots but are not exposed as working commands until the matching Core capability exists.

## Settings and display baseline

- Native WinUI 3 / Windows App SDK window with `PerMonitorV2` high-DPI awareness;
- 1440×900 Figma frames remain the UI visual SSOT;
- 44 title bar, 44 category bar, 64 tool shelf, 52 Tool Rail, 304 Inspector, 34 command line, and 30 status bar DIP;
- 228-DIP Settings navigation, 54-DIP content offset, 940×72 cards, and 35 / 12 / 8 / 30 DIP vertical rhythm;
- **App Theme and CAD Canvas Theme work independently**: App Theme changes the shell/control palette while Canvas Theme separately changes entity, preview, grid, and crosshair colors; canvas background remains independent;
- grid visibility/opacity, cursor-centered zoom, middle-button pan, reverse wheel zoom, coordinate precision, and decimal formatting are wired to runtime behavior;
- unsupported Restore Session, manual UI Scale, automatic update checking, and recent-history clearing are not presented as implemented behavior;
- Fluent / WinUI icons for common actions and UCAD-style `PathIcon` geometry for CAD-specific symbols;
- centralized `SettingsService` / `AppSettings` persistence at `%LOCALAPPDATA%\UCAD\settings.json`.

## Localization

The application and repository use Simplified Chinese (zh-CN), Japanese (ja-JP), and English (en-US). v0.3.10 selects strings through an explicit MRT Core `ResourceContext`. After disabling Follow System Language in Settings, choosing any supported language **immediately relocalizes the current UCAD process without a restart**, including Window chrome content, Start, Settings, document tabs, menus, Inspector, command area, and status bar. Existing `CadWorkspaceSession` instances are not recreated, so geometry, Undo/Redo history, and viewport state remain intact.

## Development

```powershell
dotnet restore src/UCAD.App/UCAD.App.csproj -p:Platform=x64 -r win-x64
dotnet build src/UCAD.App/UCAD.App.csproj -c Debug -p:Platform=x64 -r win-x64
dotnet test tests/UCAD.Core.Tests/UCAD.Core.Tests.csproj -c Release
```

Required CI covers Core tests, app build, real startup smoke, MSIX/package validation, localization parity, version SSOT, PerMonitorV2, Unicode-placeholder scanning, Figma dimension/color-token contracts, and Start/Settings/Canvas behavior contracts. v0.3.10 also has a Localization Smoke that switches zh-CN → ja-JP → en-US inside one running process and validates real translated strings.

Pixel-level Figma comparison remains available as the manual `UI Fidelity Screenshots` workflow. It runs only when the runner exposes a real 1440×900 interactive desktop, so an unsuitable hosted desktop never blocks functional development or release.

## Documentation

- [Roadmap](ROADMAP.md)
- [Architecture](docs/ARCHITECTURE.md)
- [Release process](docs/RELEASE-PROCESS.md)
- [v0.3.10 Release Notes](docs/RELEASE-NOTES-v0.3.10.en.md)

## License

UCAD is released under **GPL-2.0-only**.

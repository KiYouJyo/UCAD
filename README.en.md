[简体中文](README.md) | [日本語](README.ja.md) | [English](README.en.md)

# UCAD

**Urban Computer-Aided Design · 都市計画支援CAD**  
Lightweight 2D CAD for urban planning and architectural design.

![Windows](https://img.shields.io/badge/Windows-10%2F11-blue) ![Windows App SDK](https://img.shields.io/badge/Windows%20App%20SDK-1.8-blue) ![MSIX](https://img.shields.io/badge/package-MSIX-blue) ![CI](https://github.com/KiYouJyo/UCAD/actions/workflows/ci.yml/badge.svg) ![License](https://img.shields.io/badge/license-GPL--2.0--only-blue)

## Positioning

UCAD aims to be a Windows-native lightweight 2D CAD with familiar AutoCAD-style interaction and a deliberately bounded architecture/planning feature set. The project is DXF-first and 2D-first.

**Current candidate: v0.3.7 — UI Fidelity & HiDPI Foundation.** Building on the multi-document workspace and UI↔Core state bridge established in v0.3.5/0.3.6, v0.3.7 restores PerMonitorV2 high-DPI behavior and brings the running WinUI 3 shell into high-fidelity alignment with the approved Figma v0.2 workspace: browser-like drawing tabs, a persistent category tool shelf, high-frequency left Tool Rail, Inspector, command line, and status bar. It adds no new CAD Core commands and freezes the shell before v0.4.0 Selection / OSNAP / Ortho work.

## Installation

Download production builds from [GitHub Releases](https://github.com/KiYouJyo/UCAD/releases/latest):

- `UCAD-v<version>-x64-one-click.zip`: recommended.
- `UCAD_<packageVersion>_x64.msixbundle`: direct sideload package.
- `SHA256SUMS.txt`: integrity manifest.

## Workspace and commands

Each title-bar tab is an independent in-memory CAD session with its own geometry, Undo/Redo history, command state, and viewport state. File open/save is not implemented yet, so closing a tab with drawing content warns that the content will be discarded.

Available commands are `LINE/L`, `PLINE/PL`, `RECTANGLE/REC`, `CIRCLE/C`, `ARC/A`, `UNDO/U`, `REDO`, `CLEAR`, and `RESETVIEW/RV`.

The top tool shelf, left rail, command search, and bottom command line all route through the same `CommandRegistry` / `CommandSession` path. Mouse picks can be mixed with `x,y`, `@x,y`, and distance input. Enter / Space confirms, Esc cancels, and an empty confirmation repeats the previous command.

Selection, MOVE/COPY/OFFSET/TRIM, layers, OSNAP, ORTHO, and other planned capabilities already occupy their intended information-architecture slots but are not exposed as working CAD commands until the matching Core capability exists.

## Display and UI baseline

- Native WinUI 3 / Windows App SDK window;
- `PerMonitorV2` high-DPI awareness;
- approved 1440×900 Figma v0.2 frame as the current shell baseline;
- 44 px title bar, 44 px category bar, 64 px tool shelf, 52 px Tool Rail, 304 px Inspector, 34 px command line, and 30 px status bar;
- Fluent system icons for common actions, with a dedicated UCAD Fluent extension planned for CAD-specific symbols.

## Localization

The application and repository use Simplified Chinese (zh-CN), Japanese (ja-JP), and English (en-US) resources.

## Development

```powershell
dotnet restore src/UCAD.App/UCAD.App.csproj -p:Platform=x64 -r win-x64
dotnet build src/UCAD.App/UCAD.App.csproj -c Debug -p:Platform=x64 -r win-x64
dotnet test tests/UCAD.Core.Tests/UCAD.Core.Tests.csproj -c Release
```

## Documentation

- [Roadmap](ROADMAP.md)
- [Architecture](docs/ARCHITECTURE.md)
- [Release process](docs/RELEASE-PROCESS.md)
- [v0.3.7 Release Notes](docs/RELEASE-NOTES-v0.3.7.en.md)

## License

UCAD is released under **GPL-2.0-only**.

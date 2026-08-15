[简体中文](README.md) | [日本語](README.ja.md) | [English](README.en.md)

# UCAD

**Urban Computer-Aided Design · 都市計画支援CAD**  
Lightweight 2D CAD for urban planning and architectural design.

![Windows](https://img.shields.io/badge/Windows-10%2F11-blue) ![Windows App SDK](https://img.shields.io/badge/Windows%20App%20SDK-1.8-blue) ![MSIX](https://img.shields.io/badge/package-MSIX-blue) ![CI](https://github.com/KiYouJyo/UCAD/actions/workflows/ci.yml/badge.svg) ![License](https://img.shields.io/badge/license-GPL--2.0--only-blue)

## Positioning

UCAD aims to be a Windows-native lightweight 2D CAD with familiar AutoCAD-style interaction and a deliberately bounded architecture/planning feature set. The project is DXF-first and 2D-first.

**Current version: v0.3.0 — Drawing Foundation.** It now provides a complete first drawing loop: command input, LINE / PLINE / RECTANGLE / CIRCLE / ARC, mixed mouse and typed-coordinate input, live previews, and document-level Undo / Redo.

## Installation

Download from [GitHub Releases](https://github.com/KiYouJyo/UCAD/releases/latest):

- `UCAD-v0.3.0-x64-one-click.zip`: recommended.
- `UCAD_0.3.0.0_x64.msixbundle`: direct sideload package.
- `SHA256SUMS.txt`: integrity manifest.

## Commands

`LINE/L`, `PLINE/PL`, `RECTANGLE/REC`, `CIRCLE/C`, `ARC/A`, `UNDO/U`, `REDO`, `CLEAR`, and `RESETVIEW/RV` are available. Mouse picks can be mixed with `x,y`, `@x,y`, and distance input.

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
- [v0.3.0 Release Notes](docs/RELEASE-NOTES-v0.3.0.en.md)

## License

UCAD is released under **GPL-2.0-only**.

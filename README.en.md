[简体中文](README.md) | [日本語](README.ja.md) | [English](README.en.md)

# UCAD

**Urban Computer-Aided Design · 都市計画支援CAD**  
Lightweight 2D CAD for urban planning and architectural design.

![Windows](https://img.shields.io/badge/Windows-10%2F11-blue) ![Windows App SDK](https://img.shields.io/badge/Windows%20App%20SDK-1.8-blue) ![MSIX](https://img.shields.io/badge/package-MSIX-blue) ![CI](https://github.com/KiYouJyo/UCAD/actions/workflows/ci.yml/badge.svg) ![License](https://img.shields.io/badge/license-GPL--2.0--only-blue)

## Positioning

UCAD aims to be a Windows-native lightweight 2D CAD with familiar AutoCAD-style interaction and a deliberately bounded architecture/planning feature set. The project is DXF-first and 2D-first rather than an attempt to clone all of AutoCAD.

**Current version: v0.2.0 — Command Foundation.** It combines the Win2D viewport and world coordinate system with a reusable CAD command foundation: aliases, Enter / Space confirmation, Esc cancellation, repeat previous command, absolute/relative coordinates, and distance input.

## Installation

Download from [GitHub Releases](https://github.com/KiYouJyo/UCAD/releases/latest):

- `UCAD-v0.2.0-x64-one-click.zip`: recommended.
- `UCAD_0.2.0.0_x64.msixbundle`: direct sideload package.
- `SHA256SUMS.txt`: integrity manifest.

The first one-click install may show one UAC prompt to trust the UCAD public certificate in `LocalMachine\TrustedPeople`; the MSIX itself is installed in the normal user context.

## Command input

Current commands include `LINE` / `L`, `CLEAR`, and `RESETVIEW` / `RV`. Point input accepts `x,y` and `@x,y`; after a base point exists, a number alone specifies distance along the current cursor direction. Enter / Space confirms, Esc cancels, and confirming empty input repeats the previous command.

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
- [Packaging](packaging/README.md)
- [v0.2.0 Release Notes](docs/RELEASE-NOTES-v0.2.0.en.md)

## License

UCAD is released under **GPL-2.0-only**.

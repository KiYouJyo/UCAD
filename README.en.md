[简体中文](README.md) | [日本語](README.ja.md) | [English](README.en.md)

# UCAD

**Urban Computer-Aided Design · 都市計画支援CAD**  
A lightweight 2D CAD for urban planning and architecture.

![Windows](https://img.shields.io/badge/Windows-10%2F11-blue) ![Windows App SDK](https://img.shields.io/badge/Windows%20App%20SDK-1.8-blue) ![MSIX](https://img.shields.io/badge/package-MSIX-blue) ![CI](https://github.com/KiYouJyo/UCAD/actions/workflows/ci.yml/badge.svg) ![License](https://img.shields.io/badge/license-GPL--2.0-blue)

## Positioning

UCAD aims to be a Windows-native lightweight CAD with familiar AutoCAD-style interaction and a deliberately focused 2D feature set for architecture and urban planning. It follows a DXF-first, 2D-first roadmap rather than attempting to reproduce the whole AutoCAD product surface.

v0.1.0 is the Foundation Release. It currently includes a Win2D CAD viewport, adaptive grid, coordinate transforms, zoom/pan, and a basic Line entity.

## Installation

Download from [GitHub Releases](https://github.com/KiYouJyo/UCAD/releases/latest):

- `UCAD-v0.1.0-x64-one-click.zip`: recommended for most users. Extract and run `① 安装UCAD.cmd`; it verifies the release and installs the signed MSIX.
- `UCAD_0.1.0.0_x64.msixbundle`: direct sideload package.
- `SHA256SUMS.txt`: release asset integrity manifest.

GitHub MSIX builds are signed with UCAD's fixed release certificate. The one-click installer establishes trust only for the current Windows user and does not require administrator privileges.

## Three-language foundation

Since v0.1, the application and repository use Simplified Chinese (zh-CN), Japanese (ja-JP), and English (en-US) resources. Main UI text, package metadata, README files, and Release Notes follow the same structure.

## Repository layout

```text
src/UCAD.Core/          CAD geometry/document core
src/UCAD.App/           WinUI 3 / Win2D app and MSIX manifest
tests/                  automated tests
packaging/              one-click installer and release validation
release/                release SSOT metadata
docs/                   documentation and release notes
.github/workflows/       CI and GitHub Release workflows
```

## Development

Build the app with .NET 10 and a current Windows SDK:

```powershell
dotnet restore src/UCAD.App/UCAD.App.csproj -p:Platform=x64
dotnet build src/UCAD.App/UCAD.App.csproj -c Debug -p:Platform=x64
```

Run core tests:

```powershell
dotnet test tests/UCAD.Core.Tests/UCAD.Core.Tests.csproj -c Release
```

## Documentation

- [Roadmap](ROADMAP.md)
- [Contributing](CONTRIBUTING.md)
- [Support](SUPPORT.md)
- [Privacy](PRIVACY.md)
- [Third-party notices](THIRD-PARTY-NOTICES.md)
- [v0.1.0 Release Notes](docs/RELEASE-NOTES-v0.1.0.en.md)

## License

UCAD is released under **GPL-2.0**. Third-party components remain under their respective licenses.

[简体中文](README.md) | [日本語](README.ja.md) | [English](README.en.md)

# UCAD

**Urban Computer-Aided Design · 都市計画支援CAD**  
都市計画・建築設計向けの軽量 2D CAD。

![Windows](https://img.shields.io/badge/Windows-10%2F11-blue) ![Windows App SDK](https://img.shields.io/badge/Windows%20App%20SDK-1.8-blue) ![MSIX](https://img.shields.io/badge/package-MSIX-blue) ![CI](https://github.com/KiYouJyo/UCAD/actions/workflows/ci.yml/badge.svg) ![License](https://img.shields.io/badge/license-GPL--2.0--only-blue)

## 現在の位置づけ

UCAD は Windows ネイティブで AutoCAD に近い操作感を持ち、建築・都市計画の高頻度な 2D 作図へ範囲を絞った軽量 CAD を目指します。DXF-first / 2D-first が基本方針です。

**現在のバージョンは v0.3.0 — Drawing Foundation。** コマンド入力、LINE / PLINE / RECTANGLE / CIRCLE / ARC、マウスと座標の混在入力、ライブプレビュー、Undo / Redo まで一連の作図フローを実装しました。

## インストール

[GitHub Releases](https://github.com/KiYouJyo/UCAD/releases/latest) から `UCAD-v0.3.0-x64-one-click.zip` を推奨します。直接サイドロード用の `UCAD_0.3.0.0_x64.msixbundle` と `SHA256SUMS.txt` も公開します。

## コマンド

`LINE/L`、`PLINE/PL`、`RECTANGLE/REC`、`CIRCLE/C`、`ARC/A`、`UNDO/U`、`REDO`、`CLEAR`、`RESETVIEW/RV` を利用できます。`x,y`、`@x,y`、距離入力とマウス指定を混在できます。

## 3 言語対応

アプリとリポジトリは簡体字中国語（zh-CN）、日本語（ja-JP）、英語（en-US）の三言語構成です。

## 開発

```powershell
dotnet restore src/UCAD.App/UCAD.App.csproj -p:Platform=x64 -r win-x64
dotnet build src/UCAD.App/UCAD.App.csproj -c Debug -p:Platform=x64 -r win-x64
dotnet test tests/UCAD.Core.Tests/UCAD.Core.Tests.csproj -c Release
```

## ドキュメント

- [Roadmap](ROADMAP.md)
- [Architecture](docs/ARCHITECTURE.md)
- [Release process](docs/RELEASE-PROCESS.md)
- [v0.3.0 Release Notes](docs/RELEASE-NOTES-v0.3.0.ja.md)

## ライセンス

UCAD は **GPL-2.0-only** で公開されています。

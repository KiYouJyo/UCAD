[简体中文](README.md) | [日本語](README.ja.md) | [English](README.en.md)

# UCAD

**Urban Computer-Aided Design · 都市計画支援CAD**  
都市計画・建築設計向けの軽量 2D CAD。

![Windows](https://img.shields.io/badge/Windows-10%2F11-blue) ![Windows App SDK](https://img.shields.io/badge/Windows%20App%20SDK-1.8-blue) ![MSIX](https://img.shields.io/badge/package-MSIX-blue) ![CI](https://github.com/KiYouJyo/UCAD/actions/workflows/ci.yml/badge.svg) ![License](https://img.shields.io/badge/license-GPL--2.0--only-blue)

## 現在の位置づけ

UCAD は Windows ネイティブで、AutoCAD に近い操作感を持ち、建築・都市計画の高頻度な 2D 作図へ範囲を絞った軽量 CAD を目指します。DXF-first / 2D-first を基本方針とします。

**現在のバージョンは v0.2.0 — Command Foundation。** Win2D ビューポートと座標系に加え、コマンドエイリアス、Enter / Space、Esc、直前コマンドの繰り返し、絶対・相対座標、距離入力を備えました。

## インストール

[GitHub Releases](https://github.com/KiYouJyo/UCAD/releases/latest) から以下を入手できます。

- `UCAD-v0.2.0-x64-one-click.zip`：推奨。
- `UCAD_0.2.0.0_x64.msixbundle`：直接サイドロード用。
- `SHA256SUMS.txt`：整合性確認用。

初回 one-click のみ公開証明書を `LocalMachine\TrustedPeople` に登録する UAC が表示されます。MSIX 本体は通常ユーザーでインストールされます。

## コマンド入力

`LINE` / `L`、`CLEAR`、`RESETVIEW` / `RV` を利用できます。点は `x,y` と `@x,y` で指定でき、基点指定後は現在のカーソル方向へ距離だけを入力することもできます。Enter / Space で確定、Esc でキャンセル、空入力確定で直前のコマンドを繰り返します。

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
- [Packaging](packaging/README.md)
- [v0.2.0 Release Notes](docs/RELEASE-NOTES-v0.2.0.ja.md)

## ライセンス

UCAD は **GPL-2.0-only** で公開されています。

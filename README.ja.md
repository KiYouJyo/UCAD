[简体中文](README.md) | [日本語](README.ja.md) | [English](README.en.md)

# UCAD

**Urban Computer-Aided Design · 都市計画支援CAD**  
都市計画・建築設計向けの軽量 2D CAD。

![Windows](https://img.shields.io/badge/Windows-10%2F11-blue) ![Windows App SDK](https://img.shields.io/badge/Windows%20App%20SDK-1.8-blue) ![MSIX](https://img.shields.io/badge/package-MSIX-blue) ![CI](https://github.com/KiYouJyo/UCAD/actions/workflows/ci.yml/badge.svg) ![License](https://img.shields.io/badge/license-GPL--2.0--only-blue)

## 現在の位置づけ

UCAD は Windows ネイティブで AutoCAD に近い操作感を持ち、建築・都市計画の高頻度な 2D 作図へ範囲を絞った軽量 CAD を目指します。DXF-first / 2D-first が基本方針です。

**現在のバージョンは v0.3.5 — Workspace Shell Foundation。** v0.3 の LINE / PLINE / RECTANGLE / CIRCLE / ARC と Undo / Redo の作図ループに、ブラウザー型複数図面ワークスペース、常設カテゴリツール棚、左側高頻度ツール、Inspector、コマンド検索、統一 UI↔Core 状態境界を追加し、v0.4 の選択 / OSNAP / 直交へ進む土台を作りました。

## インストール

[GitHub Releases](https://github.com/KiYouJyo/UCAD/releases/latest) から `UCAD-v0.3.5-x64-one-click.zip` を推奨します。直接サイドロード用の `UCAD_0.3.5.0_x64.msixbundle` と `SHA256SUMS.txt` も公開します。

## ワークスペースとコマンド

タイトルバーの各タブは独立したインメモリ CAD セッションで、図形、Undo/Redo、コマンド状態、ビュー状態を個別に保持します。v0.3.5 ではファイルの保存/読み込みは未実装のため、作図内容を含むタブを閉じる際は内容が失われることを確認します。

利用可能なコマンドは `LINE/L`、`PLINE/PL`、`RECTANGLE/REC`、`CIRCLE/C`、`ARC/A`、`UNDO/U`、`REDO`、`CLEAR`、`RESETVIEW/RV` です。

上部ツール棚、左側ツール、コマンド検索、下部コマンドラインはすべて同じ `CommandRegistry` / `CommandSession` 経路へ接続します。`x,y`、`@x,y`、距離入力とマウス指定を混在でき、Enter / Space で確定、Esc でキャンセル、空入力の確定で直前コマンドを繰り返します。

選択、MOVE/COPY/OFFSET/TRIM、レイヤー、OSNAP、ORTHO などは将来の配置位置を先に用意していますが、対応する Core 機能が完成するまでは無効です。

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
- [v0.3.5 Release Notes](docs/RELEASE-NOTES-v0.3.5.ja.md)

## ライセンス

UCAD は **GPL-2.0-only** で公開されています。

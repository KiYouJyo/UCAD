[简体中文](README.md) | [日本語](README.ja.md) | [English](README.en.md)

# UCAD

**Urban Computer-Aided Design · 都市計画支援CAD**  
都市計画・建築設計向けの軽量 2D CAD。

![Windows](https://img.shields.io/badge/Windows-10%2F11-blue) ![Windows App SDK](https://img.shields.io/badge/Windows%20App%20SDK-1.8-blue) ![MSIX](https://img.shields.io/badge/package-MSIX-blue) ![CI](https://github.com/KiYouJyo/UCAD/actions/workflows/ci.yml/badge.svg) ![License](https://img.shields.io/badge/license-GPL--2.0--only-blue)

## 現在の位置づけ

UCAD は Windows ネイティブで AutoCAD に近い操作感を持ち、建築・都市計画の高頻度な 2D 作図へ範囲を絞った軽量 CAD を目指します。DXF-first / 2D-first が基本方針です。

**現在の候補バージョンは v0.3.7 — UI Fidelity & HiDPI Foundation。** v0.3.5/0.3.6 で構築した複数図面ワークスペースと UI↔Core 状態境界を維持しながら、PerMonitorV2 の高 DPI 対応を復元し、実際の WinUI 3 シェルを承認済み Figma v0.2 に高精度で合わせます。ブラウザー型図面タブ、常設カテゴリツール棚、左側高頻度 Tool Rail、Inspector、コマンドライン、ステータスバーを v0.4.0 の Selection / OSNAP / 直交に向けた安定基盤として固定します。本バージョンでは新しい CAD Core コマンドは追加しません。

## インストール

正式ビルドは [GitHub Releases](https://github.com/KiYouJyo/UCAD/releases/latest) から取得できます。

- `UCAD-v<version>-x64-one-click.zip`：推奨。
- `UCAD_<packageVersion>_x64.msixbundle`：直接サイドロード用。
- `SHA256SUMS.txt`：整合性確認用。

## ワークスペースとコマンド

タイトルバーの各タブは独立したインメモリ CAD セッションで、図形、Undo/Redo、コマンド状態、ビュー状態を個別に保持します。ファイルの保存/読み込みはまだ未実装のため、作図内容を含むタブを閉じる際は内容が失われることを確認します。

利用可能なコマンドは `LINE/L`、`PLINE/PL`、`RECTANGLE/REC`、`CIRCLE/C`、`ARC/A`、`UNDO/U`、`REDO`、`CLEAR`、`RESETVIEW/RV` です。

上部ツール棚、左側ツール、コマンド検索、下部コマンドラインはすべて同じ `CommandRegistry` / `CommandSession` 経路へ接続します。`x,y`、`@x,y`、距離入力とマウス指定を混在でき、Enter / Space で確定、Esc でキャンセル、空入力の確定で直前コマンドを繰り返します。

選択、MOVE/COPY/OFFSET/TRIM、レイヤー、OSNAP、ORTHO などは UI の予定位置をすでに確保していますが、対応する CAD Core が完成するまでは実動コマンドとして扱いません。

## 表示・UI 基準

- WinUI 3 / Windows App SDK のネイティブウィンドウ；
- `PerMonitorV2` 高 DPI awareness；
- 承認済み 1440×900 Figma v0.2 を現行 Shell の基準とする；
- タイトルバー 44 px、カテゴリバー 44 px、ツール棚 64 px、Tool Rail 52 px、Inspector 304 px、コマンドライン 34 px、ステータスバー 30 px；
- 一般操作には Fluent システムアイコンを優先し、CAD 固有記号は UCAD 専用 Fluent 拡張セットとして整備する。

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
- [v0.3.7 Release Notes](docs/RELEASE-NOTES-v0.3.7.ja.md)

## ライセンス

UCAD は **GPL-2.0-only** で公開されています。

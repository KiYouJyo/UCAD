[简体中文](README.md) | [日本語](README.ja.md) | [English](README.en.md)

# UCAD

**Urban Computer-Aided Design · 都市計画支援CAD**  
都市計画・建築設計向けの軽量 2D CAD。

![Windows](https://img.shields.io/badge/Windows-10%2F11-blue) ![Windows App SDK](https://img.shields.io/badge/Windows%20App%20SDK-1.8-blue) ![MSIX](https://img.shields.io/badge/package-MSIX-blue) ![CI](https://github.com/KiYouJyo/UCAD/actions/workflows/ci.yml/badge.svg) ![License](https://img.shields.io/badge/license-GPL--2.0--only-blue)

## 現在の位置づけ

UCAD は Windows ネイティブで AutoCAD に近い操作感を持ち、建築・都市計画の高頻度な 2D 作図へ範囲を絞った軽量 CAD を目指します。DXF-first / 2D-first が基本方針です。

**現在の候補バージョンは v0.3.9 — UI Completion / Figma Fidelity / Start & Settings Foundation。** 本リリースでは CAD Core の新機能を追加せず、1440×900 の Figma ファイルをビジュアル SSOT として、ブラウザー型 Document Tabs、Start Center、全 Settings、3 言語リソース、Fluent アイコン、Design Tokens、バージョン SSOT、実行時スクリーンショット検証を完成させます。v0.4.0 の Selection / OSNAP / Inspector に向けた安定 UI 基盤です。

## インストール

正式ビルドは [GitHub Releases](https://github.com/KiYouJyo/UCAD/releases/latest) から取得できます。

- `UCAD-v<version>-x64-one-click.zip`：推奨。
- `UCAD_<packageVersion>_x64.msixbundle`：直接サイドロード用。
- `SHA256SUMS.txt`：整合性確認用。

## ワークスペースページ

UCAD は 3 種類のタブ内容を明確に区別します。

- **Drawing**：Category Bar、Tool Shelf、Tool Rail、CAD Canvas、Inspector、Command Line、Status Bar。
- **Start**：CAD の新規タブ / Start Center。タイトルバーの `+` は Start を開き、「新規図面」を選んだ時だけ実際の `CadWorkspaceSession` を作成します。
- **Settings**：単一の設定タブ。CAD Tool Rail / Inspector / Command Line / Status Bar と同時には表示しません。

Start には新規/開く、最近使用した項目の空状態、Blank / Architecture / Urban Planning テンプレートの情報構造、Learn UCAD を用意します。未実装のファイル I/O やテンプレート機能は、利用可能に見せかけず明示的なプレースホルダーとして扱います。

## コマンド

各 Drawing タブは独立したインメモリ CAD セッションで、図形、Undo/Redo、コマンド状態、ビュー状態を個別に保持します。利用可能なコマンドは `LINE/L`、`PLINE/PL`、`RECTANGLE/REC`、`CIRCLE/C`、`ARC/A`、`UNDO/U`、`REDO`、`CLEAR`、`RESETVIEW/RV` です。

すべての UI 入口は `CommandRegistry → CommandSession → CAD Core` に統一されます。Selection、MOVE/COPY/OFFSET/TRIM、Layers、OSNAP、ORTHO などは UI 上の位置を確保していますが、対応する Core が完成するまでは実動コマンドとして扱いません。

## Settings・表示基準

- WinUI 3 / Windows App SDK のネイティブウィンドウと `PerMonitorV2` 高 DPI awareness；
- 1440×900 Figma ファイルを UI のビジュアル SSOT とする；
- タイトルバー 44、カテゴリバー 44、ツール棚 64、Tool Rail 52、Inspector 304、コマンドライン 34、ステータスバー 30 DIP；
- Settings ナビ 228 DIP、コンテンツ開始 54 DIP、カード 940×72 DIP、35 / 12 / 8 / 30 DIP の縦リズム；
- App Theme と CAD Canvas Theme は独立。背景、グリッド表示/強度、カーソル中心ズーム、中ボタンパン、ホイール反転は集中設定層から既存 Viewport へ適用；
- 一般操作は Fluent / WinUI アイコン、CAD 固有形状は UCAD スタイルの `PathIcon`；
- `SettingsService` / `AppSettings` により `%LOCALAPPDATA%\UCAD\settings.json` へ集中保存。

## 3 言語対応

アプリとリポジトリは簡体字中国語（zh-CN）、日本語（ja-JP）、英語（en-US）の三言語構成です。v0.3.9 の Start と全 Settings ページは同一のリソースキー構成を保ちます。

## 開発

```powershell
dotnet restore src/UCAD.App/UCAD.App.csproj -p:Platform=x64 -r win-x64
dotnet build src/UCAD.App/UCAD.App.csproj -c Debug -p:Platform=x64 -r win-x64
dotnet test tests/UCAD.Core.Tests/UCAD.Core.Tests.csproj -c Release
```

CI は Core tests、app-build、実際の起動 smoke、MSIX/package validation、3 言語リソース一致、バージョン SSOT、PerMonitorV2、Unicode 仮アイコン検査、1440×900 実行時 UI スクリーンショットを検証します。

## ドキュメント

- [Roadmap](ROADMAP.md)
- [Architecture](docs/ARCHITECTURE.md)
- [Release process](docs/RELEASE-PROCESS.md)
- [v0.3.9 Release Notes](docs/RELEASE-NOTES-v0.3.9.ja.md)

## ライセンス

UCAD は **GPL-2.0-only** で公開されています。

[简体中文](README.md) | [日本語](README.ja.md) | [English](README.en.md)

# UCAD

**Urban Computer-Aided Design · 都市計画支援CAD**  
都市計画・建築設計向けの軽量 2D CAD。

![Windows](https://img.shields.io/badge/Windows-10%2F11-blue) ![Windows App SDK](https://img.shields.io/badge/Windows%20App%20SDK-1.8-blue) ![MSIX](https://img.shields.io/badge/package-MSIX-blue) ![CI](https://github.com/KiYouJyo/UCAD/actions/workflows/ci.yml/badge.svg) ![License](https://img.shields.io/badge/license-GPL--2.0--only-blue)

## 現在の位置づけ

UCAD は Windows ネイティブで AutoCAD に近い操作感を持ち、建築・都市計画の高頻度な 2D 作図へ範囲を絞った軽量 CAD を目指します。DXF-first / 2D-first が基本方針です。

**現在の候補バージョンは v0.3.10 — Live Trilingual Localization Hotfix。** v0.3.9 で Start / Settings にリソースキーが表示される問題と、表示言語の変更に再起動が必要だった問題を修正します。明示的な MRT Core `ResourceContext` により、簡体字中国語・日本語・English を同一プロセス内で即時切り替えし、v0.3.9 の UI Foundation と既存 CAD Core の挙動は維持します。

## インストール

正式ビルドは [GitHub Releases](https://github.com/KiYouJyo/UCAD/releases/latest) から取得できます。

- `UCAD-v<version>-x64-one-click.zip`：推奨。
- `UCAD_<packageVersion>_x64.msixbundle`：直接サイドロード用。
- `SHA256SUMS.txt`：整合性確認用。

## ワークスペースページ

UCAD は 3 種類のタブ内容を明確に区別します。

- **Drawing**：Category Bar、Tool Shelf、Tool Rail、CAD Canvas、Inspector、Command Line、Status Bar。
- **Start**：CAD の新規タブ / Start Center。既定ではタイトルバーの `+` が Start を開き、「新規図面」を選んだ時だけ実際の `CadWorkspaceSession` を作成します。「新しいタブに Start を表示」を無効にすると、`+` は空白 Drawing を直接作成します。
- **Settings**：単一の設定タブ。CAD Tool Rail / Inspector / Command Line / Status Bar と同時には表示しません。

Start には新規/開く、最近使用した項目の空状態、Blank / Architecture / Urban Planning テンプレートの情報構造、Learn UCAD を用意します。未実装のファイル I/O、最近使用したファイル、テンプレート機能は、利用可能に見せかけず無効化または未対応として明示します。

## コマンド

各 Drawing タブは独立したインメモリ CAD セッションで、図形、Undo/Redo、コマンド状態、ビュー状態を個別に保持します。利用可能なコマンドは `LINE/L`、`PLINE/PL`、`RECTANGLE/REC`、`CIRCLE/C`、`ARC/A`、`UNDO/U`、`REDO`、`CLEAR`、`RESETVIEW/RV` です。

実動する UI 入口は `CommandRegistry → CommandSession → CAD Core` に統一されます。Selection、MOVE/COPY/OFFSET/TRIM、Layers、OSNAP、ORTHO などは UI 上の位置を確保していますが、対応する Core が完成するまでは実動コマンドとして扱いません。

## Settings・表示基準

- WinUI 3 / Windows App SDK のネイティブウィンドウと `PerMonitorV2` 高 DPI awareness；
- 1440×900 Figma Frame を UI のビジュアル SSOT とする；
- タイトルバー 44、カテゴリバー 44、ツール棚 64、Tool Rail 52、Inspector 304、コマンドライン 34、ステータスバー 30 DIP；
- Settings ナビ 228 DIP、コンテンツ開始 54 DIP、カード 940×72 DIP、35 / 12 / 8 / 30 DIP の縦リズム；
- **App Theme と CAD Canvas Theme は独立して実動**。App Theme は Shell/コントロールの明暗を切り替え、Canvas Theme は図形・プレビュー・グリッド・クロスヘアの配色を別に制御。Canvas 背景も独立して設定可能；
- グリッド表示/強度、カーソル中心ズーム、中ボタンパン、ホイール反転、座標精度、小数形式は実行時ロジックに接続済み；
- 未実装の Restore Session、手動 UI Scale、自動更新チェック、最近履歴の消去は「実装済み」として扱わない；
- 一般操作は Fluent / WinUI アイコン、CAD 固有形状は UCAD スタイルの `PathIcon`；
- `SettingsService` / `AppSettings` により `%LOCALAPPDATA%\UCAD\settings.json` へ集中保存。

## 3 言語対応

アプリとリポジトリは簡体字中国語（zh-CN）、日本語（ja-JP）、英語（en-US）の三言語構成です。v0.3.10 は独立した MRT Core `ResourceContext` で言語を選択します。Settings で「システム言語に従う」を無効にして言語を選ぶと、**UCAD を再起動せず**現在の Window、Start、Settings、ドキュメントタブ、メニュー、Inspector、コマンド領域、ステータスバーを即時再ローカライズします。既存の `CadWorkspaceSession` は再生成されないため、図形・Undo/Redo・ビュー状態は保持されます。

## 開発

```powershell
dotnet restore src/UCAD.App/UCAD.App.csproj -p:Platform=x64 -r win-x64
dotnet build src/UCAD.App/UCAD.App.csproj -c Debug -p:Platform=x64 -r win-x64
dotnet test tests/UCAD.Core.Tests/UCAD.Core.Tests.csproj -c Release
```

必須 CI は Core tests、app-build、実際の起動 smoke、MSIX/package validation、3 言語リソース一致、バージョン SSOT、PerMonitorV2、Unicode 仮アイコン検査、Figma の主要寸法/色 Token、Start/Settings/Canvas の動作契約を検証します。v0.3.10 ではさらに Localization Smoke が同一プロセス内で zh-CN → ja-JP → en-US を順番に切り替え、実際の翻訳文字列を検証します。

ピクセル単位の Figma 比較は手動の `UI Fidelity Screenshots` ワークフローとして残します。ランナーが実際の 1440×900 インタラクティブデスクトップを提供する場合のみ実行し、不適切なホスト画面が機能開発やリリースをブロックしないようにしています。

## ドキュメント

- [Roadmap](ROADMAP.md)
- [Architecture](docs/ARCHITECTURE.md)
- [Release process](docs/RELEASE-PROCESS.md)
- [v0.3.10 Release Notes](docs/RELEASE-NOTES-v0.3.10.ja.md)

## ライセンス

UCAD は **GPL-2.0-only** で公開されています。

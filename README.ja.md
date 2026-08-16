[简体中文](README.md) | [日本語](README.ja.md) | [English](README.en.md)

# UCAD

**Urban Computer-Aided Design · 都市計画支援CAD**  
都市計画・建築設計向けの軽量 2D CAD。

![Windows](https://img.shields.io/badge/Windows-10%2F11-blue) ![Windows App SDK](https://img.shields.io/badge/Windows%20App%20SDK-1.8-blue) ![MSIX](https://img.shields.io/badge/package-MSIX-blue) ![CI](https://github.com/KiYouJyo/UCAD/actions/workflows/ci.yml/badge.svg) ![License](https://img.shields.io/badge/license-GPL--2.0--only-blue)

## 現在の位置づけ

UCAD は Windows ネイティブで AutoCAD に近い操作感を持ち、建築・都市計画の高頻度な 2D 作図へ範囲を絞った軽量 CAD を目指します。DXF-first / 2D-first が基本方針です。

**現在の候補バージョンは v0.4.0 — Selection / OSNAP / Ortho Interaction Foundation。** このマイルストーンではピクセル単位の UI 微調整を止め、クリック/Window/Crossing 選択、追加複数選択、ERASE、Endpoint/Midpoint/Center/Intersection OSNAP、Ortho、選択連動 Inspector という最初の CAD 操作ループを完成させます。

## インストール

正式ビルドは [GitHub Releases](https://github.com/KiYouJyo/UCAD/releases/latest) から取得できます。`UCAD-v<version>-x64-one-click.zip`、`UCAD_<packageVersion>_x64.msixbundle`、`SHA256SUMS.txt` を配布します。

## ワークスペースページ

- **Drawing**：Category Bar、Tool Shelf、Tool Rail、CAD Canvas、Inspector、Command Line、Status Bar。
- **Start**：CAD の新規タブ / Start Center。既定では `+` が Start を開き、「新規図面」で実際の `CadWorkspaceSession` を作成します。
- **Settings**：単一の設定タブ。CAD 専用の Rail / Inspector / Command Line / Status Bar と重ねて表示しません。

各 Drawing は独立した `CadDocument`、`CadInteractionState`、`CadViewport`、`CommandSession` を所有し、図形、履歴、選択、OSNAP、Ortho、コマンド、ビュー状態をタブごとに分離します。

## v0.4.0 の操作

- オブジェクトをクリックして選択し、別オブジェクトを続けてクリックすると追加選択できます。
- 空白クリックまたは Esc で選択解除。
- 左→右ドラッグは **Window**：完全に含まれるオブジェクトだけを選択。
- 右→左ドラッグは **Crossing**：含まれる、または交差するオブジェクトを選択。
- プリセレクション、選択ハイライト、grip は Core/Workspace の `SelectionSet` を Viewport が表示するだけで、別の選択モデルを持ちません。
- **F3 / OSNAP**：端点・中点・中心・交点のオブジェクトスナップを切り替え。
- **F8 / ORTHO**：LINE / PLINE のマウス入力を水平/垂直に拘束。
- **Delete / ERASE / E / DELETE**：現在の選択を 1 回の Undo 単位として消去。
- Inspector は実際の Line / Polyline / Circle / Arc を読み取り、種類、数、基本ジオメトリ、Entity ID を表示します。

## コマンド

| コマンド | エイリアス | 機能 |
| --- | --- | --- |
| `LINE` | `L` | 連続線分 |
| `PLINE` | `PL` | ポリライン |
| `RECTANGLE` | `REC` | 2 点長方形 |
| `CIRCLE` | `C` | 中心/半径の円 |
| `ARC` | `A` | 3 点円弧 |
| `ERASE` | `E`, `DELETE` | 現在の選択を消去 |
| `UNDO` | `U` | 元に戻す |
| `REDO` | — | やり直し |
| `CLEAR` | — | 図面を全消去 |
| `RESETVIEW` | `RV` | ビューをリセット |

実動コマンドは `CommandRegistry → CommandSession → CAD Core` に統一されます。カテゴリの有効状態も登録済み Core 能力から導出されます。MOVE / COPY / ROTATE / OFFSET / TRIM などの Modify コマンドは v0.5.x の対象です。

## Settings・表示・3 言語

UCAD は `PerMonitorV2` を維持し、Figma の主要 Design Token は CI で回帰防止しますが、v0.4.0 のリリース条件にピクセル単位の UI 比較は含めません。App Theme と CAD Canvas Theme は引き続き独立し、選択プレビューなど既存の Viewport 設定も実行時に反映されます。

Settings の既定 OSNAP / スナップ種類 / Ortho は新しく作成する Drawing の `CadInteractionState` を初期化します。既存 Drawing の F3/F8 状態は既定値変更で上書きされません。設定は `%LOCALAPPDATA%\UCAD\settings.json` に保存されます。

簡体字中国語・日本語・English は明示的な MRT Core `ResourceContext` により**再起動なし**で切り替わります。Window、Start、Settings、ドキュメントタブ、メニュー、Inspector、コマンド領域、ステータスバーをその場で更新し、既存の図形・選択セッション・Undo/Redo・ビュー状態は再生成しません。

## 開発

```powershell
dotnet restore src/UCAD.App/UCAD.App.csproj -p:Platform=x64 -r win-x64
dotnet build src/UCAD.App/UCAD.App.csproj -c Debug -p:Platform=x64 -r win-x64
dotnet test tests/UCAD.Core.Tests/UCAD.Core.Tests.csproj -c Release
```

必須 CI は Core tests、app-build、実際の startup-smoke、MSIX/package validation、三言語リソース、version SSOT、PerMonitorV2、アイコン/Figma Token、v0.4 interaction contract を検証します。startup-smoke は実行中 UCAD で Drawing を作成し、Selection + OSNAP + Center Snap + Ortho + Inspector + capability-derived category state を確認します。Localization Smoke は同一プロセスで zh-CN → ja-JP → en-US の切り替えを継続検証します。

## ドキュメント

- [Roadmap](ROADMAP.md)
- [Architecture](docs/ARCHITECTURE.md)
- [Release process](docs/RELEASE-PROCESS.md)
- [v0.4.0 Release Notes](docs/RELEASE-NOTES-v0.4.0.ja.md)

## ライセンス

UCAD は **GPL-2.0-only** で公開されています。

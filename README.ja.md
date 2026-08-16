[简体中文](README.md) | [日本語](README.ja.md) | [English](README.en.md)

# UCAD

**Urban Computer-Aided Design · 都市計画支援CAD**  
都市計画・建築設計向けの軽量 2D CAD。

![Windows](https://img.shields.io/badge/Windows-10%2F11-blue) ![Windows App SDK](https://img.shields.io/badge/Windows%20App%20SDK-1.8-blue) ![MSIX](https://img.shields.io/badge/package-MSIX-blue) ![CI](https://github.com/KiYouJyo/UCAD/actions/workflows/ci.yml/badge.svg) ![License](https://img.shields.io/badge/license-GPL--2.0--only-blue)

## 現在の位置づけ

UCAD は Windows ネイティブで AutoCAD に近い操作感を持ち、建築・都市計画の高頻度な 2D 作図へ範囲を絞った軽量 CAD を目指します。DXF-first / 2D-first が基本方針です。

**現在の候補バージョンは v0.4.1 — CAD Selection & Cursor Interaction Refinement。** ピクセル単位の UI 微調整は引き続き停止し、2 クリック/ドラッグ両対応の Window/Crossing 選択、Shift 解除、段階的 Esc、調整可能な CAD クロスヘア・Pickbox・OSNAP aperture へ重点を移します。

## インストール

正式ビルドは [GitHub Releases](https://github.com/KiYouJyo/UCAD/releases/latest) から取得できます。`UCAD-v<version>-x64-one-click.zip`、`UCAD_<packageVersion>_x64.msixbundle`、`SHA256SUMS.txt` を配布します。

## ワークスペースページ

- **Drawing**：Category Bar、Tool Shelf、Tool Rail、CAD Canvas、Inspector、Command Line、Status Bar。
- **Start**：CAD の新規タブ / Start Center。既定では `+` が Start を開き、「新規図面」で実際の `CadWorkspaceSession` を作成します。
- **Settings**：単一の設定タブ。CAD 専用の Rail / Inspector / Command Line / Status Bar と重ねて表示しません。

各 Drawing は独立した `CadDocument`、`CadInteractionState`、`CadViewport`、`CommandSession` を所有し、図形、履歴、選択、OSNAP、Ortho、コマンド、ビュー状態をタブごとに分離します。

## v0.4.1 の CAD 操作

- オブジェクトをクリックすると現在の選択セットへ追加され、別オブジェクトを続けてクリックすると追加選択できます。
- **Shift + クリック**で現在の選択セットからオブジェクトを除外します。
- 空白部分をクリックして第1コーナーを指定し、ボタンを離したまま移動して選択枠をプレビューし、2 回目のクリックで対角点を確定できます。押下 → ドラッグ → リリースによる選択も引き続き利用できます。
- 第1点から右側で確定すると **Window**（完全に含まれるオブジェクトのみ）、左側で確定すると **Crossing**（含まれる、または交差するオブジェクト）です。
- Shift を押して選択枠を確定すると範囲内を一括解除できます。何も含まない空の選択枠を確定すると現在の選択セットをすばやく解除できます。
- Esc はまず未確定の 2 点選択をキャンセルし、未確定の選択枠がない場合は確定済みの選択セットを解除します。
- プリセレクション、選択ハイライト、grip は Core/Workspace の `SelectionSet` を Viewport が表示します。
- 作図領域では CAD クロスヘアと中央 Pickbox を使用します。クロスヘア長、Pickbox サイズ、OSNAP aperture は「設定 → 入力と操作」で即時調整できます。
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

UCAD は `PerMonitorV2` を維持し、Figma の主要 Design Token は CI で回帰防止しますが、v0.4.1 のリリース条件にピクセル単位の UI 比較は含めません。App Theme と CAD Canvas Theme は引き続き独立し、選択プレビューなど既存の Viewport 設定も実行時に反映されます。

クロスヘア長、Pickbox サイズ、OSNAP aperture は既存 Drawing へ即時反映されます。Settings の既定 OSNAP / スナップ種類 / Ortho は新しく作成する Drawing の `CadInteractionState` を初期化し、既存 Drawing の F3/F8 状態は既定値変更で上書きされません。設定は `%LOCALAPPDATA%\UCAD\settings.json` に保存されます。

簡体字中国語・日本語・English は明示的な MRT Core `ResourceContext` により**再起動なし**で切り替わります。Window、Start、Settings、ドキュメントタブ、メニュー、Inspector、コマンド領域、ステータスバーをその場で更新し、既存の図形・選択セッション・Undo/Redo・ビュー状態は再生成しません。

## 開発

```powershell
dotnet restore src/UCAD.App/UCAD.App.csproj -p:Platform=x64 -r win-x64
dotnet build src/UCAD.App/UCAD.App.csproj -c Debug -p:Platform=x64 -r win-x64
dotnet test tests/UCAD.Core.Tests/UCAD.Core.Tests.csproj -c Release
```

必須 CI は Core tests、app-build、実際の startup-smoke、MSIX/package validation、三言語リソース、version SSOT、PerMonitorV2、アイコン/Figma Token、interaction contract を検証します。Interaction Smoke は実行中 UCAD で Selection + ERASE + OSNAP + Ortho + Inspector を確認し、Localization Smoke は同一プロセスで zh-CN → ja-JP → en-US の切り替えを継続検証します。

## ドキュメント

- [Roadmap](ROADMAP.md)
- [Architecture](docs/ARCHITECTURE.md)
- [Release process](docs/RELEASE-PROCESS.md)
- [v0.4.1 Release Notes](docs/RELEASE-NOTES-v0.4.1.ja.md)

## ライセンス

UCAD は **GPL-2.0-only** で公開されています。

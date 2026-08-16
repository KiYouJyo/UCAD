[简体中文](README.md) | [日本語](README.ja.md) | [English](README.en.md)

# UCAD

**Urban Computer-Aided Design · 都市計画支援CAD**  
都市計画・建築設計向けの軽量 2D CAD。

![Windows](https://img.shields.io/badge/Windows-10%2F11-blue) ![Windows App SDK](https://img.shields.io/badge/Windows%20App%20SDK-1.8-blue) ![MSIX](https://img.shields.io/badge/package-MSIX-blue) ![CI](https://github.com/KiYouJyo/UCAD/actions/workflows/ci.yml/badge.svg) ![License](https://img.shields.io/badge/license-GPL--2.0--only-blue)

## 現在の位置づけ

UCAD は Windows ネイティブで AutoCAD に近い操作感を持ち、建築・都市計画の高頻度な 2D 作図へ範囲を絞った軽量 CAD を目指します。DXF-first / 2D-first が基本方針です。

**現在の候補バージョンは v0.5.0 — Modify Foundation。** UI の追加調整ではなく、v0.4.x の Selection / OSNAP / Ortho を再利用して MOVE / COPY / ROTATE / SCALE / MIRROR / OFFSET / TRIM / EXTEND を実装し、共通のジオメトリ変換・編集トランザクション・プレビュー入力基盤を整備します。

## インストール

正式ビルドは [GitHub Releases](https://github.com/KiYouJyo/UCAD/releases/latest) から取得できます。`UCAD-v<version>-x64-one-click.zip`、`UCAD_<packageVersion>_x64.msixbundle`、`SHA256SUMS.txt` を配布します。

## ワークスペースページ

- **Drawing**：Category Bar、Tool Shelf、Tool Rail、CAD Canvas、Inspector、Command Line、Status Bar。
- **Start**：CAD の新規タブ / Start Center。既定では `+` が Start を開き、「新規図面」で実際の `CadWorkspaceSession` を作成します。
- **Settings**：単一の設定タブ。CAD 専用の Rail / Inspector / Command Line / Status Bar と重ねて表示しません。

各 Drawing は独立した `CadDocument`、`CadInteractionState`、`CadViewport`、`CommandSession` を所有し、図形、履歴、選択、OSNAP、Ortho、コマンド、ビュー状態をタブごとに分離します。

## CAD 操作

- オブジェクトをクリックすると現在の選択セットへ追加され、別オブジェクトを続けてクリックすると追加選択できます。
- **Shift + クリック / Shift + 選択窓**で現在の選択セットからオブジェクトを除外します。
- 空白部分をクリックして第1コーナーを指定し、ボタンを離したまま移動して選択枠をプレビューし、2 回目のクリックで確定できます。押下 → ドラッグ → リリースも利用できます。
- 第1点から右側で確定すると **Window**、左側で確定すると **Crossing** です。
- Esc はまず未確定の 2 点選択をキャンセルし、未確定の選択枠がない場合は確定済みの選択セットを解除します。
- 作図領域では Win2D CAD クロスヘア + 中央 Pickbox を使用し、Windows のネイティブカーソルは透明化します。Crosshair / Pickbox / OSNAP aperture は Settings から即時調整できます。
- **F3 / OSNAP**：端点・中点・中心・交点のオブジェクトスナップを切り替え。
- **F8 / ORTHO**：LINE / PLINE、および対応する Modify 点入力を水平/垂直に拘束。
- **Delete / ERASE / E / DELETE**：現在の選択を 1 回の Undo 単位として消去。
- Inspector は Line / Polyline / Circle / Arc の実選択を表示します。

## v0.5.0 Modify

- 事前選択と、コマンド開始後に選択して Enter で確定する両方のワークフローに対応します。
- MOVE / COPY：基点 + 2 点目。OSNAP と F8 Ortho を利用できます。
- ROTATE：基点後にキャンバス上の点、または角度（度）を入力します。
- SCALE：基点後に正の尺度係数を入力、または点で倍率を決定します。
- MIRROR：2 点で鏡像軸を定義し、元オブジェクトは既定で保持します。
- OFFSET：距離 → 対象 → 側点。Line / Polyline / Circle / Arc を基本対応します。
- TRIM / EXTEND：Quick モードで他の表示エンティティを境界として連続処理し、Enter で終了します。
- 変換系コマンドと OFFSET はトランジェントプレビューを表示します。
- 実体を変更する操作は既存 Entity ID を保持し、COPY / 保持型 MIRROR / OFFSET などの生成物には新 ID を付与します。
- `CadDocument.Replace` / `ReplaceRange` により、各変更を 1 回の Undo で戻せる編集トランザクションとして扱います。

## コマンド

| コマンド | エイリアス | 機能 |
| --- | --- | --- |
| `LINE` | `L` | 連続線分 |
| `PLINE` | `PL` | ポリライン |
| `RECTANGLE` | `REC` | 2 点長方形 |
| `CIRCLE` | `C` | 中心/半径の円 |
| `ARC` | `A` | 3 点円弧 |
| `MOVE` | `M` | 選択を移動 |
| `COPY` | `CO`, `CP` | 選択を複写 |
| `ROTATE` | `RO` | 基点回りに回転 |
| `SCALE` | `SC` | 基点回りに尺度変更 |
| `MIRROR` | `MI` | 2 点軸で鏡像 |
| `OFFSET` | `O` | 距離と側点でオフセット |
| `TRIM` | `TR` | Quick Trim |
| `EXTEND` | `EX` | Quick Extend |
| `ERASE` | `E`, `DELETE` | 現在の選択を消去 |
| `UNDO` | `U` | 元に戻す |
| `REDO` | — | やり直し |
| `CLEAR` | — | 図面を全消去 |
| `RESETVIEW` | `RV` | ビューをリセット |

実動コマンドは `CommandRegistry → CommandSession → CAD Core` に統一されます。Modify カテゴリは実機能へ昇格し、Annotate / Layer / Block / Measure は Core 能力が実装されるまで予約状態を維持します。

## Settings・表示・3 言語

UCAD は `PerMonitorV2` を維持し、Figma の主要 Design Token は CI で回帰防止しますが、現在の機能マイルストーンではピクセル単位の UI 比較をリリース条件に含めません。App Theme と CAD Canvas Theme は独立しています。

Crosshair、Pickbox、OSNAP aperture は既存 Drawing へ即時反映されます。Settings の既定 OSNAP / スナップ種類 / Ortho は新規 Drawing の `CadInteractionState` を初期化します。設定は `%LOCALAPPDATA%\UCAD\settings.json` に保存されます。

簡体字中国語・日本語・English は明示的な MRT Core `ResourceContext` により**再起動なし**で切り替わります。Window、Start、Settings、ドキュメントタブ、メニュー、Inspector、コマンド領域、Modify の段階案内、ステータスバーを更新し、既存ジオメトリ・選択・Undo/Redo・ビュー状態は再生成しません。

## 開発

```powershell
dotnet restore src/UCAD.App/UCAD.App.csproj -p:Platform=x64 -r win-x64
dotnet build src/UCAD.App/UCAD.App.csproj -c Debug -p:Platform=x64 -r win-x64
dotnet test tests/UCAD.Core.Tests/UCAD.Core.Tests.csproj -c Release
```

必須 CI は Core tests、app-build、startup-smoke、MSIX/package validation、三言語リソース、version SSOT、PerMonitorV2、Figma Token を検証します。Interaction Smoke は Selection + ERASE + OSNAP + Ortho + Inspector、Localization Smoke は zh-CN → ja-JP → en-US、**Modify Smoke** は実行中の同一 UCAD プロセスで MOVE + COPY + ROTATE + SCALE + MIRROR + OFFSET + TRIM + EXTEND を検証します。

## ドキュメント

- [Roadmap](ROADMAP.md)
- [Architecture](docs/ARCHITECTURE.md)
- [Release process](docs/RELEASE-PROCESS.md)
- [v0.5.0 Release Notes](docs/RELEASE-NOTES-v0.5.0.ja.md)

## ライセンス

UCAD は **GPL-2.0-only** で公開されています。
[简体中文](README.md) | [日本語](README.ja.md) | [English](README.en.md)

# UCAD

**Urban Computer-Aided Design · 都市計画支援CAD**  
都市計画・建築設計向けの軽量 2D CAD。

![Windows](https://img.shields.io/badge/Windows-10%2F11-blue) ![Windows App SDK](https://img.shields.io/badge/Windows%20App%20SDK-1.8-blue) ![MSIX](https://img.shields.io/badge/package-MSIX-blue) ![CI](https://github.com/KiYouJyo/UCAD/actions/workflows/ci.yml/badge.svg) ![License](https://img.shields.io/badge/license-GPL--2.0--only-blue)

## 現在の候補版

UCAD は Windows ネイティブ、AutoCAD に近い操作感、2D-first / DXF-first を基本とする建築・都市計画向け軽量 CAD を目指します。

**現在の受け入れ候補は v0.7.0 — CAD Authoring Foundation。** v0.5 Modify、v0.6 Layers & Properties、v0.7 Annotation / Hatch / Blocks を一つの実機受け入れテストへ統合しています。

v0.4.1 の 2 点/ドラッグ Window-Crossing、Shift 解除、透明な Windows pointer + Win2D CAD cursor、F3 OSNAP、F8 Ortho、ERASE、Inspector、多文書分離はそのまま回帰基準です。

## v0.5 — Modify

`MOVE (M)`, `COPY (CO/CP)`, `ROTATE (RO)`, `SCALE (SC)`, `MIRROR (MI)`, `OFFSET (O)`, `TRIM (TR)`, `EXTEND (EX)` を実装します。事前選択→コマンドと、コマンド→選択→Enter の両方式に対応し、OSNAP、該当する Ortho、トランジェントプレビュー、Undo/Redo を共通パイプラインで扱います。

## v0.6 — Layers & Properties

- 保護された `0` レイヤーを含むドキュメント単位のレイヤーテーブル
- 現在レイヤーの継承、作成、名前変更、削除、切替
- 表示/非表示、ロック、色、線の太さ、線種メタデータ
- オブジェクト単位の Layer / Color / Lineweight / Linetype と ByLayer 継承
- 非表示レイヤーは描画/OSNAP 対象外、ロック/非表示は選択/Modify pick 対象外
- `LAYER / LA`、`CHPROP / CH`
- レイヤー/プロパティ状態も Undo/Redo 対象

## v0.7 — Annotation, Hatch & Blocks

- `TEXT / T`: 1 行文字
- `DIM / DLI / DIMLINEAR`: 基本の整列線形寸法
- `HATCH / H`: 選択済み closed Polyline / Circle への Solid hatch
- `BLOCK / B`: 選択からブロック定義を作成
- `INSERT / I`: 尺度・回転角・挿入点を指定してブロック参照を挿入
- `EXPLODE / X`: 1 ブロック参照を 1 回の Undo 単位で分解

新しい Text / Dimension / Hatch / Block Reference は共通レンダリング、selection geometry、grip、交差/OSNAP query、Modify transform の基盤へ統合されます。

## Commands

| Category | Commands |
| --- | --- |
| Draw | `LINE`, `PLINE`, `RECTANGLE`, `CIRCLE`, `ARC`, `HATCH` |
| Modify | `MOVE`, `COPY`, `ROTATE`, `SCALE`, `MIRROR`, `OFFSET`, `TRIM`, `EXTEND`, `EXPLODE` |
| Annotate | `TEXT`, `DIM` |
| Layers / Properties | `LAYER`, `CHPROP` |
| Blocks | `BLOCK`, `INSERT` |
| Edit / View | `ERASE`, `UNDO`, `REDO`, `CLEAR`, `RESETVIEW` |

すべての実動コマンドは `CommandRegistry → CommandSession → CadWorkspaceSession / CadDocument` に統一されます。

## Localization / Validation

簡体字中国語・日本語・English は明示的な MRT Core `ResourceContext` により**再起動なし**で切り替わります。新しい authoring dialogs/prompts も同じ runtime language context を使用します。

v0.7.0 は Core tests、app-build、startup-smoke、Interaction Smoke、Localization Smoke、Modify Smoke、Authoring Smoke、MSIX/one-click package validation を同時に通過することを受け入れ条件とします。

## 今後

v0.8 以降は DXF-first import/export、print/PDF、建築補助、計画 parcel/indicator、GIS exchange、大規模図面 performance regression を進めます。3D/BIM、rendering、point cloud、完全な DWG / AutoCAD plug-in compatibility は 1.x の対象外です。

## Documents

- [Roadmap](ROADMAP.md)
- [Architecture](docs/ARCHITECTURE.md)
- [Release process](docs/RELEASE-PROCESS.md)
- [v0.7.0 Release Notes](docs/RELEASE-NOTES-v0.7.0.ja.md)

## License

UCAD は **GPL-2.0-only** で公開されています。

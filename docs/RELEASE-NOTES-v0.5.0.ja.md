# UCAD v0.5.0 — Modify Foundation

v0.5.0 は v0.4.x で整備した Selection / OSNAP / Ortho の基盤上に、最初の実用的な CAD 修正コマンド群を追加します。UI の拡張よりも、共通のジオメトリ変換・編集トランザクション・入力フローを整備し、既存の `CommandRegistry → CommandSession → CadWorkspaceSession → CadDocument` 構造を維持することを重視しています。

## 主な更新

- **MOVE / M**：事前選択とコマンド後選択の両方に対応。基点と 2 点目で移動し、エンティティ ID を保持するため選択状態を継続できます。1 操作 = 1 Undo です。
- **COPY / CO / CP**：基点と 2 点目から新しい ID の複写を作成します。
- **ROTATE / RO**：基点を指定後、キャンバス上の点またはコマンドラインの角度（度）で回転し、トランジェントプレビューを表示します。
- **SCALE / SC**：基点後に正の尺度係数を入力するか、点を指定して倍率を決定できます。
- **MIRROR / MI**：2 点で鏡像軸を定義します。既定では元オブジェクトを残し、必要に応じて削除できます。
- **OFFSET / O**：距離、対象、側点を指定します。基本実装は Line / Polyline / Circle / Arc を対象とします。
- **TRIM / TR**：Quick Trim 方式を採用し、他の表示エンティティを境界として対象部分をクリックして連続トリムできます。Enter で終了します。
- **EXTEND / EX**：Quick Extend 方式で Line、開いた Polyline、Arc の端を進行方向にある最寄りの有効境界まで延長します。

## 共通 Modify 基盤

- `CadEntityTransform` を追加し、移動・回転・尺度変更・鏡像を共通の不変ジオメトリ変換として実装しました。
- `CadOffset` と `CadTrimExtend` を Core に配置し、WinUI イベントコードに幾何アルゴリズムを持ち込まない構成にしました。
- `CadDocument` に `Replace` / `ReplaceRange` の単一編集トランザクションを追加し、MOVE / ROTATE / SCALE / MIRROR / TRIM / EXTEND を 1 回の Undo で戻せます。
- 実体を変更する操作は既存 ID を保持し、COPY、元を残す MIRROR、OFFSET など新規生成物には新しい ID を割り当てます。
- v0.4.x の SelectionSet、OSNAP、Ortho、透明なシステムカーソル + Win2D CAD カーソルをそのまま再利用します。
- CAD で一般的な「事前選択」と「コマンド開始後の選択」の両方をサポートします。

## 操作と UI

- Modify カテゴリを予約表示から実機能へ昇格し、8 つの基礎修正コマンドを共通 CommandRegistry に登録しました。
- 点入力型 Modify コマンドは OSNAP を利用でき、MOVE / COPY の変位入力では F8 Ortho も利用できます。
- 変換系コマンドと OFFSET はキャンバス上にリアルタイムプレビューを表示します。
- 修正フェーズの案内文を追加し、簡体中文 / 日本語 / English の再起動不要切替を維持します。
- v0.4.1 で確定した 2 クリック Window/Crossing、Shift 除外、調整可能な Crosshair / Pickbox / OSNAP aperture は維持されます。

## 検証

- Core テストで ID 保持、単一 Undo、移動・回転・尺度変更・鏡像、Line / Polyline / Circle / Arc の OFFSET、Quick TRIM / EXTEND の代表的なジオメトリを検証します。
- 独立した **Modify Smoke** を追加し、実際の UCAD プロセス内で MOVE + COPY + ROTATE + SCALE + MIRROR + OFFSET + TRIM + EXTEND を順番に実行して成功マーカーを確認します。
- Core tests、app-build、startup-smoke、Interaction Smoke、Localization Smoke、MSIX / one-click package validation、PerMonitorV2、バージョン SSOT、3 言語リソース一致検証を継続します。

## スコープ

v0.5.0 は第 1 段階の Modify Foundation です。高度な面取り・フィレット・配列・ストレッチ・グリップ編集・複雑オブジェクト・DWG 互換は本バージョンの対象外で、今回の共通編集トランザクションとジオメトリサービスを基盤として後続バージョンで拡張します。

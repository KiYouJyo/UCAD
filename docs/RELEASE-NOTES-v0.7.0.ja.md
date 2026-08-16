# UCAD v0.7.0 — CAD Authoring Foundation

この候補版では、当初の v0.5 / v0.6 / v0.7 を一つの受け入れテスト対象として統合します。v0.4.1 の Selection / OSNAP / Ortho / CAD cursor 基盤の上に、Modify、Layers/Properties、Text/Dimension/Hatch、Blocks をまとめて実装します。

## v0.5: Modify

- MOVE (`M`)、COPY (`CO`/`CP`)、ROTATE (`RO`)、SCALE (`SC`)、MIRROR (`MI`)
- OFFSET (`O`)、TRIM (`TR`)、EXTEND (`EX`)
- 事前選択 → コマンド、またはコマンド → 選択 → Enter の両方に対応
- Modify point input は既存 OSNAP を再利用し、MOVE/COPY は F8 Ortho に対応
- 変換と OFFSET のトランジェントプレビュー
- 編集は Entity ID を維持し、コピー系生成物は新しい ID を使用
- `CadDocument.Replace` / `ReplaceRange` による一貫した Undo/Redo トランザクション

## v0.6: Layers & Properties

- ドキュメント単位のレイヤーテーブルと保護された `0` レイヤー
- 新規オブジェクトは現在のレイヤーを継承
- レイヤー作成、名前変更、削除、現在レイヤー切替
- 表示/非表示、ロック、色、線の太さ、線種メタデータ
- オブジェクト単位のレイヤー / 色 / 線の太さ / 線種オーバーライドと ByLayer 継承
- 非表示レイヤーは描画・OSNAP 対象外、ロック/非表示レイヤーは選択・Modify pick 対象外
- `LAYER` / `LA` と `CHPROP` / `CH`
- レイヤーとプロパティ状態も Undo/Redo スナップショットへ保存

## v0.7: Annotation, Hatch & Blocks

- `TEXT` / `T`: 1 行文字、挿入点、高さ、回転角
- `DIM` / `DLI` / `DIMLINEAR`: 基本の整列線形寸法
- `HATCH` / `H`: 選択済みの閉じた Polyline または Circle へ Solid hatch
- Text / Dimension / Hatch が共通レンダリング、選択ジオメトリ、grip、Modify transform に参加
- ドキュメント単位の Block Definition テーブル
- `BLOCK` / `B`: 現在の選択から再利用可能なブロック定義を作成し基点を指定
- `INSERT` / `I`: ブロック、尺度、回転角を選び挿入点を指定
- `EXPLODE` / `X`: ブロック参照を 1 回の Undo 対象となる Replace 操作として分解

## Validation

v0.7.0 は Core tests、WinUI app build、startup-smoke、Interaction Smoke、Localization Smoke、Modify Smoke、Authoring Smoke、MSIX/one-click package validation を同時に通過する必要があります。Authoring Smoke は実際の UCAD プロセス内で Layers + Properties + Text + Dimension + Hatch + Block + Insert + Explode を検証します。

簡体字中国語 / 日本語 / English の再起動不要切替は明示的な MRT Core `ResourceContext` を継続使用します。v0.4.1 の 2 点 Window/Crossing、Shift 解除、透明なシステムポインター、Win2D CAD cursor、F3/F8、多文書分離も回帰テスト対象です。

## Scope boundary

本版は CAD authoring foundation です。DXF import/export、print/PDF、複雑な寸法スタイル、複雑な hatch pattern/島処理、dynamic/attribute blocks、STRETCH/ARRAY/FILLET/CHAMFER、3D/BIM、完全な DWG compatibility は v0.8 以降の対象です。

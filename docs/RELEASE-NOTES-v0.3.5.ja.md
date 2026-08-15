# UCAD v0.3.5 — Workspace Shell Foundation

v0.3.5 では、Figma で確認した Fluent CAD ワークスペースを既存の CAD Core に正式接続します。この版では作図コマンド数を増やすのではなく、v0.3 までに実装済みの能力を v0.4 / v0.5 に継続できる安定したデスクトップワークスペースへ統合します。

## 新しいワークスペース

- タイトルバーをブラウザー型の複数図面タブに変更。
- 各タブは実体のある独立したインメモリ CAD セッションで、それぞれ `CadDocument`、`CadViewport`、`CommandSession`、ズーム/パン、コマンドコンテキストを保持します。
- 上部は「カテゴリバー + 常設ツール棚」。作図カテゴリを選ぶと詳細ツールが表示されたままになり、同じカテゴリをもう一度クリックしたときだけ閉じます。
- 左側に高頻度ツール、右側に Inspector、下部にコマンドラインとステータスバーを配置。
- コマンド検索は UI 独自リストではなく `CommandRegistry` から直接生成します。

## 接続済みの既存機能

- LINE / L
- PLINE / PL
- RECTANGLE / REC
- CIRCLE / C
- ARC / A
- UNDO / U
- REDO
- CLEAR
- RESETVIEW / RV
- マウス指定と座標入力の統合
- `x,y`、`@x,y`、距離入力
- Enter / Space 確定、Esc キャンセル、直前コマンドの繰り返し
- 適応グリッド、クロスヘア、ズーム、パン

## v0.4 に向けて追加した Core 契約

- `CadCommandDefinition` に `CadCommandCategory` と任意の `DrawingCommandKind` を追加し、UI がコマンド名文字列から作図フローを判定する必要をなくしました。
- `CadDocument` に `Changed`、`Revision`、構造化された変更イベントを追加し、タブ、Inspector、履歴 UI が Core 状態を直接監視できるようにしました。
- `CadViewport` は外部所有の `CadDocument` を受け取れるようになり、「1 ウィンドウ = 1 図面」という暗黙の制約を解消しました。
- `CadWorkspaceSession` を追加し、1 つのタブが所有する Core 図面、Viewport、コマンドコンテキストを明示しました。

これらを v0.4 の選択、OSNAP、直交、選択オブジェクトのプロパティ表示の接続点とします。

## 意図的に無効のまま残す機能

新 UI には、選択、MOVE/COPY/OFFSET/TRIM、レイヤー、HATCH、OSNAP、ORTHO などの配置場所を先に用意しています。ただし v0.3.5 では未実装機能を実装済みのように見せず、対応する Core 機能が完成するまでコントロールを表示したまま無効化します。

## 多言語

新ワークスペースは以下に対応します。

- 简体中文（zh-CN）
- 日本語（ja-JP）
- English（en-US）

## 注意

v0.3.5 の図面タブは現時点ではインメモリワークスペースです。ファイルの保存/読み込みは未実装のため、作図内容を含むタブを閉じる際は内容が失われることを明示して確認します。

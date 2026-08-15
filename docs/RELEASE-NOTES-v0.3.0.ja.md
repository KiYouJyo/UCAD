# UCAD v0.3.0 — Drawing Foundation

v0.3 は UCAD で初めて「コマンド入力 → ライブプレビュー → エンティティ確定 → Undo/Redo」という 2D 作図の一連の流れを完成させるリリースです。

## 5 種類の作図コマンド

- `LINE` / `L`：連続線分。各区間は独立した Line エンティティとして確定。
- `PLINE` / `PL`：頂点を連続指定し、Enter / Space で 1 つの Polyline として確定。
- `RECTANGLE` / `REC`：2 つの対角点から閉じた Polyline 長方形を生成。
- `CIRCLE` / `C`：中心 + 半径点。中心指定後に半径値を直接入力することも可能。
- `ARC` / `A`：始点 + 円弧上の第 2 点 + 終点による 3 点円弧。

すべてのコマンドでマウス指定と v0.2 の `x,y`、`@x,y`、距離入力を混在でき、青色のライブプレビューを表示します。新規図面のデモ線も削除しました。

## Undo / Redo

- `UNDO` / `U` で直前の確定操作を元に戻す。
- `REDO` で再適用。
- ツールバーの Undo / Redo は履歴状態に応じて自動で有効化。
- CLEAR も Undo 可能な履歴に含まれます。

履歴機構は `UCAD.Core` に置かれ、将来の MOVE / COPY / TRIM / OFFSET と共有できます。

## ジオメトリモデル

`PolylineEntity`、`CircleEntity`、`ArcEntity` を追加。Arc は 3 点から真の中心・半径・掃引角を計算し、描画時のみサンプリングします。

## インストール

`UCAD-v0.3.0-x64-one-click.zip` を推奨します。同じ UCAD 公開証明書をすでに信頼している環境では、通常は再登録不要です。

## 次の段階

v0.4 ではクリック/ウィンドウ/クロッシング選択、複数選択、Delete、Endpoint/Midpoint/Center/Intersection OSNAP、Ortho を実装します。

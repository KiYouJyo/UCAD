# UCAD v0.4.0 — Selection / OSNAP / Ortho Interaction Foundation

v0.4.0 では UI の微調整よりも CAD の基本操作ループを優先します。本リリースでは Modify 系編集コマンドを追加せず、選択・オブジェクトスナップ・直交・Inspector の再利用可能な基盤を整備し、v0.5.0 の MOVE / COPY / TRIM / OFFSET などに備えます。

## 主な更新

- ドキュメント単位の `SelectionSet` を追加し、選択状態を XAML の一時状態から分離しました。Undo/削除で存在しなくなったエンティティは自動的に選択集合から除外されます。
- アイドル時のクリック選択、連続追加選択、AutoCAD 型の選択窓方向を実装しました。左→右は完全包含の Window、右→左は交差も含む Crossing です。
- プリセレクション、選択ハイライト、grip 表示を追加しました。空白クリックまたは Esc で選択解除できます。
- 基本 OSNAP として Endpoint / Midpoint / Intersection を実装しました。Core には Circle / Arc の Center snap も将来利用できる形で用意しています。
- OSNAP を実際のマウス作図入力とプレビューへ接続しました。F3 またはステータスバーの OSNAP で即時切り替えでき、状態は図面セッションごとに独立しています。
- Ortho を LINE / PLINE のマウス作図入力へ接続しました。F8 またはステータスバーの ORTHO で切り替えできます。
- Settings の「既定のオブジェクトスナップ / 既定のスナップ種類 / 既定の直交」が、新しく作成する Drawing セッションへ実際に適用されます。
- Inspector が選択中の Line / Polyline / Circle / Arc を読み取り、種類、選択数、基本ジオメトリ、Entity ID を表示するようになりました。
- ツールカテゴリの有効状態を `CommandRegistry` の実際の登録能力から導出します。未実装の Modify / Annotate / Layer / Block / Measure は Core 能力があるように見せません。
- UI 非依存の `CadRect`、境界ボックス、距離、矩形交差、線/円/円弧の交点計算を追加し、後続の Modify コマンドや空間インデックスでも再利用できるようにしました。
- v0.3.10 の簡体中文 / 日本語 / English の再起動不要切り替えを維持し、v0.4.0 のインタラクション文言も三言語化しました。

## 操作

- オブジェクトをクリック：選択。別オブジェクトを続けてクリックすると追加選択。
- 空白クリックまたは Esc：選択解除。
- 左→右ドラッグ：Window 選択（完全包含）。
- 右→左ドラッグ：Crossing 選択（包含または交差）。
- F3：OSNAP 切り替え。
- F8：ORTHO 切り替え。

## スコープ

v0.4.0 には MOVE、COPY、ROTATE、TRIM、EXTEND、OFFSET などの Modify コマンドや、AutoCAD の全 OSNAP 種類は含みません。これらは v0.5.x に進め、本リリースでは Selection / Drafting Aid の Core・Workspace・Viewport 境界を先に固定します。

## 検証

Core tests は SelectionSet、ヒットテスト、Window/Crossing、Line/Circle/Arc 選択、Endpoint/Midpoint/Intersection/Center snap、線円/円円交点、Ortho、CommandSession の可観測ライフサイクルを検証します。app-build、startup-smoke、MSIX/one-click validation、三言語 key parity、version SSOT、PerMonitorV2 も継続します。ピクセル単位の UI 調整は本リリースのゲートではありませんが、既存の Figma Design Tokens は CI で保護します。

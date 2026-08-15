# UCAD v0.3.7 — UI Fidelity & HiDPI Foundation

v0.3.7 は、v0.4.0 で UI と CAD Core の結合をさらに深める前に、表示品質とワークスペース基盤を固定するためのリリースです。新しい CAD Core コマンドは追加せず、高 DPI 環境で実機 UI がぼやける問題、承認済み Figma v0.2 と実際の WinUI シェルとの差、今後のインタラクション機能が依存する UI 基盤を整理します。

## HiDPI の鮮明さ

- カスタム application manifest に `PerMonitorV2` DPI awareness を復元し、互換用の `true/pm` 宣言も維持しました。
- `PerMonitorV2` が削除された場合に失敗する CI 契約を追加しました。
- v0.3.6 で追加した実起動 smoke test を維持し、Windows runner 上で UCAD を実際に起動して startup diagnostics を確認します。

## Figma v0.2 に合わせた高精度ワークスペース

- 承認済み 1440×900 Figma v0.2 デスクトップ画面を WinUI 3 ワークスペースの基準として反映しました。
- タイトル領域をブラウザー型のマルチドキュメント構造に再構成し、固定 UCAD ブランド領域の直後から約 `190×34` の図面タブを連続配置します。
- スクリーンショットや疑似タイトルバーに置き換えず、WinUI のネイティブなタイトルバー動作とシステムウィンドウボタンを維持します。
- 上部は `ファイル / 編集 / 表示 | 作図 / 修正 / 注釈 / レイヤー / ブロック / 計測 / 表示` の固定カテゴリ構成です。
- カテゴリを一度選ぶとツール棚は開いたままになり、現在のカテゴリをもう一度クリックしたときだけ折りたたまれます。
- 左側 52 px Tool Rail は引き続き高頻度コマンド専用です。
- Inspector、コマンドライン、ステータスバーを Figma の寸法・階層・ダークサーフェスに合わせて整理しました。

## デザイントークンとアイコン

- `UcadDesignTokens.xaml` を追加し、タイトルバー、カテゴリバー、ツール棚、Inspector、Canvas、ステータスバー、文字、区切り線、Accent の Figma 由来リソースを一元化しました。
- Cursor は Microsoft Fluent System Icons のベクター形状を使用し、Move、Copy、Trim、More などの一般操作には Fluent 系のシステムアイコンを使用します。
- Line、Polyline、Offset など CAD 固有のアイコンは、意味の異なる一般アイコンに無理に置き換えず、UCAD 専用 CAD Fluent アイコンセットが確定するまで簡易記号を維持します。
- 未実装カテゴリ全体を WinUI の Disabled 表示で薄くする方式をやめ、カテゴリ自体は読みやすく切り替え可能にし、未実装ツールだけを予約状態として明示します。

## シェル動作とバージョン表示

- 各図面タブは引き続き独立した実体の `CadWorkspaceSession` を所有し、見た目だけのタブには戻していません。
- LINE / PLINE / RECTANGLE / CIRCLE / ARC、Undo / Redo、Clear、Reset View は既存の統一コマンド経路を維持します。
- ステータスバーの UCAD バージョン表示は `UCAD v0.3.5` のハードコードを廃止し、アセンブリメタデータから動的に取得します。
- zh-CN、ja-JP、en-US の各リソースに予約ツール棚状態を追加し、古い v0.3.5 固定文言も整理しました。

## CI の回帰防止

既存の Core tests、app-build、startup-smoke、MSIX / one-click validation に加え、v0.3.7 ではシェル基盤の契約を追加します。

- `PerMonitorV2` が必須；
- シェルで v0.3.x の表示バージョンをハードコードしない；
- ブラウザー型タイトルバーに別の `AppTitleBar.Title` を再導入しない；
- Figma ベースの主要 UCAD design token が存在する；
- runtime resource key は zh-CN / ja-JP / en-US の全言語で揃っている。

## 範囲

本リリースでは CAD Core の Selection、OSNAP、Ortho、Move、Copy、Offset、Trim は実装しません。v0.4.0 では v0.3.7 で固定した `CadWorkspaceSession + CadViewport + Inspector + StatusBar` の境界上に、選択・スナップ・直交・プロパティ連動を接続します。

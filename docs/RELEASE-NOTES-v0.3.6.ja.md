# UCAD v0.3.6 — Startup Reliability Hotfix

v0.3.6 は、v0.3.5 で発生した起動回帰に対する信頼性ホットフィックスです。新たに追加した実起動スモークテストと起動ログにより、正確な原因を特定しました。新 UI の `MainWindow` コンストラクターが `ResourceLoader.GetString()` を使って XAML の `x:Uid` プロパティ用 `.Text` リソースキーを直接読み込んでおり、実行時に `0x80073B17 NamedResource Not Found` が発生していました。そのためウィンドウ表示前にアプリが終了し、UCAD をクリックしても反応がないように見えていました。

## 修正

- ツールシェルフのヒントを、`ToolShelfHintText.Text` のような XAML プロパティキーではなく、通常の名前付きリソース `ToolShelfHint` から読み込むよう修正しました。
- `GetString()` に欠落したランタイム名前付きリソースへの保護と診断を追加し、単一のローカライズキー欠落がそのままアプリ全体の起動クラッシュにならないようにしました。
- 最初の CAD ワークスペース生成を `MainWindow` のコンストラクターからルート視覚ツリーの `Loaded` 後へ延期し、起動段階の Win2D / ワークスペース初期化リスクも追加で縮小しました。
- v0.3.5 で導入したマルチドキュメント構成は維持し、`CadWorkspaceSession`、独立した `CadDocument`、`CadViewport`、`CommandSession` はロールバックしていません。
- 起動段階の診断ログを追加し、ウィンドウ生成、アクティブ化、初期ワークスペース生成、未処理例外を記録します。
- 起動ログ: `%LOCALAPPDATA%\UCAD\Logs\startup.log`

## CI の再発防止

従来の CI は Core テスト、WinUI ビルド、MSIX パッケージ生成のみを検証していたため、「ビルドとパッケージは成功するが起動直後に終了する」問題を検出できませんでした。

v0.3.6 では `startup-smoke` を追加・強化しました。

- Windows runner 上で `UCAD.App.exe` を実際に起動します。
- 8 秒後もプロセスが生存していることを確認します。
- 途中終了した場合は `startup.log` を出力して CI を失敗させます。
- コード側の `GetString()` には通常の名前付きリソースのみを許可し、`.Text` / `.Content` などの `x:Uid` プロパティキーを再び使用できないよう検査します。
- すべてのランタイムリソースキーが zh-CN / ja-JP / en-US の 3 つの RESW に存在することを検証します。
- アプリが終了しなくても、起動ログに `MissingResource` が記録された場合は CI を失敗させます。

PR CI では以下をすべて要求します。

- Core tests
- WinUI app build
- Startup smoke / runtime localization contract
- MSIX / one-click package validation

PR では一時的な one-click 検収パッケージも生成し、正式リリース前に実インストール環境で起動確認を行えるようにします。

## 範囲

このバージョンは起動信頼性と検証基盤だけを修正します。v0.3.5 のワークスペース UI、作図コマンド、CAD Core 境界は変更しません。Selection、OSNAP、Ortho など v0.4.0 の対話機能は従来のロードマップ通り進めます。

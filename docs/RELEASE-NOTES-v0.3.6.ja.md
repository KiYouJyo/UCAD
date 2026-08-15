# UCAD v0.3.6 — Startup Reliability Hotfix

v0.3.6 は、v0.3.5 で発生した起動回帰に対する信頼性ホットフィックスです。v0.3.5 では実際のマルチドキュメント・ワークスペースを導入しましたが、最初の `CadWorkspaceSession` が `MainWindow` のコンストラクター内で即座に生成され、それに伴って Win2D ベースの `CadViewport` もウィンドウ表示前に初期化されていました。一部の実インストール環境では、この経路によりウィンドウが表示される前にアプリが終了し、UCAD をクリックしても反応がないように見える可能性がありました。

## 修正

- 最初の CAD ワークスペース生成を `MainWindow` のコンストラクターからルート視覚ツリーの `Loaded` 後へ延期しました。
- v0.3.5 で導入したマルチドキュメント構成は維持し、`CadWorkspaceSession`、独立した `CadDocument`、`CadViewport`、`CommandSession` はロールバックしていません。
- 起動段階の診断ログを追加し、ウィンドウ生成、アクティブ化、初期ワークスペース生成、未処理例外を記録します。
- 起動ログ: `%LOCALAPPDATA%\UCAD\Logs\startup.log`

## CI の再発防止

従来の CI は Core テスト、WinUI ビルド、MSIX パッケージ生成のみを検証していたため、「ビルドとパッケージは成功するが起動直後に終了する」問題を検出できませんでした。

v0.3.6 では `startup-smoke` を追加しました。

- Windows runner 上で `UCAD.App.exe` を実際に起動します。
- 8 秒後もプロセスが生存していることを確認します。
- 途中終了した場合は `startup.log` を出力して CI を失敗させます。

PR CI では引き続き以下をすべて要求します。

- Core tests
- WinUI app build
- Startup smoke
- MSIX / one-click package validation

## 範囲

このバージョンは起動信頼性と検証基盤だけを修正します。v0.3.5 のワークスペース UI、作図コマンド、CAD Core 境界は変更しません。Selection、OSNAP、Ortho など v0.4.0 の対話機能は従来のロードマップ通り進めます。

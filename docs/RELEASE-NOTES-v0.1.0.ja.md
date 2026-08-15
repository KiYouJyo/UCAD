# UCAD v0.1.0 — Foundation Release

UCAD v0.1.0 は、都市計画・建築設計向け軽量 2D CAD の最初の実行可能な技術基盤を構築するリリースです。

## 主な内容

- WinUI 3 ネイティブ Windows シェルと Win2D GPU アクセラレーション対応ビューポート。
- 可変グリッド、原点軸、全画面クロスヘア。
- カーソル中心のホイールズームと中ボタンパン。
- ワールド座標 / スクリーン座標変換。
- UI から分離された `UCAD.Core` のジオメトリ / ドキュメント層。
- `CadPoint`、`CadVector`、`LineEntity`、`CadDocument` の基礎型。
- 2 点指定による線分作図。
- 简体中文 / 日本語 / English の 3 言語リソース基盤。
- GitHub Windows CI、MSIXBundle、固定証明書署名、Release 自動化。

## インストール

一般ユーザーは `UCAD-v0.1.0-x64-one-click.zip` をダウンロードして展開し、`① 安装UCAD.cmd` を実行してください。公開証明書、Release SHA-256、MSIX Authenticode 署名を検証して `UCAD_0.1.0.0_x64.msixbundle` をインストールします。

初回のみ、UCAD 公開 Release 証明書を `LocalMachine\TrustedPeople` に登録するため Windows UAC が表示されます。同じ証明書で署名された後続リリースでは通常、この信頼設定を繰り返す必要はありません。

## 現在の範囲

本リリースは技術基盤版です。DXF/DWG、AutoCAD 風コマンドライン、選択、OSNAP、編集ツール、レイヤー、注釈、都市計画向け機能は今後のマイルストーンで追加予定です。

## 次のマイルストーン

v0.2 ではコマンドエイリアスと Enter / Esc / Space を中心とする CAD コマンド状態機械を優先します。

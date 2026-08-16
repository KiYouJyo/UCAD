# UCAD v0.3.9 — UI Completion / Figma Fidelity / Start & Settings Foundation

v0.3.9 は UI Foundation Completion リリースです。CAD Core の機能追加ではなく、WinUI 3 Shell、Start、Settings、ページ遷移の操作ロジックを UCAD の Figma ビジュアル SSOT に収束させ、今後 Selection / OSNAP / Inspector を追加しても大きく作り直さずに済む UI 基盤を整備します。

## 主な変更

- ブラウザー型タイトルバーを再構築：UCAD Brand → 連続した Document Tabs → `+` → ドラッグ領域 → Windows のネイティブキャプションボタン。
- Drawing / Start / Settings の 3 種類の Workspace Page を明確に分離。既定では `+` が Start を開き、「新しいタブに Start を表示」を無効にすると空白 Drawing を直接作成します。
- Start Center を完成：新規/開く、最近使用したファイルの空状態、Blank / Architecture / Urban Planning テンプレートの情報構造、Learn UCAD。未実装のファイル、最近使用、専門テンプレート機能は利用可能に見せかけません。
- General / Appearance / Drafting / Input & Interaction / Files & Save / Language & Region / About UCAD の 7 設定ページを実装。
- Settings は Figma のリズムに統一：228 px ナビ、54 px コンテンツ開始位置、940×72 カード、35 / 12 / 8 / 30 px の縦間隔。About のアプリ情報カードは 940×128。
- App Theme と CAD Canvas Theme を実際の動作でも独立化。App Theme は Shell / ネイティブコントロールの明暗パレットを切り替え、Canvas Theme は図形、作図プレビュー、グリッド、クロスヘアを独立して制御します。Canvas Background も別設定のままです。
- キャンバス背景、グリッド表示/不透明度、カーソル中心ズーム、中ボタンパン、ホイール反転、座標精度、小数形式を実行時ロジックに接続。
- 未実装の Restore Session、手動 UI Scale、自動更新チェック、最近履歴の消去は無効化または明確な予約状態とし、バックエンド機能を偽装しません。
- 一般操作は Fluent / WinUI アイコンへ移行し、CAD 専用形状は PathIcon を使用。Unicode の仮アイコンを削除。
- `SettingsService` / `AppSettings` を導入し、設定を `%LOCALAPPDATA%\UCAD\settings.json` に集中保存。
- zh-CN / ja-JP / en-US の Start / Settings リソースを完全化。表示言語は次回 Shell 作成前に一括適用し、現在のセッションで部分的な混在を起こしません。
- ルート `VERSION` をバージョン SSOT とし、Assembly / UI / release metadata / MSIX Package を 0.3.9 に統一。
- PerMonitorV2 を維持し、XAML DIP を使用。ビットマップによる UI スケーリングは導入しません。

## CAD Core

Selection、OSNAP、Ortho、MOVE、COPY、TRIM、OFFSET、DWG/DXF、GIS、Architecture Objects、Planning Objects は本リリースでは追加しません。

既存の LINE / PLINE / RECTANGLE / CIRCLE / ARC / Undo / Redo / Clear / Reset View、マルチドキュメント、コマンドライン、CommandRegistry、Zoom / Pan は従来どおり `CommandRegistry → CommandSession → CAD Core` を通ります。

## 検証

必須 CI は以下を検証します。

- Core tests；
- app-build；
- UCAD を実際に起動し Start / Settings を初期化する startup-smoke；
- package-validation と one-click package 検証；
- Figma の主要寸法・色・Design Token 契約；
- Start / Settings / Canvas の主要動作契約；
- 3 言語リソースキー一致；
- PerMonitorV2；
- バージョン SSOT；
- Unicode 仮アイコン検査。

ピクセル単位の 1440×900 Figma 比較は手動の `UI Fidelity Screenshots` ワークフローとして残します。ランナーが実際の 1440×900 インタラクティブデスクトップを提供する場合のみ実行し、ホスト画面の制約が機能検証や正式リリースを妨げないようにしています。
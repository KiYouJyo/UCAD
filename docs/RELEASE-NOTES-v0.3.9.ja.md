# UCAD v0.3.9 — UI Completion / Figma Fidelity / Start & Settings Foundation

v0.3.9 は UI Foundation Completion リリースです。CAD Core の機能追加ではなく、実際の WinUI 3 UI を UCAD の Figma ビジュアル SSOT に収束させ、今後 Selection / OSNAP / Inspector を追加しても作り直さずに済む Shell を整備します。

## 主な変更

- ブラウザー型タイトルバーを再構築：UCAD Brand → 連続した Document Tabs → `+` → ドラッグ領域 → Windows のネイティブキャプションボタン。
- Drawing / Start / Settings の 3 種類の Workspace Page を明確に分離。`+` は Start を開き、Start の「新規図面」からのみ `CadWorkspaceSession` を作成します。
- Start Center を完成：新規/開く、最近使用したファイルの空状態、Blank / Architecture / Urban Planning テンプレートの情報構造、Learn UCAD。未実装のファイル I/O やテンプレート機能は利用可能に見せかけません。
- General / Appearance / Drafting / Input & Interaction / Files & Save / Language & Region / About UCAD の 7 設定ページを実装。
- Settings は Figma のリズムに統一：228 px ナビ、54 px コンテンツ開始位置、940×72 カード、35 / 12 / 8 / 30 px の縦間隔。About のアプリ情報カードは 940×128。
- App Theme と CAD Canvas の状態を分離。キャンバス背景、グリッド表示/不透明度、カーソル中心ズーム、中ボタンパン、ホイール反転を既存 Viewport に接続。
- 一般操作は Fluent / WinUI アイコンへ移行し、CAD 専用形状は PathIcon を使用。Unicode の仮アイコンを削除。
- `SettingsService` / `AppSettings` を導入し、設定を `%LOCALAPPDATA%\UCAD\settings.json` に集中保存。
- zh-CN / ja-JP / en-US の Start / Settings リソースを完全化。表示言語は次回起動前に適用し、現在のセッションで部分的な混在を起こしません。
- ルート `VERSION` をバージョン SSOT とし、Assembly / UI / release metadata / MSIX Package を 0.3.9 に統一。
- PerMonitorV2 を維持し、XAML DIP を使用。ビットマップによる UI スケーリングは導入しません。

## CAD Core

Selection、OSNAP、Ortho、MOVE、COPY、TRIM、OFFSET、DWG/DXF、GIS、Architecture Objects、Planning Objects は本リリースでは追加しません。

既存の LINE / PLINE / RECTANGLE / CIRCLE / ARC / Undo / Redo / Clear / Reset View、マルチドキュメント、コマンドライン、CommandRegistry、Zoom / Pan は従来どおり `CommandRegistry → CommandSession → CAD Core` を通ります。

## 検証

Core tests、app-build、startup-smoke、package-validation に加えて、Figma 寸法契約、3 言語リソース一致、PerMonitorV2、バージョン SSOT、Unicode 仮アイコン検査、および Drawing / Start / Settings General / Appearance / Input & Interaction / About の 1440×900 実行時スクリーンショットを検証します。

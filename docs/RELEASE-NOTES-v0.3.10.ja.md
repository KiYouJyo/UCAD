# UCAD v0.3.10 — Live Trilingual Localization Hotfix

v0.3.10 は、v0.3.9 で Start / Settings の 3 言語リソースが正しく読み込まれず、表示言語の変更に再起動が必要だった問題を修正するホットフィックスです。CAD Core の新機能は追加しません。

## 主な修正

- Start / Settings に `Start_TabTitle`、`Settings_Nav_Title` などのリソースキー自体が表示される問題を修正。
- Windows App SDK の名前付きリソースマップ方式に合わせ、既定 PRI パス + `UcadV039` ResourceMap から `UcadV039.resw` を読み込むよう修正。
- zh-CN / ja-JP / en-US の実行時リソースコンテキストを一元管理する `LocalizationService` を追加。
- **再起動なし**で簡体字中国語 / 日本語 / English を切り替え可能。現在の Window、Start、Settings、ドキュメントタブ、メニュー、カテゴリバー、Inspector、コマンド領域、ステータスバーをその場で再ローカライズします。
- 言語変更時も Window や既存の `CadWorkspaceSession` を再生成しないため、図形、Undo/Redo 履歴、ビュー状態、複数ドキュメントセッションを保持します。
- 「システム言語に従う」も維持。無効時は zh-CN / ja-JP / en-US を選択して即時反映できます。
- 未保存図面のタブ名も「图纸 1 / 図面 1 / Drawing 1」のように言語変更へ追従します。
- 言語設定の説明を更新し、再起動が必要という案内を廃止。

## 検証

独立した Localization Smoke を追加し、**同一 UCAD プロセス**内で `zh-CN → ja-JP → en-US` を順番に切り替え、Start / Settings / Shell の代表リソースがリソースキーではなく実際の翻訳を返すことを確認します。

既存の Core tests、app-build、startup-smoke、package-validation、バージョン SSOT、PerMonitorV2、3 言語リソース key parity も継続します。

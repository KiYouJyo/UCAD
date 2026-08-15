[简体中文](README.md) | [日本語](README.ja.md) | [English](README.en.md)

# UCAD

**Urban Computer-Aided Design · 都市計画支援CAD**  
都市計画・建築設計向けの軽量 2D CAD です。

![Windows](https://img.shields.io/badge/Windows-10%2F11-blue) ![Windows App SDK](https://img.shields.io/badge/Windows%20App%20SDK-1.8-blue) ![MSIX](https://img.shields.io/badge/package-MSIX-blue) ![CI](https://github.com/KiYouJyo/UCAD/actions/workflows/ci.yml/badge.svg) ![License](https://img.shields.io/badge/license-GPL--2.0-blue)

## 位置づけ

UCAD は Windows ネイティブ、AutoCAD に近い操作感、建築・都市計画で頻繁に使う 2D 作図に焦点を置く軽量 CAD を目指します。DXF-first / 2D-first を基本方針とし、AutoCAD 全機能の複製は目標にしません。

v0.1.0 は Foundation Release です。Win2D ビューポート、グリッド、座標変換、ズーム / パン、基本 Line エンティティを実装済みです。

## インストール

[GitHub Releases](https://github.com/KiYouJyo/UCAD/releases/latest) から以下を利用できます。

- `UCAD-v0.1.0-x64-one-click.zip`：推奨。展開後 `① 安装UCAD.cmd` を実行すると、署名と SHA-256 を検証して MSIX をインストールします。
- `UCAD_0.1.0.0_x64.msixbundle`：直接サイドロードする場合。
- `SHA256SUMS.txt`：Release アセット検証用。

初回 one-click インストールでは、UCAD の公開証明書を `LocalMachine\TrustedPeople` に登録するため Windows UAC が一度表示されます。昇格は証明書の信頼設定だけに使われ、その後の MSIX インストールは通常ユーザーのコンテキストで実行されます。同じ Release 証明書を使う更新では通常、再登録は不要です。

## 3 言語対応

v0.1 から 简体中文（zh-CN）/ 日本語（ja-JP）/ English（en-US）のリソース構造を採用しています。主要 UI、Package Manifest、README、Release Notes が対象です。

## リポジトリ構造

```text
src/UCAD.Core/          CAD コア
src/UCAD.App/           WinUI 3 / Win2D と MSIX
tests/                  自動テスト
packaging/              one-click インストーラーと検証
release/                Release SSOT
docs/                   ドキュメント
.github/workflows/       CI / Release
```

## ドキュメント

- [Roadmap](ROADMAP.md)
- [Architecture](docs/ARCHITECTURE.md)
- [Release process](docs/RELEASE-PROCESS.md)
- [Packaging](packaging/README.md)
- [Contributing](CONTRIBUTING.md)
- [Support](SUPPORT.md)
- [Privacy](PRIVACY.md)
- [Third-party notices](THIRD-PARTY-NOTICES.md)
- [v0.1.0 Release Notes](docs/RELEASE-NOTES-v0.1.0.ja.md)

## ライセンス

UCAD は **GPL-2.0-only** で公開されています。第三者コンポーネントは各ライセンスに従います。

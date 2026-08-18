# UCAD v0.8.0 — ドキュメント／交換基盤

UCAD v0.8.0 は v0.7 の 2D CAD 作図ループを、保存・交換・印刷まで含むドキュメントワークフローへ拡張した受け入れ候補です。

## 主な更新
- `.ucad` 保存/読込、最近使ったファイル、関連付け起動、自動保存/復旧基盤。
- DXF 入出力と拡張 2D エンティティ交換。
- POINT / ELLIPSE / SPLINE / RAY / XLINE。
- STRETCH / ARRAY / FILLET / CHAMFER / JOIN / BREAK / Polyline Edit。
- MTEXT、Leader、角度/半径寸法、Hatch/Block 管理。
- Layout、Page Setup、複数 Viewport、印刷プレビュー、ベクター PDF。
- 建築・都市計画向け基礎ヘルパー。
- GeoJSON、CSV Point、Shapefile/DBF/PRJ、CRS 交換。
- 空間インデックス基盤。

## 対象外
完全な DWG 互換、3D/BIM、Dynamic Block、AutoCAD API 完全互換は対象外です。

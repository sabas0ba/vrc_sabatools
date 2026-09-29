# Changelog

このパッケージの変更点をまとめています。
フォーマットは [Keep a Changelog](https://keepachangelog.com/ja/1.1.0/) に、
バージョニングは [Semantic Versioning](https://semver.org/lang/ja/) に従います。

## [Unreleased]

### Changed

- Apache-2.0 の宣言、公式ライセンスURL、同梱するライセンス全文と README の表記を統一。

## [0.2.0] - 2026-09-07

### Changed

- パッケージのバージョンを `0.1.0` から `0.2.0` に更新（`a846a621`）。このバージョン更新コミットには実装コードの変更はありません。

## [0.1.0] - 2026-08-25

### Added

- `AvatarInspectionModule`: `VRCAvatarDescriptor` を実際の型で読むアバター向け検査。core の `InspectionModule` を継承し、`TypeCache` 経由で自動的に有効になります
  - Descriptor の有無と重複
  - ViewPosition (原点のまま・足元以下・過大な高さ・左右のずれ)
  - Expression Parameters のビット消費 (256 上限) と名前の重複
  - Expressions Menu の 8 コントロール上限、サブメニューの循環参照・未割り当て、階層の深さ
  - Playable Layers の未設定 Animator Controller
  - Lip Sync と Eye Look の設定漏れ
  - PhysBone が影響する Transform 数の推定
- `AvatarLimits`: 上限値の計算のみを持つ Unity 非依存クラス

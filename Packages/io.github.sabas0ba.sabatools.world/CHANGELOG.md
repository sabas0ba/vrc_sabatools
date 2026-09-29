# Changelog

このパッケージの変更点をまとめています。
フォーマットは [Keep a Changelog](https://keepachangelog.com/ja/1.1.0/) に、
バージョニングは [Semantic Versioning](https://semver.org/lang/ja/) に従います。

## [Unreleased]

### Changed

- Apache-2.0 の宣言、公式ライセンスURL、同梱するライセンス全文と README の表記を統一。


## [0.1.0] - 2026-08-25

### Added

- `WorldInspectionModule`: `VRCSceneDescriptor` を実際の型で読むワールド向け検査。core の `InspectionModule` を継承し、`TypeCache` 経由で自動的に有効になります
  - Descriptor の有無と重複
  - Spawn の有無、空スロット、傾き
  - RespawnHeightY とシーン最下部ジオメトリの比較
  - Reference Camera の割り当てと Near Clip Plane
  - Udon Behaviour / Pickup / Station / Mirror の数と、初期状態で有効なミラー
- `WorldLimits`: しきい値の計算のみを持つ Unity 非依存クラス

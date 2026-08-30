# Changelog

このファイルは [Keep a Changelog](https://keepachangelog.com/ja/1.1.0/) の形式に基づきます。

## [0.1.0] - 2026-08-30

### Added

- Avatar の Material slot と Texture property の一覧・Undo 対応編集
- 17 種類の照明環境を並列比較する Preview Scene 表示
- アバター全体で実際に使用中の Texture inventory と参照元編集
- lilToon 2.3.4 以降への VPM 依存と検証用 Material 対応
- Package Managerからimport可能なlilToon Avatar Material demo sample
- Orbit／Free Fly／Scene View Follow camera
- Render Queue設定検査、queue範囲filter、半透明world-object probe比較
- Renderer bounds overlayとViewPosition基準の48方向frustum検査
- 透明衣装を含む人型silhouetteへdemo sampleを更新
- VRChat shader fallback の近似 preview
- Android Per-Platform Override と Quest mobile shader の preview
- 日本語／英語UI切替（日本語既定）と調整可能・折りたたみ可能な左右ペイン
- 標準Render Queue範囲とMesh表示条件の並列比較、Object名／Queue区分のGizmo・Text overlay
- Near／Middle／Far固定ProbeとユーザーCameraの並列Bounds Preview、上面／側面のCamera位置関係図
- TextureソースファイルのSHA-256比較による重複Asset検出
- Directional／Point Lightの位置・方向Gizmo
- 同一PNGを別Asset pathから参照するTexture重複検出用Sample

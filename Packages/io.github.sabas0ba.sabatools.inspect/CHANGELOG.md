# Changelog

このパッケージの変更点をまとめています。
フォーマットは [Keep a Changelog](https://keepachangelog.com/ja/1.1.0/) に、
バージョニングは [Semantic Versioning](https://semver.org/lang/ja/) に従います。

## [Unreleased]

## [0.1.0] - 2026-08-25

### Added

- `Tools > SabaTools > Inspect Window`: 非破壊の検査ウィンドウ
  - 統計 (三角形数・マテリアル・テクスチャ GPU メモリ推定・ボーン・PhysBone 等)
  - Missing 参照の検出 (missing script / prefab / mesh / bone / シリアライズ参照)。Ping ボタンで該当箇所へ移動できます
  - アバター向けパフォーマンスランク参考値のチェックリスト (PC)。しきい値は 2026-08 時点の公式ドキュメントに基づく参考値です
  - レポートの Markdown コピーおよびファイル書き出し
- `Tools > SabaTools > Inspect Selection`: 選択オブジェクトをそのままスキャン
- VRChat SDK 非依存。SDK コンポーネントは型名で数えるため、SDK が無いプロジェクトでも統計と参照チェックが動作します

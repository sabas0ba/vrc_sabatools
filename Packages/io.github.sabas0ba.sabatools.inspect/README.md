# SabaTools Inspect

VRChat のアバター・ワールド編集向けの**非破壊**検査ツールです。対象の階層を走査して統計と問題点を一覧表示します。シーンには一切変更を加えません。

## 機能

- **統計**: GameObject 数、三角形数、Mesh / Skinned Mesh Renderer 数、ユニークボーン数、マテリアルスロット・ユニークマテリアル・シェーダー数、テクスチャ数と GPU メモリ推定、PhysBone / Contact / Constraint 数 (SDK 導入時)、Particle System・Light・Audio Source 等のコンポーネント数
- **Missing 参照の検出**: Missing script、Prefab アセット欠落、メッシュ未割り当て、null ボーンスロット、空マテリアルスロット、Inspector で "Missing" と表示されるシリアライズ参照。各項目は Ping ボタンで該当オブジェクトへ移動できます
- **チェックリスト**: アバターは VRChat の PC パフォーマンスランク (参考値) に対する評価、ワールドはリアルタイムライト等の一般的な注意点
- **Markdown 書き出し**: レポートをクリップボードへコピー、またはファイルへ保存

## 使い方

1. `Tools > SabaTools > Inspect Window` を開く
2. Target にアバターやワールドのルートを指定して **Scan Target**、またはシーン全体を **Scan Active Scene**
3. Mode は既定の Auto で `VRCAvatarDescriptor` / `VRCSceneDescriptor` の有無から判定されます。SDK が無いプロジェクトでは Generic (統計と参照チェックのみ) になります

Hierarchy でオブジェクトを選択して `Tools > SabaTools > Inspect Selection` でも起動できます。

## 判定の根拠と限界

- テクスチャの GPU メモリはフォーマットの bpp とミップ係数 4/3 による**推定値**です。Unity の実測とは差が出ます。未知のフォーマットは 32 bpp として扱い、その旨を Findings に出します
- パフォーマンスランクのしきい値は 2026-08 時点の [VRChat 公式ドキュメント](https://creators.vrchat.com/avatars/avatar-performance-ranking-system/) に基づく参考値です。VRChat 側で改定されうるものであり、本パッケージの CI は追随を検証していません。正式な判定は SDK と公式ドキュメントを参照してください
- VRChat SDK には依存しません。SDK コンポーネントは型名で数えており、SDK が無いプロジェクトでは該当項目が 0 になります

## 動作環境

Unity 2022.3 (VRChat 推奨バージョン準拠)。Editor 専用アセンブリのみで構成され、ビルド成果物には何も含まれません。

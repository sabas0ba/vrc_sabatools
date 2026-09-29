# SabaTools Inspect for Worlds

[SabaTools Inspect Core](../io.github.sabas0ba.sabatools.core/README.md) にワールド固有の検査を追加するパッケージです。core と同じく**非破壊**で、シーンにもアセットにも書き込みません。

**ワールド用プロジェクトにのみ導入してください。** このパッケージは `com.vrchat.worlds` に依存します。VRChat の Avatars SDK と Worlds SDK は同一プロジェクトでの併用が想定されていないため、アバター用プロジェクトには [SabaTools Inspect for Avatars](../io.github.sabas0ba.sabatools.avatar/README.md) を使ってください。

## 追加される検査

`VRCSceneDescriptor` を型名ではなく実際の型で読むため、core だけでは分からないことを検査できます。

| 項目 | 内容 |
| --- | --- |
| Descriptor | 存在すること、シーン内に複数無いこと |
| Spawn | 使用可能な Spawn があること、空スロットが無いこと、傾いていないこと |
| Respawn Height | シーン内の最下部ジオメトリより下にあること |
| Reference Camera | 割り当ての有無、Camera コンポーネントの有無、Near Clip Plane |
| SDK コンポーネント | Udon Behaviour / Pickup / Station / Mirror の数、初期状態で有効なミラー |

## 判定の根拠と限界

- Spawn の空スロットを Error としているのは、Spawn Order が Random の場合に原点へ飛ばされうるためです
- Respawn Height の比較対象は Renderer の bounds の最下部です。Collider ではなく Renderer を見ているのは、Terrain で作られたワールドを取りこぼさないためです。装飾用の巨大なメッシュがある場合は誤検出しえます
- ミラーとその数のしきい値は VRChat が定めた上限ではなく、本パッケージの判断です。Findings にもその旨を出します

## 動作環境

Unity 2022.3、`com.vrchat.worlds` 3.10.4 以降。Editor 専用アセンブリのみで構成され、ビルド成果物には何も含まれません。

## ライセンス

本パッケージは Apache License 2.0 で提供します。全文は [LICENSE.md](LICENSE.md) を参照してください。外部依存・モデル・素材には、それぞれの配布元のライセンスが適用されます。

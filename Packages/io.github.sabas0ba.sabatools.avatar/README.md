# SabaTools Inspect for Avatars

[SabaTools Inspect Core](../io.github.sabas0ba.sabatools.core/README.md) にアバター固有の検査を追加するパッケージです。core と同じく**非破壊**で、シーンにもアセットにも書き込みません。

**アバター用プロジェクトにのみ導入してください。** このパッケージは `com.vrchat.avatars` に依存します。VRChat の Avatars SDK と Worlds SDK は同一プロジェクトでの併用が想定されていないため、ワールド用プロジェクトには [SabaTools Inspect for Worlds](../io.github.sabas0ba.sabatools.world/README.md) を使ってください。

## 追加される検査

`VRCAvatarDescriptor` を型名ではなく実際の型で読むため、core だけでは分からないことを検査できます。

| 項目 | 内容 |
| --- | --- |
| Descriptor | 存在すること、階層内に複数無いこと |
| ViewPosition | 原点のまま・足元以下・人間離れした高さ・左右へのずれ |
| Expression Parameters | ビット消費と 256 上限、パラメータ名の重複、メニューがあるのに未割り当て |
| Expressions Menu | 1 メニュー 8 コントロール上限、サブメニューの循環参照と未割り当て、深すぎる階層 |
| Playable Layers | Default を外したまま Animator Controller が未設定のレイヤ |
| Lip Sync | Viseme 方式なのに顔メッシュや Viseme が未設定 |
| Eye Look | 有効なのに目のボーンが未設定 |
| PhysBone | 影響を受ける Transform 数の推定値 (コンポーネント数は core が数えます) |

## 判定の根拠と限界

- Expression Parameters のビット消費は、SDK の `CalcTotalCost()` ではなく `AvatarLimits` で自前に計算しています。計算自体を Unity 無しでテストできるようにするためです。単価は公式ドキュメントの値 (Bool = 1、Int / Float = 8 bit) です
- PhysBone の Transform 数は**推定値**です。root 以下の Transform から Ignore Transforms を除いた数であり、SDK が行う endpoint 処理や分岐の扱いは再現していません
- Enum の値は名前 (`ToString()`) で比較しています。SDK の更新で値が増えた場合、コンパイルエラーではなく「認識しない値」として扱われます

## 動作環境

Unity 2022.3、`com.vrchat.avatars` 3.10.4 以降。Editor 専用アセンブリのみで構成され、ビルド成果物には何も含まれません。

## ライセンス

本パッケージは Apache License 2.0 で提供します。全文は [LICENSE.md](LICENSE.md) を参照してください。外部依存・モデル・素材には、それぞれの配布元のライセンスが適用されます。

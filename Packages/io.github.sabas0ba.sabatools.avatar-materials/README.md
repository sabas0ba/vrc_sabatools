# SabaTools Avatar Material Studio

VRChatアバターのMaterialとTextureを一覧化し、照明、Render Queue、Renderer Boundsを同じEditorWindowで比較するVPMパッケージです。衣装の一部が特定の描画条件で消える、照明によって見え方が崩れる、遠距離や特定方向からMeshが欠ける、といった問題をアップロード前に調査できます。

![Render Queue範囲ごとにMaterial slotの表示結果を比較する画面](Documentation~/images/render-queue.png)

## 確認できること

- Material slot単位でRender Queue範囲を抽出・除外し、衣装やアクセサリーの消失箇所を特定
- 17種類の照明条件を同時比較し、Directional LightのベクトルとPoint Lightの照射元方向をGizmo表示
- Renderer Boundsを近・中・遠距離および16方向から検査し、Frustum外になるMeshを確認
- 使用中Textureの参照元、重複画像、Tiling、Offset、shader設定を一括確認
- Original、VRChat Fallback、Quest/Android近似表示を切り替えて差分を確認

Previewは非保存のPreview Scene上に複製したアバターへ適用されます。Render Queueの明示編集、Material slot、Texture、Tiling、Offsetの変更だけがSceneまたはMaterial assetへ書き込まれ、すべてUnity Undoに記録されます。

## クイックスタート

1. `Tools > SabaTools > Avatar Material Studio` を開きます。
2. Hierarchyでアバターまたはその子を選択し、`選択を使用` を押します。
3. `マテリアルとTexture`、`ライティング`、`Render Queue`、`描画Bounds`を切り替えます。
4. Previewは既定で最後に操作したScene Viewのカメラへ追従します。各Viewのサイズは`View UI倍率`で調整します。
5. 問題のあるMaterialまたはRendererを左側の一覧で特定し、必要な場合だけ設定を編集します。

各画面の操作、全パラメータ、Render Queue区分、制約は[使用ガイド](Documentation~/usage.md)を参照してください。

## Demo

Package ManagerのSamplesから`lilToon Avatar Material Demo`をimportし、`Tools > SabaTools > Open Material Studio Demo`を実行します。Shadow、Emission、MatCap、Rim Light、Outline、透明衣装、Render Queue差分、Texture重複検出を確認できる人型検証Sceneが生成されます。

## 要件

- Unity 2022.3
- VRChat Avatars SDK 3.10.4以降
- lilToon 2.3.4以降

lilToonは公式VPM repository `https://lilxyzw.github.io/vpm-repos/vpm.json` をVCCへ登録して解決してください。Editor専用であり、Runtime assemblyやavatar buildへ追加されるcomponentはありません。

## ライセンス

SabaTools Avatar Material Studioは[Apache License 2.0](LICENSE.md)で提供します。VRChat SDKおよびlilToonは外部依存であり、このパッケージへソースを同梱していません。それぞれの配布元が定めるライセンスが適用されます。

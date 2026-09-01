# Avatar Material Studio Demo

このsampleはlilToon 2.3.4以降を使い、次の検証用assetを生成します。

- `lilToon` Material: Main Texture、Shadow、Emission、MatCap、Rim Light
- `Hidden/lilToonOutline` Material: Main Texture、Shadow、Emission、Outline Texture、Outline Width Mask
- `Hidden/lilToonTransparent` Material: Render Queue 3000、ZWrite Offの透明衣装
- Background／Geometry／AlphaTest／GeometryLast／Transparent／Overlayを単一RendererのMaterial slotに割り当てたQueue比較用衣装
- 5種類のprocedural Textureと、頭・胴・腕・脚・accessoryを分離した人型silhouette

Import後に`Tools > SabaTools > Open Material Studio Demo`を実行してください。
`Assets/AvatarMaterialStudioDemo`へassetとSceneを生成し、`Avatar Material Studio`を開きます。

生成先に同名assetがある場合は、このsample用の設定で更新します。

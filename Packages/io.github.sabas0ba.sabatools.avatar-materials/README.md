# SabaTools Avatar Material Studio

アバターに割り当てた Material と Texture を一覧化し、編集と表示比較を同じ EditorWindow で行う VPM パッケージです。

`Tools > SabaTools > Avatar Material Studio` から開きます。Hierarchy でアバターまたはその子を選択して `Use Selection` を押すと、`VRCAvatarDescriptor` のあるルートを対象にします。

## 機能

- inactive object を含む全 Renderer の Material slot を Hierarchy path 順に表示
- Material、shader、VRChat fallback 種別、Quest shader 対応状況による検索・確認
- Material slot の差し替え
- shader が公開する全 Texture property の Texture、Tiling、Offset 編集
- アバター全体で使用中の全 Texture を一意に集約し、Material property と Renderer slot の参照元を表示・編集
- 異なる Texture Asset のソースファイル内容を SHA-256 で比較し、同一画像の重複配置を検出
- Material がアバター内の何 slot から共有されているかを表示
- Original／Fallback／Quest のモード選択と、17 種類の照明環境の並列グリッド表示
- 無照明、環境光の下限／上限、Directional Light の強度／色／複数灯、逆光、Point Light の距離／方向／複数灯、混合光を比較し、Light の位置と方向を Gizmo 表示
- Orbit、Free Fly、Scene View FollowのPreview camera
- Mesh／MaterialごとのRender Queue、RenderType、ZWrite／ZTest／Cull／Blend、sorting設定の一覧と矛盾検出
- 標準 Queue 範囲ごとに全表示／範囲内のみ／範囲外のみを並べ、各ObjectのBounds、名前、Queue番号、区分を重ねて表示
- Mesh／Material設定一覧の折りたたみと左右ペイン幅のドラッグ調整
- 手前の半透明world-object probeを2501／3000／3100／4000で並列比較
- MeshRenderer／SkinnedMeshRendererのlocal／world bounds表示とwireframe overlay
- ViewPositionを中心とする近距離／中距離／長距離×16方向のfrustum検査
- 選択方向の近距離／中距離／長距離とユーザー操作Cameraを並列表示し、上面／側面の位置関係図を表示
- Material、Render Queue、Renderer Boundsの左右ペイン幅をドラッグ調整
- 日本語／英語UI切替（初期値は日本語）
- Original、Fallback、Quest の Preview Scene 表示
- Quest 用 Per-Platform Override が設定済みの場合は Android avatar を自動表示
- Unity Undo、Prefab instance override、Scene dirty、Material asset dirty の適切な記録

## Preview の意味

Preview は元のアバターを変更せず、非保存の Preview Scene に複製して描画します。

Fallback mode は shader の `VRCFallback` tag を優先し、tag が無い場合は VRChat が公開している shader 名・property・keyword の規則から組み込み shader を選択します。VRChat client 内部の置換そのものではないため近似表示です。最終確認は VRChat の Action Menu にある `Options > Avatar > Fallback Shaders` で行ってください。

Quest mode は次の順序で表示します。

1. SDK の Android Per-Platform Override があればそれを、無ければ現在の avatar を表示対象にする
2. 表示対象で許可済みの `VRChat/Mobile` shader はそのまま使用
3. 許可されない shader は `VRChat/Mobile/Toon Standard`、`Standard Lite`、`Diffuse` の利用可能なものへ property 名ベースで近似変換

3 は Android build の生成や SDK validation を代替しません。透明表現、custom shader の固有 property、shader pass、platform import settings は正確に再現できません。SDK Control Panel の Android validation と実機の Build & Test を最終確認に使用してください。

参照した一次情報（2026-08-30 確認）：

- [Shader Blocking and Fallback System](https://creators.vrchat.com/avatars/shader-fallback-system/)
- [Android Content Limitations](https://creators.vrchat.com/platforms/android/quest-content-limitations/)
- [Per-Platform Avatar Overrides](https://creators.vrchat.com/avatars/per-platform-avatar-overrides/)

## 書き込み範囲

Material slot、Texture、Tiling、Offset の変更は実際の Scene または Material asset へ書き込みます。すべて Unity Undo に記録しますが、共有 Material の変更は、その Material を参照するアバター外の object にも反映されます。`Uses in avatar` と Asset path を確認してから編集してください。

このパッケージは非破壊検査用の `SabaTools Inspect` パッケージとは独立しています。導入しても `Inspect` の走査処理が Scene や Asset を変更できるようにはなりません。

## 要件

- Unity 2022.3
- VRChat Avatars SDK 3.10.4 以降
- lilToon 2.3.4 以降

lilToon は公式 VPM repository `https://lilxyzw.github.io/vpm-repos/vpm.json` を
VCC に登録して解決してください。

Package ManagerのSamplesから`lilToon Avatar Material Demo`をimportし、
`Tools > SabaTools > Open Material Studio Demo`を実行すると、Shadow、Emission、MatCap、
Rim Light、Outline、透明衣装と6個のTexture Assetを設定した人型検証Sceneを生成できます。
このうちChecker 2個は同一PNGの意図的な重複で、Texture一覧の重複検出を確認できます。

Editor 専用です。Runtime assembly や avatar build へ追加される component はありません。

# SabaTools 設計

## 目的

SabaTools Inspectは、Unity Editor上のアバターまたはワールドを読み取り、統計と問題候補を
提示する非破壊の検査ツールです。検査によってScene、Asset、Selection、Project Settingsを
変更しないことを最優先の制約とします。

自動修復、一括設定、最適化処理はこのツールへ追加しません。書き込みを伴う機能が必要に
なった場合は、検査結果と操作の境界が利用者から明確に見える別パッケージとして設計します。

Avatar Material Studioはこの境界に従う書き込み可能なEditor拡張です。Inspectの走査処理や
assemblyを参照せず、変更は利用者がMaterial slotまたはTexture propertyを操作した時だけ
Unity Undoを記録して実行します。Previewは非保存のPreview Scene上の複製だけを変更します。

## パッケージ境界

| パッケージ | 依存 | 役割 |
| --- | --- | --- |
| `io.github.sabas0ba.sabatools.core` | なし | Scene走査、共通統計、参照検査、レポート、EditorWindow、拡張点 |
| `io.github.sabas0ba.sabatools.avatar` | core、Avatars SDK | `VRCAvatarDescriptor`とAvatar固有Assetの検査 |
| `io.github.sabas0ba.sabatools.avatar-materials` | Avatars SDK、lilToon | Material／Texture編集、照明／Fallback／Quest preview |
| `io.github.sabas0ba.sabatools.world` | core、Worlds SDK | `VRCSceneDescriptor`とWorld固有Componentの検査 |

依存方向はavatarからcore、worldからcoreへの一方向です。coreはSDK assemblyや両モジュールを
参照しません。Avatars SDKとWorlds SDKを同じProjectへ導入する前提も置きません。
avatar-materialsは書き込み可能性の境界として独立し、coreやInspectionModuleへ依存しません。

## Material編集と表示比較

Avatar Material Studioは対象root以下の全Rendererを読み、Material slotをHierarchy path順に
表示します。MaterialのTexture propertyはShader APIから列挙するため、shader固有のproperty名を
固定していません。slot変更はRenderer、Texture／Tiling／Offset変更はMaterialへUndoを記録し、
Prefab instance override、Scene dirty、Asset dirtyをそれぞれ明示します。

使用中TextureはAvatar全体で一意に集約し、各TextureからMaterial propertyとRenderer slot数を
逆引きします。異なるAsset pathのソースファイルはSHA-256で比較し、同一内容の重複を示します。
表示比較はOriginal／Fallback／Questのみを選択式とし、無照明、環境光上下限、Directional／Pointの
強度・色・距離・方向・複数灯を17セルのグリッドへ並列表示し、Lightの位置と方向を重ねます。

Preview cameraはOrbit、Free Fly、最後にactiveだったScene View cameraへの追従を選べます。
Render Queue診断はMaterialのqueue／RenderType／depth／blendとRendererのsubmesh／sortingを一覧化し、
標準Queue範囲ごとに全表示／範囲内のみ／範囲外のみを並列表示します。各RendererのBounds、名前、
Queue番号と区分をPreviewに重ねます。半透明world-object probeは複数queueで並列表示し、透明衣装との
depth／sorting事故を目視比較します。

Bounds診断はMeshRendererとSkinnedMeshRendererのlocal／world AABBを表示し、ViewPositionを中心に
近・中・遠の3距離×16方向でcamera frustumとの交差を検査します。選択方向では3つの固定Probeと
独立したユーザー操作Cameraを同時表示し、ViewPositionとの位置関係を上面／側面図で示します。
AABB frustum検査は保守的でfalse positiveを含み得るため、固定Probe Previewへbounds wireframeを
重ねて確認します。ユーザーCameraを動かしても固定Probeの判定値は変更しません。

表示比較は元objectをPreview Sceneへ複製して行います。FallbackはVRChat公式文書の
`VRCFallback` tagと旧shader名heuristicを再現した近似です。QuestはAndroid Per-Platform Overrideを
優先し、無い場合に限り許可済みMobile shaderの判定とproperty名ベースの近似変換を行います。
VRChat client内部のshader replacement、Android build、textureのplatform import overrideは再現せず、
SDK validationと実機Build & Testを最終確認とします。

## 検査の流れ

1. `InspectWindow`または`InspectApi`が対象のGameObject群を受け取ります。
2. coreの`SceneScan`がHierarchy、Renderer、Material、Texture、serialized referenceを
   読み取り、`StatsSnapshot`と位置情報を構築します。
3. `InspectionModule`の派生型を`UnityEditor.TypeCache`で検出します。
4. Auto modeでは各moduleの`Detect`結果を優先し、該当しなければDescriptor型名のcensusから
   AvatarまたはWorldを選択します。どちらも無い場合はGenericとして共通検査だけを実行します。
5. 共通ruleと対象固有moduleが`InspectionReport`へ統計行とFindingを追加します。
6. EditorWindow表示、clipboard、Markdown fileのいずれかへ同じreportを出力します。

個別moduleが例外を送出してもcoreの結果は破棄しません。そのmoduleを失敗としてFindingへ
記録し、残りの検査を継続します。

## 純粋な判定とUnity依存処理

`Packages/*/Editor/Core`はUnityEngine、UnityEditor、VRChat SDKを参照しません。しきい値、
Expression Parametersのbit計算、Texture memory推定、Markdown生成などを通常の.NET 8で
直接実行できるようにするためです。

Hierarchy走査、`SerializedObject`、`EditorUtility.CollectDependencies`、Prefab状態など、
実Editor APIを必要とする処理は`Editor`直下に置きます。Avatar／World moduleのSDK field
アクセスも同様に実SDKを用いたUnity testで検証します。

## レポートモデル

Findingはseverity、category、message、Hierarchy pathを持ちます。severityはError、Warning、
Infoの3段階です。統計はgroup、label、valueの行として保持し、UIとMarkdownが同じmodelを
描画します。

UIの`Ping`は検査時に収集した位置情報から対象をEditor上で強調表示するだけで、Selectionを
書き換える修復操作やUndo対象の変更は行いません。

## 再現可能な依存

- nixpkgsは`flake.lock`のrevisionで固定します。
- Nix base image、Python、SDK取得用Alpineはdigestで固定します。
- VRChat SDK 3.10.4はlaneごとのlock fileにURLとSHA-256を記録します。
- GitHub Actionsはcommit SHAで固定します。
- Hostへdotnet、Nix、Pythonを導入せず、WindowsではPodmanとUnityのみを使用します。

配布packageのVPM dependencyは利用者側のSDK更新を阻害しない下限指定です。検証環境の
厳密な固定とは役割を分けています。

## 回帰検証

| 階層 | 対象 | 現在の件数 |
| --- | --- | ---: |
| .NET 8 | 全packageの`Editor/Core` | 28 |
| Unity EditMode | coreの収集器と公開API | 18 |
| Unity + Avatars SDK | Avatar module、Avatar Material Studio | 26 |
| Unity + Worlds SDK | World module | 15 |

合計87件です。これにRoslyn compile、documentation render、internal link、manifest、`.meta`、
dependency purityの検査を加えます。

CIでUnity licenceが利用可能な場合のみ、Unity workflowはUnity回帰試験を実行します。
licenceが無い場合はnoticeを残してUnity試験手順をskipし、非Unity回帰試験をregression
gateとします。この場合のUnity workflowの成功表示はUnity試験の成功を意味しません。

## 既知の限界

- Texture memoryはformatのbits-per-pixelとmipmap係数による推定であり、Profiler実測では
  ありません。
- `EditorUtility.CollectDependencies`が辿らないruntime生成MaterialのTextureは数えません。
- Avatar performance thresholdは参照値であり、VRChat SDKのBuild Validationを置換しません。
- PhysBoneの影響Transform数は近似です。
- Worldの床面はRenderer boundsで推定するため、巨大な装飾Meshで誤検出しえます。
- EditModeだけを対象とし、ClientSim、PlayMode、VRChatへのBuild／Uploadは行いません。
- Fallback previewは公開規則による近似で、VRChat client内の置換処理そのものではありません。
- Quest近似previewはAndroid buildとplatform別Texture import settingsを再現しません。

## リリース方針

packageごとに`<package-id>/v<version>`形式のtagを使用します。初版はpull requestのreviewと
merge後に`0.1.0`としてtagを作成します。依存するsibling packageがある場合は、先にそのversionが
listingで解決可能であることを確認してから公開します。

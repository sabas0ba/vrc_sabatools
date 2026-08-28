# SabaTools Inspect 設計

## 目的

SabaTools Inspectは、Unity Editor上のアバターまたはワールドを読み取り、統計と問題候補を
提示する非破壊の検査ツールです。検査によってScene、Asset、Selection、Project Settingsを
変更しないことを最優先の制約とします。

自動修復、一括設定、最適化処理はこのツールへ追加しません。書き込みを伴う機能が必要に
なった場合は、検査結果と操作の境界が利用者から明確に見える別パッケージとして設計します。

## パッケージ境界

| パッケージ | 依存 | 役割 |
| --- | --- | --- |
| `io.github.sabas0ba.sabatools.core` | なし | Scene走査、共通統計、参照検査、レポート、EditorWindow、拡張点 |
| `io.github.sabas0ba.sabatools.avatar` | core、Avatars SDK | `VRCAvatarDescriptor`とAvatar固有Assetの検査 |
| `io.github.sabas0ba.sabatools.world` | core、Worlds SDK | `VRCSceneDescriptor`とWorld固有Componentの検査 |

依存方向はavatarからcore、worldからcoreへの一方向です。coreはSDK assemblyや両モジュールを
参照しません。Avatars SDKとWorlds SDKを同じProjectへ導入する前提も置きません。

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
| .NET 8 | 全packageの`Editor/Core` | 25 |
| Unity EditMode | coreの収集器と公開API | 18 |
| Unity + Avatars SDK | Avatar module | 17 |
| Unity + Worlds SDK | World module | 15 |

合計75件です。これにRoslyn compile、documentation render、internal link、manifest、`.meta`、
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

## リリース方針

packageごとに`<package-id>/v<version>`形式のtagを使用します。初版はpull requestのreviewと
merge後に`0.1.0`としてtagを作成します。coreを先にlistingへ公開し、そのdependencyが解決
可能になってからavatarとworldを公開します。

# UI操作ガイド

SabaTools Inspectは1つのEditorWindowで対象選択、検査、結果確認、Markdown出力までを行います。
検査は読み取り専用で、Target、Scene、Asset、Project Settingsを変更しません。

![SabaTools Inspectウィンドウの操作箇所](images/inspect-window.svg)

図は実装されているIMGUI構成を説明用に再現したものです。色、余白、標準controlの外観は
Unityのthemeとversionによって変わります。

## ウィンドウを開く

- `Tools > SabaTools > Inspect Window`: 空のInspect Windowを開きます。
- `Tools > SabaTools > Inspect Selection`: Hierarchyで選択中のGameObjectをTargetに設定し、
  直ちに検査します。GameObjectが選択されていない場合、このmenuは無効です。

## 1. TargetとMode

`Target`には、AvatarならAvatar root、World内の一部だけを調べる場合はそのroot GameObjectを
指定します。通常はHierarchyから検査したいrootを割り当てます。

`Mode`は実行するruleを選びます。

| Mode | 動作 |
| --- | --- |
| `Auto` | moduleの実型判定、Descriptor型名のcensusの順でAvatar／Worldを選び、どちらも無ければGenericにします |
| `Generic` | SDK固有ruleを使わず、共通統計と参照検査だけを行います |
| `Avatar` | Avatar ruleを明示的に実行します。Descriptorが無い場合もその問題を報告します |
| `World` | World ruleを明示的に実行します。Descriptorが無い場合もその問題を報告します |

Modeは現在の検査だけに影響します。DescriptorやProjectの設定を書き換えません。

## 2. 検査範囲

- `Scan Target`: Target以下のHierarchyだけを検査します。Target未指定時は押せません。
- `Scan Active Scene`: Active Sceneの全root GameObjectをまとめて検査します。World全体や、
  Scene内の複数rootにまたがる参照・Componentを確認するときに使用します。

検査結果には実行時刻を記録しますが、この時刻をSceneやAssetへ保存することはありません。

## 3. 結果概要とMarkdown

検査後、対象名、解決されたMode、Error数、Warning数を先頭に表示します。

- `Copy Markdown`: 同じreportをclipboardへコピーします。
- `Export Markdown...`: 保存先を選択し、`inspect-report.md`として書き出します。

Markdownには統計とFindingが含まれるため、review、issue、作業前後の比較に利用できます。

## 4. Statistics

展開すると、Object、Geometry、Rendering、Texture、Component、Avatar／World固有値などを
group別に表示します。代表的な項目はGameObject数、triangle数、Renderer、material、shader、
texture memory推定、Light、Audio Source、Constraint、VRChat Componentです。

表示されるgroupは導入package、Mode、対象に存在するComponentによって変わります。

## 5. Textures

検査で見つかったTextureがある場合だけ表示されます。foldoutの件数は一意なTexture数です。
各行にasset名、解像度／format等のdetail、推定memoryを表示します。

`Ping`はProject上のTextureを強調表示します。Textureを選択・変換・圧縮する操作ではありません。
memory値はformatのbits-per-pixelとmipmap係数から求めた推定であり、Profiler実測値とは一致
しない場合があります。

## 6. Findings

検出項目をError、Warning、InfoのHelpBoxとして表示します。各項目にはcategory、message、
対象を特定できる場合はHierarchy pathが含まれます。

位置情報を解決できるFindingには`Ping`が表示され、該当ObjectをEditor上で強調表示します。
Findingが無い場合は`No findings.`と表示します。

## 使用例

### Avatarを確認する

1. Avatar rootをTargetへ設定します。
2. Modeを`Auto`のまま`Scan Target`します。
3. 概要が`Avatar`になったことを確認します。
4. Statisticsでperformance関連値、FindingsでDescriptor、Expression、Playable Layer、Eye Look、
   PhysBoneの設定を確認します。

### Worldを確認する

1. 対象Sceneをactiveにします。
2. `Scan Active Scene`を実行します。
3. 概要が`World`になったことを確認します。
4. Spawn、Respawn Height、Reference Camera、Mirror等のFindingを確認します。

### SDK固有検査を行わない

Modeを`Generic`へ変更して検査します。SDK packageが導入されていても、共通のHierarchy統計、
missing reference、mesh、material、texture等だけを確認できます。

## UIに保存される設定

現在、永続化されるユーザー設定はありません。Target、Mode、foldoutの開閉状態、直前の結果は
開いているWindow instanceの状態であり、検査対象のSceneやProject Settingsには保存されません。

# CLAUDE.md

Claude Code が本リポジトリで作業する際の補足。

利用者全体の共通規約は `~/.claude/CLAUDE.md` にある。開発環境は
[sabas0ba/dotfiles](https://github.com/sabas0ba/dotfiles) の nix / コンテナ環境を前提とし、
同リポジトリの `CLAUDE.md` および `docs/development.md` に従う。

リポジトリの構成、検証手順、リリース手順は [README.md](README.md) に定義してある。人間の
作業者にも同様に適用されるため、本ファイルには重複して記述しない。

以下は、エージェントが作業する際に特に注意を要する事項のみを記述する。

## 作業前の確認

作業は開発シェルの内部で行う。開発シェルの外にいる場合は `nix develop`
(direnv 導入済みであれば `direnv allow`) で入る。ツールを開発シェルの外から
導入しない。ホストのグローバル環境 (`apt install dotnet-sdk` 等) を変更しない。
Host に Nix が無い環境ではリポジトリの `Dockerfile` を使用し、dotnet / Nix は Podman
container 内で実行する。Windows の Host tool は Podman と Unity のみに限定する。

## 非破壊であること

収録パッケージはいずれも検査専用であり、シーン・アセット・選択状態のいずれも変更
しない。これは唯一の強い約束であり、`InspectApiTests.ScanningLeavesTheHierarchyUntouched`
および各モジュールの `ScanningLeavesThe...Untouched` が検査している。
`InspectionModule` の実装もこの約束を守る。1 つのモジュールが破れば全体が破れる。

書き込みを伴う機能 (一括設定、最適化など) を追加する場合は、これらに足さず別パッケージ
として分ける。検査ツールが書き込みうるという状態にしないこと。

## パッケージの分割

非破壊検査のパッケージは対象別に 3 つある。この境界は VRChat SDK の制約そのものであり、動かさない。
書き込み可能な機能は、検査の非破壊性を維持する別パッケージとして扱う。

| パッケージ | vpmDependencies | 役割 |
| --- | --- | --- |
| `sabatools.core` | なし | 収集・レポート・ウィンドウ・拡張点 |
| `sabatools.avatar` | core, `com.vrchat.avatars` | `VRCAvatarDescriptor` を型で読む検査 |
| `sabatools.avatar-materials` | `com.vrchat.avatars`、`jp.lilxyzw.liltoon` | Material／Texture編集と非保存Preview |
| `sabatools.world` | core, `com.vrchat.worlds` | `VRCSceneDescriptor` を型で読む検査 |

avatar-materialsのcamera移動、Render Queue filter、半透明probe、bounds overlayは
Preview cloneだけへ適用し、Sceneやsource Rendererを変更しない。Render Queue値の明示編集だけが
Material assetへUndo付きで書き込む。Boundsの48方向検査はViewPosition基準のAABB frustum近似とする。

- **core に SDK 依存を持ち込まない**。`vpmDependencies` に VRChat SDK を足した時点で、
  core を入れた全プロジェクトにその SDK が入る。両 SDK の同居は想定されていない
- **依存の矢印は avatar → core / world → core の一方向のみ**。core は両モジュールの
  アセンブリを参照せず、`UnityEditor.TypeCache` で `InspectionModule` の派生型を拾う。
  core 側に avatar / world の asmdef 参照を足さないこと
- **検査内の分割軸は対象であり、検査の種類ではない**。読み取り専用の検査を増やす場合は
  既存 3 パッケージのいずれかにフォルダと名前空間を足す。書き込み可能な機能を Inspect の
  assemblyへ追加しない

## Editor/Core の純粋性

`Packages/*/Editor/Core` には `using UnityEngine` / `using UnityEditor` / `using VRC` を
持ち込まない。この分離があるために、判定のしきい値やレポート生成を Unity 無しで実行して
検証できる。`verify.sh` はこの制約自体を検査するので、Unity の型が必要になった時点で
その処理は `Editor` 直下に置くべきものである。

avatar / avatar-materials / world パッケージではこの制約の重みが core より大きい。
`verify.sh` が届くのは各パッケージの `Editor/Core` だけであり、それ以外は実 SDK の
レーンでしか検証されない。
新しい判定を足すときは、まず純粋な計算として `Editor/Core` に書けないか検討すること。

検査を通すために検査自体を削除しない。

## VRChat SDK への依存

`sabatools.core` は VPM 依存を宣言していない。SDK の有無にかかわらず、アバター用・
ワールド用いずれのプロジェクトでも導入できることが要件である。SDK コンポーネントは
`VrcComponentCensus` が型名で数えており、core 側に asmdef 参照や `defineConstraints` を
足すとこの要件が壊れる。

avatar / avatar-materials / world の `vpmDependencies` は依存の**下限**のみを宣言する
（SDKは`>=3.10.4`、lilToonは`>=2.3.4`）。VPM は
範囲指定しか書けず、厳密に固定すると利用者側の SDK 更新を壊す。厳密な固定は
`.github/verify/vrchat/packages.<lane>.lock` の URL + SHA256 が担う。この二段構えを
崩さないこと。

モジュールが読む SDK のフィールド名は、実 SDK のレーンでしか検証されない。SDK の enum
は値名 (`ToString()`) で比較しており、SDK 更新で値が増えてもコンパイルエラーにならない。
新しいフィールドを読み始める場合は、対応するレーンのテストを同時に足すこと。

### SDK のアセンブリ (3.10.4 で実測)

SDK の主要な型は asmdef ではなく **precompiled plugin** に入っている。asmdef として
存在するのは `VRC.SDKBase` / `VRC.SDKBase.Editor` / `VRC.SDK3` / `VRC.SDK3.Editor` /
`VRC.SDK3A` / `VRC.SDK3A.Editor` / `VRC.Udon` などである。

| 型 | 所在 | 種別 |
| --- | --- | --- |
| `VRC.SDK3.Components.VRCSceneDescriptor` | `VRCSDK3.dll` | plugin |
| `VRC.SDK3.Avatars.Components.VRCAvatarDescriptor` | `VRCSDK3A.dll` | plugin |
| `VRC.SDK3.Dynamics.PhysBone.Components.VRCPhysBone` | `VRC.SDK3.Dynamics.PhysBone.dll` | plugin |
| `VRCPhysBoneBase` / `VRCPhysBoneColliderBase` | `VRC.Dynamics.dll` | plugin |
| `ViewPosition` / `spawns` / `RespawnHeightY` (基底型) | `VRCSDKBase.dll` | plugin |

plugin は `overrideReferences: false` の asmdef へ自動参照される。モジュール本体が
これに当たるため、`references` に書かなくてもコンパイルは通る。一方 **テスト用の
asmdef は `overrideReferences: true`** であり、自動参照が無効になるので
`precompiledReferences` に DLL 名を明示しないと参照できない。SDK の型を新たに使う
テストを足す場合はここを確認すること。

## パフォーマンス参考値

`ChecklistRules.AvatarMetrics` のしきい値は VRChat 公式ドキュメントを手で写した参考値
であり、CI は追随を検証していない。値を変更する場合は出典の URL と参照日を
コメントおよび `CHANGELOG.md` に残すこと。断定的な判定として提示しない。

## 依存とバージョンの固定

GitHub Actions は SHA、コンテナイメージは digest、nixpkgs はリビジョンで固定する。
タグやブランチによる参照を追加しない。依存パッケージを増やす場合は事前に確認を取る。

## 変更後の検証

まず Unity 非依存の回帰試験を単独で通し、その後に全検証を通すこと。

```bash
dotnet run \
  --project .github/verify/offline/SabaTools.Inspect.OfflineTests.csproj \
  --configuration Release
./.github/verify/verify.sh
```

avatar / avatar-materials / world パッケージに手を入れた場合は
`./.github/verify/vrchat/run-tests.sh <avatars|worlds>` も実行する。avatar と
avatar-materials は `avatars`、world は `worlds` を選ぶ。前段の Unity 非依存検証は
SDK package の `Editor/Core` しか見ていないため、通っても SDK 依存部分は未検証である。

CI で Unity licence が利用可能な場合、`unity.yml` は Unity 回帰試験を実行する。licence が
無い場合は notice を残して Unity 試験手順を skip し、`verify.yml` を regression gate とする。
この場合の `unity.yml` の成功表示を Unity 試験の成功と解釈しないこと。

ファイルを追加した場合は `.github/scripts/run.sh .github/scripts/gen_meta.py` で
`.meta` を生成してからコミットする。

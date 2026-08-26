# 引き継ぎ (Claude Code 向け)

このファイルは初回セットアップ用である。作業がリポジトリへ載った後は削除してよい。
継続的な規約は [CLAUDE.md](CLAUDE.md) と [README.md](README.md) にある。

## この成果物の状態

`vrc_sabaprops` / `vrc_sabashader` を参照して作成した VPM 配布リポジトリの初期状態
である。GitHub への push は行っていない。

- リポジトリ雛形: `vrc_sabaprops` の構成を踏襲 (`Packages/` + `source.json` + `Website/`
  + 各 workflow + `.github/scripts/run.sh` による digest 固定コンテナでの Python 実行)
- 開発環境: `vrc_sabashader` と同様に `flake.nix` / `flake.lock` を用意した。nixpkgs は
  `sabas0ba/dotfiles` と同一リビジョン (`597283ad8aa0b331c788e97c4c262d58877074ef`,
  nixos-26.05) で固定してある
- 収録パッケージ: 対象別に 3 つ。いずれも 0.1.0

| パッケージ | vpmDependencies |
| --- | --- |
| `io.github.sabas0ba.sabatools.core` | なし |
| `io.github.sabas0ba.sabatools.avatar` | core, `com.vrchat.avatars` >=3.10.4 |
| `io.github.sabas0ba.sabatools.world` | core, `com.vrchat.worlds` >=3.10.4 |

分割の理由と依存の向きは [README.md](README.md#3-つに分けている理由) と
[CLAUDE.md](CLAUDE.md) にある。要点は「core は SDK を参照せず、`TypeCache` で
`InspectionModule` の派生型を拾う」「矢印は avatar → core / world → core の一方向」。

## 環境の前提

作業は `sabas0ba/dotfiles` の nix / コンテナ環境を前提とする。同リポジトリの
`CLAUDE.md` と `docs/development.md` に従い、開発シェルの内部で作業すること。
本リポジトリ側の開発シェルは以下で入る。

```bash
nix develop        # direnv 導入済みなら direnv allow
```

開発シェルが供給するのは dotnet SDK 8 / curl / unzip / zip / jq / shellcheck / shfmt /
git である。Python はホストに入れず、`.github/scripts/run.sh` が digest 固定した
`python:3.12-slim` コンテナで実行する。したがって **podman または docker がホストに
必要**である。

## 検証の実施状況

作成環境 (Anthropic のクラウドサンドボックス) には .NET SDK が無く、NuGet・コンテナ
レジストリ・GitHub Releases への通信も遮断されていたため、C# のコンパイルと実行は
一度も行えていなかった。2026-08-27 に手元 (Windows 11 + WSL) で全階層を実行し、
すべて通過させた。

| 階層 | 検証 | 結果 |
| --- | --- | --- |
| 1 | `verify.sh` (Unity 不要の全工程) | 通過 |
| 2 | 実 Unity の EditMode テスト (`CIProject`, SDK 無し) | 9/9 通過 |
| 3 | 実 VRChat SDK の EditMode テスト (worlds) | 9/9 通過 |
| 3 | 実 VRChat SDK の EditMode テスト (avatars) | 9/9 通過 |

Unity は 2022.3.22f1、VRChat SDK は 3.10.4 である。

初回の実行では 3 件の失敗が出た。いずれも実物に当たらなければ分からない類であり、
修正は `fix:` コミットに分けて記録してある。

- テスト用 asmdef が `overrideReferences: true` のため、precompiled plugin
  (`VRCSDK3.dll` / `VRCSDK3A.dll` / `VRCSDKBase.dll`) が参照できていなかった
- `JsonUtility.ToJson` は engine 型を扱えないため、`ScanningLeavesTheHierarchyUntouched`
  が実行不能だった。非破壊性は SDK 無しの階層では未検証の状態だった
- `EditorUtility.CollectDependencies` は persistent な参照しか辿らないため、
  メモリ上に作ったマテリアル経由ではテクスチャが見つからなかった

### 手元で回す

```bash
./.github/verify/verify.sh                       # Unity 不要の全検証
./.github/verify/vrchat/run-tests.sh worlds      # 実 SDK (worlds)
./.github/verify/vrchat/run-tests.sh avatars     # 実 SDK (avatars)
```

`run-tests.sh` は対象プロジェクトが既にあると `assemble.sh` を呼ばない。パッケージや
テストを変更した後は `assemble.sh <lane>` を明示的に先に実行すること。

階層 2 は `run-tests.sh` の対象外である。`.github/verify/CIProject` を Unity で開いて
Test Runner を回すか、`unity.yml` の `editmode` job と同じ手順でプロジェクトを組んで
バッチモードで実行する。

## 残作業

### 1. avatars レーンの SHA256 を埋める — 完了 (2026-08-27)

公式リスティング <https://packages.vrchat.com/official> の公表値へ差し替えた。同じ
リスティングから引いた `com.vrchat.base` と `com.vrchat.worlds` のハッシュが既存値と
一致すること、および zip 実物の実測値が公表値と一致することの二点で確認してある。

### 2. GitHub リポジトリの作成と push — 完了 (2026-08-27)

<https://github.com/sabas0ba/vrc_sabatools> を public で作成し、`main` を push した。

トピックは未設定である。`vrchat` / `vpm` / `unity-editor` / `vrchat-avatars` /
`vrchat-worlds` あたりが既存 2 リポジトリと揃う。

なお push 後も Actions の実行が 0 件のままである。4 つの workflow はいずれも `active`
として登録され、`actions/permissions` も `enabled: true` を返すので、原因は未特定で
ある。`workflow_dispatch` で手動起動して切り分けること。

### 3. GitHub Pages の有効化

Settings → Pages → Source を **GitHub Actions** にする。これを行わないと
`build-listing.yml` の deploy が失敗する。`github-pages` environment は既定で main のみ
許可され、`build-release.yml` はそれを前提に listing を main 上で dispatch する構造に
なっている (理由は `build-release.yml` のコメントにある)。

### 4. Unity licence (任意)

`unity.yml` の 2 つの job (`editmode` と `sdk`) は、いずれも `UNITY_LICENSE` または
`UNITY_SERIAL` が無ければ全ステップを skip して success で終わる。未設定でも
`verify.yml` は動く。

### 5. 初回リリース

検証が通り、実際に Unity プロジェクトへ入れて動作を確認してからタグを打つ。
パッケージが 3 つあるため、タグは **必ず** `<package-id>/v<version>` 形式にする
(`v0.1.0` 形式は `build-release.yml` が明示的に失敗させる)。

```bash
git tag io.github.sabas0ba.sabatools.core/v0.1.0
git push origin io.github.sabas0ba.sabatools.core/v0.1.0
```

`avatar` / `world` は core の release が listing に載ってからにすること。
`vpmDependencies` が解決できないと VCC 側でインストールできない。

## 設計意図

変更を加える前に把握しておくべき点。詳細は各ファイル冒頭のコメントにある。

- **非破壊であること**が唯一の強い約束である。モジュールも含めて守る。書き込みを伴う
  機能を追加する場合は別パッケージに分ける
- **core に SDK 依存を持ち込まない**。`vpmDependencies` に SDK を足した時点で、core を
  入れた全プロジェクトにその SDK が入る
- **`Editor/Core` は Unity 非依存**に保つ。avatar / world ではこの制約の重みが core より
  大きい。`verify.sh` が届くのはその 2 パッケージの `Editor/Core` だけである
- **公開 API は `InspectApi` と `InspectionModule` のみ**
- **SDK の enum は値名で比較**している。SDK 更新で値が増えてもコンパイルエラーに
  ならず、「認識しない値」として扱われる
- **パフォーマンスしきい値は参考値**である。値を変える場合は出典 URL と参照日を
  コメントと CHANGELOG に残すこと

## 既知の未確定事項

- `TextureMemoryEstimate` の推定値と Unity の実測 (`Profiler.GetRuntimeMemorySizeLong`)
  の突き合わせは未実施
- `TextureUsageCollector` は `EditorUtility.CollectDependencies` に依存するため、
  実行時に組み立てられたマテリアルのテクスチャは数えない (2022.3.22f1 で実測)
- PhysBone の影響 Transform 数は近似であり、SDK 自身の数え方とは一致しない
- `.github/scripts/run.sh` と `vrchat/fetch.sh` のイメージ digest は `vrc_sabaprops`
  から引き継いだもの。更新する場合は 3 リポジトリで揃えるか、揃えない理由を残すこと
- `Website/index.html` は `vrc_sabaprops` のものを文言だけ差し替えて流用している
- 実 SDK レーンは EditMode のみで、PlayMode (ClientSim) の構成は入れていない。物理の
  検証が必要になった場合は `vrc_sabaprops` の `.github/verify/vrchat/` が参考になる

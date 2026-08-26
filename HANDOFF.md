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

## 未実施の検証 (最初にやること)

作成環境 (Anthropic のクラウドサンドボックス) には .NET SDK が無く、NuGet・コンテナ
レジストリ・GitHub Releases への通信も遮断されていた。このため **C# のコンパイルと
実行は一度も行えていない**。手元で最初に通すこと。

```bash
./.github/verify/verify.sh                       # Unity 不要の全検証
./.github/verify/vrchat/run-tests.sh worlds      # 実 SDK (worlds)
./.github/verify/vrchat/run-tests.sh avatars     # 実 SDK (avatars) ※下記の準備が必要
```

サンドボックスで実行済みなのは以下に限られる。

| 検証 | 結果 |
| --- | --- |
| `check_package.py` (3 パッケージのマニフェスト・CHANGELOG・`.meta`・兄弟依存) | 通過 |
| `build_docs.py` + `check_docs.py` (7 ページ生成とリンク検査) | 通過 |
| `bash -n` / `sh -n` (verify.sh, run.sh, vrchat/*.sh) | 通過 |
| JSON の妥当性 (source.json, 各 package.json, asmdef, flake.lock) | 通過 |
| 全 `Editor/Core` に Unity / VRC の using が無いこと | 通過 |
| C# 24 ファイルの括弧対応と `StatsSnapshot` 参照の解決 | 通過 (静的照合のみ) |
| `UnityEditorStub.cs` がオフラインで使う UnityEditor メンバを網羅すること | 通過 (静的照合のみ) |
| GitHub Actions が SHA 固定、コンテナが digest 固定であること | 通過 |
| **C# のコンパイル (`verify.sh`)** | **未実行** |
| **オフラインのルール実行 (`InspectRulesTests`)** | **未実行** |
| **実 Unity の EditMode テスト** | **未実行** |
| **実 VRChat SDK の EditMode テスト** | **未実行** |

静的照合は正規表現による近似であり、コンパイラの代わりにはならない。落ちた場合に
疑うべきはこの順である。

1. **asmdef の SDK アセンブリ名**。`VRC.SDK3A` / `VRC.SDK3` / `VRCSDKBase` /
   `VRC.SDK3.Dynamics.PhysBone` / `VRC.Udon` を参照しているが、公式ドキュメントで
   確認できたのは Editor 側の `VRC.SDK3A.Editor` / `VRC.SDK3.Editor` /
   `VRC.SDKBase.Editor` のみで、ランタイム側の名前は SDK 実物で未確認である。
   Unity が "Assembly reference not found" を出したら、SDK の asmdef を見て直すこと
2. モジュールが読む SDK のフィールド名 (`ViewPosition`、`expressionParameters`、
   `spawns`、`RespawnHeightY` など)
3. `UnityEditorStub.cs` のシグネチャ (引数の型や順序は手書きであり未検証)
4. `offline/InspectRulesTests.cs` のオーバーロード解決

## 残作業

### 1. avatars レーンの SHA256 を埋める

`.github/verify/vrchat/packages.avatars.lock` の `com.vrchat.avatars` のハッシュは
`FILL_ME_IN_SHA256_FROM_THE_OFFICIAL_LISTING` のままである。サンドボックスから
GitHub Releases へ到達できず、算出できなかった。公式リスティング
<https://packages.vrchat.com/official> の値に差し替えること。未記入のままだと
`fetch.sh` が明示的に失敗する (推測して埋めるより失敗させる方針)。

`com.vrchat.base` と `com.vrchat.worlds` のハッシュは `vrc_sabaprops` の
`packages.lock` から引き継いだ検証済みの値である。

### 2. GitHub リポジトリの作成と push

```bash
gh repo create sabas0ba/vrc_sabatools --public \
  --description "Avatar and world editing utilities for VRChat"
git remote add origin git@github.com:sabas0ba/vrc_sabatools.git
git push -u origin main
```

トピックは `vrchat` / `vpm` / `unity-editor` / `vrchat-avatars` / `vrchat-worlds` あたりが
既存 2 リポジトリと揃う。

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

- asmdef の SDK ランタイムアセンブリ名 (上記 1)
- `TextureMemoryEstimate` の推定値と Unity の実測 (`Profiler.GetRuntimeMemorySizeLong`)
  の突き合わせは未実施
- PhysBone の影響 Transform 数は近似であり、SDK 自身の数え方とは一致しない
- `.github/scripts/run.sh` と `vrchat/fetch.sh` のイメージ digest は `vrc_sabaprops`
  から引き継いだもの。更新する場合は 3 リポジトリで揃えるか、揃えない理由を残すこと
- `Website/index.html` は `vrc_sabaprops` のものを文言だけ差し替えて流用している
- 実 SDK レーンは EditMode のみで、PlayMode (ClientSim) の構成は入れていない。物理の
  検証が必要になった場合は `vrc_sabaprops` の `.github/verify/vrchat/` が参考になる

# 引き継ぎ (Claude Code 向け)

このファイルは初回セットアップ用である。作業がリポジトリへ載った後は削除してよい。
継続的な規約は [CLAUDE.md](CLAUDE.md) と [README.md](README.md) にある。

## この成果物の状態

`vrc_sabaprops` / `vrc_sabashader` を参照して作成した VPM 配布リポジトリの初期状態
(コミット 1 本) である。GitHub への push は行っていない。

- リポジトリ雛形: `vrc_sabaprops` の構成を踏襲 (`Packages/` + `source.json` + `Website/`
  + `build-release.yml` / `build-listing.yml` / `verify.yml` / `unity.yml`
  + `.github/scripts/run.sh` による digest 固定コンテナでの Python 実行)
- 開発環境: `vrc_sabashader` と同様に `flake.nix` / `flake.lock` を用意した。nixpkgs は
  `sabas0ba/dotfiles` と同一リビジョン (`597283ad8aa0b331c788e97c4c262d58877074ef`,
  nixos-26.05) で固定してある
- 収録パッケージ: `io.github.sabas0ba.sabatools.inspect` (SabaTools Inspect) 0.1.0

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

作成環境 (Anthropic のクラウドサンドボックス) には .NET SDK が無く、NuGet および
コンテナレジストリへの通信も遮断されていた。このため **`.github/verify/verify.sh` は
一度も実行できていない**。手元で最初に通すこと。

```bash
./.github/verify/verify.sh
```

初回は Unity の参照アセンブリ (`unityengine.modules` 2021.3.33) と .NET Framework
参照アセンブリを NuGet から取得し `.verify/refs` に置く。

サンドボックスで実行済みなのは以下に限られる。C# のコンパイルと実行は含まれない。

| 検証 | 結果 |
| --- | --- |
| `check_package.py` (マニフェスト・CHANGELOG・`.meta` の整合) | 通過 |
| `build_docs.py` + `check_docs.py` (ドキュメントサイト生成とリンク検査) | 通過 (3 ページ) |
| `bash -n` (`verify.sh`, `run.sh`) | 通過 |
| JSON の妥当性 (`source.json`, `package.json`, `flake.lock` 他) | 通過 |
| `Editor/Core` に Unity の using が無いこと | 通過 |
| C# 16 ファイルの括弧対応と `StatsSnapshot` 参照の解決 | 通過 (静的照合のみ) |
| `UnityEditorStub.cs` が使用中の UnityEditor メンバを網羅していること | 通過 (静的照合のみ) |
| GitHub Actions が SHA 固定、コンテナが digest 固定であること | 通過 |
| **C# のコンパイル (`verify.sh`)** | **未実行** |
| **オフラインのルール実行 (`InspectRulesTests`)** | **未実行** |
| **実 Unity の EditMode テスト** | **未実行** |

静的照合は正規表現による近似であり、コンパイラの代わりにはならない。`verify.sh` が
落ちた場合、疑うべきはこの順である。

1. `UnityEditorStub.cs` のシグネチャ (引数の型や順序は手書きであり未検証)
2. `Editor/*.cs` が使う UnityEngine API の綴りと名前空間
3. `offline/InspectRulesTests.cs` のオーバーロード解決

## 残作業

### 1. GitHub リポジトリの作成と push

```bash
gh repo create sabas0ba/vrc_sabatools --public \
  --description "Avatar and world editing utilities for VRChat"
git remote add origin git@github.com:sabas0ba/vrc_sabatools.git
git push -u origin main
```

トピックは `vrchat` / `vpm` / `unity-editor` / `vrchat-avatars` / `vrchat-worlds` あたりが
既存 2 リポジトリと揃う。

### 2. GitHub Pages の有効化

Settings → Pages → Source を **GitHub Actions** にする。これを行わないと
`build-listing.yml` の deploy が失敗する。`github-pages` environment は既定で main のみ
許可され、`build-release.yml` はそれを前提に listing を main 上で dispatch する構造に
なっている (この理由は `build-release.yml` のコメントにある)。

### 3. Unity licence (任意)

`unity.yml` は `UNITY_LICENSE` または `UNITY_SERIAL` が無ければ全ステップを skip して
success で終わる。実 Unity での検証を有効にする場合のみ secrets を設定する。
未設定でも `verify.yml` は動く。

### 4. 初回リリース

`verify.sh` が通り、実際に Unity プロジェクトへ入れて動作を確認してからタグを打つ。

```bash
git tag io.github.sabas0ba.sabatools.inspect/v0.1.0
git push origin io.github.sabas0ba.sabatools.inspect/v0.1.0
```

パッケージが 1 つだけの間は `v0.1.0` でも自動判別される。タグのバージョンと
`package.json` の `version` が一致しない場合、workflow は明示的に失敗する。

## パッケージの設計意図

変更を加える前に把握しておくべき点を挙げる。詳細は各ファイル冒頭のコメントにある。

- **非破壊であること**が唯一の強い約束である。書き込みを伴う機能 (一括設定、最適化) を
  追加する場合は本パッケージに足さず別パッケージに分ける。
  `InspectApiTests.ScanningLeavesTheHierarchyUntouched` がこの性質を検査している
- **`Editor/Core` は Unity 非依存**に保つ。判定しきい値・テクスチャメモリ推定・レポート
  生成が Unity 無しで実行・検証できるのはこの分離による。`verify.sh` はこの制約自体を
  検査する
- **VRChat SDK に依存しない**。SDK コンポーネントは `VrcComponentCensus` が型名で数える。
  asmdef 参照や `defineConstraints` を足すと、SDK の無いプロジェクトやワールド用
  プロジェクトで導入できなくなる
- **公開 API は `InspectApi` のみ**。ウィンドウも EditMode テストもここを通る。
  収集器の内部構造を変えても利用側は壊れない
- **パフォーマンスしきい値は参考値**である。`ChecklistRules.AvatarMetrics` の値は VRChat
  公式ドキュメント (2026-08 時点) を手で写したものであり、CI は追随を検証していない。
  値を変える場合は出典 URL と参照日をコメントと CHANGELOG に残すこと

## 既知の未確定事項

- `TextureMemoryEstimate` の bpp 表は主要フォーマットを網羅しているが、Unity の実測値
  (`Profiler.GetRuntimeMemorySizeLong`) とは一致しない。実 Unity での突き合わせは未実施
- `VrcComponentCensus` の型名判定は SDK の型名変更に追随しない。実 SDK を入れた
  プロジェクトでの確認が必要
- `.github/scripts/run.sh` の python イメージ digest は `vrc_sabaprops` から引き継いだもの
  (python:3.12-slim, 2026-08-23 時点)。更新する場合は 3 リポジトリで揃えるか、揃えない
  理由を残すこと
- `Website/index.html` は `vrc_sabaprops` のものを文言だけ差し替えて流用している。
  デザインを分けるかどうかは未検討

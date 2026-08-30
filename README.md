# vrc_sabatools

VRChat 向けの**アバター／ワールド編集用ユーティリティ**を **VCC (VRChat Creator Companion) / VPM** で配布するためのリポジトリです。

複数パッケージの集合体として育てていく前提の構成です。非破壊の検査ツール **SabaTools Inspect** と、書き込み可能な **SabaTools Avatar Material Studio** を収録しています。

SabaTools Inspect は対象別に 3 パッケージへ分かれています。VRChat の Avatars SDK と Worlds SDK は同一プロジェクトでの併用が想定されていないため、SDK に依存する部分をプロジェクトの種類ごとに切り離してあります。Scene／Asset を変更する Material Studio は、Inspect の非破壊性を維持するため独立したパッケージです。

`vrc_sabaprops` (アセット) / `vrc_sabashader` (シェーダー) と同じ配布・検証の構成を踏襲しており、こちらは「編集を助ける Editor 拡張」を担当します。

---

## VCC への追加

VCC の「Settings → Packages → Add Repository」に以下の URL を登録してください。

```
https://sabas0ba.github.io/vrc_sabatools/index.json
```

または、リスティングサイトの「VCC に追加」ボタンからワンクリックで追加できます。

> リスティングは GitHub Releases から自動生成され、GitHub Pages で公開されます。
> リポジトリの Settings → Pages で Source を **GitHub Actions** にしておく必要があります。

---

## 収録パッケージ

| Package ID | 名前 | 依存する SDK | 概要 |
| --- | --- | --- | --- |
| `io.github.sabas0ba.sabatools.core` | SabaTools Inspect Core | なし | 統計、Missing 参照の検出、レポート出力。単体で完結します |
| `io.github.sabas0ba.sabatools.avatar` | SabaTools Inspect for Avatars | `com.vrchat.avatars` | `VRCAvatarDescriptor` を型で読む検査を追加します |
| `io.github.sabas0ba.sabatools.avatar-materials` | SabaTools Avatar Material Studio | `com.vrchat.avatars`、`jp.lilxyzw.liltoon` | Material／Texture編集、照明・Fallback・Quest・Render Queue・Bounds preview |
| `io.github.sabas0ba.sabatools.world` | SabaTools Inspect for Worlds | `com.vrchat.worlds` | `VRCSceneDescriptor` を型で読む検査を追加します |

**どれを入れるか**: 非破壊検査はアバター用プロジェクトなら `avatar`、ワールド用なら `world` を入れてください。`core` は依存として自動的に入ります。Material 編集と表示比較が必要なアバタープロジェクトには `avatar-materials` を追加します。これは Inspect へ依存せず単独でも導入できます。`avatar` と `world` を同時に入れる必要はなく、入れると両方の SDK がプロジェクトへ引き込まれます。

各パッケージの詳細は `Packages/<package-id>/README.md` を参照してください。

導入後の最短手順は `Tools > SabaTools > Inspect Window` です。対象を指定して Scan するだけで、シーンには何も書き込みません。

### Inspect を 3 つに分けている理由

`vpmDependencies` は VCC が実際に解決してインストールします。1 つのパッケージが `com.vrchat.avatars` を宣言すれば、それを入れたワールドプロジェクトにもアバター SDK が入ります。両 SDK の同居は公式にサポートされていないため、SDK 依存を持つ部分を対象別に切り離しています。

Inspect 内の分割軸を「ツールの種類」ではなく「対象」にしているのは、SDK がプロジェクト単位の硬い制約である一方、検査の種類は分類にすぎないためです。一方、書き込み可能な機能は利用者が操作の影響範囲を導入時点で判別できるよう、非破壊の Inspect へ混在させません。

core は両モジュールのアセンブリを参照しません。`InspectionModule` を継承した型を `UnityEditor.TypeCache` で拾うため、依存の矢印は avatar → core、world → core の一方向だけです。

---

## リポジトリ構成

```
.
├── Packages/                       # 配布する VPM パッケージ群（1 フォルダ = 1 パッケージ）
│   ├── io.github.sabas0ba.sabatools.core/
│   │   ├── package.json            # VPM マニフェスト
│   │   └── Editor/
│   │       ├── Core/               # Unity 非依存のロジック（判定・推定・レポート）
│   │       └── *.cs                # Unity に依存する収集器とウィンドウ、拡張点
│   ├── io.github.sabas0ba.sabatools.avatar/
│   ├── io.github.sabas0ba.sabatools.avatar-materials/
│   └── io.github.sabas0ba.sabatools.world/
├── Website/                        # GitHub Pages で公開するリスティングサイト
├── docs/design.md                  # package境界・非破壊性・検証階層の設計文書
├── source.json                     # VPM リスティングのメタ情報
├── flake.nix / flake.lock          # 検証に必要なツールチェーン（dotnet SDK 等）
└── .github/
    ├── scripts/
    │   ├── run.sh                  # Python を digest 固定コンテナで実行する
    │   ├── gen_meta.py             # 不足している .meta を生成する
    │   ├── build_listing.py        # Releases → index.json 生成
    │   └── build_docs.py           # パッケージの Markdown → ドキュメントサイト
    ├── verify/
    │   ├── verify.sh               # Unity 無しの検証一式（下記）
    │   ├── offline/                # 素の .NET 8 で実行する独立した回帰テスト
    │   ├── CIProject/              # SDK 無しの Unity プロジェクト
    │   └── vrchat/                 # SDK を hash 固定で取得する 2 レーンの検証
    └── workflows/
        ├── verify.yml              # PR ごとのオフライン検証
        ├── unity.yml               # 実 Unity での EditMode テスト（licence がある場合）
        ├── build-release.yml       # タグを打つと zip を作って Release を発行
        └── build-listing.yml       # Release 発行時にリスティングを再生成して Pages へ
```

### Editor/Core を分けている理由

`Editor/Core` には `using UnityEngine` / `using UnityEditor` を持つファイルを置きません。判定のしきい値、テクスチャメモリの推定、レポートの生成は Unity の型を必要としないため、そのまま素の .NET 上で実行してテストできます。`verify.sh` はこの分離自体も検査します (Core に Unity の using が入ったら失敗します)。

Unity が要る側 (階層の走査、`SerializedObject`、`CollectDependencies`) は `Editor` 直下に置き、実 Unity の EditMode テストで確認します。

---

## 開発

設計上のpackage境界、非破壊性、検査のデータフロー、依存固定、既知の限界は
[docs/design.md](docs/design.md)にまとめています。

作業は nix の開発シェル内で行います。

```bash
nix develop        # direnv 導入済みなら direnv allow
```

Host に Nix を導入しない場合は、dotfiles と同じ固定 Nix base image から project の
development profile を構築します。Host で使用するのは Podman のみです。

```powershell
podman build --pull=never -t localhost/vrc-sabatools-dev:latest .
podman run --rm --network=none `
  -v "${PWD}:/workspace" -w /workspace `
  localhost/vrc-sabatools-dev:latest `
  dotnet run --project .github/verify/offline/SabaTools.Inspect.OfflineTests.csproj `
  --configuration Release
```

image build 時は `flake.lock` で固定した Nix closure を取得します。実行時は build 済み
profile を使うため、Unity 非依存回帰試験に network は不要です。

検証は 3 段構えです。Unity 非依存の回帰テストは単独で実行でき、`verify.sh` からも同じ
プロジェクトが呼ばれます。

```bash
# 1a. Unity 不要。全パッケージの Editor/Core を直接リンクして実行する回帰テスト。
dotnet run \
  --project .github/verify/offline/SabaTools.Inspect.OfflineTests.csproj \
  --configuration Release

# 1b. Unity 不要。1a に加え、core の Editor アセンブリのコンパイル、
#    ドキュメント生成、マニフェスト検査まで。
#    podman または docker が必要 (Python を固定コンテナで動かすため)。
./.github/verify/verify.sh

# 2. 実 Unity (SDK 無し)。core の収集器と公開 API を実際の UnityEditor API で
#    動かします。GitHub Actions では licence がある場合のみ実行します。
#    ローカルでは CIProject をそのまま Unity で開いて Test Runner を実行します。

# 3. 実 Unity + 実 VRChat SDK。avatar 向けツール / world モジュールを対象別のプロジェクトで
#    検証します。SDK は SHA256 で固定して取得します。
./.github/verify/vrchat/run-tests.sh worlds
./.github/verify/vrchat/run-tests.sh avatars
```

1 で `avatar` / `world` パッケージの SDK 依存部分は検証できません。SDK のアセンブリは再配布できず NuGet にも無いためで、その分を 3 が担います。詳細は [.github/verify/vrchat/README.md](.github/verify/vrchat/README.md) にあります。

GitHub Actions の `Verify` は 1a を独立 job として表示し、1b の検証 job と並行して実行します。
どちらの .NET 検査も `Dockerfile` から構築した固定 Nix profile 内で実行し、Host runner に
.NET SDK または Nix を導入しません。Python 検査も digest 固定コンテナで実行します。
`Unity` は 2 と 3 を実行します。`UNITY_LICENSE` / `UNITY_SERIAL` が利用可能な場合のみ実行し、
利用できない場合は notice を残して Unity 回帰 job を skip します。この場合は `Verify` を
regression gate とし、Unity workflow の結果を Unity 回帰試験の成功とは扱いません。

新しいファイルを追加したら `.meta` を生成してからコミットします。GUID を固定しておかないと導入のたびに参照が壊れます。

```bash
.github/scripts/run.sh .github/scripts/gen_meta.py
```

### リリース

パッケージごとに独立してリリースします。core だけの修正で avatar のバージョンを上げる必要はありません。

1. `Packages/<package-id>/package.json` の `version` を上げ、`CHANGELOG.md` に同じバージョンの節を追加する
2. `<package-id>/v<version>` の形式でタグを打つ
3. `build-release.yml` が zip と manifest を Release に添付し、`build-listing.yml` が `index.json` を再生成して Pages へ配置する

パッケージが複数あるため、`v<version>` だけのタグは `build-release.yml` が明示的に失敗させます。どのパッケージを指すか決められないためです。

core に破壊的変更を入れる場合は、`avatar` / `world` の `vpmDependencies` の下限も同時に上げてください。

---

## ライセンス

Apache License 2.0。詳細は [LICENSE](LICENSE) を参照してください。

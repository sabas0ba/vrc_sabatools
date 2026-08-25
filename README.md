# vrc_sabatools

VRChat 向けの**アバター／ワールド編集用ユーティリティ**を **VCC (VRChat Creator Companion) / VPM** で配布するためのリポジトリです。

複数パッケージの集合体として育てていく前提の構成です。第一弾として、非破壊の検査ツール **SabaTools Inspect** を収録しています。

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

| Package ID | 名前 | 概要 |
| --- | --- | --- |
| `io.github.sabas0ba.sabatools.inspect` | SabaTools Inspect | アバター・ワールドの非破壊検査。統計、Missing 参照の検出、パフォーマンス参考値のチェックリストを一覧表示し Markdown で書き出せます。 |

各パッケージの詳細は `Packages/<package-id>/README.md` を参照してください。

導入後の最短手順は `Tools > SabaTools > Inspect Window` です。対象を指定して Scan するだけで、シーンには何も書き込みません。

---

## リポジトリ構成

```
.
├── Packages/                       # 配布する VPM パッケージ群（1 フォルダ = 1 パッケージ）
│   └── io.github.sabas0ba.sabatools.inspect/
│       ├── package.json            # VPM マニフェスト
│       └── Editor/
│           ├── Core/               # Unity 非依存のロジック（判定・推定・レポート）
│           └── *.cs                # Unity に依存する収集器とウィンドウ
├── Website/                        # GitHub Pages で公開するリスティングサイト
├── source.json                     # VPM リスティングのメタ情報
├── flake.nix / flake.lock          # 検証に必要なツールチェーン（dotnet SDK 等）
└── .github/
    ├── scripts/
    │   ├── run.sh                  # Python を digest 固定コンテナで実行する
    │   ├── gen_meta.py             # 不足している .meta を生成する
    │   ├── build_listing.py        # Releases → index.json 生成
    │   └── build_docs.py           # パッケージの Markdown → ドキュメントサイト
    ├── verify/                     # Unity 無しの検証一式（下記）
    └── workflows/
        ├── verify.yml              # PR ごとのオフライン検証
        ├── unity.yml               # 実 Unity での EditMode テスト（licence 必須）
        ├── build-release.yml       # タグを打つと zip を作って Release を発行
        └── build-listing.yml       # Release 発行時にリスティングを再生成して Pages へ
```

### Editor/Core を分けている理由

`Editor/Core` には `using UnityEngine` / `using UnityEditor` を持つファイルを置きません。判定のしきい値、テクスチャメモリの推定、レポートの生成は Unity の型を必要としないため、そのまま素の .NET 上で実行してテストできます。`verify.sh` はこの分離自体も検査します (Core に Unity の using が入ったら失敗します)。

Unity が要る側 (階層の走査、`SerializedObject`、`CollectDependencies`) は `Editor` 直下に置き、実 Unity の EditMode テストで確認します。

---

## 開発

作業は nix の開発シェル内で行います。

```bash
nix develop        # direnv 導入済みなら direnv allow
```

検証は次の 2 段構えです。

```bash
# 1. Unity 不要。Editor アセンブリのコンパイル、ルールの実行、
#    ドキュメント生成、マニフェスト検査まで。
#    podman または docker が必要 (Python を固定コンテナで動かすため)。
./.github/verify/verify.sh

# 2. 実 Unity。GitHub Actions 側で UNITY_LICENSE が設定されている場合のみ動きます。
#    ローカルでは CIProject をそのまま Unity で開いて Test Runner を実行します。
```

新しいファイルを追加したら `.meta` を生成してからコミットします。GUID を固定しておかないと導入のたびに参照が壊れます。

```bash
.github/scripts/run.sh .github/scripts/gen_meta.py
```

### リリース

1. `Packages/<package-id>/package.json` の `version` を上げ、`CHANGELOG.md` に同じバージョンの節を追加する
2. `<package-id>/v<version>` の形式でタグを打つ (パッケージが 1 つだけなら `v<version>` でも可)
3. `build-release.yml` が zip と manifest を Release に添付し、`build-listing.yml` が `index.json` を再生成して Pages へ配置する

---

## ライセンス

MIT License。詳細は [LICENSE](LICENSE) を参照してください。

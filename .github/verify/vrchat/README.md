# VRChat SDK 検証プロジェクト

`sabatools.avatar` と `sabatools.world` は VRChat SDK の型を直接参照します。SDK のアセンブリは再配布できず NuGet にも無いため、`.github/verify/verify.sh` はこの 2 パッケージの `Editor/Core` (Unity 非依存の計算部分) しか検証できません。

ここにあるのは、残りを実際の SDK と Unity で検証するための組み立て手順です。

## 方針

- **レーンを分ける**: `avatars` と `worlds` は別プロジェクトとして組みます。`com.vrchat.avatars` と `com.vrchat.worlds` は同一プロジェクトでの併用が想定されておらず、それこそがパッケージを分けた理由です。両方入れたプロジェクトで検証しても、利用者に存在しない構成を確かめたことにしかなりません
- **SDK は hash で固定**: `packages.<lane>.lock` に URL と SHA256 で固定します。ローカルの VCC / ALCOM のキャッシュには依存しません
- **取得はコンテナ内**: digest で固定した alpine イメージの busybox (`wget` / `unzip` / `sha256sum`) だけを使い、コンテナ内でのパッケージ導入も行いません
- **Unity はホストのもの**: Unity をコンテナで動かすにはライセンスが必要で、それは `.github/workflows/unity.yml` と同じ制約です

## 手順

```sh
./.github/verify/vrchat/run-tests.sh worlds
./.github/verify/vrchat/run-tests.sh avatars
```

SDK の取得・プロジェクト組み立て・EditMode テスト実行までを行います。個別に実行することもできます。

```sh
./.github/verify/vrchat/fetch.sh    worlds   # SDK を取得し build/vpm-worlds へ展開
./.github/verify/vrchat/assemble.sh worlds   # build/worldsProject を組む
```

Unity は Unity Hub の既定の場所から `ProjectVersion.txt` に一致するバージョンを探します。見つからない場合は `UNITY` に Editor の実行ファイルか Hub のインストールルートを指定してください。コンテナエンジンは `podman`、無ければ `docker` を自動で選びます。

初回は SDK が要求する UPM パッケージ (burst、collections 等) を Unity がレジストリから取得するため、数分かかります。

## avatars レーンの初回設定

`packages.avatars.lock` の `com.vrchat.avatars` の SHA256 は未記入です。公式リスティング <https://packages.vrchat.com/official> の値に差し替えてください。未記入のまま実行すると `fetch.sh` が明示的に失敗します。ハッシュを推測して埋めるより、失敗させるほうが安全なためです。

## これで検証できること

- `AvatarInspectionModule` / `WorldInspectionModule` が実際の SDK 型に対してコンパイルできること。読んでいるフィールド名が SDK の更新で消えていれば、ここで落ちます
- `TypeCache` によるモジュール検出が実際に働くこと。core は両モジュールのアセンブリを参照しないため、この経路が壊れてもコンパイルは通ってしまいます
- Auto モードが実際の descriptor を見て Avatar / World に解決すること (型名一致のフォールバックではなく)
- descriptor に書き込んでいないこと

## これでは検証できないこと

- 実機の VRChat へアップロードした結果。ビルド＆アップロードには VRChat アカウントでのログインが必要で、自動化の対象外です
- PlayMode の挙動。EditMode テストのみを実行するため、レイヤーと Collision Matrix の設定セッションも行っていません。物理を検証する必要が生じた場合は、`vrc_sabaprops` の `.github/verify/vrchat/` にある ClientSim を使った構成が参考になります

## SDK のバージョンを上げるには

`packages.<lane>.lock` の該当行のバージョン・SHA256・URL を公式リスティングの値に差し替えて `fetch.sh` を再実行してください。ハッシュが合わない場合は取得を失敗させます。

パッケージ側の `vpmDependencies` は下限 (`>=3.10.4`) のみを宣言しています。VPM は範囲指定しか書けず、厳密に固定すると利用者の SDK 更新を壊すためです。厳密な固定はこの lock ファイルが担い、配布物とは役割を分けています。

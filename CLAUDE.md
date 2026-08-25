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

## 非破壊であること

`io.github.sabas0ba.sabatools.inspect` は検査専用のパッケージであり、シーン・アセット・
選択状態のいずれも変更しない。これはこのパッケージの唯一の強い約束であり、
`InspectApiTests.ScanningLeavesTheHierarchyUntouched` が検査している。

書き込みを伴う機能 (一括設定、最適化など) を追加する場合は、本パッケージに足さず
別パッケージとして分ける。検査ツールが書き込みうるという状態にしないこと。

## Editor/Core の純粋性

`Packages/*/Editor/Core` には `using UnityEngine` / `using UnityEditor` を持ち込まない。
この分離があるために、判定のしきい値やレポート生成を Unity 無しで実行して検証できる。
`verify.sh` はこの制約自体を検査するので、Unity の型が必要になった時点でその処理は
`Editor` 直下に置くべきものである。

検査を通すために検査自体を削除しない。

## VRChat SDK への依存

VPM 依存を宣言していない。SDK の有無にかかわらず、アバター用・ワールド用いずれの
プロジェクトでも導入できることが要件である。SDK コンポーネントは
`VrcComponentCensus` が型名で数えており、asmdef 参照や `defineConstraints` を
足すとこの要件が壊れる。

## パフォーマンス参考値

`ChecklistRules.AvatarMetrics` のしきい値は VRChat 公式ドキュメントを手で写した参考値
であり、CI は追随を検証していない。値を変更する場合は出典の URL と参照日を
コメントおよび `CHANGELOG.md` に残すこと。断定的な判定として提示しない。

## 依存とバージョンの固定

GitHub Actions は SHA、コンテナイメージは digest、nixpkgs はリビジョンで固定する。
タグやブランチによる参照を追加しない。依存パッケージを増やす場合は事前に確認を取る。

## 変更後の検証

`./.github/verify/verify.sh` を通すこと。ファイルを追加した場合は
`.github/scripts/run.sh .github/scripts/gen_meta.py` で `.meta` を生成してから
コミットする。

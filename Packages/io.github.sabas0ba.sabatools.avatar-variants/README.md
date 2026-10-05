# SabaTools Avatar Variants

Scene中でPrefabの継承関係を持たずに複製し、改変したVRChatアバターを対象とするVPMパッケージです。元のアバターをBase Prefab、改変した側をそのPrefab Variantへ変換し、衣装やアクセサリの違いをPrefabのoverrideとして管理できる状態にします。

> 開発中です。現在は比較と変換前検査の計算部分のみを収録しており、EditorWindowと変換の実行はまだありません。

## 収録済みの機能

- **差分抽出**: BaseとDerivedの階層を比較し、プロパティ変更、Component／GameObjectの追加・削除、子の並び替えを列挙します。GameObjectは親が対応する範囲で名前により対応付け、Componentは型と出現順で対応付けます。ルートの名前と配置は比較しません。
- **変換前検査**: 差分から、変換によって編集内容が失われる条件を判定します。

| 検出内容 | 扱い | 理由 |
| --- | --- | --- |
| 同名の兄弟配下の変更 | 変換不可 | Unityの変換処理は同名の兄弟を対応付けますが、その差分を記録しません |
| 改名と推定されるGameObject | 警告 | Variantでは削除と追加の組として記録されます |
| 移動と推定されるGameObject | 警告 | VariantはBase由来のGameObjectの親を変更できないため、削除と追加の組として記録されます |
| 共通する子の並び替え | 警告 | 変換後の並び順の一致は変換後の検証で確認します |

## 書き込みの範囲

このパッケージは、SabaTools Inspectとは異なりSceneとAssetへ書き込む機能を持つ予定です。現在収録している差分抽出と変換前検査は計算のみで、書き込みを行いません。

## Unityの変換APIについて

変換には `PrefabUtility.ConvertToPrefabInstance` を使用します。削除したオブジェクトの復活、同名の兄弟での変更の消失など、公式ドキュメントに記載のない挙動は、リポジトリの `.github/verify/vrchat/Tests/Avatar/PrefabConversionBehaviourTests.cs` がUnity 2022.3.22f1で固定しています。

## ライセンス

Apache License 2.0。全文は同梱の [LICENSE.md](LICENSE.md) を参照してください。

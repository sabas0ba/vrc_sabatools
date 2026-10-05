# Changelog

このファイルは [Keep a Changelog](https://keepachangelog.com/ja/1.1.0/) の形式に基づきます。

## [0.1.0] - Unreleased

### Added

- 階層のSnapshotモデルと、BaseとDerivedの差分抽出（プロパティ変更、Component／GameObjectの追加・削除、子の並び替え）
- 変換前検査。同名の兄弟配下の変更を変換不可として検出し、改名・移動・並び替えを警告として報告

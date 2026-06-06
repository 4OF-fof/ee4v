# ee4v Docs

ee4v は VRChat 向け Unity Editor 拡張の再実装プロジェクトです。このドキュメントは、実装時に参照する設計方針、共通ルール、データ構造をまとめます。

## Sections

| section | 内容 |
|---|---|
| [API](./api/) | Core などの公開 API 契約、引数、返り値、副作用 |
| [Core](./core/) | feature bootstrap、Injector、Settings、I18N、Test 登録の共通ルール |
| [UI](./ui/) | UI Toolkit コンポーネントの分類、実装制約、Catalog 追加手順 |
| [AssetManager](./asset-manager/) | Booth / Eagle / ee4v datasource を統合する DB schema とデータ要素 |
| [Maintenance](./maintenance/) | Unity バージョン更新時に確認する互換性メモ |
| [Roadmap](./roadmap.md) | プロジェクト全体の実装計画 |

## 前提

- `new` は `old` の段階移植ではなく、再実装に向けた基盤整備として進める
- `old` は要件や既存挙動を確認する参照資料として扱う
- feature 固有の仕様や状態は feature 側へ置き、横断基盤だけを Core に寄せる
- 永続表示文言、UI、設定、テスト登録は各 section のルールに従う

## よく見るページ

- [Core 実装チェックリスト](./core/checklist.md)
- [Core API](./api/core.md)
- [AssetManager API](./api/asset-manager.md)
- [UI 実装チェックリスト](./ui/checklist.md)
- [AssetManager DB Schema](./asset-manager/schema.md)
- [Roadmap](./roadmap.md)
- [Unity Upgrade Notes](./maintenance/unity-upgrade.md)

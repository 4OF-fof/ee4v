# AssetManager

AssetManager は Booth / Eagle / ee4v 管理ファイルを統合して扱うためのドメインです。外部 datasource の情報は正本として直接使わず、AssetManager 側 DB に snapshot とアプリ独自情報を保持します。

## 設計方針

- Booth item ID を商品同一性の強いキーとして扱う
- Eagle / BLM は datasource として読み取り、AssetManager DB に取り込む
- ユーザー上書き可能な表示情報と datasource 由来 snapshot を分ける
- file 実体の解決は origin ごとに行い、Item とは論理的に分離する

## Pages

- [DB Schema](./schema.md)
- [Datasource Data Elements](./data-elements.md)
- [Roadmap](./roadmap.md)
- [BLM data.db Structure](../datasource/blm_db_structure.md)

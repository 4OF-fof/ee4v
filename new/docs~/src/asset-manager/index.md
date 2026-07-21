# AssetManager

AssetManager は Booth / Eagle / ee4v 管理ファイルを統合して扱うためのドメインです。外部 datasource の情報は正本として直接使わず、AssetManager 側 DB に snapshot とアプリ独自情報を保持します。

## 設計方針

- Booth item ID を商品同一性の強いキーとして扱う
- Eagle / BLM は datasource として読み取り、AssetManager DB に取り込む
- ユーザー上書き可能な表示情報と datasource 由来 snapshot を分ける
- file 実体の解決は origin ごとに行い、Item とは論理的に分離する
- file は Item 配下の論理要素として扱い、必要に応じて Version Group / Variant Group で束ねる

## 現在の実装状態

- schema v2、Item/File/Tag/Collection/Dependency/Import Target API を実装済み
- BLM `data.db` と Eagle library の読み取り同期、安定した source identity、datasource tag、欠落 origin の reconciliation を実装済み
- Main View は DB 読み込みを background で行い、前回 load の cancellation と thumbnail の最大 4 並列取得に対応
- Eagle Booth bridge は loopback bind、BOOTH origin 制限、session token、1 MiB request body 上限を適用

未完了なのは UI からの import 実行、全件を辿る pagination、toolbar 検索・絞り込みの接続です。Smart Collection は現状 Item ごとの条件評価を含むため、大規模 DB 向けの query 一括化も今後の課題です。

## Pages

- [AssetManager API](../api/asset-manager.md)
- [DB Schema](./schema.md)
- [Datasource Data Elements](./data-elements.md)
- [BLM data.db Structure](../datasource/blm_db_structure.md)

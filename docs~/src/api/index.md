# API

API カテゴリは、feature 実装から直接呼ぶ公開 API の契約をまとめます。設計方針や配置ルールは各カテゴリのページに置き、ここでは引数、返り値、副作用、重複登録時の扱いなど、実装時に判断がぶれやすい仕様を中心に扱います。

## Pages

- [Core API](./core.md)
- [AssetManager API](./asset-manager.md)

## 記載方針

各 API は次の粒度で記載します。

| 項目 | 内容 |
|---|---|
| 目的 | その API が担当する境界 |
| 引数 | 必須値、null 許容、識別子の安定性 |
| 返り値 | 成功時、未登録時、fallback 時の値 |
| 副作用 | 登録、保存、再描画、reload、cache 更新 |
| 注意点 | callback の制約、重複時の扱い、feature 側で避ける実装 |

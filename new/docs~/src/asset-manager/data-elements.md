# Datasource Data Elements

このドキュメントは AssetManager の独自 DB を設計するため、Eagle JSON と BLM `data.db` 構造メモから確認できるデータ要素をすべて列挙する。  
BLM / Eagle は datasource として扱い、AssetManager 側 DB はそれらの snapshot とアプリ独自情報を保持する。

## 1. 参照元

- Eagle Booth metadata JSON
  - `A:/Eagle/root.library/images/MPNXFTEQ7P24L.info/キプフェル Kipfel オリジナル3Dモデル.json`
- Eagle item metadata JSON
  - `A:/Eagle/root.library/images/MPNXFTEQ7P24L.info/metadata.json`
- BLM DB 構造メモ
  - [BLM data.db Structure](../datasource/blm_db_structure.md)

## 2. Eagle JSON データ要素

### 2-1. Booth metadata JSON

Eagle item として保存される Booth metadata 本文。

| path | 型 | 内容 |
|---|---|---|
| `schemaVersion` | integer | Booth metadata schema version |
| `boothItemId` | integer | Booth item ID |
| `itemUrl` | string | Booth item URL |
| `name` | string | Booth 商品名 |
| `description` | string | Booth 商品説明 |
| `thumbnailUrl` | string | Booth 商品 thumbnail URL |
| `shopName` | string | Booth shop 名 |
| `shopUrl` | string | Booth shop URL |
| `shopThumbnailUrl` | string | Booth shop thumbnail URL |
| `tags[]` | string[] | Booth 商品 tag |
| `attachedAt` | string datetime | Eagle 側で metadata を紐付けた時刻 |
| `lastUpdatedAtUtc` | string datetime | Booth 正本から metadata を最後に更新した時刻 |
| `downloads[]` | object[] | Booth download 情報 |
| `downloads[].downloadUrl` | string | Booth download URL |
| `downloads[].downloadId` | integer | Booth download ID |
| `downloads[].filename` | string | download file name |
| `downloads[].requestedAt` | string datetime | download 取り込み要求時刻 |
| `downloads[].importedAt` | string datetime | Eagle への取り込み完了時刻 |
| `downloads[].importedItemIds[]` | string[] | download から作成された Eagle item ID |

### 2-2. Eagle item metadata JSON

Eagle が item ごとに持つ metadata。

| path | 型 | 内容 |
|---|---|---|
| `id` | string | Eagle item ID |
| `name` | string | Eagle item 名 |
| `size` | integer | item file size |
| `btime` | integer timestamp | 作成時刻 |
| `mtime` | integer timestamp | 更新時刻 |
| `ext` | string | 拡張子 |
| `tags[]` | string[] | Eagle tag |
| `folders[]` | string[] | 所属 Eagle folder ID |
| `isDeleted` | boolean | Eagle 上の削除状態 |
| `url` | string | item URL |
| `annotation` | string | Eagle annotation |
| `modificationTime` | integer timestamp | Eagle item の modification time |
| `height` | integer | thumbnail / preview height |
| `width` | integer | thumbnail / preview width |
| `customThumbnail` | boolean | custom thumbnail 有無 |
| `palettes[]` | object[] | Eagle が抽出した palette |
| `palettes[].color[]` | integer[] | RGB color |
| `palettes[].ratio` | number | palette 比率 |
| `lastModified` | integer timestamp | Eagle metadata の最終更新時刻 |

## 3. BLM `data.db` データ要素

### 3-1. リスト関連

#### `lists`

| column | 型 | 内容 |
|---|---|---|
| `id` | INTEGER | list ID |
| `title` | TEXT | list 名 |
| `description` | TEXT | list 説明 |
| `created_at` | TEXT datetime | 作成時刻 |
| `updated_at` | TEXT datetime | 更新時刻 |

#### `list_items`

| column | 型 | 内容 |
|---|---|---|
| `id` | INTEGER | list item ID |
| `list_id` | INTEGER | 所属 list ID |
| `item_id` | TEXT | `registered_items.id` |
| `added_at` | TEXT datetime | list 追加時刻 |

#### `smart_lists`

| column | 型 | 内容 |
|---|---|---|
| `id` | INTEGER | smart list ID |
| `title` | TEXT | smart list 名 |
| `description` | TEXT | smart list 説明 |
| `created_at` | TEXT datetime | 作成時刻 |
| `updated_at` | TEXT datetime | 更新時刻 |

#### `smart_list_criteria`

| column | 型 | 内容 |
|---|---|---|
| `id` | INTEGER | criteria ID |
| `smart_list_id` | INTEGER | 所属 smart list ID |
| `text` | TEXT | テキスト検索条件 |
| `category_id` | INTEGER | parent category ID |
| `subcategory_id` | INTEGER | sub category ID |
| `age_restriction` | TEXT | `all`, `adult_only`, `safe` |

#### `smart_list_tags`

| column | 型 | 内容 |
|---|---|---|
| `id` | INTEGER | smart list tag ID |
| `smart_list_id` | INTEGER | 所属 smart list ID |
| `tag` | TEXT | tag 名 |

### 3-2. 作品 / タグ関連

#### `registered_items`

| column | 型 | 内容 |
|---|---|---|
| `id` | TEXT | BLM registered item ID |
| `booth_item_id` | INTEGER | Booth item ID |
| `created_at` | TEXT datetime | 作成時刻 |
| `updated_at` | TEXT datetime | 更新時刻 |
| `user_item_info_id` | INTEGER | user item info ID |

#### `user_item_info`

| column | 型 | 内容 |
|---|---|---|
| `id` | INTEGER | user item info ID |
| `name` | TEXT | ユーザー定義 item 名 |
| `shop_name` | TEXT | ユーザー定義 shop 名 |
| `thumbnail_filename` | TEXT | thumbnail file name |
| `sub_category` | INTEGER | sub category ID |
| `description` | TEXT | ユーザー定義説明 |
| `adult` | BOOLEAN | adult flag |
| `created_at` | TEXT datetime | 作成時刻 |
| `updated_at` | TEXT datetime | 更新時刻 |

#### `booth_items`

| column | 型 | 内容 |
|---|---|---|
| `id` | INTEGER | Booth item ID |
| `name` | TEXT | Booth 商品名 |
| `shop_subdomain` | TEXT | Booth shop subdomain |
| `thumbnail_url` | TEXT | 商品 thumbnail URL |
| `sub_category` | INTEGER | sub category ID |
| `description` | TEXT | 商品説明 |
| `adult` | BOOLEAN | adult flag |
| `published_at` | TEXT datetime | 公開時刻 |
| `updated_at` | TEXT datetime | 更新時刻 |

#### `booth_item_variations`

| column | 型 | 内容 |
|---|---|---|
| `id` | INTEGER | variation ID |
| `booth_item_id` | INTEGER | Booth item ID |
| `variation_name` | TEXT | variation 名 |
| `order_id` | INTEGER | 表示順 / order ID |

#### `booth_tags`

| column | 型 | 内容 |
|---|---|---|
| `name` | TEXT | Booth tag 名 |

#### `booth_item_tag_relations`

| column | 型 | 内容 |
|---|---|---|
| `booth_item_id` | INTEGER | Booth item ID |
| `tag` | TEXT | Booth tag 名 |

#### `overwritten_booth_items`

| column | 型 | 内容 |
|---|---|---|
| `booth_item_id` | INTEGER | Booth item ID |
| `name` | TEXT | 上書き商品名 |
| `description` | TEXT | 上書き説明 |
| `adult` | BOOLEAN | 上書き adult flag |

#### `overwritten_booth_item_tags`

| column | 型 | 内容 |
|---|---|---|
| `booth_item_id` | INTEGER | Booth item ID |
| `tag` | TEXT | 上書き tag 名 |

#### `booth_item_update_history`

| column | 型 | 内容 |
|---|---|---|
| `booth_item_id` | INTEGER | Booth item ID |
| `last_updated_at` | TEXT datetime | Booth 正本から最後に更新した時刻 |

### 3-3. カテゴリ / ショップ関連

#### `parent_categories`

| column | 型 | 内容 |
|---|---|---|
| `id` | INTEGER | parent category ID |
| `name` | TEXT | parent category 名 |

#### `sub_categories`

| column | 型 | 内容 |
|---|---|---|
| `id` | INTEGER | sub category ID |
| `name` | TEXT | sub category 名 |
| `parent_category_id` | INTEGER | parent category ID |

#### `shops`

| column | 型 | 内容 |
|---|---|---|
| `subdomain` | TEXT | Booth shop subdomain |
| `name` | TEXT | Booth shop 名 |
| `thumbnail_url` | TEXT | shop thumbnail URL |

### 3-4. 設定 / 運用関連

#### `preferences`

| column | 型 | 内容 |
|---|---|---|
| `id` | INTEGER | preference ID |
| `theme` | TEXT | BLM theme |
| `language` | TEXT | BLM language |
| `item_directory_path` | BLOB | BLM item directory path |

#### `notifications`

| column | 型 | 内容 |
|---|---|---|
| `id` | INTEGER | notification ID |
| `title` | TEXT | notification title |
| `content` | TEXT | notification body |
| `read` | INTEGER | read flag |
| `created_at` | TEXT datetime | 作成時刻 |

#### `schema_version`

| column | 型 | 内容 |
|---|---|---|
| `version` | INTEGER | BLM DB schema version |

#### `tos_agreements`

| column | 型 | 内容 |
|---|---|---|
| `version` | INTEGER | TOS version |
| `agreed_at` | TEXT datetime | 同意時刻 |

## 4. BLM リレーション要素

| from | to | delete rule |
|---|---|---|
| `booth_item_tag_relations.tag` | `booth_tags.name` | CASCADE |
| `booth_item_tag_relations.booth_item_id` | `booth_items.id` | CASCADE |
| `booth_item_update_history.booth_item_id` | `booth_items.id` | CASCADE |
| `booth_item_variations.booth_item_id` | `booth_items.id` | CASCADE |
| `booth_items.sub_category` | `sub_categories.id` | CASCADE |
| `booth_items.shop_subdomain` | `shops.subdomain` | CASCADE |
| `list_items.item_id` | `registered_items.id` | CASCADE |
| `list_items.list_id` | `lists.id` | CASCADE |
| `overwritten_booth_item_tags.booth_item_id` | `booth_items.id` | CASCADE |
| `overwritten_booth_items.booth_item_id` | `booth_items.id` | CASCADE |
| `registered_items.booth_item_id` | `booth_items.id` | CASCADE |
| `registered_items.user_item_info_id` | `user_item_info.id` | CASCADE |
| `smart_list_criteria.subcategory_id` | `sub_categories.id` | SET NULL |
| `smart_list_criteria.category_id` | `parent_categories.id` | SET NULL |
| `smart_list_criteria.smart_list_id` | `smart_lists.id` | CASCADE |
| `smart_list_tags.smart_list_id` | `smart_lists.id` | CASCADE |
| `sub_categories.parent_category_id` | `parent_categories.id` | CASCADE |

## 5. Eagle / BLM 共通データ要素

ここでは、Eagle Booth metadata JSON と BLM `data.db` の両方から同じ意味として扱える情報をまとめる。  
BLM 側で URL が直接保存されていないものは、`shops.subdomain` と `booth_items.id` から導出する。

| 共通要素 | Eagle | BLM | 補足 |
|---|---|---|---|
| Booth item ID | `boothItemId` | `booth_items.id` | 商品の最も強い同一性キー |
| Booth item URL | `itemUrl` | `shops.subdomain` + `booth_items.id` | BLM は `https://{subdomain}.booth.pm/items/{id}` として導出 |
| 商品名 | `name` | `booth_items.name`, `overwritten_booth_items.name` | BLM は overwrite があれば優先 |
| 商品説明 | `description` | `booth_items.description`, `overwritten_booth_items.description` | BLM は overwrite があれば優先 |
| 商品 thumbnail URL | `thumbnailUrl` | `booth_items.thumbnail_url` | 画像本体ではなく URL |
| shop 名 | `shopName` | `shops.name` |  |
| shop URL | `shopUrl` | `shops.subdomain` | BLM は `https://{subdomain}.booth.pm` として導出 |
| shop thumbnail URL | `shopThumbnailUrl` | `shops.thumbnail_url` |  |
| Booth tag | `tags[]` | `booth_item_tag_relations.tag`, `overwritten_booth_item_tags.tag` | BLM は overwritten tag があれば優先 |
| Booth 正本の更新時刻 | `lastUpdatedAtUtc` | `booth_item_update_history.last_updated_at` | Booth から最後に取得した時刻 |
| metadata 紐付け / 登録時刻 | `attachedAt` | `registered_items.created_at` | 厳密には同じ意味ではないが、source に登録された時刻として近い |

## 6. Eagle / BLM の片側にのみ存在する主な情報

共通データとして正規化できない情報は以下。

### Eagle のみ

| 要素 | Eagle |
|---|---|
| download 情報 | `downloads[]` |
| download URL | `downloads[].downloadUrl` |
| download ID | `downloads[].downloadId` |
| download filename | `downloads[].filename` |
| download requested/imported time | `downloads[].requestedAt`, `downloads[].importedAt` |
| imported Eagle item IDs | `downloads[].importedItemIds[]` |
| Eagle item ID | `metadata.json.id` |
| Eagle item size | `metadata.json.size` |
| Eagle item ext | `metadata.json.ext` |
| Eagle folder IDs | `metadata.json.folders[]` |
| Eagle deleted flag | `metadata.json.isDeleted` |
| Eagle annotation | `metadata.json.annotation` |
| Eagle preview size | `metadata.json.width`, `metadata.json.height` |
| Eagle custom thumbnail flag | `metadata.json.customThumbnail` |
| Eagle palette | `metadata.json.palettes[]` |

### BLM のみ

| 要素 | BLM |
|---|---|
| adult flag | `booth_items.adult`, `overwritten_booth_items.adult` |
| published time | `booth_items.published_at` |
| Booth category | `parent_categories`, `sub_categories`, `booth_items.sub_category` |
| Booth variation | `booth_item_variations` |
| BLM registered item ID | `registered_items.id` |
| BLM user item info | `user_item_info` |
| list / list item | `lists`, `list_items` |
| smart list / criteria / tag | `smart_lists`, `smart_list_criteria`, `smart_list_tags` |
| BLM preference | `preferences` |
| BLM notification | `notifications` |
| BLM schema version | `schema_version` |
| BLM TOS agreement | `tos_agreements` |

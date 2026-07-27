# data.db スキーマ構造メモ

このドキュメントは **DBの構造のみ** を整理したものです。レコード件数や実データ内容には触れていません。

## 概要

- テーブル数: **21**
- DB種別: **SQLite**
- 主な領域: リスト、作品/タグ、カテゴリ、設定

## テーブル一覧

### リスト関連

- `lists`
- `list_items`
- `smart_lists`
- `smart_list_criteria`
- `smart_list_tags`

### 作品・タグ関連

- `registered_items`
- `user_item_info`
- `booth_items`
- `booth_item_variations`
- `booth_tags`
- `booth_item_tag_relations`
- `overwritten_booth_items`
- `overwritten_booth_item_tags`
- `booth_item_update_history`

### カテゴリ・ショップ関連

- `parent_categories`
- `sub_categories`
- `shops`

### 設定・運用関連

- `preferences`
- `notifications`
- `schema_version`
- `tos_agreements`

## リレーション概要

- `booth_item_tag_relations.tag` → `booth_tags.name` (ON DELETE CASCADE)
- `booth_item_tag_relations.booth_item_id` → `booth_items.id` (ON DELETE CASCADE)
- `booth_item_update_history.booth_item_id` → `booth_items.id` (ON DELETE CASCADE)
- `booth_item_variations.booth_item_id` → `booth_items.id` (ON DELETE CASCADE)
- `booth_items.sub_category` → `sub_categories.id` (ON DELETE CASCADE)
- `booth_items.shop_subdomain` → `shops.subdomain` (ON DELETE CASCADE)
- `list_items.item_id` → `registered_items.id` (ON DELETE CASCADE)
- `list_items.list_id` → `lists.id` (ON DELETE CASCADE)
- `overwritten_booth_item_tags.booth_item_id` → `booth_items.id` (ON DELETE CASCADE)
- `overwritten_booth_items.booth_item_id` → `booth_items.id` (ON DELETE CASCADE)
- `registered_items.booth_item_id` → `booth_items.id` (ON DELETE CASCADE)
- `registered_items.user_item_info_id` → `user_item_info.id` (ON DELETE CASCADE)
- `smart_list_criteria.subcategory_id` → `sub_categories.id` (ON DELETE SET NULL)
- `smart_list_criteria.category_id` → `parent_categories.id` (ON DELETE SET NULL)
- `smart_list_criteria.smart_list_id` → `smart_lists.id` (ON DELETE CASCADE)
- `smart_list_tags.smart_list_id` → `smart_lists.id` (ON DELETE CASCADE)
- `sub_categories.parent_category_id` → `parent_categories.id` (ON DELETE CASCADE)

## `booth_item_tag_relations`

| 列名 | 型 | NOT NULL | PK | DEFAULT |
|---|---|---:|---:|---|
| `booth_item_id` | `INTEGER` | Yes |  | `` |
| `tag` | `TEXT` | Yes |  | `` |

**外部キー**

- `tag` → `booth_tags.name` (ON DELETE CASCADE)
- `booth_item_id` → `booth_items.id` (ON DELETE CASCADE)

**インデックス / UNIQUE**

- `sqlite_autoindex_booth_item_tag_relations_1`: UNIQUE (`booth_item_id`, `tag`)

**CREATE TABLE**

```sql
CREATE TABLE booth_item_tag_relations (
   booth_item_id INTEGER NOT NULL,
   tag TEXT NOT NULL,
   FOREIGN KEY (booth_item_id) REFERENCES booth_items(id) ON DELETE CASCADE,
   FOREIGN KEY (tag) REFERENCES booth_tags(name) ON DELETE CASCADE,
   UNIQUE(booth_item_id, tag)
)
```

## `booth_item_update_history`

| 列名 | 型 | NOT NULL | PK | DEFAULT |
|---|---|---:|---:|---|
| `booth_item_id` | `INTEGER` | Yes |  | `` |
| `last_updated_at` | `TEXT` | Yes |  | `STRFTIME('%Y-%m-%dT%H:%M:%SZ', 'now')` |

**外部キー**

- `booth_item_id` → `booth_items.id` (ON DELETE CASCADE)

**インデックス / UNIQUE**

- `sqlite_autoindex_booth_item_update_history_1`: UNIQUE (`booth_item_id`)

**CREATE TABLE**

```sql
CREATE TABLE booth_item_update_history (
   booth_item_id INTEGER NOT NULL,
   last_updated_at TEXT NOT NULL DEFAULT (STRFTIME('%Y-%m-%dT%H:%M:%SZ', 'now')),
   UNIQUE(booth_item_id),
   FOREIGN KEY (booth_item_id) REFERENCES booth_items(id) ON DELETE CASCADE
)
```

## `booth_item_variations`

| 列名 | 型 | NOT NULL | PK | DEFAULT |
|---|---|---:|---:|---|
| `id` | `INTEGER` | Yes | Yes | `` |
| `booth_item_id` | `INTEGER` | Yes |  | `` |
| `variation_name` | `TEXT` |  |  | `` |
| `order_id` | `INTEGER` |  |  | `` |

**外部キー**

- `booth_item_id` → `booth_items.id` (ON DELETE CASCADE)

**CREATE TABLE**

```sql
CREATE TABLE booth_item_variations (
   id INTEGER PRIMARY KEY NOT NULL,
   booth_item_id INTEGER NOT NULL,
   variation_name TEXT, order_id INTEGER,
   FOREIGN KEY (booth_item_id) REFERENCES booth_items(id) ON DELETE CASCADE
)
```

## `booth_items`

| 列名 | 型 | NOT NULL | PK | DEFAULT |
|---|---|---:|---:|---|
| `id` | `INTEGER` | Yes | Yes | `` |
| `name` | `TEXT` | Yes |  | `` |
| `shop_subdomain` | `TEXT` | Yes |  | `` |
| `thumbnail_url` | `TEXT` |  |  | `` |
| `sub_category` | `INTEGER` | Yes |  | `` |
| `description` | `TEXT` |  |  | `` |
| `adult` | `BOOLEAN` | Yes |  | `` |
| `published_at` | `TEXT` | Yes |  | `` |
| `updated_at` | `TEXT` | Yes |  | `STRFTIME('%Y-%m-%dT%H:%M:%SZ', 'now')` |

**外部キー**

- `sub_category` → `sub_categories.id` (ON DELETE CASCADE)
- `shop_subdomain` → `shops.subdomain` (ON DELETE CASCADE)

**CREATE TABLE**

```sql
CREATE TABLE booth_items (
   id INTEGER PRIMARY KEY NOT NULL,
   name TEXT NOT NULL,
   shop_subdomain TEXT NOT NULL,
   thumbnail_url TEXT,
   sub_category INTEGER NOT NULL,
   description TEXT,
   adult BOOLEAN NOT NULL,
   published_at TEXT NOT NULL,
   updated_at TEXT NOT NULL DEFAULT (STRFTIME('%Y-%m-%dT%H:%M:%SZ', 'now')),
   FOREIGN KEY (shop_subdomain) REFERENCES shops(subdomain) ON DELETE CASCADE,
   FOREIGN KEY (sub_category) REFERENCES sub_categories(id) ON DELETE CASCADE
)
```

## `booth_tags`

| 列名 | 型 | NOT NULL | PK | DEFAULT |
|---|---|---:|---:|---|
| `name` | `TEXT` | Yes | Yes | `` |

**インデックス / UNIQUE**

- `sqlite_autoindex_booth_tags_1`: UNIQUE (`name`)

**CREATE TABLE**

```sql
CREATE TABLE booth_tags (
   name TEXT PRIMARY KEY NOT NULL
)
```

## `list_items`

| 列名 | 型 | NOT NULL | PK | DEFAULT |
|---|---|---:|---:|---|
| `id` | `INTEGER` | Yes | Yes | `` |
| `list_id` | `INTEGER` | Yes |  | `` |
| `item_id` | `TEXT` | Yes |  | `` |
| `added_at` | `TEXT` | Yes |  | `STRFTIME('%Y-%m-%dT%H:%M:%SZ', 'now')` |

**外部キー**

- `item_id` → `registered_items.id` (ON DELETE CASCADE)
- `list_id` → `lists.id` (ON DELETE CASCADE)

**インデックス / UNIQUE**

- `sqlite_autoindex_list_items_1`: UNIQUE (`list_id`, `item_id`)

**CREATE TABLE**

```sql
CREATE TABLE list_items (
   id INTEGER PRIMARY KEY AUTOINCREMENT NOT NULL,
   list_id INTEGER NOT NULL,
   item_id TEXT NOT NULL,
   added_at TEXT NOT NULL DEFAULT (STRFTIME('%Y-%m-%dT%H:%M:%SZ', 'now')),
   FOREIGN KEY (list_id) REFERENCES lists(id) ON DELETE CASCADE,
   FOREIGN KEY (item_id) REFERENCES registered_items(id) ON DELETE CASCADE,
   UNIQUE(list_id, item_id)
)
```

## `lists`

| 列名 | 型 | NOT NULL | PK | DEFAULT |
|---|---|---:|---:|---|
| `id` | `INTEGER` | Yes | Yes | `` |
| `title` | `TEXT` | Yes |  | `` |
| `description` | `TEXT` |  |  | `` |
| `created_at` | `TEXT` | Yes |  | `STRFTIME('%Y-%m-%dT%H:%M:%SZ', 'now')` |
| `updated_at` | `TEXT` | Yes |  | `STRFTIME('%Y-%m-%dT%H:%M:%SZ', 'now')` |

**CREATE TABLE**

```sql
CREATE TABLE lists (
   id INTEGER PRIMARY KEY AUTOINCREMENT NOT NULL,
   title TEXT NOT NULL,
   description TEXT,
   created_at TEXT NOT NULL DEFAULT (STRFTIME('%Y-%m-%dT%H:%M:%SZ', 'now')),
   updated_at TEXT NOT NULL DEFAULT (STRFTIME('%Y-%m-%dT%H:%M:%SZ', 'now'))
)
```

## `notifications`

| 列名 | 型 | NOT NULL | PK | DEFAULT |
|---|---|---:|---:|---|
| `id` | `INTEGER` | Yes | Yes | `` |
| `title` | `TEXT` |  |  | `` |
| `content` | `TEXT` | Yes |  | `` |
| `read` | `INTEGER` | Yes |  | `0` |
| `created_at` | `TEXT` | Yes |  | `STRFTIME('%Y-%m-%dT%H:%M:%SZ', 'now')` |

**CREATE TABLE**

```sql
CREATE TABLE notifications (
   id INTEGER PRIMARY KEY AUTOINCREMENT NOT NULL,
   title TEXT,
   content TEXT NOT NULL,
   read INTEGER NOT NULL DEFAULT 0,
   created_at TEXT NOT NULL DEFAULT (STRFTIME('%Y-%m-%dT%H:%M:%SZ', 'now'))
)
```

## `overwritten_booth_item_tags`

| 列名 | 型 | NOT NULL | PK | DEFAULT |
|---|---|---:|---:|---|
| `booth_item_id` | `INTEGER` | Yes |  | `` |
| `tag` | `TEXT` | Yes |  | `` |

**外部キー**

- `booth_item_id` → `booth_items.id` (ON DELETE CASCADE)

**インデックス / UNIQUE**

- `sqlite_autoindex_overwritten_booth_item_tags_1`: UNIQUE (`booth_item_id`, `tag`)

**CREATE TABLE**

```sql
CREATE TABLE overwritten_booth_item_tags (
   booth_item_id INTEGER NOT NULL,
   tag TEXT NOT NULL,
   FOREIGN KEY (booth_item_id) REFERENCES booth_items(id) ON DELETE CASCADE,
   UNIQUE(booth_item_id, tag)
)
```

## `overwritten_booth_items`

| 列名 | 型 | NOT NULL | PK | DEFAULT |
|---|---|---:|---:|---|
| `booth_item_id` | `INTEGER` | Yes | Yes | `` |
| `name` | `TEXT` |  |  | `` |
| `description` | `TEXT` |  |  | `` |
| `adult` | `BOOLEAN` |  |  | `` |

**外部キー**

- `booth_item_id` → `booth_items.id` (ON DELETE CASCADE)

**CREATE TABLE**

```sql
CREATE TABLE overwritten_booth_items (
   booth_item_id INTEGER PRIMARY KEY NOT NULL,
   name TEXT,
   description TEXT,
   adult BOOLEAN,
   FOREIGN KEY (booth_item_id) REFERENCES booth_items(id) ON DELETE CASCADE
)
```

## `parent_categories`

| 列名 | 型 | NOT NULL | PK | DEFAULT |
|---|---|---:|---:|---|
| `id` | `INTEGER` | Yes | Yes | `` |
| `name` | `TEXT` | Yes |  | `` |

**インデックス / UNIQUE**

- `sqlite_autoindex_parent_categories_1`: UNIQUE (`name`)

**CREATE TABLE**

```sql
CREATE TABLE parent_categories (
   id INTEGER PRIMARY KEY AUTOINCREMENT NOT NULL,
   name TEXT NOT NULL,
   UNIQUE(name)
)
```

## `preferences`

| 列名 | 型 | NOT NULL | PK | DEFAULT |
|---|---|---:|---:|---|
| `id` | `INTEGER` | Yes | Yes | `` |
| `theme` | `TEXT` | Yes |  | `` |
| `language` | `TEXT` | Yes |  | `` |
| `item_directory_path` | `BLOB` | Yes |  | `` |

**CREATE TABLE**

```sql
CREATE TABLE preferences (
    id INTEGER PRIMARY KEY AUTOINCREMENT NOT NULL,
    theme TEXT NOT NULL,
    language TEXT NOT NULL,
    item_directory_path BLOB NOT NULL
)
```

## `registered_items`

| 列名 | 型 | NOT NULL | PK | DEFAULT |
|---|---|---:|---:|---|
| `id` | `TEXT` | Yes | Yes | `` |
| `booth_item_id` | `INTEGER` |  |  | `` |
| `created_at` | `TEXT` | Yes |  | `STRFTIME('%Y-%m-%dT%H:%M:%SZ', 'now')` |
| `updated_at` | `TEXT` | Yes |  | `STRFTIME('%Y-%m-%dT%H:%M:%SZ', 'now')` |
| `user_item_info_id` | `INTEGER` |  |  | `` |

**外部キー**

- `booth_item_id` → `booth_items.id` (ON DELETE CASCADE)
- `user_item_info_id` → `user_item_info.id` (ON DELETE CASCADE)

**インデックス / UNIQUE**

- `sqlite_autoindex_registered_items_1`: UNIQUE (`id`)

**CREATE TABLE**

```sql
CREATE TABLE registered_items (
   id TEXT PRIMARY KEY NOT NULL,
   booth_item_id INTEGER,
   created_at TEXT NOT NULL DEFAULT (STRFTIME('%Y-%m-%dT%H:%M:%SZ', 'now')),
   updated_at TEXT NOT NULL DEFAULT (STRFTIME('%Y-%m-%dT%H:%M:%SZ', 'now')), user_item_info_id INTEGER REFERENCES user_item_info(id) ON DELETE CASCADE,
   FOREIGN KEY (booth_item_id) REFERENCES booth_items(id) ON DELETE CASCADE
)
```

## `schema_version`

| 列名 | 型 | NOT NULL | PK | DEFAULT |
|---|---|---:|---:|---|
| `version` | `INTEGER` |  |  | `` |

**CREATE TABLE**

```sql
CREATE TABLE schema_version (version INTEGER)
```

## `shops`

| 列名 | 型 | NOT NULL | PK | DEFAULT |
|---|---|---:|---:|---|
| `subdomain` | `TEXT` | Yes | Yes | `` |
| `name` | `TEXT` | Yes |  | `` |
| `thumbnail_url` | `TEXT` |  |  | `` |

**インデックス / UNIQUE**

- `sqlite_autoindex_shops_1`: UNIQUE (`subdomain`)

**CREATE TABLE**

```sql
CREATE TABLE shops (
   subdomain TEXT PRIMARY KEY NOT NULL,
   name TEXT NOT NULL,
   thumbnail_url TEXT
)
```

## `smart_list_criteria`

| 列名 | 型 | NOT NULL | PK | DEFAULT |
|---|---|---:|---:|---|
| `id` | `INTEGER` | Yes | Yes | `` |
| `smart_list_id` | `INTEGER` | Yes |  | `` |
| `text` | `TEXT` |  |  | `` |
| `category_id` | `INTEGER` |  |  | `` |
| `subcategory_id` | `INTEGER` |  |  | `` |
| `age_restriction` | `TEXT` | Yes |  | `'all'` |

**外部キー**

- `subcategory_id` → `sub_categories.id` (ON DELETE SET NULL)
- `category_id` → `parent_categories.id` (ON DELETE SET NULL)
- `smart_list_id` → `smart_lists.id` (ON DELETE CASCADE)

**補足制約**

- CHECK: `age_restriction IN ('all', 'adult_only', 'safe'`

**CREATE TABLE**

```sql
CREATE TABLE smart_list_criteria (
   id INTEGER PRIMARY KEY AUTOINCREMENT NOT NULL,
   smart_list_id INTEGER NOT NULL,
   text TEXT,
   category_id INTEGER,
   subcategory_id INTEGER,
   age_restriction TEXT NOT NULL DEFAULT 'all' CHECK(age_restriction IN ('all', 'adult_only', 'safe')),
   FOREIGN KEY (smart_list_id) REFERENCES smart_lists(id) ON DELETE CASCADE,
   FOREIGN KEY (category_id) REFERENCES parent_categories(id) ON DELETE SET NULL,
   FOREIGN KEY (subcategory_id) REFERENCES sub_categories(id) ON DELETE SET NULL
)
```

## `smart_list_tags`

| 列名 | 型 | NOT NULL | PK | DEFAULT |
|---|---|---:|---:|---|
| `id` | `INTEGER` | Yes | Yes | `` |
| `smart_list_id` | `INTEGER` | Yes |  | `` |
| `tag` | `TEXT` | Yes |  | `` |

**外部キー**

- `smart_list_id` → `smart_lists.id` (ON DELETE CASCADE)

**インデックス / UNIQUE**

- `sqlite_autoindex_smart_list_tags_1`: UNIQUE (`smart_list_id`, `tag`)

**CREATE TABLE**

```sql
CREATE TABLE smart_list_tags (
   id INTEGER PRIMARY KEY AUTOINCREMENT NOT NULL,
   smart_list_id INTEGER NOT NULL,
   tag TEXT NOT NULL,
   FOREIGN KEY (smart_list_id) REFERENCES smart_lists(id) ON DELETE CASCADE,
   UNIQUE(smart_list_id, tag)
)
```

## `smart_lists`

| 列名 | 型 | NOT NULL | PK | DEFAULT |
|---|---|---:|---:|---|
| `id` | `INTEGER` | Yes | Yes | `` |
| `title` | `TEXT` | Yes |  | `` |
| `description` | `TEXT` |  |  | `` |
| `created_at` | `TEXT` | Yes |  | `STRFTIME('%Y-%m-%dT%H:%M:%SZ', 'now')` |
| `updated_at` | `TEXT` | Yes |  | `STRFTIME('%Y-%m-%dT%H:%M:%SZ', 'now')` |

**CREATE TABLE**

```sql
CREATE TABLE smart_lists (
   id INTEGER PRIMARY KEY AUTOINCREMENT NOT NULL,
   title TEXT NOT NULL,
   description TEXT,
   created_at TEXT NOT NULL DEFAULT (STRFTIME('%Y-%m-%dT%H:%M:%SZ', 'now')),
   updated_at TEXT NOT NULL DEFAULT (STRFTIME('%Y-%m-%dT%H:%M:%SZ', 'now'))
)
```

## `sub_categories`

| 列名 | 型 | NOT NULL | PK | DEFAULT |
|---|---|---:|---:|---|
| `id` | `INTEGER` | Yes | Yes | `` |
| `name` | `TEXT` | Yes |  | `` |
| `parent_category_id` | `INTEGER` | Yes |  | `` |

**外部キー**

- `parent_category_id` → `parent_categories.id` (ON DELETE CASCADE)

**インデックス / UNIQUE**

- `sqlite_autoindex_sub_categories_1`: UNIQUE (`name`)

**CREATE TABLE**

```sql
CREATE TABLE sub_categories (
   id INTEGER PRIMARY KEY NOT NULL,
   name TEXT NOT NULL,
   parent_category_id INTEGER NOT NULL,
   UNIQUE(name),
   FOREIGN KEY (parent_category_id) REFERENCES parent_categories(id) ON DELETE CASCADE
)
```

## `tos_agreements`

| 列名 | 型 | NOT NULL | PK | DEFAULT |
|---|---|---:|---:|---|
| `version` | `INTEGER` | Yes | Yes | `` |
| `agreed_at` | `TEXT` | Yes |  | `STRFTIME('%Y-%m-%dT%H:%M:%SZ', 'now')` |

**CREATE TABLE**

```sql
CREATE TABLE tos_agreements (
  version INTEGER PRIMARY KEY NOT NULL,
  agreed_at TEXT NOT NULL DEFAULT (STRFTIME('%Y-%m-%dT%H:%M:%SZ', 'now'))
)
```

## `user_item_info`

| 列名 | 型 | NOT NULL | PK | DEFAULT |
|---|---|---:|---:|---|
| `id` | `INTEGER` | Yes | Yes | `` |
| `name` | `TEXT` | Yes |  | `` |
| `shop_name` | `TEXT` | Yes |  | `` |
| `thumbnail_filename` | `TEXT` |  |  | `` |
| `sub_category` | `INTEGER` |  |  | `` |
| `description` | `TEXT` |  |  | `` |
| `adult` | `BOOLEAN` | Yes |  | `` |
| `created_at` | `TEXT` | Yes |  | `STRFTIME('%Y-%m-%dT%H:%M:%SZ', 'now')` |
| `updated_at` | `TEXT` | Yes |  | `STRFTIME('%Y-%m-%dT%H:%M:%SZ', 'now')` |

**CREATE TABLE**

```sql
CREATE TABLE user_item_info (
   id INTEGER PRIMARY KEY AUTOINCREMENT NOT NULL,
   name TEXT NOT NULL,
   shop_name TEXT NOT NULL,
   thumbnail_filename TEXT,
   sub_category INTEGER,
   description TEXT,
   adult BOOLEAN NOT NULL,
   created_at TEXT NOT NULL DEFAULT (STRFTIME('%Y-%m-%dT%H:%M:%SZ', 'now')),
   updated_at TEXT NOT NULL DEFAULT (STRFTIME('%Y-%m-%dT%H:%M:%SZ', 'now'))
)
```

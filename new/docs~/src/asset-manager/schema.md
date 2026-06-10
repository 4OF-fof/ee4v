# AssetManager DB Schema

AssetManager 独自 DB の schema です。Item / File / Collection を中心に、Booth・Eagle・ee4v の origin を分離して保持します。

関連する datasource の元データは [Datasource Data Elements](./data-elements.md) を参照してください。

## Enum

```sql
CHECK (source_type IN ('blm', 'eagle', 'ee4v'))
CHECK (last_sync_status IN ('success', 'failed', 'partial'))
CHECK (file_lifecycle IN ('active', 'archived'))
CHECK (file_dependency_type IN ('requires'))
CHECK (smart_collection_match_mode IN ('all', 'any'))
CHECK (smart_collection_condition_field IN ('name', 'description', 'tag', 'source_type', 'file_name', 'extension', 'lifecycle'))
CHECK (smart_collection_condition_operator IN ('contains', 'equals', 'in', 'exists'))
```

## Relation Diagram

```mermaid
erDiagram
    item_info ||--o{ file_info : ""
    file_info ||--o{ file_dependency : ""
    file_info ||--o{ file_dependency : ""
    file_info ||--o{ file_import_target : ""
    file_info ||--o| eagle_file_origin : ""
    file_info ||--o| blm_file_origin : ""
    file_info ||--o| ee4v_file_origin : ""
    shop_info ||--o{ booth_info : ""
    item_info ||--o| booth_info : ""
    item_info ||--o{ item_tag : ""
    tag_info ||--o{ item_tag : ""
    item_info ||--o{ item_collection : ""
    collection_info ||--o{ item_collection : ""
    collection_info ||--o| smart_collection_info : ""
    smart_collection_info ||--o{ smart_collection_condition : ""
    collection_info ||--o{ collection_collection : ""
    collection_info ||--o| collection_collection : ""

    item_info {
        GUID string id PK
    }

    shop_info {
        GUID string id PK
    }

    schema_version {
        INTEGER version PK
    }

    sync_info {
        source_type source_type PK
        sync_status last_sync_status
    }

    tag_info {
        GUID string id PK
    }

    item_tag {
        GUID string item_info_id PK, FK
        GUID string tag_info_id PK, FK
    }

    collection_info {
        GUID string id PK
    }

    smart_collection_info {
        GUID string collection_info_id PK, FK
        smart_collection_match_mode match_mode
    }

    smart_collection_condition {
        GUID string id PK
        GUID string collection_info_id FK
    }

    collection_collection {
        GUID string parent_collection_id PK, FK
        GUID string child_collection_id PK, FK
    }

    item_collection {
        GUID string item_info_id PK, FK
        GUID string collection_info_id PK, FK
    }

    booth_info {
        GUID string id PK
        GUID string item_info_id FK
        GUID string shop_info_id FK
    }

    file_info {
        GUID string id PK
        GUID string item_info_id FK
    }

    file_dependency {
        GUID string dependent_file_info_id PK, FK
        GUID string dependency_file_info_id PK, FK
        file_dependency_type dependency_type PK
    }

    file_import_target {
        GUID string id PK
        GUID string file_info_id FK
    }

    eagle_file_origin {
        GUID string file_info_id PK, FK
    }

    blm_file_origin {
        GUID string file_info_id PK, FK
        TEXT registered_item_id
    }

    ee4v_file_origin {
        GUID string file_info_id PK, FK
    }

```

## Item Info

AssetManager 上の表示単位。Booth 商品、Eagle item、手動登録 file などを統合して扱う。

Booth 由来 item の初回作成時は `booth_info.name` / `booth_info.description` で初期化し、`item_info.name` / `item_info.description`は上書きを許可する。

| column | type | required | unique | note |
|---|---|---:|---|---|
| `id` | GUID string | Yes |  | Item Info の識別子 |
| `name` | TEXT | Yes |  | 表示名。ユーザー上書き可能 |
| `description` | TEXT | Yes |  | 表示用説明。ユーザー上書き可能。空文字を許容 |
| `created_at` | DATETIME | Yes |  | 作成時刻 |
| `updated_at` | DATETIME | Yes |  | 更新時刻 |

## Tag Info

AssetManager 独自 tag の正本。Booth tag / Eagle tag とは完全に独立して扱う。

| column | type | required | unique | note |
|---|---|---:|---|---|
| `id` | GUID string | Yes |  | Tag Info の識別子 |
| `name` | TEXT | Yes | Yes | tag 名 |
| `created_at` | DATETIME | Yes |  | 作成時刻 |
| `updated_at` | DATETIME | Yes |  | 更新時刻 |

```sql
name TEXT NOT NULL UNIQUE
```

## Item Tag

Item と AssetManager 独自 tag の付与関係。

| column | type | required | unique | note |
|---|---|---:|---|---|
| `item_info_id` | GUID string | Yes | `(item_info_id, tag_info_id)` | 親 Item Info |
| `tag_info_id` | GUID string | Yes | `(item_info_id, tag_info_id)` | 付与 Tag Info |
| `created_at` | DATETIME | Yes |  | 付与作成時刻 |

```sql
CREATE UNIQUE INDEX unique_item_tag
ON item_tag(item_info_id, tag_info_id);
```

## Collection Info

AssetManager 独自 folder / collection。階層構造は `collection_collection` で管理し、Item は複数 Collection に所属できる。

| column | type | required | unique | note |
|---|---|---:|---|---|
| `id` | GUID string | Yes |  | Collection Info の識別子 |
| `name` | TEXT | Yes |  | collection 名 |
| `created_at` | DATETIME | Yes |  | 作成時刻 |
| `updated_at` | DATETIME | Yes |  | 更新時刻 |

## Smart Collection Info

条件に一致する Item を自動収集する Collection。

通常 Collection と同じ `collection_info` として階層に配置し、条件定義だけを Smart Collection 固有情報として保持する。

Smart Collection の所属 Item は永続化しない。条件評価による Item 抽出は `smart_collection_condition` から実行する。

| column | type | required | unique | note |
|---|---|---:|---|---|
| `collection_info_id` | GUID string | Yes | Yes | 対応する Collection Info |
| `match_mode` | smart_collection_match_mode | Yes |  | 複数条件の結合方法 |
| `created_at` | DATETIME | Yes |  | 作成時刻 |
| `updated_at` | DATETIME | Yes |  | 更新時刻 |

```sql
CHECK (match_mode IN ('all', 'any'))
```

## Smart Collection Condition

Smart Collection の検索条件。

`collection_info_id` が示す Smart Collection に対して、`field` / `operator` / `query_text` の条件定義を表す。

| column | type | required | unique | note |
|---|---|---:|---|---|
| `id` | GUID string | Yes |  | Smart Collection Condition の識別子 |
| `collection_info_id` | GUID string | Yes |  | 親 Smart Collection Info |
| `field` | smart_collection_condition_field | Yes |  | 評価対象 field |
| `operator` | smart_collection_condition_operator | Yes |  | 評価方法 |
| `query_text` | TEXT |  |  | 検索文字列。`exists` では NULL を許容 |
| `created_at` | DATETIME | Yes |  | 作成時刻 |
| `updated_at` | DATETIME | Yes |  | 更新時刻 |

```sql
CHECK (field IN ('name', 'description', 'tag', 'source_type', 'file_name', 'extension', 'lifecycle'))
CHECK (operator IN ('contains', 'equals', 'in', 'exists'))
CHECK (operator = 'exists' OR query_text IS NOT NULL)
```

## Collection Collection

Collection 同士の親子関係。`parent_collection_id` が `child_collection_id` を含む。tree として扱い、子 Collection は最大 1 つの親 Collection だけを持てる。親を持たない Collection は root collection として扱う。

| column | type | required | unique | note |
|---|---|---:|---|---|
| `parent_collection_id` | GUID string | Yes | `(parent_collection_id, child_collection_id)` | 親 Collection Info |
| `child_collection_id` | GUID string | Yes | `(parent_collection_id, child_collection_id)`, `child_collection_id` | 子 Collection Info |
| `created_at` | DATETIME | Yes |  | 親子関係の作成時刻 |

```sql
CHECK (parent_collection_id != child_collection_id)

CREATE UNIQUE INDEX unique_collection_collection
ON collection_collection(parent_collection_id, child_collection_id);

CREATE UNIQUE INDEX unique_collection_collection_child
ON collection_collection(child_collection_id);

CREATE TRIGGER prevent_collection_collection_cycle_insert
BEFORE INSERT ON collection_collection
BEGIN
  SELECT RAISE(ABORT, 'collection cycle is not allowed')
  WHERE NEW.parent_collection_id = NEW.child_collection_id
     OR EXISTS (
       WITH RECURSIVE descendants(id) AS (
         SELECT child_collection_id
         FROM collection_collection
         WHERE parent_collection_id = NEW.child_collection_id

         UNION

         SELECT cc.child_collection_id
         FROM collection_collection cc
         INNER JOIN descendants d
           ON cc.parent_collection_id = d.id
       )
       SELECT 1
       FROM descendants
       WHERE id = NEW.parent_collection_id
     );
END;

CREATE TRIGGER prevent_collection_collection_cycle_update
BEFORE UPDATE OF parent_collection_id, child_collection_id ON collection_collection
BEGIN
  SELECT RAISE(ABORT, 'collection cycle is not allowed')
  WHERE NEW.parent_collection_id = NEW.child_collection_id
     OR EXISTS (
       WITH RECURSIVE descendants(id) AS (
         SELECT child_collection_id
         FROM collection_collection
         WHERE parent_collection_id = NEW.child_collection_id
           AND NOT (
             parent_collection_id = OLD.parent_collection_id
             AND child_collection_id = OLD.child_collection_id
           )

         UNION

         SELECT cc.child_collection_id
         FROM collection_collection cc
         INNER JOIN descendants d
           ON cc.parent_collection_id = d.id
       )
       SELECT 1
       FROM descendants
       WHERE id = NEW.parent_collection_id
     );
END;
```

## Item Collection

Item と Collection の所属関係。

| column | type | required | unique | note |
|---|---|---:|---|---|
| `item_info_id` | GUID string | Yes | `(item_info_id, collection_info_id)` | 親 Item Info |
| `collection_info_id` | GUID string | Yes | `(item_info_id, collection_info_id)` | 所属 Collection Info |
| `created_at` | DATETIME | Yes |  | 所属作成時刻 |

```sql
CREATE UNIQUE INDEX unique_item_collection
ON item_collection(item_info_id, collection_info_id);
```

## Schema Version

AssetManager DB の schema version。開発段階では `1` 固定で、既存 DB の破棄や再作成は手動で行う。

| column | type | required | unique | note |
|---|---|---:|---|---|
| `version` | INTEGER | Yes | Yes | schema version |
| `created_at` | DATETIME | Yes |  | 作成時刻 |
| `updated_at` | DATETIME | Yes |  | 更新時刻 |

```sql
CHECK (version >= 1)
```

## Sync Info

Datasource 別 sync 状態。

| column | type | required | unique | note |
|---|---|---:|---|---|
| `source_type` | source_type | Yes | Yes | sync 対象 datasource |
| `last_sync_at` | DATETIME |  |  | 最後に sync した時刻。未 sync の場合は NULL |
| `last_sync_status` | sync_status | Yes |  | 最後の sync 結果。`success` / `failed` / `partial` |

```sql
CHECK (source_type IN ('blm', 'eagle', 'ee4v'))
CHECK (last_sync_status IN ('success', 'failed', 'partial'))
```

## Booth Info

Booth 商品単位の identity。

`name` / `description` は Booth 由来 snapshot として固定保持し、表示には使わない。

conflict は確認後上書きで対応。

| column | type | required | unique | note |
|---|---|---:|---|---|
| `id` | GUID string | Yes |  | Booth Info の識別子 |
| `item_info_id` | GUID string | Yes | Yes | 親 Item Info |
| `booth_item_id` | INTEGER | Yes | Yes | Booth item ID。BLM `booth_items.id` / Eagle `boothItemId` |
| `shop_info_id` | GUID string | Yes |  | Shop Info への参照 |
| `name` | TEXT | Yes |  | Booth 商品名 |
| `description` | TEXT | Yes |  | Booth 商品説明。取得元に説明がない場合は空文字を保持 |
| `thumbnail_url` | TEXT |  |  | 商品 thumbnail URL |
| `last_updated_at` | DATETIME |  |  | Booth 正本から最後に更新した時刻 |

## Shop Info

Booth shop 単位の情報。複数 Booth Info から共有する。

| column | type | required | unique | note |
|---|---|---:|---|---|
| `id` | GUID string | Yes |  | Shop Info の識別子 |
| `name` | TEXT | Yes |  | ショップ名 |
| `subdomain` | TEXT | Yes | Yes | Booth shop subdomain |
| `thumbnail_url` | TEXT |  |  | shop thumbnail URL |

```sql
subdomain TEXT NOT NULL UNIQUE
```

## File Info

Item に紐付く論理 file。Item は複数 File Info を持てる。

| column | type | required | unique | note |
|---|---|---:|---|---|
| `id` | GUID string | Yes |  | File Info の識別子 |
| `item_info_id` | GUID string | Yes |  | 親 Item Info |
| `file_name` | TEXT | Yes |  | ファイル名 |
| `extension` | TEXT |  |  | 拡張子 |
| `size_bytes` | INTEGER |  |  | サイズ |
| `download_id` | INTEGER |  | `download_id WHERE download_id IS NOT NULL` | Booth download ID。同一 file 判定に使う。BLM / 手動登録など download ID を持たない file は NULL |
| `lifecycle` | file_lifecycle | Yes |  | file の管理状態 |
| `created_at` | DATETIME | Yes |  | 作成時刻 |
| `updated_at` | DATETIME | Yes |  | 更新時刻 |

```sql
CHECK (lifecycle IN ('active', 'archived'))

CREATE UNIQUE INDEX unique_file_info_download_id
ON file_info(download_id)
WHERE download_id IS NOT NULL;
```

同一 file 判定は `download_id` のみで行う。`download_id` が NULL の File Info は、同じ Item・同じ file name でも自動統合しない。

代表 file origin は `assetManager.sourcePriority` 設定順で存在する origin に fallback する。既定値は `ee4v,eagle,blm`。origin が 0 件の場合は missing として扱う。

## File Dependency

File Info 同士の依存関係。`dependent_file_info_id` が `dependency_file_info_id` に依存する。

| column | type | required | unique | note |
|---|---|---:|---|---|
| `dependent_file_info_id` | GUID string | Yes | `(dependent_file_info_id, dependency_file_info_id, dependency_type)` | 依存している File Info |
| `dependency_file_info_id` | GUID string | Yes | `(dependent_file_info_id, dependency_file_info_id, dependency_type)` | 依存される File Info |
| `dependency_type` | file_dependency_type | Yes | `(dependent_file_info_id, dependency_file_info_id, dependency_type)` | 依存種別 |
| `created_at` | DATETIME | Yes |  | 依存関係の作成時刻 |

```sql
CHECK (dependent_file_info_id != dependency_file_info_id)
CHECK (dependency_type IN ('requires'))

CREATE UNIQUE INDEX unique_file_dependency
ON file_dependency(dependent_file_info_id, dependency_file_info_id, dependency_type);
```

## File Import Target

Unity へ取り込む対象 file / directory entry。`file_info` の実体が directory または zip の場合、配下 entry を `relative_path` で保持する。`relative_path` が空文字の場合は `file_info` 実体そのものを import 対象にする。

| column | type | required | unique | note |
|---|---|---:|---|---|
| `id` | GUID string | Yes |  | File Import Target の識別子 |
| `file_info_id` | GUID string | Yes | `(file_info_id, relative_path)` | 親 File Info |
| `relative_path` | TEXT | Yes | `(file_info_id, relative_path)` | file 実体からの相対 path。zip 内 entry も `/` 区切りで保持する |
| `is_directory` | BOOLEAN | Yes |  | import 対象が directory entry か |
| `created_at` | DATETIME | Yes |  | 作成時刻 |
| `updated_at` | DATETIME | Yes |  | 更新時刻 |

```sql
CHECK (is_directory IN (0, 1))

CREATE UNIQUE INDEX unique_file_import_target_file_path
ON file_import_target(file_info_id, relative_path);
```

## Eagle File Origin

Eagle item として管理される file 実体の origin。Eagle 固有情報は AssetManager DB では正本にせず、実体解決に必要な identity と cache のみ保持する。

| column | type | required | unique | note |
|---|---|---:|---|---|
| `file_info_id` | GUID string | Yes | Yes | 親 File Info |
| `eagle_item_id` | TEXT | Yes | Yes | Eagle item ID |
| `file_path_cache` | TEXT |  |  | 最後に解決できた path。正本にはしない |
| `is_deleted` | BOOLEAN |  |  | 最後に同期した Eagle 上の削除状態 |
| `imported_at` | DATETIME |  |  | datasource から取り込んだ時刻 |

```sql
CHECK (is_deleted IS NULL OR is_deleted IN (0, 1))

CREATE UNIQUE INDEX unique_eagle_file_origin_item_id
ON eagle_file_origin(eagle_item_id);
```

## BLM File Origin

BLM registered item 配下の file / directory entry を表す origin。実体種別は保存せず、解決した path を runtime で判定する。BLM sync では他 datasource origin と自動統合せず、BLM entry ごとに File Info を作る。

| column | type | required | unique | note |
|---|---|---:|---|---|
| `file_info_id` | GUID string | Yes | Yes | 親 File Info |
| `registered_item_id` | TEXT | Yes | `(registered_item_id, relative_path)` | BLM `registered_items.id` |
| `relative_path` | TEXT | Yes | `(registered_item_id, relative_path)` | BLM registered item directory からの相対 path |
| `file_path_cache` | TEXT |  |  | 最後に解決できた path。正本にはしない |
| `imported_at` | DATETIME |  |  | datasource から取り込んだ時刻 |

```sql
CREATE UNIQUE INDEX unique_blm_file_origin_registered_relative_path
ON blm_file_origin(registered_item_id, relative_path);
```

## ee4v File Origin

ee4v が管理する file 実体の origin。

| column | type | required | unique | note |
|---|---|---:|---|---|
| `file_info_id` | GUID string | Yes | Yes | 親 File Info |
| `ee4v_file_id` | GUID string | Yes | Yes | ee4v 管理 file ID |
| `file_path_cache` | TEXT | Yes |  | 最後に解決できた path。正本にはしない |
| `imported_at` | DATETIME |  |  | datasource から取り込んだ時刻 |

```sql
CREATE UNIQUE INDEX unique_ee4v_file_origin_ee4v_file_id
ON ee4v_file_origin(ee4v_file_id);
```

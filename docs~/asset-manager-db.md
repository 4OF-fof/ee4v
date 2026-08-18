# AssetManager DB 設計

この文書は、対話で確定した範囲の論理設計と現行schemaを記載します。

## 全体構造

```text
file       ── 0..1 item
file       ── 0..* file_target
file       ── 0..* file_dependency
item       ── 0..* tag       （item_tag経由）
collection ── 1..* collection_node
```

ItemとFileが管理対象の基本構造です。TagはItemへ付加する分類情報、CollectionはItemを動的に抽出する保存済みの絞り込み条件です。現行SourceはEagleとee4vです。

## 保存形式

- SQLiteを1ファイル使用する
- 内部IDは文字列で保持する
- `source_id`はSource側が定める不透明な安定IDとして保持する
- 時刻はUTCのISO 8601文字列で保持する
- 真偽値は`0`または`1`で保持する
- schema versionは1に固定し、`PRAGMA user_version`が異なる場合はDBを再作成する

## Item

### `item`

| column | 保持する値 | 目的 | 制約 |
|---|---|---|---|
| `id` | Item ID | Itemの識別 | PK |
| `name` | Item名 | 表示と名前の部分一致 | 空白不可 |
| `description` | Item説明 | 表示と説明の部分一致 | NULL不可。未設定は空文字 |
| `source_type` | Itemを自動作成したSource種別 | 手動作成とSource同期による作成を区別 | NULL可。`eagle`または`ee4v` |
| `source_id` | Source内の安定ID | folder rename後も同じItemとして識別 | NULL可 |
| `is_archived` | Itemの論理削除状態 | 通常検索から除外し、復元可能にする | `0`または`1` |
| `created_at` | 作成時刻 | 作成順と履歴 | UTC |
| `updated_at` | 更新時刻 | 更新判定 | UTC |

Itemは手動、Eagle同期、ee4v Sourceへの取り込みのいずれかで作成します。手動作成では`source_type`と`source_id`を両方NULL、Source由来では両方に値を持たせ、`(source_type, source_id)`をUNIQUEとします。Fileを持たなくてもよく、Tagも必須ではありません。

## File

### `file`

| column | 保持する値 | 目的 | 制約 |
|---|---|---|---|
| `id` | File ID | Fileの識別 | PK |
| `item_id` | 所属Item ID | ItemとFileの関連 | NULL可、Item FK |
| `file_name` | ファイル名 | 表示と実体の識別補助 | 空白不可。Collectionの条件には使用しない |
| `extension` | 拡張子 | 拡張子条件の評価 | NULL可 |
| `source_type` | Source種別 | Fileの取得元を識別 | NULL不可。`eagle`または`ee4v` |
| `source_id` | Source内の安定ID | renameやpath変更後も同じFileとして識別 | NULL不可 |
| `source_path` | 現在の実体path | Fileの読み取り | NULL可、変更可能 |
| `is_available` | Source上の存在状態 | 利用できないFileを残して識別 | `0`または`1` |
| `is_archived` | Fileの論理削除状態 | 通常一覧とCollection評価から除外する | `0`または`1` |
| `created_at` | 作成時刻 | 作成順と履歴 | UTC |
| `updated_at` | 更新時刻 | 更新判定 | UTC |

`(source_type, source_id)`をUNIQUEとします。1つのFileは1つのSourceだけを持ちます。同じ実体に見えるFileでもSourceが異なる場合は別のFileとして保存します。`item_id`がNULLのFileは未所属Fileです。Itemを実体削除する場合は、所属Fileも同じ操作で削除します。

## Target

### `file_target`

| column | 保持する値 | 目的 | 制約 |
|---|---|---|---|
| `file_id` | 対象File ID | Targetを所有するFileの識別 | File FK |
| `target_path` | File内の相対Target path | Unityへ取り込む対象の識別 | NULL不可、File内で大文字小文字を区別せず一意 |

`(file_id, target_path)`を複合PKとします。レコードがないFileはTarget未設定です。空文字はFile自身、空文字以外はFileとして保持しているZIP内の要素を表します。区切りは`/`へ統一し、絶対path、空segment、`.`、`..`を許可しません。同じ`target_path`を異なるFileが持つことは可能です。File削除時は所属Targetも削除します。

## Dependency

### `file_dependency`

| column | 保持する値 | 目的 | 制約 |
|---|---|---|---|
| `dependent_file_id` | 依存するFile ID | 後から取り込むFileの識別 | File FK |
| `dependency_file_id` | 依存先File ID | 先に取り込むFileの識別 | File FK |

`(dependent_file_id, dependency_file_id)`を複合PKとします。1つのFileは0個以上のFileへ依存できます。自己依存、重複、直接または間接的な循環を禁止します。所属ItemやSourceが異なるFile間の依存も許可します。File削除時は、そのFileが依存元または依存先である関係を削除します。アーカイブでは依存関係を維持します。

## Tag

### `tag`

| column | 保持する値 | 目的 | 制約 |
|---|---|---|---|
| `id` | Tag ID | Tagの識別 | PK |
| `path` | `foo/bar`形式の完全path | 階層表現と絞り込み | 小文字、UNIQUE、空白不可。DBでも小文字を制約 |

`#`は保存せず、path全体を小文字へ変換してから保存します。先頭と末尾の`/`、連続する`/`による空segmentは許可しません。親Tagの行は必須ではなく、`foo`と完全一致するpathまたは`foo/`で始まるpathを子孫を含む範囲として扱います。

### `item_tag`

| column | 保持する値 | 目的 | 制約 |
|---|---|---|---|
| `item_id` | Item ID | Tagを付与するItem | Item FK |
| `tag_id` | Tag ID | 付与するTag | Tag FK |

`(item_id, tag_id)`を複合PKとします。Tagの付与対象はItemだけです。どのItemからも参照されなくなったTagは、関連の更新と同じtransactionで削除します。

## Collection

### `collection`

| column | 保持する値 | 目的 | 制約 |
|---|---|---|---|
| `id` | Collection ID | Collectionの識別 | PK |
| `name` | Collection名 | 保存済み絞り込みの表示 | UNIQUE、空白不可 |
| `created_at` | 作成時刻 | 作成順と履歴 | UTC |
| `updated_at` | 更新時刻 | 更新判定 | UTC |

Collectionは親子関係を持たず、フラットに管理します。Itemとの所属関係は保持しません。

### `collection_node`

| column | 保持する値 | 目的 | 制約 |
|---|---|---|---|
| `id` | Node ID | 条件Nodeの識別 | PK |
| `collection_id` | Collection ID | 所属する論理式 | Collection FK |
| `parent_node_id` | 親Node ID | 論理式の木構造 | ルートだけNULL、同じCollection内のNode FK |
| `node_type` | `and`、`or`、`not`、`condition` | Nodeの評価方法 | 列挙値以外は禁止 |
| `condition_type` | `name_contains`、`description_contains`、`has_tag`、`has_file_extension` | 個別条件の種類 | `condition`だけ値を持つ |
| `value` | 検索文字列、Tag path、拡張子 | 個別条件の引数 | `condition`では空白不可。それ以外はNULL |
| `sort_order` | 兄弟Node内の順序 | 編集時の表示順 | 0以上、兄弟内でUNIQUE |

Collectionごとに`parent_node_id IS NULL`のルートNodeを1つだけ許可します。親子の循環を禁止し、ANDとORは複数の子、NOTは1つの子、Conditionは子を持たない構造とします。子数とルートの存在は保存前に検証し、式木全体を1 transactionで置換します。

条件の意味は次のとおりです。

| condition | 一致するItem |
|---|---|
| `name_contains` | Item名が値を部分一致で含む |
| `description_contains` | Item説明が値を部分一致で含む |
| `has_tag` | 指定Tagまたはその子Tagを持つ |
| `has_file_extension` | 指定拡張子の利用可能かつ未アーカイブのFileを1つ以上持つ |

AND、OR、NOTは再帰的に組み合わせられます。一時的な検索にも同じ論理式を使用しますが、一時的な検索条件はDBへ保存しません。

## DB制約

- `PRAGMA foreign_keys = ON`で、すべての外部キーを有効にする
- Item、File、Tag、CollectionのIDはDB内で一意にする
- Collection Nodeは同じCollectionのNodeだけを親にできる
- Collection Nodeの循環を拒否し、ルートNodeを部分UNIQUE indexで1つに制限する
- FileはSourceを1つだけ持ち、Sourceをまたいで統合しない
- ItemのSource列は両方NULLまたは両方非NULLにし、Source同期で作成したItemをSource内の安定IDで一意にする
- ItemとFileのアーカイブ状態はSource同期で変更しない
- Fileの実体削除はee4v Sourceだけに許可する
- File Targetは0個以上とし、同じFile内の重複を禁止する
- File Dependencyは循環しない有向グラフとする

## Index

PKとUNIQUE制約に加え、`file(item_id)`、`file(extension, item_id)`、`item_tag(tag_id, item_id)`、`collection_node(collection_id, parent_node_id, sort_order)`へ索引を作成します。`file_target`と依存先一覧は複合PKを検索に使用し、`file_dependency(dependency_file_id, dependent_file_id)`で依存元を逆引きします。

## 現行のEagle同期

- `VRCAsset`を既定の対象rootとし、その配下の各folderをItemとして同期する
- Eagle folder IDをItemの`source_id`、Eagle item IDをFileの`source_id`として使用する
- folder名やfile pathが変わってもSource IDが同じなら既存行を更新する
- Source上で確認できなくなったFileは削除せず、`is_available = 0`にする
- Booth metadata JSONはFileにせず、見つかった名前と説明をItemへ反映する
- 同じEagle itemが複数folderに属する場合も、1 Fileが複数Itemに属さないよう最初のItemだけへ関連付ける

## 現行のee4v Source

```text
<library>/
└─ Assets/
   └─ <source-id>/
      ├─ metadata.json
      └─ <file-name>
```

- 1つのSource directoryでJSONとFileを1つずつ保持する
- JSONの`kind`はItemとFileの組、またはFile単独のどちらかを示す
- ItemとFileの組では同じ安定した`source-id`を使用する
- JSONにはschema version、種別、Source ID、File名を保持する
- ItemとFileの組ではItem名、説明、Tag pathも保持する
- 取り込み時は一時directoryへJSONとFileを書き、完成後にSource directoryへ移動する
- `RegisterFile`は指定Itemへの所属または未所属としてFile単独のSourceを登録する
- Item名、説明、TagのAPI更新をJSONへ反映する
- DB再作成後も`SyncEe4v`で内容を復元し、File単独のSourceは未所属Fileとする
- File削除時はSource directoryを削除し、Item削除時は所属するee4v Source directoryをすべて削除する
- 不正なJSONがある場合は同期全体を失敗させ、DB上の利用状態を変更しない

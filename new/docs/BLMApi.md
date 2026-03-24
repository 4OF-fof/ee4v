# BLM API

`Editor/AssetManager/BoothLibraryManager` には、Booth Library Manager の `data.db` を読み取るための API を配置する。

## 公開 API

### `BoothLibraryManagerApi.GetDefaultDatabasePath()`

- 戻り値: `string`
- 既定の `data.db` パスを返す
- 既定値は `%AppData%/pm.booth.library-manager/data.db`

### `BoothLibraryManagerApi.DatabaseExists(string databasePath = null)`

- 戻り値: `bool`
- 対象 `data.db` が存在するかを返す

### `BoothLibraryManagerApi.GetItemById(long boothItemId, string databasePath = null)`

- 戻り値: `BoothLibraryManagerItemRecord` または `null`
- `booth_items.id` をキーに商品情報を取得する
- 上書きテーブル `overwritten_booth_items` があれば `name` と `description` はその値を優先する
- タグは `overwritten_booth_item_tags` が存在すればその一覧を優先し、なければ `booth_item_tag_relations` を使う

### `BoothLibraryManagerApi.TryGetItemById(long boothItemId, out BoothLibraryManagerItemRecord item, string databasePath = null)`

- 戻り値: `bool`
- 取得できた場合は `true` と `item` を返す
- 商品が見つからない、または DB が存在しない場合は `false`

## 返却モデル

### `BoothLibraryManagerItemRecord`

- `BoothItemId`
- `Name`
- `ItemUrl`
- `Description`
- `ThumbnailUrl`
- `ShopName`
- `ShopUrl`
- `ShopThumbnailUrl`
- `Tags`

## 実装メモ

- DB は直接開かず、一時ディレクトリへ `data.db` と `-wal` / `-shm` をコピーしたスナップショットを読む
- SQLite provider 初期化は `Ee4v.SQLite.SqliteBootstrap` を使う
- パス解決は内部ヘルパーで行い、公開 API には出さない
- 確認用 Window は削除前提の暫定実装であり、`BoothLibraryManagerItemLookupWindow.cs` 単一ファイルに閉じる

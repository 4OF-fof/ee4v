# AssetManager API

AssetManager API は [DB Schema](../asset-manager/schema.md) を直接触らずに、Item、File、Tag、Collection、Datasource origin を扱うための公開契約です。

この API は AssetManager DB を正本として扱います。BLM / Eagle / ee4v origin は file 実体の参照元であり、UI 表示用の item 情報は `item_info` を優先します。

契約は `Ee4v.AssetManager.Contracts.Editor` の `IAssetManager` と DTO に定義します。instance は
`AssetManager` Module の composition root が生成し、UI や他の利用側へ constructor / factory
経由で渡します。static API や global event から取得しません。

## Data Contracts

### `AssetItem`

AssetManager 上の表示単位です。`item_info` を中心に、Booth snapshot、tag、file summary をまとめた読み取り model として扱います。

```csharp
public sealed class AssetItem
{
    public string Id { get; set; }
    public string Name { get; set; }
    public string Description { get; set; }
    public bool IsAvailable { get; set; }
    public BoothSnapshot Booth { get; set; }
    public IReadOnlyList<AssetTag> Tags { get; set; }
    public IReadOnlyList<AssetFileSummary> Files { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
```

Notes:

- `Name` と `Description` は `item_info` のユーザー上書き可能な値を返す。
- Booth 商品由来の固定 snapshot は `Booth` に分離する。
- `Files` は一覧表示用 summary であり、origin の詳細解決は `IAssetManager.GetFiles(...)` を使う。
- 通常 query は unavailable item を返さない。履歴・診断用途では `AssetItemQuery.IncludeUnavailable` を指定する。

### `AssetFile`

Item に紐付く論理 file です。実体 path は origin から解決します。

```csharp
public sealed class AssetFile
{
    public string Id { get; set; }
    public string ItemId { get; set; }
    public string FileName { get; set; }
    public string Extension { get; set; }
    public long? SizeBytes { get; set; }
    public long? DownloadId { get; set; }
    public AssetFileLifecycle Lifecycle { get; set; }
    public bool IsAvailable { get; set; }
    public IReadOnlyList<AssetFileOrigin> Origins { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
```

Notes:

- 代表 origin は `assetManager.sourcePriority` 設定順で解決する。既定値は `ee4v,eagle,blm`。
- `DownloadId` は Booth download ID。NULL でない場合だけ、datasource sync 時の同一 file 判定に使う。
- `Lifecycle` は `Active` または `Archived`。
- file を削除する API は物理 file を消さず、原則 `Archived` へ遷移させる。
- `IsAvailable` は active な origin が存在するかを表し、`Lifecycle` とは独立している。

### `AssetFileSummary`

Item 詳細に含める file 一覧用の summary です。origin 詳細や更新時刻は含めず、一覧表示に必要な file 情報だけを返します。

```csharp
public sealed class AssetFileSummary
{
    public string Id { get; set; }
    public string FileName { get; set; }
    public string Extension { get; set; }
    public long? SizeBytes { get; set; }
    public long? DownloadId { get; set; }
    public AssetFileLifecycle Lifecycle { get; set; }
    public bool IsAvailable { get; set; }
}
```

Notes:

- `DownloadId` の意味は `AssetFile.DownloadId` と同じ。
- origin 詳細が必要な場合は `IAssetManager.GetFiles(...)` を使う。

### `AssetCollection`

通常 Collection と Smart Collection を同じ tree node として扱う model です。

```csharp
public sealed class AssetCollection
{
    public string Id { get; set; }
    public string Name { get; set; }
    public AssetCollectionIcon Icon { get; set; }
    public string IconAssetGuid { get; set; }
    public bool IsSmartCollection { get; set; }
    public string ParentCollectionId { get; set; }
    public int SortOrder { get; set; }
    public SmartCollectionRule SmartRule { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
```

Notes:

- `ParentCollectionId` が `null` の場合は root collection。
- `SortOrder` は同じ親を持つ兄弟 Collection 内の0始まり表示順。
- 通常 Collection の `Icon` は `Folder`、`IconAssetGuid` は `null` に固定する。
- Smart Collection の `IconAssetGuid` が有効な Texture asset を指す場合は任意アイコンとして `Icon` より優先する。asset が見つからない場合は `Icon` へフォールバックする。
- 子 Collection は最大 1 つの親だけを持つ。
- Smart Collection は子 Collection にはなれるが、Collection 系の子を持つ親にはなれない。
- Smart Collection の rule は保存して返す。`SearchItems(...)` で Smart Collection を指定すると条件を評価して item を抽出する。

### その他の model

```csharp
public sealed class BoothSnapshot
{
    public string Id { get; set; }
    public long BoothItemId { get; set; }
    public string ItemUrl { get; set; }
    public string Name { get; set; }
    public string Description { get; set; }
    public string ThumbnailUrl { get; set; }
    public string ShopName { get; set; }
    public string ShopUrl { get; set; }
    public string ShopThumbnailUrl { get; set; }
    public DateTime? LastUpdatedAt { get; set; }
    public IReadOnlyList<string> DatasourceTags { get; set; }
}

public sealed class AssetFileOrigin
{
    public AssetSourceType SourceType { get; set; }
    public string SourceId { get; set; }
    public string FilePathCache { get; set; }
    public DateTime? ImportedAt { get; set; }
    public bool IsAvailable { get; set; }
}

public sealed class AssetFilePathResolution
{
    public bool Found { get; set; }
    public string Path { get; set; }
    public AssetSourceType? SourceType { get; set; }
    public string MissingReason { get; set; }
}

public sealed class AssetFileImportTarget
{
    public string Id { get; set; }
    public string FileId { get; set; }
    public string RelativePath { get; set; }
}

public sealed class AssetTag
{
    public string Id { get; set; }
    public string Name { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public sealed class SmartCollectionRule
{
    public SmartCollectionMatchMode MatchMode { get; set; }
    public IReadOnlyList<SmartCollectionCondition> Conditions { get; set; }
}

public sealed class SmartCollectionCondition
{
    public string Id { get; set; }
    public SmartCollectionConditionField Field { get; set; }
    public SmartCollectionConditionOperator Operator { get; set; }
    public string QueryText { get; set; }
}

public sealed class AssetFileDependency
{
    public string DependentFileId { get; set; }
    public string DependencyFileId { get; set; }
}

public sealed class AssetSyncResult
{
    public AssetSyncResult(int createdCount, int updatedCount, int unchangedCount, int errorCount)
    {
        CreatedCount = createdCount;
        UpdatedCount = updatedCount;
        UnchangedCount = unchangedCount;
        ErrorCount = errorCount;
    }

    public int CreatedCount { get; private set; }
    public int UpdatedCount { get; private set; }
    public int UnchangedCount { get; private set; }
    public int ErrorCount { get; private set; }
    public AssetSyncState State { get; private set; }
}

public sealed class AssetSyncInfo
{
    public AssetSourceType SourceType { get; set; }
    public DateTime? LastSyncAt { get; set; }
    public AssetSyncState LastSyncState { get; set; }
}

public enum AssetSyncState
{
    Success,
    Failed,
    Partial
}

public enum AssetSyncStatus
{
    Created,
    Updated,
    Unchanged,
    Error
}

public enum AssetSourceType
{
    Blm,
    Eagle,
    Ee4v
}

public enum AssetFileLifecycle
{
    Active,
    Archived
}

public enum SmartCollectionMatchMode
{
    All,
    Any
}

public enum AssetCollectionIcon
{
    Folder,
    Star,
    Package,
    Tag,
    Search
}

public enum SmartCollectionConditionField
{
    Name,
    Description,
    Tag,
    FileName,
    Extension
}

public enum SmartCollectionConditionOperator
{
    Contains,
    Equals,
    In,
    Exists
}

public enum AssetManagerErrorCode
{
    Unknown,
    NotFound,
    Duplicate,
    InvalidRequest,
    CollectionCycle,
    InvalidCollectionHierarchy,
    InvalidSmartCollectionCondition,
    DatabaseError,
    DatasourceError
}
```

### Query / Request contracts

```csharp
public sealed class AssetItemQuery
{
    public string Keyword { get; set; }
    public string CollectionId { get; set; }
    public IReadOnlyList<string> TagIds { get; set; }
    public IReadOnlyList<AssetSourceType> SourceTypes { get; set; }
    public AssetFileLifecycle? Lifecycle { get; set; }
    public bool HasBoothInformation { get; set; }
    public bool UncategorizedOnly { get; set; }
    public bool IncludeUnavailable { get; set; }
    public int Offset { get; set; }
    public int Limit { get; set; }
}

public sealed class AssetSearchResult
{
    public IReadOnlyList<AssetItem> Items { get; set; }
    public int TotalCount { get; set; }
}

public sealed class CreateAssetItemRequest
{
    public string Name { get; set; }
    public string Description { get; set; }
    public IReadOnlyList<string> TagIds { get; set; }
    public IReadOnlyList<string> CollectionIds { get; set; }
}

public sealed class UpdateAssetItemRequest
{
    public string Name { get; set; }
    public string Description { get; set; }
}

public sealed class AssetFileQuery
{
    public AssetSourceType? SourceType { get; set; }
    public AssetFileLifecycle? Lifecycle { get; set; }
    public string Extension { get; set; }
    public bool IncludeUnavailable { get; set; }
}

public sealed class RegisterFileRequest
{
    public string FileName { get; set; }
    public string FilePath { get; set; }
    public long? SizeBytes { get; set; }
}

public sealed class AssetFileImportTargetRequest
{
    public string RelativePath { get; set; }
}

public sealed class CreateCollectionRequest
{
    public string Name { get; set; }
    public string ParentCollectionId { get; set; }
}

public sealed class CreateSmartCollectionRequest
{
    public string Name { get; set; }
    public AssetCollectionIcon Icon { get; set; }
    public string IconAssetGuid { get; set; }
    public string ParentCollectionId { get; set; }
    public SmartCollectionMatchMode MatchMode { get; set; }
    public IReadOnlyList<SmartCollectionCondition> Conditions { get; set; }
}

public sealed class UpdateSmartCollectionRequest
{
    public SmartCollectionMatchMode MatchMode { get; set; }
    public IReadOnlyList<SmartCollectionCondition> Conditions { get; set; }
}

public sealed class BlmSyncRequest
{
    public BlmSyncRequest(string databasePath = null, string itemDirectoryPath = null)
    {
        DatabasePath = databasePath;
        ItemDirectoryPath = itemDirectoryPath;
    }

    public string DatabasePath { get; private set; }
    public string ItemDirectoryPath { get; private set; }
}

public sealed class EagleSyncRequest
{
    public EagleSyncRequest(string libraryPath = null, string targetRoot = null)
    {
        LibraryPath = libraryPath;
        TargetRoot = targetRoot;
    }

    public string LibraryPath { get; private set; }
    public string TargetRoot { get; private set; }
}
```

Notes:

- `BlmSyncRequest` と `EagleSyncRequest` は `Ee4v.AssetManager.Contracts` namespace に属する。connector 実装は公開 API ではなく、`IAssetManager.SyncBlm(...)` / `IAssetManager.SyncEagle(...)` からのみ呼び出す。

## Item

### `IAssetManager.SearchItems`

Item 一覧を検索します。

```csharp
public AssetSearchResult SearchItems(AssetItemQuery query)
```

Parameters:

- `query`: keyword、tag、通常 collection、source type、Booth 情報の有無、未分類、file lifecycle、paging を含む検索条件。

Returns:

- 条件に一致する `AssetItem` の page。
- `TotalCount` は paging 前の件数。

Effects:

- DB を読み取る。
- `SourceTypes` は file origin の存在で絞り込む。代表 origin だけでなく、指定 datasource origin を持つ file があれば一致する。
- `HasBoothInformation` は datasource の種類ではなく、`booth_info` snapshot の存在で絞り込む。
- `UncategorizedOnly` は通常 Collection に直接所属せず、どの Smart Collection の条件にも一致しない Item だけに絞り込む。

Notes:

- keyword は `item_info.name`、`item_info.description`、`tag_info.name`、`file_info.file_name` を対象にする。
- `CollectionId` に通常 Collection を指定した場合は `item_collection` で絞り込む。Smart Collection を指定した場合は `smart_collection_condition` を評価して絞り込む。

```csharp
var result = assetManager.SearchItems(new AssetItemQuery
{
    Keyword = "avatar",
    SourceTypes = new[] { AssetSourceType.Blm, AssetSourceType.Eagle },
    Lifecycle = AssetFileLifecycle.Active,
    Offset = 0,
    Limit = 50
});
```

### `IAssetManager.GetItem`

Item 詳細を取得します。

```csharp
public AssetItem GetItem(string itemId)
```

Parameters:

- `itemId`: `item_info.id`。

Returns:

- 対象 Item。
- 見つからない場合は `null`。

Effects:

- DB を読み取る。

Notes:

- Booth snapshot、tag、file summary をまとめて返す。
- file origin の解決済み path が必要な場合は `GetFiles(...)` / `ResolveFilePath(...)` を使う。

### `IAssetManager.CreateItem`

手動管理 Item を作成します。

```csharp
public AssetItem CreateItem(CreateAssetItemRequest request)
```

Parameters:

- `request.Name`: 表示名。
- `request.Description`: 表示用説明。空文字を許容する。
- `request.TagIds`: 初期 tag。
- `request.CollectionIds`: 初期所属 Collection。

Returns:

- 作成された Item。

Effects:

- `item_info` を追加する。
- 指定があれば `item_tag` と `item_collection` を追加する。
- `created_at` と `updated_at` を設定する。

Notes:

- Booth / Eagle / BLM 由来 item の作成は datasource sync API から行う。
- 同一 name の Item は許容する。

### `IAssetManager.UpdateItem`

Item の表示情報を更新します。

```csharp
public AssetItem UpdateItem(string itemId, UpdateAssetItemRequest request)
```

Parameters:

- `itemId`: 更新対象 Item。
- `request.Name`: 更新後の表示名。
- `request.Description`: 更新後の表示用説明。

Returns:

- 更新後の Item。

Effects:

- `item_info.name`、`item_info.description`、`updated_at` を更新する。

Notes:

- Booth snapshot は更新しない。
- datasource sync による Booth 情報更新があっても、ユーザー上書き済みの `item_info` は維持する。

## File

### `IAssetManager.GetFiles`

Item に紐付く file 一覧を取得します。

```csharp
public IReadOnlyList<AssetFile> GetFiles(string itemId, AssetFileQuery query = null)
```

Parameters:

- `itemId`: 親 Item。
- `query`: lifecycle、source type、extension の絞り込み。

Returns:

- 条件に一致する file 一覧。

Effects:

- DB を読み取る。

Notes:

- file origin の代表 datasource は `assetManager.sourcePriority` 設定順で解決する。

### `IAssetManager.RegisterFile`

Editor 操作で選択された file を Item に追加します。

```csharp
public AssetFile RegisterFile(string itemId, RegisterFileRequest request)
```

Parameters:

- `itemId`: 親 Item。
- `request.FileName`: 表示 file 名。
- `request.FilePath`: 追加する file の path。
- `request.SizeBytes`: file size。

Returns:

- 作成された `AssetFile`。

Effects:

- `file_info` を追加する。
- `ee4v_file_origin` を追加する。

Notes:

- Editor からの手動追加用 API として扱い、origin は常に `ee4v`。
- BLM / Eagle 由来 file はこの API ではなく `SyncBlm(...)` / `SyncEagle(...)` から作成する。
- `file_path_cache` は最後に解決できた path として保存する。

### `IAssetManager.ArchiveFile`

file を archived にします。

```csharp
public void ArchiveFile(string fileId)
```

Parameters:

- `fileId`: 対象 File。

Returns:

- `void`

Effects:

- `file_info.lifecycle` を `Archived` に更新する。
- `updated_at` を更新する。

Notes:

- 物理 file は削除しない。
- dependency は残す。表示側で archived file を含めるかを選ぶ。

### `IAssetManager.ResolveFilePath`

file の代表 origin から実体 path を解決します。

```csharp
public AssetFilePathResolution ResolveFilePath(string fileId)
```

Parameters:

- `fileId`: 対象 File。

Returns:

- 解決できた file / folder path、使用した source type、missing 理由。

Effects:

- DB を読み取る。
- available origin の `file_path_cache` を読み取り、file または folder の存在確認をして返す。

Notes:

- origin は `assetManager.sourcePriority` 設定順で確認する。既定値は `ee4v,eagle,blm`。
- origin が 0 件の場合は missing として返す。

## Tag

### `IAssetManager.GetTags`

AssetManager 独自 tag 一覧を取得します。

```csharp
public IReadOnlyList<AssetTag> GetTags(string keyword = null)
```

Parameters:

- `keyword`: tag 名の部分一致条件。不要なら `null`。

Returns:

- tag 名順の `AssetTag` 一覧。

Effects:

- DB を読み取る。

### `IAssetManager.CreateTag`

AssetManager 独自 tag を作成します。

```csharp
public AssetTag CreateTag(string name)
```

Parameters:

- `name`: tag 名。

Returns:

- 作成された tag。

Effects:

- `tag_info` を追加する。

Notes:

- tag 名は unique。
- 既存 tag 名と一致する場合は既存 tag を返す。

### `IAssetManager.SetItemTags`

Item に付与する tag を置き換えます。

```csharp
public void SetItemTags(string itemId, IReadOnlyList<string> tagIds)
```

Parameters:

- `itemId`: 対象 Item。
- `tagIds`: 付与後の tag ID 一覧。

Returns:

- `void`

Effects:

- `item_tag` を指定一覧に同期する。

Notes:

- Booth tag / Eagle tag は datasource snapshot であり、この API では変更しない。

## Collection

### `IAssetManager.GetCollections`

Collection tree を取得します。

```csharp
public IReadOnlyList<AssetCollection> GetCollections()
```

Parameters:

- なし

Returns:

- root collection と子 collection を含む一覧。

Effects:

- DB を読み取る。

Notes:

- tree 表示側は `ParentCollectionId` で階層化し、兄弟を `SortOrder` で並べる。
- cycle は DB constraint で禁止する。

### `IAssetManager.CreateCollection`

通常 Collection を作成します。

```csharp
public AssetCollection CreateCollection(CreateCollectionRequest request)
```

Parameters:

- `request.Name`: collection 名。
- `request.ParentCollectionId`: 親 Collection。root に置く場合は `null`。

Returns:

- 作成された Collection。

Effects:

- `collection_info` を追加する。
- `icon` は `folder`、`icon_asset_guid` は `null` で保存する。
- 親が指定されていれば `collection_collection` を追加する。
- 指定した親の兄弟一覧の末尾へ追加する。

Notes:

- `request.ParentCollectionId` に Smart Collection は指定できない。

### `IAssetManager.CreateSmartCollection`

Smart Collection を作成します。

```csharp
public AssetCollection CreateSmartCollection(CreateSmartCollectionRequest request)
```

Parameters:

- `request.Name`: collection 名。
- `request.Icon`: Smart Collection の組み込み表示アイコン。
- `request.IconAssetGuid`: 任意 Texture asset の GUID。
- `request.ParentCollectionId`: 親 Collection。
- `request.MatchMode`: `All` または `Any`。
- `request.Conditions`: 評価条件。

Returns:

- 作成された Smart Collection。

Effects:

- `collection_info` を追加する。
- `smart_collection_info` を追加する。
- `smart_collection_condition` を追加する。
- 親が指定されていれば `collection_collection` を追加する。
- 指定した親の兄弟一覧の末尾へ追加する。

Notes:

- `exists` operator 以外では `query_text` が必須。
- `request.ParentCollectionId` に Smart Collection は指定できない。
- Smart Collection の item 所属は `item_collection` に保存しない。`SearchItems(...)` で Smart Collection を指定した場合に条件から item を抽出する。

### `IAssetManager.UpdateCollection`

Collection の表示情報を変更します。

```csharp
public AssetCollection UpdateCollection(
    string collectionId,
    UpdateCollectionRequest request)
```

通常 Collection と Smart Collection の両方を対象にし、更新後の
`AssetCollection` を返します。`request.Name` は必須です。
Smart Collection では `Icon` と `IconAssetGuid` も更新します。
通常 Collection のアイコンは `Folder` に固定し、`IconAssetGuid` は保存しません。

### `IAssetManager.UpdateSmartCollection`

Smart Collection の match mode と条件一覧を置き換えます。

```csharp
public AssetCollection UpdateSmartCollection(
    string collectionId,
    UpdateSmartCollectionRequest request)
```

`request.Conditions` は1件以上必要で、`exists` 以外の operator では
`QueryText` が必須です。通常 Collection を指定した場合は `InvalidRequest` です。

### `IAssetManager.DeleteCollection`

Collection を削除します。

```csharp
public void DeleteCollection(string collectionId)
```

指定した Collection と配下の子孫 Collection を再帰的に削除します。
削除対象 Collection の item 所属と Smart Collection 定義は cascade で削除しますが、
`item_info` は削除しません。残った兄弟の `SortOrder` は正規化します。

### `IAssetManager.MoveCollection`

Collection の親または兄弟順を変更します。

```csharp
public void MoveCollection(
    string collectionId,
    string parentCollectionId,
    int siblingIndex = -1)
```

Parameters:

- `collectionId`: 移動する Collection。
- `parentCollectionId`: 移動先の親 Collection。root に移動する場合は `null`。
- `siblingIndex`: 移動先の兄弟一覧に挿入する0始まりindex。負数の場合は末尾。

Returns:

- `void`

Effects:

- `collection_collection` と `collection_info.sort_order` を更新する。

Notes:

- 自分自身や子孫 Collection の下へ移動する操作は例外。
- 子 Collection は最大 1 つの親だけを持つ。
- Smart Collection は通常 Collection の子にできるが、Smart Collection 自身は Collection 系の子を持てない。
- Smart Collection を `parentCollectionId` に指定すると `InvalidCollectionHierarchy`。
- 移動元と移動先の兄弟は `SortOrder` が連続するように正規化する。

### `IAssetManager.MoveCollections`

複数 Collection を選択順を保ったブロックとして移動します。

```csharp
public void MoveCollections(
    IReadOnlyList<string> collectionIds,
    string parentCollectionId,
    int siblingIndex = -1)
```

親とその子が同時に指定された場合、親の移動に含まれる子は個別の移動対象から除外します。
cycle 制約と兄弟順の正規化は `MoveCollection(...)` と同じです。

### `IAssetManager.SetItemCollections`

Item の通常 Collection 所属を置き換えます。

```csharp
public void SetItemCollections(string itemId, IReadOnlyList<string> collectionIds)
```

Parameters:

- `itemId`: 対象 Item。
- `collectionIds`: 所属させる通常 Collection ID 一覧。

Returns:

- `void`

Effects:

- `item_collection` を指定一覧に同期する。

Notes:

- Smart Collection は指定できない。

### `IAssetManager.AddItemsToCollection`

複数 Item を通常 Collection へ追加します。

```csharp
public void AddItemsToCollection(
    IReadOnlyList<string> itemIds,
    string collectionId)
```

`SetItemCollections(...)` と異なり、各 Item の既存 Collection 所属は保持します。
全 Item と対象 Collection を検証してから同一 transaction で追加し、重複する指定または
すでに存在する所属は無視します。Smart Collection は指定できません。新しい所属を追加した
場合だけ、`RelatedId` に対象 Collection ID を持つ `ItemCollections` change を通知します。

## Dependency

### `IAssetManager.GetFileDependencies`

file の依存関係を取得します。

```csharp
public IReadOnlyList<AssetFileDependency> GetFileDependencies(string fileId)
```

Parameters:

- `fileId`: 対象 File。

Returns:

- `fileId` が依存している file 一覧。

Effects:

- DB を読み取る。

### `IAssetManager.SetFileDependencies`

file の依存関係を置き換えます。

```csharp
public void SetFileDependencies(
    string dependentFileId,
    IReadOnlyList<string> dependencyFileIds)
```

Parameters:

- `dependentFileId`: 依存している File。
- `dependencyFileIds`: 依存先 File 一覧。

Returns:

- `void`

Effects:

- `dependency` の file-to-file 関係だけを同期する。file-to-version 関係は保持する。

Notes:

- 自己依存は例外。
- 置換処理は transaction 内で実行され、検証失敗時は既存関係を維持する。

## Import Target

### `IAssetManager.GetFileImportTargets`

file に紐付く Unity import 対象を取得します。

```csharp
public IReadOnlyList<AssetFileImportTarget> GetFileImportTargets(string fileId)
```

Parameters:

- `fileId`: 対象 File。

Returns:

- `file_info` 実体または配下 entry の import 対象一覧。

Effects:

- DB を読み取る。

Notes:

- `RelativePath` は `ResolveFilePath(fileId)` で解決される実体 path からの相対 path。
- `RelativePath` は空文字にならず、file root 自体は import 対象にしない。
- zip 内 entry も `/` 区切りの relative path として返す。
- ZIP 全体が ZIP file と同名の単一 root folder 配下にある場合、その root folder は `RelativePath` から省略する。root に兄弟 entry がある場合は省略しない。

### `IAssetManager.SetFileImportTargets`

file に紐付く Unity import 対象を置き換えます。

```csharp
public void SetFileImportTargets(
    string fileId,
    IReadOnlyList<AssetFileImportTargetRequest> targets)
```

Parameters:

- `fileId`: 対象 File。
- `targets`: import 対象の relative path 一覧。

Returns:

- `void`

Effects:

- Application use case が Domain policy で全 path を検証・正規化した後、Infrastructure の
  transactional store が `file_import_target` を指定一覧へ一括置換する。
- `FileImportTargets` と `FileTree` の change を順に通知する。`Catalog` change は通知しない。
- 永続化に失敗した場合は読み戻しと change 通知を行わない。

Notes:

- `RelativePath` は先頭 `/` と末尾 `/` を取り除き、`\` を `/` に正規化する。
- 空の `RelativePath` は file root を指すため拒否する。
- `..` を含む path は拒否する。
- 同一 file 内の重複 `RelativePath` は 1 件にまとめる。
- 検証に失敗した場合は store を呼ばないため、既存の Import Target は変更しない。
- 標準 File Tree UI は `FileImportTargets` change の変更内容を memory cache と表示中の行へ反映し、filesystem / ZIP を再走査しない。

### `IAssetManager.ImportFileTargets`

file に設定済みの Import Target を Unity project へ取り込みます。

```csharp
public void ImportFileTargets(string itemId, string fileId)
```

- File Tree の file root と Version Group から実行する。Version Group は代表 file の target を使う。
- `.unitypackage` は Unity の package import へ渡す。user setting `assetManager.showUnityPackageImportDialog` は既定で `true` とし、`false` の場合は内容選択画面を表示せず package 全体を直接 import する。
- それ以外は `Assets/<asset name>/<file name>/` 配下へ target の相対 path を維持して copy し、最後に AssetDatabase を refresh する。
- Import Target がない file root では標準 File Tree の context menu に表示しない。

### `IAssetManager.ImportFileEntry`

file root 配下の実 file 1 件を Unity project へ取り込みます。

```csharp
public void ImportFileEntry(string itemId, string fileId, string relativePath)
```

- Import Target への登録有無にかかわらず、標準 File Tree の実 file context menu から実行できる。
- directory 行には表示しない。
- ZIP entry は必要な entry だけを読み出す。File Tree で同名 root folder を省略した ZIP は、import 時に実 entry path へ戻して解決し、destination には省略した folder を作らない。path traversal を含む relative path は拒否する。
- `.unitypackage` とそれ以外の取り扱いは `ImportFileTargets` と同じ。

## Change Notifications

```csharp
public event Action<AssetManagerChange> Changed;
```

`AssetManagerChange.Kind` は次を取ります。

| kind | `SubjectId` | `RelatedId` / `ImportTargets` | 用途 |
|---|---|---|---|
| `Catalog` | 空 | 空 | item 一覧や file tree の構造・内容を再取得する |
| `ItemCollections` | 空 | `RelatedId` に追加先 Collection ID | 追加先 Collection と Uncategorized の item 一覧だけを再取得する |
| `Collections` | 空 | 空 | Collection 一覧・親子関係・兄弟順だけを再取得する |
| `SmartCollectionRule` | Smart Collection ID | 空 | 対象 Smart Collection と Uncategorized の検索結果を再取得する |
| `FileTree` | 空 | 空 | File Tree に関係する変更を知らせる |
| `FileImportTargets` | file ID | 保存後の `ImportTargets` | cache 上の target state だけを更新する |
| `VersionGroupPrimaryFile` | Version Group ID | `RelatedId` に代表 file ID | cache 上の代表 state だけを更新する |

Collection の作成・更新・削除・移動、`SetFileImportTargets(...)`、`SetVersionGroupPrimaryFile(...)` は
`Catalog` change を発行しません。
Smart Collection の作成・条件更新・削除は `Collections` に加えて
`SmartCollectionRule` を発行します。
標準 UI は詳細 change から既存の File Tree 行 state だけを更新し、Main View の再検索や File Tree の
再構築を行いません。

## Datasource Sync

### `IAssetManager.SyncBlm`

BLM `data.db` から AssetManager DB へ snapshot を取り込みます。

```csharp
public AssetSyncResult SyncBlm(BlmSyncRequest request)
```

Parameters:

- `request.DatabasePath`: BLM `data.db` path。`null` の場合は既定 path。
- `request.ItemDirectoryPath`: BLM registered item の実体 root。

Returns:

- 作成、更新、更新不要、error 件数、成功状態を含む sync 結果。

Effects:

- Booth item / shop snapshot を upsert する。
- BLM registered item 配下の top-level entry ごとに `file_info` と `blm_file_origin` を upsert する。
- `(blm, registered_item_id)` を item identity として保存し、BLM tag を datasource snapshot として更新する。
- item directory を完全に列挙できた同期では、消えた origin を unavailable にする。
- `sync_info.last_sync_at` と `sync_info.last_sync_status` を更新する。

Notes:

- BLM entry は他 datasource origin と自動統合しない。
- BLM は Booth download ID を持たないため、`file_info.download_id` は NULL として扱う。
- `booth_item_id` が同じ場合は同じ `item_info` に紐付ける。

### `IAssetManager.SyncEagle`

Eagle library から AssetManager DB へ snapshot を取り込みます。

```csharp
public AssetSyncResult SyncEagle(EagleSyncRequest request)
```

Parameters:

- `request.LibraryPath`: Eagle library path。
- `request.TargetRoot`: 同期対象 root。未指定時は `VRCAsset`。

Returns:

- 作成、更新、更新不要、error 件数、成功状態を含む sync 結果。

Effects:

- `TargetRoot` 自身は item 化せず、`TargetRoot` 配下の descendant Eagle folder を AssetManager item として upsert する。
- 各 folder 内の通常 Eagle item を `file_info` と `eagle_file_origin` として upsert する。
- `(eagle, folder_id)` を item identity として保存し、folder rename では同じ Item を更新する。
- 完全 snapshot から消えた folder / Eagle item は履歴を削除せず unavailable にする。
- Booth metadata の tag を user tag と分離した datasource snapshot として更新する。
- folder 内に Booth metadata がある場合は item の Booth snapshot と表示情報として使う。
- `sync_info.last_sync_at` と `sync_info.last_sync_status` を更新する。

Notes:

- AssetManager sync は読み取りのみ。Eagle への書き戻しは行わない。
- Booth metadata file は item 情報として使い、file としては登録しない。
- Booth metadata の `downloads[].downloadId` を `file_info.download_id` に保存する。`importedItemIds[]` が Eagle item ID と一致する場合はその Eagle origin に紐付け、ID が欠けていても filename が一意に一致する場合は filename で補完する。
- Eagle item に対応しない Booth download も `file_info` として作成する。この場合は origin を持たないため、実体 path 解決はできない。
- Booth metadata を持たない folder も同期対象に含め、folder 名を item 名、説明を空文字として扱う。

### datasource 同期

Unity Editor session の開始時に、設定で有効な datasource の変更確認を background で行い、変更がある source だけを順番に同期します。同じ session 内の domain reload では再実行しません。

Navigation の同期操作では、起動時同期の有効設定にかかわらず、設定済みで存在する BLM / Eagle datasource を同じ prepare / conflict / apply 経路で手動同期します。同期処理が進行中の場合、重複した要求は開始しません。

- `assetManager.autoSyncBlmOnStartup`: BLM `data.db` と item directory を同期する
- `assetManager.autoSyncEagleOnStartup`: Eagle library を同期する
- datasource path が未設定または存在しない source は skip する
- BLM / Eagle の同期対象 record を正規化した fingerprint は `<ee4v global path>/cache/sync` に保存する
- DB 内に同じ source の成功した `sync_info` があり、fingerprint も前回成功時と一致する場合は DB sync と `Catalog` change 通知を行わない。DB を削除・再生成した場合は fingerprint が残っていても再同期する
- 外部の item 情報に差分があり、Unity の `item_info.updated_at` が datasource update time（取得できない場合は前回 `imported_at`）より新しい場合は競合として扱う
- 競合時は AssetManager window を開き、`DiffConfirmationOverlay` で名前・説明の現在値と同期元値を表示する
- `上書き` は差分を同期元の値で上書きして同期を続行し、`キャンセル` は今回の同期全体を中止する
- 実際に同期を実行した場合だけ、完了後の `Catalog` change を main thread で通知する
- background activity は統合 AssetManagerと単独 Main View windowの `StatusOverlay` に表示し、単独 Navigation windowには表示しない

File Tree の完成済み表示データは Unity Editor のメモリ上に最大 64 件共有します。同一 item / file の再表示ではこの memory cache をそのまま利用し、filesystem / ZIP の再確認と loading text の表示を行いません。Import Target と Version Group の代表変更は cache 上の node state を更新し、構造を含む `Catalog` change で全件を破棄します。cache は Unity 終了または domain reload で揮発します。

File Tree の構築中は File Tree 内の loading text だけを表示し、`IBackgroundActivityTracker` には登録しないため `StatusOverlay` は表示しません。

File Tree の ZIP metadata cache は、thumbnail と同じ cache root の `<ee4v global path>/cache/file-tree` に保存します。cache は source ZIP の更新日時と file size が一致する場合だけ利用し、不一致時は background task で再生成します。これは memory cache と異なり Unity を終了しても保持されます。cache には archive 内の実 path を保持し、読み出し時に ZIP と同名の単一 root folder を表示 path から省略します。

File Tree の画像 file hover は user setting `assetManager.showFileTreeImageTooltip` で切り替えます。既定値は `true` で `ImageTooltip` に画像 preview と file 名を表示し、`false` では Unity 標準の text tooltip に戻します。設定変更は開いている File Tree へ即時反映します。

### `IAssetManager.GetSyncInfo`

Datasource 別の最後の sync 状態を取得します。

```csharp
public IReadOnlyList<AssetSyncInfo> GetSyncInfo()
```

Returns:

- `source_type` ごとの `last_sync_at` と `last_sync_status`。

## Error Handling

### `AssetManagerException`

AssetManager API の domain error を表します。

```csharp
public sealed class AssetManagerException : Exception
{
    public AssetManagerErrorCode Code { get; private set; }
}
```

Notes:

- DB constraint 違反、存在しない ID、cycle、invalid smart condition は `AssetManagerException` で返す。
- ただし各 API の Returns に missing 時の戻り値が明記されている場合は、その記述を優先する。`GetItem(...)` は Item が見つからない場合に `null`、`ResolveFilePath(...)` は file が見つからない場合に `Found = false` を返す。
- 置き換え系 API は、入力 ID や条件が不正な場合に既存の tag / collection / dependency 関連を変更しない。
- datasource 読み取り失敗と個別 upsert 失敗は sync result の `ErrorCount` に集約する。

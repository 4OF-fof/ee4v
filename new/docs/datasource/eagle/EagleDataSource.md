# Eagle Data Source

`Editor/AssetManager/Eagle` には、Eagle が起動時に公開するローカル API と、Eagle プラグインが管理する Booth attachment 情報を読み取るための API を配置する。

このドキュメントは **ee4v 側の read-only 連携仕様** を整理する。Eagle への書き戻しや UI 実装は対象外とする。

## 前提

- Eagle のローカル API エンドポイントは `http://localhost:41595` を既定値とする
- Eagle は起動中のみ API を提供するため、未起動時はデータソースを無効扱いにする
- Booth 情報は Eagle folder 本体には保存せず、Eagle プラグインが library ごとに管理する attachment JSON を唯一の参照元とする
- v1 は BLM と同様に読み出しだけを実装し、ee4v から Eagle の folder / item を更新しない

## 公開 API

### `EagleApi.GetDefaultEndpoint()`

- 戻り値: `string`
- 既定の Eagle ローカル API endpoint を返す
- 既定値は `http://localhost:41595`

### `EagleApi.IsAvailable(string endpoint = null)`

- 戻り値: `bool`
- Eagle ローカル API へ接続できるかを返す
- `GET /api/application/info` または同等の軽量 endpoint を用いて可用性を確認する
- 接続失敗、タイムアウト、異常レスポンス時は `false`

### `EagleApi.GetFolders(string endpoint = null)`

- 戻り値: `IReadOnlyList<EagleFolderRecord>`
- `GET /api/folder/list` を使って folder 一覧を取得する
- Eagle の API レスポンスに含まれる `id`、`name`、`description`、`children`、`modificationTime` を正規化して返す
- folder 階層はレスポンス構造を保ったまま返してよいが、ee4v 側で探索しやすいよう `ParentId` を持たせる

### `EagleApi.GetItems(EagleItemQuery query, string endpoint = null)`

- 戻り値: `IReadOnlyList<EagleItemRecord>`
- `GET /api/item/list` を使って item 一覧を取得する
- `folders` と `tags` を使った絞り込みをサポートする
- v1 では以下の用途を満たせればよい
  - Booth attachment 付き folder 配下の item 一覧取得
  - standalone item のうち `VRCAsset` タグ付き item 一覧取得

### `EagleApi.GetBoothAttachments(string libraryPath = null)`

- 戻り値: `IReadOnlyList<EagleBoothAttachmentRecord>`
- Eagle プラグインの管理 JSON から Booth attachment 一覧を取得する
- attachment は Eagle library ごとに分離して保存する
- `libraryPath` 未指定時は現在開いている library を基準に解決する

### `EagleApi.GetImportCandidates(string endpoint = null, string libraryPath = null)`

- 戻り値: `IReadOnlyList<EagleImportCandidateRecord>`
- Eagle folder / item / Booth attachment を結合し、ee4v が実際に取り込む候補一覧を返す
- v1 では以下のルールを実装対象とする
  - Booth attachment が付いた folder は常に同期対象
  - その folder 配下の item は `VRCAsset` タグがなくても同期対象
  - folder に属さない standalone item は `VRCAsset` タグ付きのみ同期対象

## 返却モデル

### `EagleFolderRecord`

- `FolderId`
- `ParentId`
- `Name`
- `Description`
- `Children`
- `ModificationTime`

### `EagleItemRecord`

- `ItemId`
- `Name`
- `Url`
- `Annotation`
- `Tags`
- `FolderIds`
- `Extension`
- `Size`
- `Width`
- `Height`
- `ModificationTime`
- `IsDeleted`

### `EagleBoothAttachmentRecord`

- `FolderId`
- `BoothItemId`
- `ItemUrl`
- `Name`
- `Description`
- `ThumbnailUrl`
- `ShopName`
- `ShopUrl`
- `ShopThumbnailUrl`
- `Tags`
- `AttachedAt`
- `LastUpdatedAtUtc`
- `SchemaVersion`

### `EagleImportCandidateRecord`

- `SourceKind`
  - `AttachedFolder`
  - `FolderItem`
  - `StandaloneItem`
- `Folder`
- `Item`
- `BoothAttachment`

## 同期ルール

### Folder ベース

- Booth attachment が付いた folder は、Eagle 側のタグ有無に関係なく ee4v の同期対象とする
- folder 自体の Booth 情報は attachment JSON を参照し、folder `description` などの文字列解析には依存しない
- attachment が存在しても対応する folder が Eagle 上から削除されている場合、その attachment は stale として無視する

### Item ベース

- Booth attachment 付き folder 配下の item は `VRCAsset` タグ不要で同期対象とする
- standalone item は `VRCAsset` タグ付きのみ同期対象とする
- item が複数 folder に所属する場合は、少なくとも 1 つの同期対象 folder に属していれば対象に含める

### BLM との関係

- BLM は Booth 情報の補助データソースとして扱う
- read path は Eagle plugin の Booth attachment スナップショットだけで完結できるようにする
- BLM が存在する場合のみ、Booth item の再確認や追加補完に利用できる設計に留める

## 異常系

- Eagle 未起動、API 不達、HTTP エラー、JSON パース失敗時はデータソース無効として扱う
- attachment JSON が壊れている場合は、その library の attachment 読み取りを失敗扱いにし、folder / item 取得だけは継続できるようにする
- 再取得処理が失敗した場合、最後の正常な Booth attachment スナップショットは保持する
- API レスポンスに存在する folder / item と attachment JSON の対応が取れない場合は警告対象とし、同期対象からは除外する

## 実装メモ

- HTTP client は read-only の `GET` のみを使用する
- endpoint は setting 化を想定して引数で上書き可能にするが、公開 API の既定値は `GetDefaultEndpoint()` に寄せる
- Eagle API の folder / item レスポンス形状は Eagle 側バージョン差分を吸収するため、内部 DTO と公開 record を分ける
- attachment JSON の保存場所解決は Eagle plugin 側仕様に合わせるが、公開 API には storage path を露出しない

## テスト観点

- Eagle 起動中は `IsAvailable()` が `true`、停止中は `false`
- Booth attachment 付き folder はタグ不要で同期対象になる
- attachment 付き folder 配下の item はタグなしでも取得対象になる
- standalone item は `VRCAsset` タグ付きのみ取得対象になる
- stale attachment を安全に無視できる
- attachment JSON が壊れていても API 経由の folder / item 取得パスは独立して扱える

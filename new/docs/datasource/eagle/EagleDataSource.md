# Eagle Data Source

`Editor/AssetManager/Eagle` には、Eagle が起動時に公開するローカル API と、`_boothmeta.json` item を読み取るための API を配置する想定である。  
このドキュメントは **ee4v 側の read-only 連携仕様** を整理する。今回の実装対象は plugin と docs のみで、ee4v 側コードはまだ作らない。

## 前提

- Eagle のローカル API endpoint は `http://localhost:41595`
- 同期ルートは library 直下の `VRCAsset`
- 同期対象判定に tag や folder description は使わない
- `VRCAsset` 配下の item は構造だけで対象を決める
- Booth metadata の唯一の正本は `_boothmeta.json` 本文

## 読み取りモデル

### `BoothMeta`

- `_boothmeta.json` を正規化したモデル
- 最低限以下を持つ
  - `SchemaVersion`
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

### `EagleImportCandidateRecord`

- `RootFolder`
  - 最も近い祖先 `_boothmeta.json` root。無ければ `null`
- `Item`
  - 対象 Eagle item
- `BoothMeta`
  - `RootFolder` に対応する metadata。無ければ `null`

## 同期ルール

- `VRCAsset` 配下の item はすべて連携対象候補
- `_boothmeta.json` item 自体は連携対象に含めない
- `VRCAsset` 配下で、最も近い祖先 folder に `_boothmeta.json` があればその metadata を継承する
- 祖先に `_boothmeta.json` が無い subtree も対象には含めるが、`BoothMeta` は `null`
- nested `_boothmeta.json` は不正構成とし、親 root subtree を警告付きで除外する

## API 方向性

### `EagleApi.GetDefaultEndpoint()`

- `string`

### `EagleApi.IsAvailable(string endpoint = null)`

- `bool`

### `EagleApi.GetFolders(string endpoint = null)`

- `IReadOnlyList<EagleFolderRecord>`

### `EagleApi.GetItems(EagleItemQuery query, string endpoint = null)`

- `IReadOnlyList<EagleItemRecord>`
- tag ベースの絞り込みは前提にしない

### `EagleApi.GetBoothMetaItems(string endpoint = null)`

- `IReadOnlyList<EagleBoothMetaRecord>`
- `VRCAsset` 配下の `_boothmeta.json` item を収集して返す

### `EagleApi.GetImportCandidates(string endpoint = null)`

- `IReadOnlyList<EagleImportCandidateRecord>`
- `VRCAsset` 配下 item と `_boothmeta.json` を結合して返す

## 異常系

- Eagle 未起動、API 不達、HTTP エラー時はデータソース無効
- `_boothmeta.json` が壊れている場合はその root を失敗扱いにする
- nested `_boothmeta.json` は warning 対象
- `VRCAsset` が存在しない場合は対象 0 件

## テスト観点

- `VRCAsset/hoge/_boothmeta.json` の配下 item が metadata を継承する
- `VRCAsset/hoge/fuga/_boothmeta.json` のような孫以降 root も許容される
- `_boothmeta.json` が無い subtree も対象には含まれる
- `_boothmeta.json` item 自体は対象から除外される
- nested `_boothmeta.json` は不正構成として検出される

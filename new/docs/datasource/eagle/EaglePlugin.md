# Eagle Plugin

このドキュメントは **Eagle 側プラグインで Booth 情報を folder に紐付けるための設計** を整理する。ee4v 側はこのプラグインが管理する attachment JSON を read-only に参照する。

## 目的

- Eagle folder に対して Booth item 情報を紐付ける
- 紐付けた Booth 情報を Eagle folder 本体には保存せず、plugin 管理データとして保持する
- ee4v 側からは、そのスナップショットだけで Booth 情報を解決できるようにする

## スコープ

- 対象は folder への Booth attachment 管理
- Eagle item 自体のメタデータ更新やタグ更新は v1 の責務に含めない
- Eagle folder へ `VRCAsset` タグを自動付与する仕様は持たない
  - Booth attachment が付いている folder は、それ自体を ee4v の同期対象 folder として扱う

## プラグイン操作

### 1. 選択 folder へ Booth item 情報をアタッチ

- ユーザーが Eagle 上で folder を選択した状態で実行する
- 入力は少なくとも `boothItemId` または `itemUrl`
- 取得元は以下を優先する
  - 既存の保存済みスナップショット
  - BLM などのローカルデータソース
  - 必要なら Booth item URL を使った再取得
- 取得できた主要項目を attachment JSON に保存する

### 2. 保存済みスナップショットの再取得

- 既存 attachment を対象に明示操作で再取得する
- 成功時のみ `name`、`description`、`thumbnailUrl`、`shopName`、`shopUrl`、`shopThumbnailUrl`、`tags`、`lastUpdatedAtUtc` を更新する
- 失敗時は最後の正常スナップショットを保持し、attachment 自体は削除しない

### 3. アタッチ解除

- 対象 folder の attachment を削除する
- Eagle folder 本体の `description` や item 群には変更を加えない

## 保存形式

### 保存方針

- Booth attachment は Eagle library ごとに分離して保存する
- 理由は folder ID の衝突防止と library 切替時の誤参照防止
- Eagle folder 本体の `description`、`annotation`、任意ファイル名には Booth 情報を保存しない

### ルート構造

```json
{
  "schemaVersion": 1,
  "libraryId": "sample-library-id",
  "attachments": [
    {
      "folderId": "KBXXXXXXX",
      "boothItemId": 1234567,
      "itemUrl": "https://sample.booth.pm/items/1234567",
      "name": "Sample Item",
      "description": "Snapshot text",
      "thumbnailUrl": "https://...",
      "shopName": "Sample Shop",
      "shopUrl": "https://sample.booth.pm",
      "shopThumbnailUrl": "https://...",
      "tags": ["3D Model", "VRChat"],
      "lastUpdatedAtUtc": "2026-03-26T10:00:00Z"
    }
  ]
}
```

### attachment レコード

- `folderId`
  - Eagle folder ID
- `boothItemId`
  - Booth item 識別子
- `itemUrl`
  - Booth item URL
- `name`
  - attachment 作成時点の item 名スナップショット
- `description`
  - attachment 作成時点の説明文スナップショット
- `thumbnailUrl`
  - item サムネイル URL
- `shopName`
  - shop 名
- `shopUrl`
  - shop URL
- `shopThumbnailUrl`
  - shop サムネイル URL
- `tags`
  - Booth item タグ一覧
- `lastUpdatedAtUtc`
  - Booth 正本から取得した最新スナップショットの最終更新時刻

## 保存・更新ルール

- `folderId` は library 内で一意キーとして扱う
- 同一 folder に対する attachment は 1 件のみ許可する
- 同じ Booth item を複数 folder に付けることは許可しない
- `itemUrl` と `boothItemId` は相互導出可能であっても両方保存し、片方の欠損に備える
- 文字列項目は取得時点の値を保持し、ee4v 側は原則このスナップショットを信頼する

## ee4v 連携ルール

- ee4v は attachment JSON を唯一の Booth 紐付けソースとして読む
- Booth attachment が存在する folder は常に同期対象 folder として扱う
- その folder 配下の Eagle item は `VRCAsset` タグなしでも同期対象になる
- folder に属さない standalone item は `VRCAsset` タグ付きのみ同期対象になる
- ee4v は v1 では 読み出しのみ行う

## 異常系

- attachment は存在するが Eagle folder が存在しない場合は attachmentを削除
- 保存データが壊れている場合は schemaVersion と JSON 検証で検出し、ロード失敗扱いにする
- 再取得時に取得元へ接続できない場合は既存スナップショットを維持する
- library が切り替わった場合は別 library の attachment を読み込まない

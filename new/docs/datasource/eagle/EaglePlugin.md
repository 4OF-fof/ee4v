# Eagle Plugin

このドキュメントは **Eagle 側で `_boothmeta.json` item を管理するための設計** を整理する。  
Booth 情報の唯一の正本は Eagle item として保存された `_boothmeta.json` 本文であり、folder 本体や tag は参照しない。

## 目的

- `VRCAsset` 配下の subtree に Booth metadata を紐付ける
- Booth metadata は `_boothmeta.json` item 本文に保存する
- `_boothmeta.json` を選択した時だけ専用 inspector で編集できるようにする

## 前提

- 同期ルートは **library 直下の `VRCAsset` folder**
- `VRCAsset` 配下の任意 folder に `_boothmeta.json` を置ける
- `_boothmeta.json` を持つ folder が Booth subtree root になる
- tag、annotation、folder description などは Booth 連携判定に用いない

## Plugin 構成

### Booth Sync

- 1 つの plugin に `window plugin` と `json inspector` の両方を持たせる
- window 側は frameless の疑似 popup として動作する
- `frame: false`, 固定サイズ, `alwaysOnTop: true` を採用する
- 起動時は現在カーソルがあるディスプレイ中央へ毎回再配置する
- 選択 item が `BoothMeta` タグ付き JSON 1 件なら window を開かず、その item の Booth 情報を直接 sync する
- それ以外で起動した場合は URL 入力 popup を表示し、Booth item を取得して商品名ベースの folder を `VRCAsset` 直下へ新規作成し、その直下へ `BoothMeta` タグ付き JSON item を作成する
- 同じ plugin の inspector は `BoothMeta` タグ付き JSON item を選択した時だけ専用 UI を表示する
- これは Eagle 既存 UI にアンカーされたネイティブ popover ではない

## `_boothmeta.json` 保存形式

```json
{
  "schemaVersion": 1,
  "boothItemId": 1234567,
  "itemUrl": "https://sample.booth.pm/items/1234567",
  "name": "Sample Item",
  "description": "Snapshot text",
  "thumbnailUrl": "https://...",
  "shopName": "Sample Shop",
  "shopUrl": "https://sample.booth.pm",
  "shopThumbnailUrl": "https://...",
  "tags": ["3D Model", "VRChat"],
  "attachedAt": "2026-03-27T00:00:00Z",
  "lastUpdatedAtUtc": "2026-03-27T00:00:00Z"
}
```

## フィールド

- `schemaVersion`
  - 現在は `1`
- `boothItemId`
  - Booth item ID
- `itemUrl`
  - Booth item URL
- `name`
  - Booth item 名のスナップショット
- `description`
  - 説明文のスナップショット
- `thumbnailUrl`
  - item サムネイル URL
- `shopName`
  - shop 名
- `shopUrl`
  - shop URL
- `shopThumbnailUrl`
  - shop サムネイル URL
- `tags`
  - Booth item tags
- `attachedAt`
  - `_boothmeta.json` 初回作成時刻
- `lastUpdatedAtUtc`
  - Booth 正本から最後に更新した時刻

## 構造ルール

- `VRCAsset` 自体には `_boothmeta.json` を置かない
- `VRCAsset` 配下の任意深さの folder を subtree root にできる
- ただし nested root は不正構成とする
  - 例: `VRCAsset/hoge/_boothmeta.json` と `VRCAsset/hoge/fuga/_boothmeta.json` の共存は不可
- create plugin は nested root を新規作成しないように検証する

## 運用ルール

- `_boothmeta.json` が不要になった場合は通常の Eagle item と同様に削除する
- plugin 独自の disabled / archived 状態は持たない
- `_boothmeta.json` 以外の json item には専用 inspector を表示しない

## 異常系

- 選択 folder が `VRCAsset` 配下でない場合、create plugin は作成を拒否する
- 選択 folder に既に `_boothmeta.json` がある場合、重複作成を拒否する
- ancestor / descendant に `_boothmeta.json` がある場合、nested root 作成を拒否する
- Booth item URL が無効な場合、refresh は失敗として扱い既存 JSON は保持する
- JSON が壊れている場合、inspector は default schema を表示し直して保存できるようにする

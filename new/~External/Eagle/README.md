# Eagle Plugins

`new/~External/Eagle` には Eagle 連携用 plugin を配置する。

## Booth Sync

- plugin path: `new/~External/Eagle/ee4v-boothmeta-create`
- window plugin として動作するが、見た目は frameless の疑似 popup
- 起動時は現在カーソルがある画面の中央へ固定サイズで表示する
- フォーカスを失うか `Esc` を押すと hide する
- `Booth item URL` の入力から `VRCAsset/<itemId>` folder と `BoothMeta` タグ付き JSON item をまとめて作成する
- Eagle UI へアンカーされたネイティブ popover ではない

## ee4v BoothMeta Inspector

- plugin path: `new/~External/Eagle/ee4v-boothmeta-inspector`
- `_boothmeta.json` item 選択時だけ専用 inspector を表示する
- Booth metadata は `_boothmeta.json` 本文が唯一の正本

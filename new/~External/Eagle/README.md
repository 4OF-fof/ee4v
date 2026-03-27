# Eagle Plugins

`new/~External/Eagle` には Eagle 連携用 plugin を配置する。

## Booth Sync

- plugin path: `new/~External/Eagle/ee4v-boothmeta-create`
- 1 つの plugin に `window` と `json inspector` の両方を持つ
- 通常起動時は frameless の疑似 popup を開き、Booth item URL から `VRCAsset/<商品名>` folder と `BoothMeta` タグ付き JSON item を作成する
- `BoothMeta` タグ付き JSON item を選択中に起動した場合は window を開かず、その item を直接 sync する
- `BoothMeta` タグ付き JSON item 選択時は同じ plugin の inspector を右ペインへ表示する
- Booth metadata は JSON item 本文が唯一の正本

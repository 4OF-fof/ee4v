# Eagle Plugins

`new/External~/Eagle` は Eagle 連携 plugin の単一 npm project。
ビルド前の実装は `src` 配下の TypeScript を正本とし、`npm run build` で `dist` 配下へ Eagle plugin として出力する。

## Build

```sh
npm run build
```

ビルド後の plugin path:

- `new/External~/Eagle/dist/BoothCompat`
- `new/External~/Eagle/dist/BoothCompatService`

## BoothCompat

- `serviceMode: false`
- 右クリックメニュー / 通常 plugin 起動用
- frameless の疑似 popup を開き、Booth item URL から `VRCAsset/<商品名>` folder と `BoothMeta` タグ付き JSON item を作成する
- `BoothMeta` タグ付き JSON item を選択中に起動した場合は window を開かず、その item を直接 sync する
- `BoothMeta` タグ付き JSON item 選択時は inspector を右ペインへ表示する
- Booth metadata は JSON item 本文が唯一の正本

## BoothCompatService

- `serviceMode: true`
- Eagle 起動時の自動起動と Scriptcat 連携 bridge 専用
- `http://127.0.0.1:41596` を公開する
- bridge は `GET /health`, `POST /v1/status`, `POST /v1/import` を受け、BOOTH library userscript へ BoothMeta / download 状態を返す

## Shared Source

- `src/shared/core.ts` をビルド時に両 plugin の `js/core.js` へコピーする
- plugin 固有 entrypoint は `src/plugins/*/js/*.ts` から `dist/*/js/*.js` へ展開する

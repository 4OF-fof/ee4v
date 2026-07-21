# Eagle Plugins

`new/External~/Eagle` は Eagle 連携 plugin の単一 npm project。
ビルド前の実装は `src` 配下の TypeScript を正本とし、`npm run build` で `dist` 配下へ Eagle plugin として出力する。

## Build

```sh
npm run build
```

型チェック、ビルド出力、日英 locale のキー一致をまとめて確認する場合:

```sh
npm run check
```

ビルド後の plugin path:

- `new/External~/Eagle/dist/BoothCompat`
- `new/External~/Eagle/dist/BoothCompatService`

## BoothCompat

- `serviceMode: false`
- 右クリックメニュー / 通常 plugin 起動用
- 通常起動では frameless の疑似 popup を開き、Booth item URL から `VRCAsset/<商品名>` folder と `BoothMeta` タグ付き JSON item を作成する
- `BoothMeta` タグ付き JSON item 選択時は inspector を右ペインへ表示する
- Booth metadata は JSON item 本文が唯一の正本
- Window / Inspector は Eagle の locale、theme、library 変更へ追従する
- 選択変更中の Inspector は loading state を表示し、古い選択結果で上書きしない

## BoothCompatService

- `serviceMode: true`
- Eagle 起動時の自動起動と Scriptcat 連携 bridge 専用
- Eagle 起動時は status window を非表示にし、plugin を手動で開いた時だけ表示する
- plugin を手動で開くと 480x320 の frameless status window を表示する
- status window は bridge、`VRCAsset`、保留 import 件数を表示する
- `http://127.0.0.1:41596` を公開する
- bridge は `GET /health`, `POST /v1/status`, `POST /v1/import` を受け、BOOTH library userscript へ BoothMeta / download 状態を返す
- library 切替時は旧 library の保留 import job と path cache を破棄し、別 library への誤取り込みを防ぐ

## Shared Source

- `src/shared/core.ts` をビルド時に両 plugin の `js/core.js` へコピーする
- plugin 固有 entrypoint は `src/plugins/*/js/*.ts` から `dist/*/js/*.js` へ展開する

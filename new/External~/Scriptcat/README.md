# Scriptcat Scripts

`new/External~/Scriptcat` には ee4v 連携用の userscript を配置する。

## Booth to Ealge

- script path: `new/External~/Scriptcat/B2E.user.js`
- BOOTH library の商品カードと download 行に Eagle 連携状態 badge を追加する
- Eagle plugin 側 bridge `http://127.0.0.1:41596` と通信する
- `GET /health`, `POST /v1/status`, `POST /v1/import` を使う
- import action は bridge に BoothMeta / download request を登録してから BOOTH の通常 download button を発火する

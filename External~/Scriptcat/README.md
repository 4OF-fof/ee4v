# Scriptcat Scripts

`External~/Scriptcat` には ee4v 連携用の userscript を配置する。

## Booth to ee4v

- script path: `External~/Scriptcat/B2ee4v.user.js`
- BOOTH library、gifts、商品ページでdownload単位の状態表示・単品取り込み・まとめ取り込みを提供する
- Unity Editor内のC#データソースserver `http://127.0.0.1:48197` に接続する。Node.jsは不要
- データソースをEe4vにするとEditor起動時に自動起動する。`ee4v/Datasource/Start Server`と`Stop Server`で手動操作できる
- AssetManager設定の「ee4v ダウンロード監視フォルダー」をブラウザのダウンロード先に合わせる
- Eagle版とはどちらか一つだけを有効にする
- 起動・保存・DB同期の契約は[AssetManagerデータソース](../../docs~/datasources.md)を参照する

## Booth to Eagle

- script path: `External~/Scriptcat/B2E.user.js`
- BOOTH library の商品カードと download 行に Eagle 連携状態 badge を追加する
- Eagle plugin 側 bridge `http://127.0.0.1:48196` と通信する
- `GET /health`, `POST /v1/status`, `POST /v1/import` を使う
- `/health` で取得した session token を後続 POST の `X-EE4V-Bridge-Token` header に自動設定する
- status要求は同時に1件だけ実行し、実行中に発生した更新要求は完了後にまとめて再実行する。古い応答で新しい取込状態を上書きせず、bridgeのsession tokenが変わった場合は旧sessionの応答を反映しない
- status応答に含まれないdownloadは「確認中」として操作を無効にし、未取り込み一覧からも除外する。未確認の状態をImport可能と扱わない
- import action は bridge に BoothMeta / download request を登録してから BOOTH の通常 download button を発火する
- library と gifts では未取り込みのdownloadを右下へ一覧表示し、`すべて取り込む`で取り込み待ちを除く対象を順番に取り込む
- library の解析は `data-test` と商品・download action の包含関係を優先し、BOOTH の utility class 変更時は既存 class selector を fallback として扱う
- Eagle版・ee4v版とも商品カードの探索範囲は商品サムネイル1件を含む祖先内に限定する。商品URLはそのサムネイルのリンクを優先し、downloadのないカードはstatus要求・取り込み操作の対象から除外する。他のカードを含む商品一覧全体を1商品のカードとして扱わない
- 商品ページの解析は canonical item URL、`#variations` 配下の `/downloadables/` link、`data-test="other-downloads-button"` を優先し、localized URL と shop subdomain の両方を扱う
- 商品ページに複数の download がある場合、各 import action は対応する「その他のDL方法」の直下へ表示する

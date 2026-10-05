# BOOTH userscript

ee4vデータソース用は `External~/Scriptcat/B2ee4v.user.js` です。Eagle版と同じ画面・download単位の操作を使い、手動起動のloopback serverへ接続します。どちらか1つだけ有効にします。起動・保存・DB同期の契約は [datasources.md](./datasources.md) を参照してください。

`External~/Scriptcat/B2E.user.js` はBOOTHのlibrary、gifts、商品ページからEagle連携bridgeを呼び出します。downloadごとに取り込み状態を表示し、未取り込みであれば単品で取り込めます。

商品ページで1商品に複数のdownloadがある場合、各`Import Eagle`は対応する`その他のDL方法`の直下に表示します。

libraryとgiftsでは表示中の商品から未取り込みのdownloadを集め、画面右下へ商品名とファイル名を一覧表示します。`すべて取り込む`は取り込み待ちを除く対象を上から順番に処理します。すべて取り込み済みになると一覧を非表示にします。

取り込みは単品操作と同じ経路を使います。Eagle bridgeへ商品情報とdownload要求を登録した後、BOOTHの通常downloadを別タブで開始します。

# AssetManager データソース

同じAssetManagerのDB内の表情専用テーブルをee4v固定で同期し、表情タブから利用します。[表情ライブラリの契約](features/face-expression-library-sync.md)を参照してください。以下は通常SourceとItem／Fileの契約です。

Sourceは設定 `assetManager.datasource` の `Ee4v`、`BoothLibraryManager`、`Eagle` から1つだけ選択します。設定画面の文字はCoreのUiTextFactory対応rendererを使用します。Sourceを明示したmanagerは選択したSource以外の同期を拒否し、起動時と再読込も選択したSourceだけを同期します。設定を切り替えるとWindowのmanagerと表示状態を再作成します。既にDBへ保存した別SourceのItemを切替時に削除する処理はありません。

起動時の自動同期と再読込は同じUI同期処理を使用します。同期前に環境変数を展開した保存先フォルダーと、BOOTH Library Manager選択時のDBファイルの存在を確認します。空欄・不正なパス・存在しない保存先、同期結果のエラー、同期中の例外はConsoleへ理由を出力します。パスの不備ではsnapshotを適用せず、既存DBの取り込みデータを維持します。

表示Catalogは選択したSourceの素材と、Sourceに依存しない制作物Itemを扱います。Item一覧・検索・Collectionの検索結果と一致判定はSQLiteの件数計算とページ分割の前にこの条件で絞ります。拡張子条件は選択中SourceのFileだけを評価します。Tag一覧・編集候補は表示対象Itemに付いたSource由来Tagと手動編集Tagを返します。File一覧とItem検索結果内のFileは選択中Sourceに限定し、未所属Fileにも同じ条件を適用します。派生アセット一覧は表示対象Itemが所有するものを表示します。Sourceなしの通常の手動Itemはee4vのCatalogに含め、`IsOriginal`の制作物ItemはすべてのSourceで表示します。他SourceのデータはDBに保持し、元のSourceへ戻すと再表示します。制作物Itemの正本とDB再生成の契約は[AssetManager](./asset-manager.md)を参照します。

Sourceを明示した`Open(databasePath, datasource)`と共有sessionはCatalogを絞ります。Sourceを指定しない非共有`Open(databasePath)`は全Catalogを扱う管理APIです。IDを指定した単体取得・依存関係・取り込み済みGUIDの関連付けはSource切替で遮断しません。DBに残る別Sourceの取り込み済みAssetの保護と保存済み参照を維持します。Collection定義自体はSource共通で保持します。

`OpenSession(databasePath, datasource)` はSourceを明示してsessionを選択し、`OpenSession(databasePath)` は同じDBの選択済みsessionを再利用します。MCPとUIは同じmanagerの更新通知を共有します。MCPの名・説明編集も外部Sourceを事前拒否します。

Source由来の名・説明・File配置は元Sourceが正本です。同期のsnapshotにはSource固有のIDを使い、`item.source_type`、`file.source_type`、`item_source_tag.source_type`へ `ee4v`、`blm`、`eagle` を保存します。外部Source由来Tagとee4v編集Tagは別に保持します。DB schemaはv1、新規生成を前提とし、自動migrationと後方互換layerはありません。

## BLM

`Infrastructure/BoothLibraryManager/BoothLibraryManagerApi` は `GetItemById`、`TryGetItemById`、`DatabaseExists`、`GetRegisteredItems` を公開します。lookupはBOOTH商品ID、同期は `registered_items.id` を識別子とします。BLMの `booth_items`、`shops`、`overwritten_booth_items`、通常Tagと上書きTag、`registered_items`、`user_item_info`を読みます。上書き名・説明はCOALESCE、上書きTagがあれば通常Tagより優先します。商品未登録のlookupはnull/falseです。

BOOTH Library Manager DBは `SQLiteOpenFlags.ReadOnly` で開き、一つのread transactionでWALを含む整合したsnapshotを取得します。入力DBをコピー・更新せず、preferencesやcredentialsも読みません。設定画面は正式名称の`BOOTH Library Manager`を表示します。DBパスの初期値はAPIと共通の `%APPDATA%/pm.booth.library-manager/data.db` を展開した絶対パスです。folderlibraryの初期値は空欄で、同期にはその保存先の指定が必要です。

Eagleライブラリ、ee4v共通データ、BOOTH Library Managerのfolderlibraryは共通`PathField`でフォルダーを選択します。BOOTH Library Manager DBは`.db`ファイルを選択します。手入力も可能です。Eagleの同期対象ルートはライブラリ内のフォルダー名を表すため通常の文字列入力です。

Fileは指定folderlibrary直下の `<registered_items.id>/` 以下の実ファイルを再帰列挙します。directoryはFileとして登録しません。フォルダーが未配置でもメタデータItemは取り込めます。symlink/junctionと相対path traversalは拒否します。rootは明示指定し、BLM preferencesの自動読取は行いません。実ユーザーDBや実File配置は検証で参照しません。

## ee4vライブラリと待受server

ee4vの保存先は共通データの保存先です。`Assets/<id>/`に保存する手動登録pairと`Items/<boothItemId>/`に保存するdownload Itemをまとめ、1回のee4v snapshotとしてDBへ適用します。片方だけのsnapshotで他方を欠落扱いにしません。Itemの名・説明・Tag編集は対応するmetadataへ保存します。複数FileのItemもFile単位で削除でき、残るFileをmetadataへ保持します。全Fileを削除するとItemのSource directoryを除きます。削除準備はtrashへ退避し、DB更新失敗時にFileとmetadataを復元します。

`Infrastructure/Ee4v/Ee4vDatasourceServer`はUnity Editor内で動くC#のHTTP待受serverです。`UI/Ee4vDatasourceBootstrap`がMCPと同じEditor lifecycleに接続し、データソースが`Ee4v`の場合だけ起動時とassembly再読み込み後に自動起動します。batchmodeでは自動起動しません。Ee4vへ切り替えると起動し、別Sourceへ切り替えると停止します。Node.jsや外部process、OSへの自動起動登録、credential永続化、Firewall変更は不要です。bindは`127.0.0.1`、portはuserscriptと共通の48197固定です。port使用中などの起動失敗はConsoleへ理由を出力し、別portへの自動切り替えは行いません。

保存先には共通データの保存先を使い、監視先には設定`assetManager.downloadDirectory`（「ee4v ダウンロード監視フォルダー」）を使います。監視先の既定値はユーザーフォルダー配下の`Downloads`です。ブラウザのダウンロード先を変更・移動している場合は明示指定します。監視先が空欄・不正・存在しない場合は起動せず、Consoleに理由を表示します。共通データと監視先のsymlink/junctionは拒否します。ダウンロード元を消さず、完了したFileを共通データのlibraryへコピーします。

`ee4v/Datasource/Start Server`と`ee4v/Datasource/Stop Server`で手動起動・停止できます。StartはEe4v選択中かつ停止中、Stopは起動中だけ有効です。手動停止はEditor session内でassembly再読み込みとpath変更を越えて維持し、手動Start、Sourceの再選択、Editor再起動で解除します。共通データまたは監視先の変更時は実行中serverを停止して新設定へ接続し直します。Editor終了とassembly再読み込み前には待受とコピーworkerを終了し、一時Fileを除去します。停止中の取り込み待ちは破棄し、再起動後はuserscriptから再要求します。

`External~/Scriptcat/B2ee4v.user.js` はEagle版と同じBOOTH library/gifts/商品ページのdownload単位UI・まとめ取り込みを使い、48197へ接続します。Eagle版とはどちらか1つだけ有効にします。serverに登録したdownload要求のfilenameと新しい更新時刻を照合し、連続したpollでサイズ・時刻が安定し部分download拡張子がない場合だけコピーします。ブラウザの `(n)` suffixも許容します。同名の未完了要求は衝突を防ぐため拒否し、30分でtimeoutします。既存の古いdownloadを拾わないため、要求の5秒前以降に更新されたFileだけを対象にします。

HTTPは `/health` と、session token必須の `POST /v1/status`、`POST /v1/import` を提供します。tokenはprocess内でだけ保持します。Hostをloopback名、OriginをBOOTHまたはuserscriptのOriginなし通信へ限定し、本文は1MiB以内です。serverはBOOTH credentialsを受け取らず、商品やFileをネットワークから取得しません。

保存構造は `Items/<boothItemId>/metadata.json` と同directoryのFileです。metadataは `schemaVersion:1`、`id`、名・説明・BOOTH/shop URL・thumbnail URL・tags、`files` 配列を持ちます。File entryはdownload URLと元filenameから求めたSHA256の `id`、内部保存名 `fileName`、表示名 `originalFileName`、`downloadUrl`です。metadataは一時Fileからrenameで確定します。Fileコピー失敗・download timeout時に完成metadataを登録しません。

UnityでDatasourceをEe4vに設定し、AssetManagerの再読込でDBへ同期します。BLM用folderlibrary設定はee4vに使用しません。待受・コピー・metadata保存はbackground workerが担当し、serverはSQLiteへ直接書きません。取り込みにはUnity Editorの起動とserverの稼働が必要です。

## 検証

連携はC#のコンパイルと、隔離フォルダーでの一時loopback通信・コピー・重複取り込み・停止後のport解放で確認します。Editor lifecycleはSourceによる自動起動・停止、path変更、手動停止後のassembly再読み込みで確認します。Unity側の同期とCatalogの保証範囲は[test.md](./test.md)のAssetManagerテストを参照してください。実ユーザーDB・ブラウザ設定への書き込みは行いません。

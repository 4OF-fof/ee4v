# AssetManager データソース

Sourceは設定 `assetManager.datasource` の `Ee4v`、`BoothLibraryManager`、`Eagle` から1つだけ選択します。設定画面の文字はCoreのUiTextFactory対応rendererを使用します。Sourceを明示したmanagerは選択したSource以外の同期を拒否し、起動時と再読込も選択したSourceだけを同期します。設定を切り替えるとWindowのmanagerと表示状態を再作成します。既にDBへ保存した別SourceのItemを切替時に削除する処理はありません。

起動時の自動同期と再読込は同じUI同期処理を使用します。同期前に環境変数を展開した保存先フォルダーと、BOOTH Library Manager選択時のDBファイルの存在を確認します。空欄・不正なパス・存在しない保存先、同期結果のエラー、同期中の例外はConsoleへ理由を出力します。パスの不備ではsnapshotを適用せず、既存DBの取り込みデータを維持します。

表示Catalogも選択したSourceだけに限定します。Item一覧・検索・Collectionの検索結果と一致判定はSQLiteの件数計算とページ分割の前にSourceで絞ります。拡張子条件も選択中SourceのFileだけを評価します。Tag一覧・編集候補は選択中SourceのItemに付いたSource由来Tagと手動編集Tagだけを返します。File一覧とItem検索結果内のFileも選択中Sourceに限定し、未所属Fileにも同じ条件を適用します。派生アセット一覧は選択中Sourceの親Itemを持つものだけを表示します。Sourceなしの手動Itemはee4vのCatalogに含めます。他SourceのデータはDBに保持し、元のSourceへ戻すと再表示します。

Sourceを明示した`Open(databasePath, datasource)`と共有sessionはCatalogを絞ります。Sourceを指定しない非共有`Open(databasePath)`は全Catalogを扱う管理APIです。IDを指定した単体取得・依存関係・取り込み済みGUIDの関連付けはSource切替で遮断しません。DBに残る別Sourceの取り込み済みAssetの保護と保存済み参照を維持します。Collection定義自体はSource共通で保持します。

`OpenSession(databasePath, datasource)` はSourceを明示してsessionを選択し、`OpenSession(databasePath)` は同じDBの選択済みsessionを再利用します。MCPとUIは同じmanagerの更新通知を共有します。MCPの名・説明編集も外部Sourceを事前拒否します。

Source由来の名・説明・File配置は元Sourceが正本です。同期のsnapshotにはSource固有のIDを使い、`item.source_type`、`file.source_type`、`item_source_tag.source_type`へ `ee4v`、`blm`、`eagle` を保存します。外部Source由来Tagとee4v編集Tagは別に保持します。DB schemaはv1、新規生成を前提とし、自動migrationと後方互換layerはありません。

## BLM

`Infrastructure/BoothLibraryManager/BoothLibraryManagerApi` は `GetItemById`、`TryGetItemById`、`DatabaseExists`、`GetRegisteredItems` を公開します。lookupはBOOTH商品ID、同期は `registered_items.id` を識別子とします。BLMの `booth_items`、`shops`、`overwritten_booth_items`、通常Tagと上書きTag、`registered_items`、`user_item_info`を読みます。上書き名・説明はCOALESCE、上書きTagがあれば通常Tagより優先します。商品未登録のlookupはnull/falseです。

BOOTH Library Manager DBは `SQLiteOpenFlags.ReadOnly` で開き、一つのread transactionでWALを含む整合したsnapshotを取得します。入力DBをコピー・更新せず、preferencesやcredentialsも読みません。設定画面は正式名称の`BOOTH Library Manager`を表示します。DBパスの初期値はAPIと共通の `%APPDATA%/pm.booth.library-manager/data.db` を展開した絶対パスです。folderlibraryの初期値は空欄で、同期にはその保存先の指定が必要です。

Eagleライブラリ、ee4v共通データ、BOOTH Library Managerのfolderlibraryは共通`PathField`でフォルダーを選択します。BOOTH Library Manager DBは`.db`ファイルを選択します。手入力も可能です。Eagleの同期対象ルートはライブラリ内のフォルダー名を表すため通常の文字列入力です。

Fileは指定folderlibrary直下の `<registered_items.id>/` 以下の実ファイルを再帰列挙します。この配置は過去connector (`142a6558` の `new/Editor/AssetManager/api/connecter/blm/BlmConnectorApi.cs`、`ReadRegisteredItemFiles`) の `Path.Combine(itemDirectoryPath, registeredItemId)` と一致します。directoryをFileにしない現構成の制約に合わせて実Fileだけを再帰列挙します。フォルダーが未配置でもメタデータItemは取り込めます。symlink/junctionと相対path traversalは拒否します。rootは明示指定し、BLM preferencesの自動読取は行いません。実ユーザーDBや実File配置は検証で参照しません。

## ee4vライブラリと待受server

ee4vの保存先は共通データの保存先です。`Assets/<id>/`に保存する手動登録pairと`Items/<boothItemId>/`に保存するdownload Itemをまとめ、1回のee4v snapshotとしてDBへ適用します。片方だけのsnapshotで他方を欠落扱いにしません。Itemの名・説明・Tag編集は対応するmetadataへ保存します。複数FileのItemもFile単位で削除でき、残るFileをmetadataへ保持します。全Fileを削除するとItemのSource directoryを除きます。削除準備はtrashへ退避し、DB更新失敗時にFileとmetadataを復元します。

`External~/Datasource/server.mjs` はNode標準APIのみで動き、手動起動・手動終了します。自動起動登録、credential永続化、Firewall変更は行いません。bindは常に `127.0.0.1`、既定portは48197です。データ位置に既定値はなく、`--library` と `--downloads` が必須です。ダウンロード元を消さず、完了したFileを指定libraryへコピーします。

```powershell
node External~/Datasource/server.mjs --library 'D:/isolated/folderlibrary' --downloads 'D:/isolated/downloads'
```

`External~/Scriptcat/B2ee4v.user.js` はEagle版と同じBOOTH library/gifts/商品ページのdownload単位UI・まとめ取り込みを使い、48197へ接続します。Eagle版とはどちらか1つだけ有効にします。serverに登録したdownload要求のfilenameと新しい更新時刻を照合し、連続したpollでサイズ・時刻が安定し部分download拡張子がない場合だけコピーします。ブラウザの `(n)` suffixも許容します。同名の未完了要求は衝突を防ぐため拒否し、30分でtimeoutします。既存の古いdownloadを拾わないため、要求の5秒前以降に更新されたFileだけを対象にします。

HTTPは `/health` と、session token必須の `POST /v1/status`、`POST /v1/import` を提供します。tokenはprocess内でだけ保持します。Hostをloopback名、OriginをBOOTHまたはuserscriptのOriginなし通信へ限定し、本文は1MiB以内です。serverはBOOTH credentialsを受け取らず、商品やFileをネットワークから取得しません。

保存構造は `Items/<boothItemId>/metadata.json` と同directoryのFileです。metadataは `schemaVersion:1`、`id`、名・説明・BOOTH/shop URL・thumbnail URL・tags、`files` 配列を持ちます。File entryはdownload URLと元filenameから求めたSHA256の `id`、内部保存名 `fileName`、表示名 `originalFileName`、`downloadUrl`です。metadataは一時Fileからrenameで確定します。Fileコピー失敗・download timeout時に完成metadataを登録しません。

UnityでDatasourceをEe4v、共通データの保存先をserverの`--library`と同じpathに設定し、AssetManagerの再読込でDBへ同期します。BLM用folderlibrary設定はee4vに使用しません。serverとUnityのDB更新を分離し、serverはSQLiteへ直接書きません。Editorを開いていない間もee4vライブラリへの取り込みは可能です。

## 検証

新規自動テストやケースは追加しません。JavaScript構文、隔離フォルダーでの一時loopback通信・コピー・重複取り込みを手動検証します。Unityは2022.3の隔離VerifyProjectから自worktreeのsrcだけを参照し、既存AssetManagerのEditModeテストを実行します。実ユーザーDB・ブラウザ設定への書込、Unity6検証は行いません。

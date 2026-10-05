# AssetManager データソース

外部Sourceは設定 `assetManager.datasource` の `Eagle`、`BoothLibraryManager`、`Custom` から1つだけ選択します。設定画面の文字はCoreのUiTextFactory対応rendererを使用します。Factoryで開いたmanagerは選択したSource以外の同期を拒否し、起動時と再読込も選択したSourceだけを同期します。ee4vの手動保存・登録Fileは共存します。設定を切り替えるとWindowのmanagerを再作成します。既にDBへ保存した別SourceのItemを切替時に削除する処理はありません。

Source由来の名・説明・File配置は元Sourceが正本です。同期のsnapshotにはSource固有のIDを使い、`item.source_type`、`file.source_type`、`item_source_tag.source_type`へ `eagle`、`ee4v`、`blm`、`custom` を保存します。外部Source由来Tagとee4v編集Tagは別に保持します。DB schemaはv1、新規生成を前提とし、自動migrationと後方互換layerはありません。

## BLM

`Infrastructure/BoothLibraryManager/BoothLibraryManagerApi` は `GetItemById`、`TryGetItemById`、`DatabaseExists`、`GetRegisteredItems` を公開します。lookupはBOOTH商品ID、同期は `registered_items.id` を識別子とします。BLMの `booth_items`、`shops`、`overwritten_booth_items`、通常Tagと上書きTag、`registered_items`、`user_item_info`を読みます。上書き名・説明はCOALESCE、上書きTagがあれば通常Tagより優先します。商品未登録のlookupはnull/falseです。

BLM DBは `SQLiteOpenFlags.ReadOnly` で開き、一つのread transactionでWALを含む整合したsnapshotを取得します。入力DBをコピー・更新せず、preferencesやcredentialsも読みません。APIの既定DB位置は `%APPDATA%/pm.booth.library-manager/data.db` ですが、AssetManagerの同期にはBLM DBとfolderlibraryの明示設定が必要です。

Fileは指定folderlibrary直下の `<registered_items.id>/` 以下の実ファイルを再帰列挙します。この配置は過去connector (`142a6558` の `new/Editor/AssetManager/api/connecter/blm/BlmConnectorApi.cs`、`ReadRegisteredItemFiles`) の `Path.Combine(itemDirectoryPath, registeredItemId)` と一致します。directoryをFileにしない現構成の制約に合わせて実Fileだけを再帰列挙します。フォルダーが未配置でもメタデータItemは取り込めます。symlink/junctionと相対path traversalは拒否します。rootは明示指定し、BLM preferencesの自動読取は行いません。実ユーザーDBや実File配置は検証で参照しません。

## 独自folderlibraryと待受server

`External~/Datasource/server.mjs` はNode標準APIのみで動き、手動起動・手動終了します。自動起動登録、credential永続化、Firewall変更は行いません。bindは常に `127.0.0.1`、既定portは48197です。データ位置に既定値はなく、`--library` と `--downloads` が必須です。ダウンロード元を消さず、完了したFileを指定libraryへコピーします。

```powershell
node External~/Datasource/server.mjs --library 'D:/isolated/folderlibrary' --downloads 'D:/isolated/downloads'
```

`External~/Scriptcat/B2ee4v.user.js` はEagle版と同じBOOTH library/gifts/商品ページのdownload単位UI・まとめ取り込みを使い、48197へ接続します。Eagle版とはどちらか1つだけ有効にします。serverに登録したdownload要求のfilenameと新しい更新時刻を照合し、連続したpollでサイズ・時刻が安定し部分download拡張子がない場合だけコピーします。ブラウザの `(n)` suffixも許容します。同名の未完了要求は衝突を防ぐため拒否し、30分でtimeoutします。既存の古いdownloadを拾わないため、要求の5秒前以降に更新されたFileだけを対象にします。

HTTPは `/health` と、session token必須の `POST /v1/status`、`POST /v1/import` を提供します。tokenはprocess内でだけ保持します。Hostをloopback名、OriginをBOOTHまたはuserscriptのOriginなし通信へ限定し、本文は1MiB以内です。serverはBOOTH credentialsを受け取らず、商品やFileをネットワークから取得しません。

保存構造は `Items/<boothItemId>/metadata.json` と同directoryのFileです。metadataは `schemaVersion:1`、`id`、名・説明・BOOTH/shop URL・thumbnail URL・tags、`files` 配列を持ちます。File entryはdownload URLと元filenameから求めたSHA256の `id`、内部保存名 `fileName`、表示名 `originalFileName`、`downloadUrl`です。metadataは一時Fileからrenameで確定します。Fileコピー失敗・download timeout時に完成metadataを登録しません。

UnityでDatasourceをCustom、folderlibraryをserverと同じpathに設定し、AssetManagerの再読込でDBへ同期します。serverとUnityのDB更新を分離し、serverはSQLiteへ直接書きません。Editorを開いていない間もfolderlibraryの取り込みは可能です。

## 検証

新規自動テストやケースは追加しません。JavaScript構文、隔離フォルダーでの一時loopback通信・コピー・重複取り込みを手動検証します。Unityは2022.3の隔離VerifyProjectから自worktreeのsrcだけを参照し、既存AssetManagerのEditModeテストを実行します。実ユーザーDB・ブラウザ設定への書込、Unity6検証は行いません。

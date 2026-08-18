# AssetManager

AssetManagerはItem、File、Target、Dependency、Tag、CollectionをSQLiteへ保存し、UIから利用できるEditor APIを提供します。UI自体はまだ含みません。

## 構成

| アセンブリ | 役割 | Unity依存 |
|---|---|---|
| `Ee4v.AssetManager.Contracts.Editor` | 公開モデル、要求、`IAssetManager`、変更通知 | なし |
| `Ee4v.AssetManager.Domain.Editor` | 入力の正規化、Collection式の検証と評価 | なし |
| `Ee4v.AssetManager.Application.Editor` | 公開APIのユースケース、検索、同期の調整 | なし |
| `Ee4v.AssetManager.Infrastructure.Editor` | SQLite保存、Sourceの読み書き、Unityへの取り込み | あり |

公開入口は`AssetManagerFactory.Open(databasePath)`です。返された`IAssetManager`をUIなどの利用側が保持します。外部SourceとDBはApplicationの小さなportの外側に置き、APIの処理から実装詳細を分離しています。

SQLite接続はmasterと同じ`Ee4v.SQLite.Editor`境界とvendor済みの`sqlite-net`、`SQLitePCLRaw`、`e_sqlite3`を再利用します。現行のnative plugin対象はWindows x86_64 Editorです。

## 公開API

`IAssetManager`は次を提供します。

- 条件式によるItem検索とCollection検索
- 手動Itemの作成、取得、更新、アーカイブ、削除
- Fileの取得、ee4v Sourceへの登録、所属変更、アーカイブ、削除
- File Targetの取得、完全置換、Unity projectへの取り込み
- File Dependencyの取得と完全置換
- 複数ItemへのTag一括設定とTag一覧取得
- Collectionの作成、取得、更新、削除
- Eagle libraryからの同期
- ee4v libraryへのFile取り込みと同期
- CatalogまたはCollectionの更新通知

検索の`Limit = 0`は件数制限なしです。Itemは名前、IDの順で安定して返します。保存処理が成功した後だけ`Changed`を通知し、購読側の例外はAPI操作を失敗させません。

アーカイブ、削除、Fileの所属変更、Tag設定は対象IDを複数指定できます。アーカイブは`is_archived`だけを変更し、`false`を指定すると復元します。通常のItem検索とFile一覧はアーカイブ済みを除外し、必要な場合だけ明示的に含めます。

Item削除は所属Fileも削除します。File実体の削除はee4v Sourceだけを許可し、JSONとFileを含むSource directoryを削除します。Eagle由来FileやEagle由来Fileを含むItemの削除は拒否します。

## 規則

- Tagは先頭の`#`を除き、segment単位でtrimして小文字へ統一する
- 空segmentを含むTag pathを拒否し、未使用Tagを関連更新と同じtransactionで削除する
- Collection名は一意とする
- ANDとORは2子以上、NOTは1子、Conditionは子なしとする
- Collection条件は名前、説明、Tag、利用可能なFileの拡張子を対象とする
- 名前と説明は大文字小文字を区別しない部分一致とする
- Tag条件`foo`は`foo`と`foo/*`へ一致する
- 拡張子は先頭の`.`を除き、小文字へ正規化する
- Targetの空pathはFile自身、空path以外はZIP内の相対pathを表す
- Target pathは区切りを`/`へ統一し、絶対path、空segment、`.`、`..`を拒否する
- File Dependencyは別Itemと別SourceのFile間も許可する
- 自己依存と直接または間接的な循環を拒否する

## Source同期

既定ではEagle library内の`VRCAsset`配下のfolderをItemとして同期します。folder IDとEagle item IDを安定IDとして使用するため、名前やpathの変更ではDB内のIDを維持します。見つからなくなったFileは削除せず利用不可へ更新します。

`EagleSyncRequest`でlibrary pathと対象rootを指定できます。読み取りまたは形式の問題は失敗結果として返し、DBへの反映は1 transactionで行います。

ee4v Sourceは`<library>/Assets/<source-id>/`ごとに`metadata.json`と実体Fileを1つずつ保持します。`RegisterFile`はFileをSourceへコピーし、指定したItemまたは未所属Fileとして登録します。`ImportEe4vFile`はItemとFileをまとめて作成します。

`SyncEe4v`はJSONから内容を復元します。File単独で登録したSourceは既存のItem所属を維持しますが、DB再作成後は未所属Fileとして復元します。ee4v Itemの名前、説明、TagをAPIで変更した場合はJSONも更新します。不正なJSONが1件でもある同期はDBへ反映しません。

Source同期は既存のアーカイブ状態を変更しません。ee4v Fileを削除した場合はSource directory自体がなくなるため、後続の同期でも復元されません。

## Targetの取り込み

`SetFileTargets`は1つのFileに対するTarget一覧を完全置換します。空の一覧でTarget未設定へ戻します。同じTargetの大文字小文字違いは重複として除外します。

`ImportFileTargets`は設定済みTargetを`Assets/<Item名>/<File名から拡張子を除いた名前>/`へ取り込みます。空pathのTargetはFile自身をコピーし、ZIP内Targetは相対pathを保って展開します。`.unitypackage`はコピーせずUnity packageとして取り込みます。ZIPの全要素がZIP名と同じroot directory内にある場合は、masterと同様にそのrootを省略したTarget pathも解決します。

Target未設定での取り込みは何も行いません。未所属、利用不可、アーカイブ済みのFileにTargetが設定されている場合は取り込みを拒否します。

Fileに依存先が設定されている場合は推移的な依存先を解決し、依存先から依存元の順に各Fileを1回だけ取り込みます。依存関係自体に順序は持たせず、順序が必要な場合は追加の依存関係で表現します。`.unitypackage`は完了を待ってから次のFileへ進み、失敗またはキャンセル時は後続の取り込みを停止します。

## 未実装範囲

- EditorWindow、Inspector、Project Window装飾を含むUI
- 設定画面、起動時の自動同期
- Eagleとee4v以外のSource
- 取り込み済みAssetの追跡と保護処理

永続化する値と制約は[AssetManager DB設計](./asset-manager-db.md)を参照してください。

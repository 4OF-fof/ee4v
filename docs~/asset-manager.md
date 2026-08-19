# AssetManager

AssetManagerはItem、File、Target、Dependency、Tag、Collection、取り込み済みAsset GUIDをSQLiteへ保存し、Editor APIとUIを提供します。

`ee4v/Asset Manager`から管理画面を開けます。画面は左のナビゲーション、中央の一覧、右の情報表示からなる固定幅の3ペイン構成です。左ペインは240px、右ペインは300pxで、中央ペインだけがウィンドウ幅へ追従します。ペイン境界のドラッグ操作はありません。ナビゲーション上部では全件、未所属、アーカイブ、タグを切り替えます。コレクションは件数付きのフォルダ行として並び、見出し右端の追加ボタンから作成します。Item作成はMain View右下の円形追加ボタンから開きます。タグ画面では既存タグと該当Item数を表示し、タグを選ぶとそのタグまたは下位タグを持つItemへ絞り込みます。中央ツールバーは戻る・進む履歴とパンくずリストを左端に置き、列数スライダーを中央に配置します。パンくずは現在位置の末尾だけを表示し、2階層以上ではホバー中にフルパスを表示します。ホバー表示の親階層を選ぶとその位置へ移動できます。画面名はパンくずだけに表示し、重複する見出しと件数は置きません。右端の再読み込みボタンはEagleとee4vを同期した後に一覧を更新します。その左の検索欄と合わせて操作領域としてまとめ、Main View下部に状態フッターは置きません。検索欄のFluent UI System Icons `Search`を選ぶとUnity標準メニューを開き、名前、説明、タグを検索対象へ含めるか個別に切り替えられます。File一覧では名前の指定をFile名へ適用します。Itemをダブルクリックすると中央ペインをそのItemに所属するFile一覧へ切り替え、パンくずまたは履歴から元の一覧へ移動できます。再読み込みにはmasterと同じMicrosoft Fluent UI System Iconsの`Arrow Clockwise`を使用し、必要なアイコンだけをruntime assetとして保持します。Item一覧は表示範囲の行だけをプールする可変列Gridです。masterと同じ1〜12列を設定範囲とし、表示領域から算出した推奨最小列数をスライダーの下限へ反映します。スライダーと±ボタンは、入力処理内で表示中の行を新しい列数へ組み替えます。全行の再生成や次のUI更新を待ちません。表示幅と高さが変わると列間隔、カード幅、固定行高もまとめて再計算します。カード名は横方向と縦方向の中央へ揃え、表示幅を超える場合は末尾を`…`で省略します。カード選択とサムネイル表示にも対応し、選択中のカードへホバーした場合は青系の選択表現を維持した専用スタイルを使用します。Itemの編集とTag設定、Fileの登録と所属変更、TargetとDependencyの設定、Archive解析とUnity projectへの取り込みを同じ画面から実行できます。Eagleとee4vのパス設定はAssetManager画面には置かず、`Preferences/4OF/ee4v`のUser Settingsで管理します。

Item Gridでは検索欄の左にソートボタンを表示します。Unity標準メニューから名前、作成日、更新日、File数を選択でき、逆順指定で方向を反転します。同値の場合は名前とIDを使って安定した順序にします。ボタンにはMicrosoft Fluent UI System Iconsの`Arrow Sort`を使用し、固定したvendor版の元SVGと生成PNGだけを保持します。

コレクション見出しの追加ボタンはmasterと同様にボタン直下へ入力ポップアップを開きます。コレクション名と条件グループを入力し、グループごとにANDまたはORを選択できます。条件グループは任意の深さへ入れ子にでき、各条件またはグループをNOTで反転できます。既存コレクションの編集も同じポップアップで行い、Informationペインはコレクションの作成と編集に使用しません。条件の選択肢はAssetManagerの表示言語に合わせて翻訳します。

ナビゲーションには独立した上部ツールバーを置かず、全件を左ペインの先頭に表示します。

AssetManager画面にはデータソースと取り込みの専用ページを置きません。Eagleとee4vの同期は再読み込み操作とセッション開始時に行い、取り込みや関連付け検索のAPIはUIと独立して提供します。

Itemに所属するFileと未所属FileのGridでもソートボタンを維持します。File画面のメニューは名前、作成日、更新日と逆順を表示し、File数は表示しません。Item画面からFile数指定のまま移動した場合はFile名順として扱います。

AssetManagerが独自に描画する操作アイコンはMicrosoft Fluent UI System Iconsの固定版へ統一します。ツールバー、検索解除、列数の増減、折りたたみ、通知、パンくず区切り、File Gridで使う分だけをvendor資産として保持します。ナビゲーションの分類アイコンは共有UIの`Icon`を使用します。アイコンの組み立ては`AssetManagerControls`へ集約し、文字記号は使用しません。

AssetManagerの固定表示文字はAssetManagerスコープのi18nカタログから取得します。英語と日本語を収録し、言語設定または翻訳アセットの再読込時には開いているAssetManager画面とウィンドウタイトルを再構築します。Item名、File名、コレクション名などの保存データは翻訳しません。

Gridカードは通常クリックで単一選択、Ctrlクリックで選択の追加と解除、Shiftクリックで直前の基準Itemから範囲選択します。CtrlとShiftを併用した場合は現在の選択へ範囲を追加します。選択はItem ID集合として保持するため、スクロールによるカード再利用後も維持します。情報ペインには最後に操作した選択Itemを表示します。

Itemに所属するFile一覧と未所属File一覧もItem一覧と同じ仮想化Gridで表示します。Fileはサムネイルを持たないため、File名の拡張子を優先し、拡張子がない場合はSourceパスから種別を判定します。画像、音声、動画はそれぞれ専用アイコンを使用します。アーカイブ、コード、3Dモデルにも専用アイコンを割り当て、それ以外は汎用ドキュメントとして表示します。アイコンは固定したMicrosoft Fluent UI System Iconsから必要な7点だけを追加し、カード内では同じTextureを共有します。

Item GridとFile GridはEscapeまたはカード外の空白を左クリックすると全選択を解除し、情報ペインを未選択表示へ戻します。Ctrlで選択中の最後のカードを解除した場合も同じ未選択状態になります。Fileカードのアイコン枠はサムネイル用の塗りを使わず透過し、カード自身の通常・選択・ホバー背景をそのまま表示します。

`ee4v/Asset Manager (Separated)`はNavigation、Main、Informationの3ウィンドウをまとめて開きます。各ウィンドウは同じ`IAssetManager`と表示状態を共有するため、Navigationでのページ切替とMainでのItem選択がほかのウィンドウへ反映されます。個別に開く場合は`ee4v/Window/Asset Manager Navigation`、`Asset Manager Main`、`Asset Manager Information`を使用します。

実画面と同じ`AssetManagerView`は`ee4v/Debug/Catalog`の`Domain/AssetManager/AssetManagerView` Storyでサンプルデータを使って確認できます。Grid単体は同じGroupの`AssetItemGridView` Storyで列組み、選択状態、サムネイルの有無を確認できます。`AssetItemGridView · File icons` StoryではFile Gridと拡張子別アイコンを確認できます。`AssetManagerBreadcrumb` Storyでは末尾表示とホバー中のフルパスを確認できます。`AssetManagerView · Separated modes` Storyでは分離表示間の状態連動を確認できます。`AssetCollectionCreationPopup` Storyでは作成と編集のポップアップ、入れ子のAND、OR、NOT、条件とグループの追加と削除を確認できます。`AssetManagerControls` Storyではボタン、入力、列挙選択、折りたたみ、通知の各状態を確認できます。`AssetManagerThreePaneLayout`は`AssetManagerView`と表示内容が重複するため、単独Storyを持ちません。Storyの操作はSQLiteや外部Sourceを変更しません。

内部操作部品は`Editor/AssetManager/UI/AssetManagerControls.cs`に置き、`AssetManagerView`はUnity標準部品を直接生成しません。文字はすべて`UiTextFactory`を通します。通常ボタンはmasterの`UiButton`、入力欄は`InputField`、検索欄は`SearchField`の寸法、余白、hover／active／focus表現を踏襲します。現行APIとの接続と状態管理はAssetManager向けに実装し直しています。これはAssetManager内だけの境界であり、ほかの機能で実利用されるまではCoreまたは共有UIへ移しません。UIからの更新操作中はmanagerの変更通知を蓄積し、操作完了時に一覧と詳細を1回だけ再描画します。

## 構成

| アセンブリ | 役割 | Unity依存 |
|---|---|---|
| `Ee4v.AssetManager.Contracts.Editor` | 公開モデル、要求、`IAssetManager`、変更通知 | なし |
| `Ee4v.AssetManager.Domain.Editor` | 入力の正規化、Collection式の検証と評価 | なし |
| `Ee4v.AssetManager.Application.Editor` | 公開APIのユースケース、検索、同期の調整 | なし |
| `Ee4v.AssetManager.Infrastructure.Editor` | SQLite保存、Sourceの読み書き、Unityへの取り込み | あり |
| `Ee4v.AssetManager.UI.Editor` | 管理画面、backend操作との接続、UI Story | あり |

公開入口は`AssetManagerFactory.Open(databasePath)`です。返された`IAssetManager`をUIなどの利用側が保持します。外部SourceとDBはApplicationの小さなportの外側に置き、APIの処理から実装詳細を分離しています。

SQLite接続はmasterと同じ`Ee4v.SQLite.Editor`境界とvendor済みの`sqlite-net`、`SQLitePCLRaw`、`e_sqlite3`を再利用します。現行のnative plugin対象はWindows x86_64 Editorです。

## 公開API

`IAssetManager`は次を提供します。

- 条件式によるItem検索、Collection検索、Item単体のCollection一致判定
- 手動Itemの作成、取得、更新、アーカイブ、削除
- Itemサムネイルの単体・一括取得
- Fileの取得、ee4v Sourceへの登録、所属変更、アーカイブ、削除
- File Targetの取得、完全置換、Unity projectへの取り込み
- File Dependencyの取得と完全置換
- ZIPとUnityPackageの内容解析
- File・Item単位の取り込み済みUnity Asset GUID取得とGUIDからの関連逆引き
- 複数ItemへのTag一括設定とTag一覧取得
- Collectionの作成、取得、更新、削除
- Eagle libraryからの同期
- ee4v libraryへのFile取り込みと同期
- 変更種別、対象ID、影響先IDを含む更新通知

検索の`Limit = 0`は件数制限なしです。通常検索はアーカイブ済みItemを除外します。条件評価、件数取得、ページングはSQLiteで行い、該当ページのItemだけTagとFileを含む完全なモデルへ復元します。Itemは名前、IDの順で安定して返します。保存処理が成功した後だけ`Changed`を通知し、購読側の例外はAPI操作を失敗させません。

`MatchesCollection(collectionId, itemId)`は指定したCollectionとItemだけを読み、現在の条件への一致を返します。UIは変更通知の`SubjectIds`または`RelatedIds`に含まれるItemを再取得し、このAPIで表示中Collectionへの追加・更新・除外を判断できます。初期表示の`SearchCollection`以外でCatalog全体を再取得する必要はありません。

## 変更通知

`AssetManagerChange.SubjectIds`はAPIが直接変更したItem、File、CollectionのIDです。`RelatedIds`はFileの変更によって一覧や集計が影響を受けるItemのIDです。これにより利用側は、変更種別に応じて対象のItem、File、Collectionだけを再取得できます。複数対象APIでは全対象IDを1回の通知に含めます。

| 変更種別 | 発行するAPI | `SubjectIds` | `RelatedIds` |
| --- | --- | --- | --- |
| `ItemCreated` | `CreateItem`、`ImportEe4vFile` | 作成Item | なし |
| `ItemUpdated` | `UpdateItem` | 更新Item | なし |
| `ItemArchiveChanged` | `SetItemArchived` | 対象Item | なし |
| `ItemDeleted` | `DeleteItem` | 削除Item | なし |
| `ItemTagsChanged` | `SetItemTags` | 対象Item | なし |
| `FileCreated` | `RegisterFile`、`ImportEe4vFile` | 作成File | 所属Item |
| `FilePlacementChanged` | `SetFileItem` | 対象File | 変更前と変更後の所属Item |
| `FileArchiveChanged` | `SetFileArchived` | 対象File | 所属Item |
| `FileDeleted` | `DeleteFile`、所属Fileを持つ`DeleteItem` | 削除File | 削除前の所属Item |
| `FileTargetsChanged` | `SetFileTargets` | 対象File | なし |
| `FileDependenciesChanged` | `SetFileDependencies`、依存先を削除する`DeleteFile`／`DeleteItem` | 依存関係が変わった依存元File | なし |
| `FileImportedAssetGuidsChanged` | 成功した`ImportFileEntries`、`ImportFileTargets` | 取り込みが完了したFile | 所属Item |
| `CollectionCreated` | `CreateCollection` | 作成Collection | なし |
| `CollectionUpdated` | `UpdateCollection` | 更新Collection | なし |
| `CollectionDeleted` | `DeleteCollection` | 削除Collection | なし |
| `SourceSynchronized` | 成功した`SyncEagle`、`SyncEe4v` | 影響を受けたItem | 影響を受けたFile |

`SourceSynchronized`は`SourceType`で`Eagle`または`Ee4v`を示し、作成・更新・削除されたItemとFileに加えて、依存関係の連鎖削除や未所属化で影響を受けたIDを1回の通知にまとめます。IDが空なら同期による変更はありません。同期の読み取りに失敗してDBを変更しなかった場合と、Target未設定または取り込み失敗でGUIDを変更しなかった場合は通知しません。読み取りAPIと`AnalyzeFile`も通知しません。

アーカイブ、削除、Fileの所属変更、Tag設定は対象IDを複数指定できます。アーカイブは`is_archived`だけを変更し、`false`を指定すると復元します。通常のItem検索とFile一覧はアーカイブ済みを除外し、必要な場合だけ明示的に含めます。

Item削除は所属Fileも削除します。File実体の削除はee4v Sourceだけを許可し、JSONとFileを含むSource directoryを削除します。削除時はSource directoryを`<library>/.trash/`へ退避してからDBを更新し、DB更新に失敗した場合は元へ戻します。DB更新に成功した場合だけ退避内容を削除します。Eagle由来FileやEagle由来Fileを含むItemの削除は拒否します。ee4vへの新規登録後にDB更新が失敗した場合も、作成したSource directoryを削除します。

Eagle同期で作成したItemの名前、説明、TagはEagleを正本とし、`UpdateItem`と`SetItemTags`による変更を拒否します。Eagle由来Fileの所属もEagle folderを正本とし、`SetFileItem`による変更を拒否します。アーカイブ状態、Target、DependencyはAssetManager固有の状態として変更できます。

## 規則

- Tagは先頭の`#`を除き、segment単位でtrimして小文字へ統一する
- 空segmentを含むTag pathを拒否し、未使用Tagを関連更新と同じtransactionで削除する
- Collection名は一意とする
- ANDとORは2子以上、NOTは1子、Conditionは子なしとする
- Collection条件は名前、説明、Tag、未アーカイブFileの拡張子を対象とする
- 名前と説明は大文字小文字を区別しない部分一致とする
- Tag条件`foo`は`foo`と`foo/*`へ一致する
- 拡張子は先頭の`.`を除き、小文字へ正規化する
- Targetの空pathはFile自身、空path以外はZIP内の相対pathを表す
- Target pathは区切りを`/`へ統一し、絶対path、空segment、`.`、`..`を拒否する
- File Dependencyは別Itemと別SourceのFile間も許可する
- 自己依存と直接または間接的な循環を拒否する
- Fileとして登録できる実体は通常Fileだけとし、directoryを拒否する

## Source同期

AssetManager UIは`Preferences/4OF/ee4v`のUser Settingsから、Eagleライブラリのパス、Eagleの同期対象ルート、ee4v共通データの保存先を操作時に読み取ります。設定値はAssetManager内へ複製しません。ee4v共通データの保存先を変更した場合はmanagerと共有表示状態を破棄し、開いているAssetManager Windowを新しいDBで再構築します。公開APIを直接利用する場合は、従来どおり各requestへパスを指定できます。

Unityエディターのセッション開始時には、存在するEagleとee4vのSourceを1回ずつ自動同期します。スクリプトの再コンパイルでは同じセッション中の同期を繰り返しません。各Sourceの自動同期はUser Settingsで個別に無効化できます。バッチモードでは利用者のDBを変更しないため実行しません。

既定ではEagle library内の`VRCAsset`配下のfolderをItemとして同期します。folder IDとEagle item IDを安定IDとして使用するため、名前やpathの変更ではDB内のIDを維持します。Itemの名前、説明、サムネイルURL、Tagは同期のたびにEagleの値へ合わせます。Booth metadata itemに付いたTagはItemのTagへ取り込まず、通常Fileに付いた`BoothMeta`と旧形式の`VRCMeta`もシステム用Tagとして除外します。同じEagle itemが複数folderに属する場合は、1つのFileが複数Itemに属さないよう最初のItemだけへ関連付けます。完全同期で見つからなくなったItemとFileはDBから削除します。削除されたItemへ別Source由来のFileが所属していた場合、そのFileは削除せず未所属へ戻します。directory payloadとBooth metadata JSONはFileとして登録しません。

Eagleとee4vから取り込むItem名、説明、File名、Tagは保存前にUnicode NFKCで正規化します。全角文字と数学英数字は通常の文字へ寄せ、BMP内の星記号は`*`、著作権記号はASCII表記へ変換します。対応する通常文字がない`OtherSymbol`、補助文字、異体字セレクター、ゼロ幅結合子、不要な制御文字は除去します。これによりUI用フォントに存在しない絵文字や装飾記号が表示文字列へ入ることを防ぎます。

`EagleSyncRequest`でlibrary pathと対象rootを指定できます。対象rootが存在しない場合は失敗として扱い、既存状態を変更しません。対象rootが存在して子Itemが0件の場合は正常な空スナップショットとして扱います。読み取りまたは形式の問題は失敗結果として返し、DBへの反映は1 transactionで行います。

`AssetSyncResult`はItemとFileを分けて作成・更新・削除IDを返し、影響を受けたItem／File ID、未変更件数、失敗理由も保持します。Tagだけが変わったItemも更新として数えます。

ee4v Sourceは`<library>/Assets/<source-id>/`ごとに`metadata.json`と実体Fileを1つずつ保持します。取り込み時は一時directoryへ両方を書き、完成後にSource directoryへ移動します。`RegisterFile`はFileをSourceへコピーし、指定したItemまたは未所属Fileとして登録します。`ImportEe4vFile`はItemとFileをまとめて作成します。

`SyncEe4v`はJSONから内容を復元します。File単独で登録したSourceは既存のItem所属を維持しますが、DB再作成後は未所属Fileとして復元します。ee4v Itemの名前、説明、TagをAPIで変更した場合はJSONも更新します。不正なJSONが1件でもある同期はDBへ反映しません。

Source同期は既存のアーカイブ状態を変更しません。完全同期に含まれないSource由来ItemとFileは、他の同期更新と同じtransactionでDBから削除します。ee4v Fileを削除した場合はSource directory自体がなくなるため、後続の同期でも復元されません。

## Itemサムネイル

`AssetItem.ThumbnailUrl`はItemに対応するサムネイルの取得元です。Eagle同期では同FolderのBooth metadataにある`thumbnailUrl`を正本として保存します。`GetThumbnail`と`GetThumbnails`は`Task`を返し、HTTP通信とcacheの読み書きを呼び出し元を止めずに行います。`CancellationToken`で待機中の通信と一括取得を中止できます。画像はDBと同じdirectoryの`cache/asset-manager/thumbnails/`へItem IDとURL単位で保存します。取得元がない場合や取得に失敗した場合は例外ではなく`AssetThumbnail.Found = false`と理由を返します。一括取得は重複Item IDを除外し、最大4並列で未cache画像を取得します。Item一覧は未取得のItemだけを表示順に`GetThumbnail`へ要求し、1件の取得が完了するたびに該当カードを描画します。取得結果は画像の欠落も含めて`AssetManagerView`単位のCore `CachedImageCache`が保持するため、画面遷移や再検索では再取得しません。Item一覧とItem詳細は同じcacheを使用します。Source同期または再読み込み操作では保持内容を破棄し、最新のURLと取得結果を反映します。画面遷移または再検索時には進行中の要求を中止します。デコード済みTextureも同じcacheで共有し、スクロールでカードが再利用されても再デコードしません。

Gridは表示範囲の行だけを保持します。スクロール位置が同じ行内にある間はカードを再バインドしません。先頭行が変わっても表示範囲に残る行は維持し、新しく見える行だけをプールから再利用します。

## File内容の解析

`AnalyzeFile`はFile実体を読み、ZIPまたはUnityPackageの内容を返します。ZIPは各entryの相対path、種別、非圧縮sizeを返します。全entryがZIP名と同じ単一root directory内にある場合は、そのrootをpathから省略します。UnityPackageはAsset path、FileまたはDirectoryの種別、size、Unity Asset GUIDを返します。ZIPとUnityPackage以外は解析対象外です。UIの解析ボタンは返されたpathをアーカイブ項目欄へ設定し、取り込み対象として編集できる状態にします。

## Targetの取り込み

`SetFileTargets`は1つのFileに対するTarget一覧を完全置換します。空の一覧でTarget未設定へ戻します。同じTargetの大文字小文字違いは重複として除外します。

`SetFileDependencies`は複数の依存元Fileを受け取り、全Fileの依存先を1 transactionで同じ一覧へ完全置換します。複数Fileを同時に変更した結果も含めて循環を検証し、失敗時はどのFileも変更しません。

`ImportFileEntries(fileId, paths)`は指定したpathだけを`Assets/<Item名>/<File名から拡張子を除いた名前>/`へ取り込み、Fileに保存したTargetは変更しません。これにより`AnalyzeFile`で得たZIP内の1要素を一時的に選んで取り込めます。空pathはFile自身を表します。ZIP内要素は相対pathを保って展開し、ZIP名と同じ単一root directoryは省略して指定できます。`.unitypackage`はコピーせずUnity packageとして取り込みます。

`ImportFileTargets`は保存済みTargetを読み、依存先から順に各Fileの`ImportFileEntries`を呼びます。両APIは`Task<AssetImportResult>`を返し、結果は成功・失敗・キャンセル、処理対象File ID、取り込んだGUID、失敗理由を保持します。キャンセル時は後続Fileとentryの開始を止めますが、Unityへ既に渡したUnityPackageの処理自体は停止できません。

Target未設定での取り込みは何も行いません。未所属またはアーカイブ済みのFileにTargetが設定されている場合は取り込みを拒否します。

Fileに依存先が設定されている場合は推移的な依存先を解決し、依存先から依存元の順に各Fileを1回だけ取り込みます。依存関係自体に順序は持たせず、順序が必要な場合は追加の依存関係で表現します。`.unitypackage`は完了を待ってから次のFileへ進み、失敗またはキャンセル時は後続の取り込みを停止します。

取り込みに成功すると、通常FileはUnity refresh後に解決したGUID、UnityPackageはpackage内のGUIDをFile単位で完全置換します。失敗またはキャンセル時は既存GUIDを変更しません。`GetFileImportedAssetGuids`はFile単位、`GetItemImportedAssetGuids`は所属Fileを集約したItem単位のGUIDを返します。`GetImportedAssetAssociations`は全関連または指定GUIDに一致するItem ID・File ID・取り込み時刻を返します。GUIDを使用したAsset保護は仕様対象外です。

## 永続化

AssetManagerはSQLiteを1ファイル使用します。DBはUser Settingsの`ee4v 共通データの保存先`直下に`asset-manager-v1.db`として保存します。論理構造は次のとおりです。

```text
file       ── 0..1 item
file       ── 0..* file_target
file       ── 0..* file_dependency
file       ── 0..* file_imported_asset_guid
item       ── 0..* tag       （item_tag経由）
collection ── 1..* collection_node
```

内部IDは文字列、時刻はUTCのISO 8601文字列、真偽値は`0`または`1`で保持します。`source_id`はSourceが定める不透明な安定IDです。スキーマバージョンは1に固定し、`PRAGMA user_version`が異なるDBは削除して再作成します。接続ごとに`PRAGMA foreign_keys = ON`を設定します。

### `item`

| column | 保持する値 | 制約 |
| --- | --- | --- |
| `id` | Item ID | PK |
| `name` | Item名 | 空白不可 |
| `description` | Item説明 | NULL不可。未設定は空文字 |
| `thumbnail_url` | サムネイル取得元URL | NULL可 |
| `source_type` | Itemを作成したSource | NULL可。`eagle`または`ee4v` |
| `source_id` | Source内の安定ID | NULL可 |
| `is_archived` | 論理削除状態 | `0`または`1` |
| `created_at` | 作成時刻 | UTC |
| `updated_at` | 更新時刻 | UTC |

手動Itemは`source_type`と`source_id`を両方NULLにします。Source由来Itemは両方に値を持ち、組み合わせを一意にします。FileやTagを持たないItemも保存できます。

### `file`

| column | 保持する値 | 制約 |
| --- | --- | --- |
| `id` | File ID | PK |
| `item_id` | 所属Item ID | NULL可。Item FK |
| `file_name` | File名 | 空白不可 |
| `extension` | 拡張子 | NULL可 |
| `source_type` | FileのSource | NULL不可。`eagle`または`ee4v` |
| `source_id` | Source内の安定ID | NULL不可 |
| `source_path` | 現在の実体path | NULL可 |
| `is_archived` | 論理削除状態 | `0`または`1` |
| `created_at` | 作成時刻 | UTC |
| `updated_at` | 更新時刻 | UTC |

`source_type`と`source_id`の組み合わせを一意にします。`item_id`がNULLのFileは未所属です。Sourceが異なるFileは同じ実体に見えても統合しません。Source接続部はdirectoryをFileへ変換しません。

### `file_target`

| column | 保持する値 | 制約 |
| --- | --- | --- |
| `file_id` | Targetを所有するFile | File FK |
| `target_path` | File内の相対path | NULL不可。大文字小文字を区別せずFile内で一意 |

`file_id`と`target_path`を複合PKにします。空文字はFile自身を表します。空文字以外は区切りを`/`に統一し、絶対pathとpath traversalをDB制約でも拒否します。File削除時はTargetを連鎖削除します。

### `file_dependency`

| column | 保持する値 | 制約 |
| --- | --- | --- |
| `dependent_file_id` | 依存元File | File FK |
| `dependency_file_id` | 依存先File | File FK |

両列を複合PKにします。自己依存はCHECK制約、間接的な循環は挿入triggerで拒否します。File削除時は依存元と依存先の関係を連鎖削除します。アーカイブでは関係を維持します。

### `file_imported_asset_guid`

| column | 保持する値 | 制約 |
| --- | --- | --- |
| `file_id` | 取り込み元File | File FK |
| `asset_guid` | Unity Asset GUID | 32桁の小文字16進数 |
| `imported_at` | 取り込み完了時刻 | UTC |

`file_id`と`asset_guid`を複合PKにします。取り込み成功時だけFileのGUID一覧を1 transactionで完全置換し、File削除時は連鎖削除します。GUIDごとの保護状態は保持しません。

### `tag`と`item_tag`

`tag`はTag IDと小文字の完全pathを保持し、pathを一意にします。`item_tag`はItem IDとTag IDを複合PKとしてItemへの付与を表します。未使用Tagは関連更新と同じtransactionで削除します。親Tagの行は必須ではありません。

### `collection`と`collection_node`

`collection`はID、一意な名前、作成時刻、更新時刻を保持します。Itemとの所属関係や親子関係は保存しません。

`collection_node`は次の値を保持します。

| column | 保持する値 | 制約 |
| --- | --- | --- |
| `id` | Node ID | PK |
| `collection_id` | 所属Collection | Collection FK |
| `parent_node_id` | 親Node | rootだけNULL。同じCollection内のNode FK |
| `node_type` | `and`、`or`、`not`、`condition` | 列挙値以外を拒否 |
| `condition_type` | 条件の種類 | `condition`だけ保持 |
| `value` | 条件の引数 | `condition`では空白不可。それ以外はNULL |
| `sort_order` | 兄弟内の順序 | 0以上。兄弟内で一意 |

Collectionごとにrootを1つだけ許可します。親子の循環はDB triggerでも拒否します。式木はApplicationで検証してから1 transactionで完全置換します。

### 索引

PKと一意制約に加え、次の検索用索引を作成します。

- `file(item_id)`と`file(extension, item_id)`
- `item_tag(tag_id, item_id)`
- `collection_node(collection_id, parent_node_id, sort_order)`
- `file_dependency(dependency_file_id, dependent_file_id)`
- `file_imported_asset_guid(asset_guid, file_id)`

`file_target`と依存先一覧は複合PKを検索に使用します。

## 未実装範囲

- Inspector、Project Window装飾
- Eagleとee4v以外のSource

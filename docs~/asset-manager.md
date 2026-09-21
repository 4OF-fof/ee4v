# AssetManager

AssetManagerはItem、File、Target、Dependency、Tag、Collection、取り込み済みAsset GUIDをSQLiteへ保存し、Editor APIとUIを提供します。

この文書は実装時に守る契約を記載します。現在の実装との差分は末尾の[未実装範囲](#未実装範囲)を正とし、人間向けの利用案内には未実装の内容を掲載しません。

## UI契約

### Information Window

Information Windowはマウスホイールなどによるスクロール操作を維持したまま、外側のスクロールバーを表示しません。Information見出し用のツールバーは置かず、上端に標準余白を設けてItemサムネイルを幅の約80%、最大288pxの正方形プレビューとして表示します。その上にItem名や種別の見出しは置きません。複数のItemを選択した場合は末尾3件までをmasterと同じオフセットと回転角のstack形式で表示します。最後に選択したItemを最上面へ置き、編集フォームは表示しません。単一選択ではFileとAsset GUIDを表示せず、名前、説明、Tagをラベルの下に値を置く共通配置と標準の項目間隔で表示します。Item詳細と同じ情報セクションも表示し、Boothストア、Booth商品リンク、Fileの合計サイズ、作成日時、更新日時を確認できます。説明欄には共通`InputField`を使用し、144pxの固定高を超える内容には欄内スクロールバーを表示します。Eagle由来Itemでは名前と説明だけを読み取り専用にし、長い説明も入力欄内でスクロールして確認できます。TagはSourceに関係なく、masterと同じチップ表示と専用UIから編集できます。タグ選択画面は追加ボタンを押したカーソル位置を起点に開く300×400pxのポップアップとし、見出し、検索欄、使用数順の件数付き`TagPill`、新規作成用の`TagPill`を表示します。キーボード操作では追加ボタンを起点にします。検索・閉じるアイコンとタグ名は各コントロールの高さ中央へ揃えます。タグ選択画面、Target選択画面、コレクション作成画面は共有`CustomPopup`を使用し、外枠、背景、ヘッダー、フッターとドロップダウン配置を共通化します。名前と説明はフォーカスが外れた時点、Tagは選択画面を閉じた時点に保存し、保存ボタンは置きません。

### Window構成とNavigation

`ee4v/Window/Asset Manager/Open All`からNavigation、Main、Informationの3つの独立Windowをまとめて開きます。統合Windowは提供しません。Navigation Windowは240px、Information Windowは300pxを最小幅とし、Main Windowは作業領域へ追従します。ナビゲーション上部では全件、未所属、アーカイブ、タグを切り替えます。コレクションは件数付きのフォルダ行として並び、見出し右端の追加ボタンから作成します。タグ画面では既存タグと該当Item数を表示し、タグを選ぶとそのタグまたは下位タグを持つItemへ絞り込みます。Main Windowのツールバーは戻る・進む履歴とパンくずリストを左端に置き、列数スライダーを中央に配置します。パンくずは現在位置の末尾だけを表示し、2階層以上ではホバー中にフルパスを表示します。ホバー表示の親階層を選ぶとその位置へ移動できます。画面名はパンくずだけに表示し、重複する見出しと件数は置きません。右端の再読み込みボタンはEagleとee4vを順番に再同期した後に一覧を更新します。片方の同期に失敗しても残りを実行し、同期エラーはConsoleへ表示します。その左の検索欄と合わせて操作領域としてまとめ、Main Window下部に状態フッターは置きません。検索欄のFluent UI System Icons `Search`を選ぶとUnity標準メニューを開き、名前、説明、タグを検索対象へ含めるか個別に切り替えられます。未所属File一覧では名前の指定をFile名へ適用します。ItemをダブルクリックするとMain WindowをItem詳細へ切り替えます。Item詳細は左の検索付きFile Treeと右の詳細表示からなる2ペイン構成です。左ペインの先頭には概要を置き、その下へ所属Fileをルート、ZIPとUnityPackageの内容を子階層として表示します。右ペインは概要選択時にItemの名前、Tag、Item Target、File数、形式とSourceを表示します。情報セクションにはリンク付きBoothストア名、Booth商品リンク、Fileの合計サイズ、作成日時、更新日時を左寄せで表示します。Boothメタデータまたは有効なURLがない項目は表示しません。File選択時は実体単位のDependency Targetを含む設定を表示し、GUID一覧は表示しません。内容選択時はPath、種別、Size、取り込み済みかを表示します。UnityPackage内のAssetは解析結果のGUIDと取り込み済みGUIDを照合します。ZIP内のUnityPackage実体は、そのFileに取り込み済みGUIDがある場合に取り込み済みとして扱います。ZIPなど解析結果にGUIDがないその他の形式は、保存済みGUIDのProject pathから選択実体への対応を解決します。File TreeのFileまたは内容を右クリックすると選択状態を変えずにコンテキストメニューだけを開き、Item Targetの追加・解除と1実体のImportを実行できます。ZIP自身、ZIP内のZIP、DirectoryではImportを無効にします。Item詳細を開いている間、Information WindowはMain WindowのFile選択に影響されず、そのItemを表示し続けます。パンくずまたは履歴から元の一覧へ移動できます。再読み込みにはmasterと同じMicrosoft Fluent UI System Iconsの`Arrow Clockwise`を使用し、必要なアイコンだけをruntime assetとして保持します。Item一覧は表示範囲の行だけをプールする可変列Gridです。masterと同じ1〜12列を設定範囲とし、表示領域から算出した推奨最小列数をスライダーの下限へ反映します。スライダーと±ボタンは、入力処理内で表示中の行を新しい列数へ組み替えます。全行の再生成や次のUI更新を待ちません。表示幅と高さが変わると列間隔、カード幅、固定行高もまとめて再計算します。カード名は横方向と縦方向の中央へ揃え、表示幅を超える場合は末尾を`…`で省略します。カード選択とサムネイル表示にも対応し、選択中のカードへホバーした場合は青系の選択表現を維持した専用スタイルを使用します。Itemの編集とTag設定、Fileの登録と所属変更、Item TargetとDependency Targetの設定、Archive解析とUnity projectへの取り込みを同じ画面から実行できます。Eagleとee4vのパス設定はAssetManager画面には置かず、`Preferences/4OF/ee4v`のUser Settingsで管理します。

### Item詳細と派生Asset

Item詳細はHTMLモックの比率と視覚階層を基準にします。File Treeは詳細領域の31%を基本幅とし、見出しとFile登録ボタン、検索欄、30pxのアイコン付き行を配置します。右側は上下20px、左右24pxを基準に余白を取り、概要の先頭へ128pxのサムネイル、15pxのItem名、12pxのTagをまとめます。ItemのImportボタンは名前とTagに隣接する概要上部へ常に表示します。状態バッジはアーカイブ時だけ表示し、有効時は表示しません。その下へFile数、形式、データソースを3列で表示し、縦線は列間の区切りだけに置きます。取り込み設定はItem Targetを境界線付きツリーで表示し、TargetはFile Treeと同じアイコンを実体名の前だけに付け、`実体名(File名)`で表示します。Target一覧右端の編集ボタンは一覧の高さにかかわらず1行の高さで中央へ配置し、検索付きFile Tree形式のポップアップを開きます。FileまたはFile内実体のトグルからItem Targetを追加・解除します。Target一覧はGroupを親、そのGroupのTargetだけを子とする30px行で構成します。GroupなしのTargetは専用Groupを置かずルート直下へ表示し、複数行では項目名を一覧全体の高さ中央へ揃えます。ルート直下のTarget同士を重ねるとGroupを作成し、既存GroupまたはTargetへのドロップで移動・統合します。Target項目名または一覧の空き領域へドロップするとGroupを解除します。一覧には選択欄や操作説明を置かず、Importボタンを押した時だけGroupごとにTargetを1件選ぶポップアップを表示します。取り込み設定と情報の間には派生アセット一覧を置き、Item一覧と同じ形式のGridで表示します。派生アセットがない場合は追加アイコン付きカードを1枚だけ表示し、選択すると作成画面へ遷移します。この画面は履歴とパンくずには参加しません。通常ツールバーとFile Treeも表示せず、左上の戻るボタンだけでItem概要へ戻ります。戻るボタンは透過背景のアイコンだけで構成し、第三者製Fluent UI System Iconsの`Arrow Left`を使用します。作成画面では名前、Prefab、説明を入力します。Prefab欄は`CustomPopup` componentを土台にした専用selectorです。現在のItemへ取り込まれたGUIDから解決したPrefabだけを平坦な一覧で表示します。候補はPrefab名またはProject内パスで検索でき、選ぶとselectorへ反映して一覧を閉じます。ドラッグなどで直接入力した場合も、選択AssetのGUIDが現在のItemの取り込み済みGUIDと一致するときだけ受け付けます。候補がなければ作成を無効にします。作成したルートPrefab Variantは`Assets/!ee4vAsset/Variant/<名前>/<名前>.prefab`へ保存します。参照スロットを持つMaterialだけをMaterial Variantとして`Assets/Materials`へ保存し、作成Prefab内の参照を差し替えます。Animator Controllerを含むその他の参照アセットはコピーせず、元を共有します。説明と親Item IDは作成Prefabのimporter metadataへ保存し、概要の派生一覧からPrefabを選択できます。個別Fileの依存関係はFile詳細の設定一覧から、Item Targetと同じ検索付きFile Tree形式のポップアップで実体単位に編集します。Dependency Targetの候補は所属Itemを親とする階層で表示し、設定元Fileと同じItemを先頭に置き、別Itemのグループとの間に区切り線を表示します。Target選択Tree上にポインターがある間はArchive解析による再構築を保留し、Treeから離れた時に最新状態を1回だけ反映します。File詳細は形式付き見出し、操作ボタン、設定一覧、内容操作の順に配置します。長い見出しとSource pathは右端で切り、全文をツールチップで確認できるようにします。右ペインはInputFieldと同じ細いスクロールバーで縦方向だけをスクロールします。

### 派生Asset作成フォーム

作成フォームは左右を同じ幅の2列に分け、左列へ大きなPrefabプレビューとPrefab selector、右列へ名前、説明、作成ボタンを配置します。左のプレビューはProject内のPrefab実体を`PreviewRenderUtility`で表示領域に合わせて描画し、拡大したキャッシュサムネイルは使用しません。背景には明暗2段階のグリッドを表示し、右上の太陽または月アイコンの背景ボタンで切り替えます。背景ボタンとリセットボタンは全状態で背景を透明にし、太陽と月は背景上で判別しやすい高コントラスト色を使用します。明るい表示はUnityのScene Viewに近い青みがかったグレーを使用します。表情エディタと同じく右ドラッグで回転、中ドラッグで移動、ホイールでズームし、リセットボタンで実際に表示されるアセット全体が小さな余白で収まる初期表示へ戻します。名前と説明には共通`InputField`を使用します。Prefab selectorの候補一覧にはUnityが生成した軽量なPrefabサムネイルを表示します。

### 並び替え

Item Gridでは検索欄の左にソートボタンを表示します。Unity標準メニューから名前、作成日、更新日、File数を選択でき、逆順指定で方向を反転します。同値の場合は名前とIDを使って安定した順序にします。ボタンにはMicrosoft Fluent UI System Iconsの`Arrow Sort`を使用し、固定したvendor版の元SVGと生成PNGだけを保持します。

### Collection

コレクション見出しの追加ボタンはmasterと同様にボタン直下へ入力ポップアップを開きます。コレクション名と条件グループを入力し、グループごとにANDまたはORを選択できます。条件グループは任意の深さへ入れ子にでき、各条件またはグループをNOTで反転できます。既存コレクションの編集と削除はナビゲーション上のコレクションを右クリックして行います。コレクション画面には条件要約と操作ボタンを表示しません。編集には作成時と同じポップアップを使用し、Informationペインはコレクションの作成と編集に使用しません。条件の選択肢はAssetManagerの表示言語に合わせて翻訳します。

### Navigationと同期

ナビゲーションには独立した上部ツールバーを置かず、全件を左ペインの先頭に表示します。

AssetManager画面にはデータソースと取り込みの専用ページを置きません。Eagleとee4vの同期は再読み込み操作とセッション開始時に行い、取り込みや関連付け検索のAPIはUIと独立して提供します。

### 未所属File

未所属File一覧ではソートボタンを維持します。File画面のメニューは名前、作成日、更新日と逆順を表示し、File数は表示しません。Item一覧からFile数指定のまま移動した場合はFile名順として扱います。Item詳細ではソート、検索、Grid列数の操作を表示しません。

### Iconとi18n

AssetManagerが描画するアイコンはMicrosoft Fluent UI System Iconsの固定版へ統一し、Unity組み込みアイコンを混在させません。ツールバー、検索と解除、列数の増減、ナビゲーション、Tagの追加と削除、折りたたみ、通知、パンくず区切りで使う分だけをvendor資産として保持します。Tag選択画面には共有`SearchField`を使用し、その検索と解除にも同じFluentアイコンを指定します。アイコンの組み立ては`AssetManagerControls`へ集約し、文字記号は使用しません。

AssetManagerの固定表示文字はAssetManagerスコープのi18nカタログから取得します。英語と日本語を収録し、言語設定または翻訳アセットの再読込時には開いているAssetManager画面とウィンドウタイトルを再構築します。Item名、File名、コレクション名などの保存データは翻訳しません。

### 選択とcontext menu

Gridカードは通常クリックで単一選択、Ctrlクリックで選択の追加と解除、Shiftクリックで直前の基準Itemから範囲選択します。CtrlとShiftを併用した場合は現在の選択へ範囲を追加します。選択はItem ID集合として保持するため、スクロールによるカード再利用後も維持します。情報ペインには最後に操作した選択Itemを表示します。Itemカードの右クリックメニューからImport、アーカイブ、復元、削除を実行します。Importは単一ItemかつItem Target設定済みの場合だけ有効にします。アーカイブ済みItemでは復元と削除だけを表示し、詳細画面を開けないようにします。情報ペインの名前、説明、Tagも読み取り専用にします。削除は選択対象がすべてアーカイブ済みの場合だけ表示し、実行前に確認ダイアログを表示します。未選択カードを右クリックした場合はそのItemを選択し、選択済みカードの場合は現在の複数選択を操作対象にします。情報ペインにはアーカイブと削除の操作ボタンを置きません。

Itemに所属するFileはItem詳細のFile Treeへ表示し、未所属Fileは縦方向の行形式で一覧表示します。File用のGridViewは使用しません。masterのFile Treeと同様にFile名を左へ、拡張子を右端のメタ情報として表示します。拡張子がない場合はSource種別を表示します。行は通常、ホバー、選択の状態を背景色で区別します。

Item GridはEscapeまたはカード外の空白を左クリックすると全選択を解除し、情報ペインを未選択表示へ戻します。Ctrlで選択中の最後のカードを解除した場合も同じ未選択状態になります。Item詳細のFile Treeで選んだ内容は中央右ペインへ表示します。未所属File一覧でFile行を選んだ場合だけ、InformationペインへFile詳細を表示します。

### Window間の状態共有

3つのWindowは同じ`IAssetManager`と表示状態を共有するため、Navigationでのページ切替とMainでのItem選択がほかのWindowへ反映されます。各Window型は同名のスクリプト資産として保持し、Unityのレイアウト保存から再起動後も復元します。個別に開く場合は`ee4v/Window/Asset Manager/Navigation`、`Main`、`Information`を使用します。

### Storyとcomponent境界

実画面と同じ`AssetManagerView`は`ee4v/Debug/Catalog`の`Domain/AssetManager/AssetManagerView` Storyでサンプルデータを使って確認できます。同じGroupには分離表示とコレクション編集Popupを配置します。`AssetItemGridView`と`SearchableFileTree`は複数項目を管理するため`Domain/AssetManager/Collections`に配置します。

画面へ組み込む小さな部品もCoreと同じ責務で分類します。`Inputs`にはBreadcrumb、Tag Field、文字入力、列数入力、列挙値入力、Grid Card、Filter Editor、編集可能なSetting Rowを配置します。`Displays`にはNotice、Thumbnail Stack、詳細のFactとKey Value Rowを配置します。`Containers`には詳細のHeader、Section、Setting Listを配置します。複数の実部品をまとめた`AssetManagerControls`や`AssetDetail components`というStoryは作りません。`AssetTagField`はCoreの`TagPill`を組み合わせ、選択済みTagを内容幅のpillとして表示します。共有外枠は`Containers/CustomPopup` Storyで確認できます。Storyの操作はSQLiteや外部Sourceを変更しません。

内部操作部品は`Editor/AssetManager/UI/AssetManagerControls.cs`と`UI/Components`に置き、`AssetManagerView`はCore componentを組み合わせます。文字はすべて`UiTextFactory`を通します。Toolbarは`ActionBar`、Navigation一件は`NavigationItem`、件数は`Badge`、サムネイル枠は`PreviewContainer`、ラベル付き入力は`FormInput`を使用します。関連する複数の入力を囲う場合は`InputGroup`へまとめます。検索欄は先頭操作に対応した共有`SearchField`を使用します。`SearchableFileTree`は共有`SearchableTreeView<FileTreeNode>`から派生し、File解析と行表示だけを追加します。詳細の見出し、セクション、概要値、設定行は`AssetDetailComponents.cs`へ集約します。File Treeと詳細領域の追加スタイルは`searchable-file-tree.uss`と`asset-detail.uss`へ分け、`asset-manager.uss`は画面全体の構成を担当します。Domain固有の入力、一覧行、Card、詳細PanelもCoreと同じ境界線、面、角丸、hover、選択状態を使用します。Fluentアイコンの読込とAssetManager固有の選択状態だけを機能側へ残します。UIからの更新操作中はmanagerの変更通知を蓄積し、操作完了時に一覧と詳細を1回だけ再描画します。

## 構成

| アセンブリ | 役割 | Unity依存 |
|---|---|---|
| `Ee4v.AssetManager.Contracts.Editor` | 公開モデル、要求、`IAssetManager`、変更通知 | なし |
| `Ee4v.AssetManager.Domain.Editor` | 入力の正規化、Collection式の検証と評価 | なし |
| `Ee4v.AssetManager.Application.Editor` | 公開APIのユースケース、検索、同期の調整 | なし |
| `Ee4v.AssetManager.Infrastructure.Editor` | SQLite保存、Sourceの読み書き、Unityへの取り込み | あり |
| `Ee4v.AssetManager.UI.Editor` | 管理画面、backend操作との接続、UI Story | あり |
| `Ee4v.AssetManager.AssetProtection.Editor` | 取り込み済みAssetの直接編集と保存の防止 | あり |

公開入口は`AssetManagerFactory.Open(databasePath)`です。返された`IAssetManager`をUIなどの利用側が保持します。取り込み済みAssetとの関連だけを参照する機能は`IImportedAssetAssociationProvider`へ依存します。外部SourceとDBはApplicationの小さなportの外側に置き、APIの処理から実装詳細を分離しています。

SQLite接続はmasterと同じ`Ee4v.SQLite.Editor`境界とvendor済みの`sqlite-net`、`SQLitePCLRaw`、`e_sqlite3`を再利用します。現行のnative plugin対象はWindows x86_64 Editorです。

## 公開API

`IAssetManager`は次を提供します。

- 条件式によるItem検索、Collection検索、Item単体のCollection一致判定
- 手動Itemの作成、取得、更新、アーカイブ、削除
- Itemサムネイルの単体・一括取得
- Fileの取得、ee4v Sourceへの登録、所属変更、アーカイブ、削除
- Item Targetの取得、完全置換、Group設定、Item単位でのUnity projectへの取り込み
- 実体単位のFile Dependency Targetの取得と完全置換
- ZIPとUnityPackageの同期・非同期内容解析
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
| `ItemTargetsChanged` | `SetItemTargets`、`SetItemTargetGroup` | 対象Item | Targetが参照するFile |
| `FileCreated` | `RegisterFile`、`ImportEe4vFile` | 作成File | 所属Item |
| `FilePlacementChanged` | `SetFileItem` | 対象File | 変更前と変更後の所属Item |
| `FileArchiveChanged` | `SetFileArchived` | 対象File | 所属Item |
| `FileDeleted` | `DeleteFile`、所属Fileを持つ`DeleteItem` | 削除File | 削除前の所属Item |
| `FileDependenciesChanged` | `SetFileDependencies`、依存先を削除する`DeleteFile`／`DeleteItem` | 依存関係が変わった依存元File | なし |
| `FileImportedAssetGuidsChanged` | 成功した`ImportFileEntries`、`ImportItemTargets` | 取り込みが完了したFile | 所属Item |
| `CollectionCreated` | `CreateCollection` | 作成Collection | なし |
| `CollectionUpdated` | `UpdateCollection` | 更新Collection | なし |
| `CollectionDeleted` | `DeleteCollection` | 削除Collection | なし |
| `SourceSynchronized` | 成功した`SyncEagle`、`SyncEe4v` | 影響を受けたItem | 影響を受けたFile |

`SourceSynchronized`は`SourceType`で`Eagle`または`Ee4v`を示し、作成・更新・削除されたItemとFileに加えて、依存関係の連鎖削除や未所属化で影響を受けたIDを1回の通知にまとめます。IDが空なら同期による変更はありません。同期の読み取りに失敗してDBを変更しなかった場合と、Target未設定または取り込み失敗でGUIDを変更しなかった場合は通知しません。読み取りAPIと`AnalyzeFile`も通知しません。

アーカイブ、削除、Fileの所属変更、Tag設定は対象IDを複数指定できます。アーカイブは`is_archived`だけを変更し、`false`を指定すると復元します。通常のItem検索とFile一覧はアーカイブ済みを除外し、必要な場合だけ明示的に含めます。

ItemとFileの削除は対象がすべてアーカイブ済みの場合だけ許可します。UIは非アーカイブItemとFileに削除操作を表示しません。削除は実行前に確認ダイアログを表示します。Item削除は所属Fileも削除します。File実体の削除はee4v Sourceだけを許可し、JSONとFileを含むSource directoryを削除します。削除時はSource directoryを`<library>/.trash/`へ退避してからDBを更新し、DB更新に失敗した場合は元へ戻します。DB更新に成功した場合だけ退避内容を削除します。Eagle由来FileやEagle由来Fileを含むItemの削除は拒否します。ee4vへの新規登録後にDB更新が失敗した場合も、作成したSource directoryを削除します。

Eagle同期で作成したItemの名前と説明はEagleを正本とし、`UpdateItem`による変更を拒否します。TagはAssetManagerを正本として変更でき、Eagle由来Tagは取り込みません。Eagle由来Fileの所属もEagle folderを正本とし、`SetFileItem`による変更を拒否します。アーカイブ状態、Tag、Target、DependencyはAssetManager固有の状態として変更できます。

## 規則

- Tagは先頭の`#`を除き、segment単位でtrimして小文字へ統一する
- 空segmentを含むTag pathを拒否し、未使用Tagを関連更新と同じtransactionで削除する
- Collection名は一意とする
- ANDとORは2子以上、NOTは1子、Conditionは子なしとする
- Collection条件は名前、説明、Tag、未アーカイブFileの拡張子を対象とする
- 名前と説明は大文字小文字を区別しない部分一致とする
- Tag条件`foo`は`foo`と`foo/*`へ一致する
- 拡張子は先頭の`.`を除き、小文字へ正規化する
- Targetの空pathはFile自身、空path以外はFile内の相対pathを表す
- ZIP自身とFile内のZIPは取り込みTargetに指定できない
- File TreeではFile自身またはFile内に存在するDirectory以外のentryをItem Targetとして複数選択できる
- Item Target選択ポップアップには概要を表示しない
- Item TargetのGroup名はItem内で共通に扱い、同じGroup名のTargetを選択肢とする。GroupなしのTargetは常に取り込む
- Target pathは区切りを`/`へ統一し、絶対path、空segment、`.`、`..`を拒否する
- File Dependency Targetは依存先FileとFile内pathの組で保持し、別Itemと別SourceのFileも許可する
- 自己依存と直接または間接的な循環を拒否する
- Fileとして登録できる実体は通常Fileだけとし、directoryを拒否する

## Source同期

AssetManager UIは`Preferences/4OF/ee4v`のUser Settingsから、Eagleライブラリのパス、Eagleの同期対象ルート、ee4v共通データの保存先を操作時に読み取ります。設定値はAssetManager内へ複製しません。ee4v共通データの保存先を変更した場合はmanagerと共有表示状態を破棄し、開いているAssetManager Windowを新しいDBで再構築します。公開APIを直接利用する場合は、従来どおり各requestへパスを指定できます。

Unityエディターのセッション開始時には、存在するEagleとee4vのSourceを1回ずつ自動同期します。スクリプトの再コンパイルでは同じセッション中の同期を繰り返しません。各Sourceの自動同期はUser Settingsで個別に無効化できます。バッチモードでは利用者のDBを変更しないため実行しません。

既定ではEagle library内の`VRCAsset`配下のfolderをItemとして同期します。folder IDとEagle item IDを安定IDとして使用するため、名前やpathの変更ではDB内のIDを維持します。Itemの名前、説明、サムネイルURLとBooth商品・ストア情報は同期のたびにEagleの値へ合わせます。Eagle itemに付いたTagはItemへ取り込まず、AssetManagerで設定したTagを再同期後も維持します。同じEagle itemが複数folderに属する場合は、1つのFileが複数Itemに属さないよう最初のItemだけへ関連付けます。完全同期で見つからなくなったItemとFileはDBから削除します。削除されたItemへ別Source由来のFileが所属していた場合、そのFileは削除せず未所属へ戻します。directory payloadとBooth metadata JSONはFileとして登録しません。Eagleのfolder metadataはUnityのシリアライズ深度制限を受けないJSONパーサーで読み込み、10階層を超えるfolderも同期対象にします。

Eagleとee4vから取り込むItem名、説明、File名と、ee4vから取り込むTagは保存前にUnicode NFKCで正規化します。全角文字と数学英数字は通常の文字へ寄せ、BMP内の星記号は`*`、著作権記号はASCII表記へ変換します。対応する通常文字がない`OtherSymbol`、補助文字、異体字セレクター、ゼロ幅結合子、不要な制御文字は除去します。これによりUI用フォントに存在しない絵文字や装飾記号が表示文字列へ入ることを防ぎます。

`EagleSyncRequest`でlibrary pathと対象rootを指定できます。対象rootが存在しない場合は失敗として扱い、既存状態を変更しません。対象rootが存在して子Itemが0件の場合は正常な空スナップショットとして扱います。読み取りまたは形式の問題は失敗結果として返し、DBへの反映は1 transactionで行います。

`AssetSyncResult`はItemとFileを分けて作成・更新・削除IDを返し、影響を受けたItem／File ID、未変更件数、失敗理由も保持します。Tagだけが変わったItemも更新として数えます。

ee4v Sourceは`<library>/Assets/<source-id>/`ごとに`metadata.json`と実体Fileを1つずつ保持します。取り込み時は一時directoryへ両方を書き、完成後にSource directoryへ移動します。`RegisterFile`はFileをSourceへコピーし、指定したItemまたは未所属Fileとして登録します。`ImportEe4vFile`はItemとFileをまとめて作成します。

`SyncEe4v`はJSONから内容を復元します。File単独で登録したSourceは既存のItem所属を維持しますが、DB再作成後は未所属Fileとして復元します。ee4v Itemの名前、説明、TagをAPIで変更した場合はJSONも更新します。不正なJSONが1件でもある同期はDBへ反映しません。

Source同期は既存のアーカイブ状態を変更しません。完全同期に含まれないSource由来ItemとFileは、他の同期更新と同じtransactionでDBから削除します。ee4v Fileを削除した場合はSource directory自体がなくなるため、後続の同期でも復元されません。

## Itemサムネイル

`AssetItem.ThumbnailUrl`はItemに対応するサムネイルの取得元です。Eagle同期では同FolderのBooth metadataにある`thumbnailUrl`を正本として保存します。`GetThumbnail`と`GetThumbnails`は`Task`を返し、HTTP通信とcacheの読み書きを呼び出し元を止めずに行います。`CancellationToken`で待機中の通信と一括取得を中止できます。画像はDBと同じdirectoryの`cache/asset-manager/thumbnails/`へItem IDとURL単位で保存します。取得元がない場合や取得に失敗した場合は例外ではなく`AssetThumbnail.Found = false`と理由を返します。一括取得は重複Item IDを除外し、最大4並列で未cache画像を取得します。Item一覧は未取得のItemだけを表示順に`GetThumbnail`へ要求し、1件の取得が完了するたびに該当カードを描画します。取得結果は画像の欠落も含めて`AssetManagerView`単位のCore `CachedImageCache`が保持するため、画面遷移や再検索では再取得しません。Item一覧とItem詳細は同じcacheを使用します。Source同期または再読み込み操作では保持内容を破棄し、最新のURLと取得結果を反映します。画面遷移または再検索時には進行中の要求を中止します。デコード済みTextureも同じcacheで共有し、スクロールでカードが再利用されても再デコードしません。

Importに成功すると、取り込み済みAsset GUIDからFileごとに実Assetの共通親folderを求め、対応するItemのサムネイルをProject Styleの初期アイコンとして設定します。`<shop名>/<item名>/中身`と`<item名>/中身`のどちらでもitem名folderへ適用します。UnityPackageに含まれるshop名folderのmetaは適用先にしません。実Assetを含まない場合はGUIDが指すfolderを使用します。対象folderが親子関係にある場合は最上位だけへ適用します。複数Itemが同じGUIDを持つ場合は最後に取り込んだ関連付けを使用します。既にProject Styleのアイコンがあるfolderは上書きせず、自動設定後も利用者がProject Styleから変更または解除できます。サムネイルは`Assets/ee4v/Generated/AssetManager/Thumbnails/`へTexture assetとして保存し、共有元の同期後に既存の生成Textureを更新します。以前の保存先にある生成Textureは更新時に新しい保存先へ移動し、GUIDを維持します。`Preferences/4OF/ee4v`の「Import時にサムネイルをProject Styleへ設定」から、以後のImportに対する自動設定を無効化できます。Project描画中にAssetManagerのDB、filesystem、サムネイルは読み込みません。

Gridは表示範囲の行だけを保持します。スクロール位置が同じ行内にある間はカードを再バインドしません。先頭行が変わっても表示範囲に残る行は維持し、新しく見える行だけをプールから再利用します。

## File内容の解析

`AnalyzeFile`はFile実体を同期的に読み、`AnalyzeFileAsync`は同じ解析をバックグラウンドで実行してZIPまたはUnityPackageの内容を返します。非同期APIは`CancellationToken`を受け取り、File Treeの再構築や画面移動時に不要になった解析を中断します。UIはFile一覧を先に表示し、解析結果をFile IDとSource path、更新日時の組み合わせで現在のItem内だけにキャッシュします。Target選択画面は詳細画面のキャッシュを初期表示に再利用し、未解析のArchiveは1件の解析が終わるたびに階層とトグルを反映します。通常のFile Treeは全Archiveの解析後に階層をまとめて反映します。DBアクセスは呼び出し元で完了させてSQLite接続を別スレッドへ持ち出しません。Archive走査と階層データ構築はUIスレッドから分離し、Unity UIへの反映だけをメインスレッドで行います。ZIPは各entryの相対path、種別、非圧縮sizeを返します。全entryがZIP名と同じ単一root directory内にある場合は、そのrootをpathから省略します。UnityPackageはAsset path、FileまたはDirectoryの種別、size、Unity Asset GUIDを返します。ZIPとUnityPackage以外は解析対象外です。UIの解析ボタンは返されたpathをアーカイブ項目欄へ設定し、取り込み対象として編集できる状態にします。

## Targetの取り込み

`SetItemTargets`は1つのItemに対するItem Target一覧を、参照するFile IDとFile内pathの組み合わせで完全置換します。空の一覧で未設定へ戻し、同じTargetの大文字小文字違いは重複として除外します。置換前後に残るGroup名は保持します。`SetItemTargetGroup`はItem TargetへGroup名を設定し、空のGroup名でGroupから外します。ZIP自身を表す空pathと、拡張子が`.zip`の内部entryは拒否します。

`SetFileDependencies`は複数の依存元Fileを受け取り、全Fileの依存Targetを1 transactionで同じ一覧へ完全置換します。依存Targetは依存先File IDとFile内pathの組で指定し、同じ依存先Fileの複数実体を保持できます。複数Fileを同時に変更した結果も含めて循環をFile単位で検証し、失敗時はどのFileも変更しません。

`ImportFileEntries(fileId, paths)`は指定したpathだけを`Assets/<Item名>/<File名から拡張子を除いた名前>/`へ取り込み、保存済みItem TargetとDependency Targetは変更しません。これにより`AnalyzeFile`で得たZIP内の1要素を一時的に選んで取り込めます。空pathはFile自身を表しますが、File自身がZIPの場合は拒否します。ZIP内要素は相対pathを保って展開し、ZIP名と同じ単一root directoryは省略して指定できます。内部のZIPも拒否し、`.unitypackage`はコピーせずUnity packageとして取り込みます。

`ImportItemTargets`は保存済みItem TargetからGroupごとに選択した1件とGroupなしの全Targetを取り込みます。対象Fileに依存関係がある場合は、各Dependency Targetの実体を依存先から順に取り込みます。選択漏れ、同一Groupの複数選択、別ItemやGroup外のTarget指定は拒否します。`ImportFileEntries`と`ImportItemTargets`は`Task<AssetImportResult>`を返し、結果は成功・失敗・キャンセル、処理対象File ID、取り込んだGUID、失敗理由を保持します。キャンセル時は後続Fileとentryの開始を止めますが、Unityへ既に渡したUnityPackageの処理自体は停止できません。

Target未設定での取り込みは何も行いません。未所属またはアーカイブ済みのFileにTargetが設定されている場合は取り込みを拒否します。

Fileに依存先が設定されている場合は推移的な依存先を解決し、依存先から依存元の順に各Fileを1回だけ取り込みます。依存関係自体に順序は持たせず、順序が必要な場合は追加の依存関係で表現します。`.unitypackage`は完了を待ってから次のFileへ進み、失敗またはキャンセル時は後続の取り込みを停止します。

取り込みに成功すると、通常FileはUnity refresh後に解決した取り込み先ルートfolderと各実体のGUID、UnityPackageはpackage内の全GUIDをFile単位で完全置換します。`.meta`を直接取り込んだ場合は対応するAssetのGUIDとして解決します。失敗またはキャンセル時は既存GUIDを変更しません。`GetFileImportedAssetGuids`はFile単位、`GetItemImportedAssetGuids`は所属Fileを集約したItem単位のGUIDを返します。`GetImportedAssetAssociations`は全関連または指定GUIDに一致するItem ID・File ID・取り込み時刻を返します。[Asset Protection](./asset-protection.md)はこの関連GUIDを保護対象として使用します。

## 永続化

AssetManagerはSQLiteを1ファイル使用します。DBはUser Settingsの`ee4v 共通データの保存先`直下に`asset-manager-v1.db`として保存します。論理構造は次のとおりです。

```text
file       ── 0..1 item
item       ── 0..* item_target
file       ── 0..* item_target
item       ── 0..1 item_booth_metadata
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

### `item_booth_metadata`

| column | 保持する値 | 制約 |
| --- | --- | --- |
| `item_id` | Booth情報を持つItem | PK、Item FK |
| `item_url` | Booth商品URL | NULL可 |
| `shop_name` | Boothストア名 | NULL可 |
| `shop_url` | BoothストアURL | NULL可 |

EagleのBooth metadataがあるItemだけに保存します。再同期でBooth情報がなくなった場合は行を削除します。

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

### `item_target`

| column | 保持する値 | 制約 |
| --- | --- | --- |
| `item_id` | Targetを所有するItem | Item FK |
| `file_id` | Targetの実体を持つFile | File FK。対象Itemへの所属が必要 |
| `target_path` | File内の相対path | NULL不可。大文字小文字を区別せずItem・File内で一意 |
| `group_name` | Item取り込み時の選択Group | NULL可。空文字と改行不可。大文字小文字を区別しない |

`item_id`、`file_id`、`target_path`を複合PKにします。同じItem内で`group_name`が一致するTargetを1つの選択Groupとして扱います。Fileが別Itemへ移動した場合はそのFileを参照するItem Targetを削除し、ItemまたはFileの削除時も連鎖削除します。空文字はFile自身を表します。空文字以外は区切りを`/`に統一し、絶対pathとpath traversalをDB制約でも拒否します。ApplicationはZIP自身を表す空文字と、拡張子が`.zip`の内部entryを拒否します。

### `file_dependency`

| column | 保持する値 | 制約 |
| --- | --- | --- |
| `dependent_file_id` | 依存元File | File FK |
| `dependency_file_id` | 依存先File | File FK |
| `target_path` | 依存先File内の実体path | NULL不可。大文字小文字を区別しない |

3列を複合PKにします。自己依存はCHECK制約、間接的な循環は挿入triggerで拒否します。同じ依存先Fileの異なる実体pathは複数保持できます。path制約とZIP拒否はItem Targetと同じです。File削除時は依存元と依存先の関係を連鎖削除し、アーカイブでは関係を維持します。

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
- `item_target(file_id, item_id)`

`item_target`と依存Target一覧は複合PKを検索に使用します。

## 未実装範囲

- Inspector、Project Window装飾
- Eagleとee4v以外のSource

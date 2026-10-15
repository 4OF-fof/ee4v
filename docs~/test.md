# テスト一覧

Variantの名前・説明・タグ編集と版保存では、Git全履歴の再取得を行わず対象索引だけを更新し、以前の保存版と他Variantを維持することを確認します。同内容の保存、metadata編集後の保存、索引の再構築でもタグ・作成日時・アーカイブ状態を維持します。ギャラリーは画像0件と画像ありの再取得でGit検索を繰り返さず、返却一覧の書き換えがcacheへ影響しないこと、画像追加・削除・並び替え、別sessionでの更新、loose refとpacked refの切り替えを次回取得へ反映することを手動確認します。新規作成画面の初期表示で全Prefabを読み込まず、選択・ドラッグの拒否、ベース解除と値の復元を維持することも確認します。保存・破棄のホバーは同じ状態で結果を再利用し、編集・Undo／Redo・Scene保存・対象変更後は最新の差分へ更新します。既存`AssetManagerApiTests`・`DerivedAssetCreatorTests`を回帰確認に使用し、新規テストケースは追加しません。

AssetManagerのアーカイブ・解除・削除は、既存`AssetManagerApiTests`でItemとFileの所有権、複数選択と変更通知を確認します。手動では保存済みVariantの操作が全VariantのGit履歴再読み込みを起こさず、対象の索引とアーカイブ表示だけを更新し、削除したVariantの履歴だけが索引から消えることを確認します。途中で失敗した複数操作でも完了済みの状態を一覧へ反映し、所有Itemとの同期を維持することも確認します。構成Prefabの「＋」は初回のPopup表示と候補取得完了を分けて計測し、2回目はキャッシュを使って候補取得のスピナーを表示しないことを確認します。Item更新・取り込みGUID変更・アセット追加／移動／削除・Manager切り替えによる再取得、取得中の終了、編集中Prefab自身の除外、選択時の循環参照検査と追加拒否も確認します。新規テストケースは追加しません。

CLIは[CLI動作確認](cli.md)に従い、起動中Unity 2022.3 EditorでMCP catalogとの全操作一致、引数、構造化失敗、画像、job・取消・reloadを確認します。Scene/Projectのwriterは隔離SceneとAssetへ実行し、元Sceneと共通Libraryを保持します。共通Library・FBX分類・照明preset・制作履歴などProjectをまたぐwriterはCLI導入時の実機検証から除外します。新規テストケースは追加しません。

MCPのauthoring操作は、既存FaceExpressionTests・DerivedAssetCreatorTests・AssetManagerApiTestsで共有の曲線・Prefab・Library・履歴処理を回帰確認します。手動ではbody調査／dryRunでpreset・Sceneを変更しないこと、元Prefab基準の倍率とViewPosition連動、個別・GroupのBlendShape同期／remap、追加装着の推奨先・外部参照拒否・既存設定保持・Undoを確認します。Collectionの検索tree、Item／Fileの所有権とArchive、制作物の新規作成・metadata・試着、Projectにない保存版の一覧・復元、dirty Sceneの明示的な破棄指定、Galleryの画像検証とID・順序、animation曲線の除去・再有効化とrevisionも確認します。新規テストケースは追加しません。

MCPの入力・参照・transportは手動で、必須項目や型の欠落、未知の項目、操作別入力の不一致が書き込み前に拒否されること、未発行参照・Prefab asset・内部Preview SceneへAvatar設定を書き込めないこと、Prefab Modeの調査はできることを確認します。sessionごとのversion交渉とheader拒否、同じIDの重複、別sessionのキャンセル分離、待機中のキャンセル・Server停止・session終了、Importのキャンセル、FacialSet失敗のerror応答とplanの設定検証、アニメーションのremap拒否を確認します。Prefab追加はUIとMCPで同じ素体倍率追従・Undo・循環拒否となることを確認します。関連する既存FaceExpressionTestsとDerivedAssetCreatorTestsで回帰を確認し、新規テストケースは追加しません。

Prefab名による一覧・装着警告の除外は手動で、既定のExMenu・FacialSet生成Prefabが上部item一覧と警告対象から外れ、通常の衣装Prefabは表示と警告対象を維持することを確認します。Preferencesの「一覧・装着警告から除外するPrefab名」へ正規表現を追加・削除し、統合版のitem一覧と総合画面、単独AvatarInfo Windowの警告が更新されること、除外対象を選択中に設定を変更しても非表示のPrefabが選択範囲に残らないこと、空リストではExMenuも含め名前による両方の除外が解除されること、Sceneでの名前変更が照合に影響しないことを確認します。完全一致・部分一致・大文字小文字・カンマを含む数量指定・バックスラッシュの保存と再読込、不正な正規表現の保存拒否と修正、照合timeoutでも一覧表示と警告対象を維持することを確認します。Prefab本体と装着設定を変更せず、ExMenuの生成Prefab保存が維持されることも確認します。新規テストケースは追加しません。

Expression Menuの除外設定は手動で、既存MA Item・SDK Controlの除外／復元、Installerの登録全体の除外、読取専用の元asset、共有・循環参照、Undo／Redo・Prefab保存と再読み込みを確認します。元Component・Controlを保持し、合成ツリー・ExMenu・Play Mode・ビルド結果が一致すること、MAのパラメーターとReactive Componentsを保持して個別Controlだけを除外すること、NDMFの前後で元Scene・共有assetを変更しないこと、生成結果からEditorOnly設定と一時識別名を除去することを確認します。元SDK Controlの設定変更・曖昧化は警告とビルド停止、参照消失は復元で設定解除になることを確認します。MCPでは除外済みID・登録元ID・revisionを使用して除外／復元し、ee4v生成項目だけが本体削除になることを確認します。自動テストは追加しません。

マイアセットの新規作成では、ベース未選択で通常Prefab、ベース選択でPrefab Variantとなることを確認します。ベースの解除後は未選択として作成できること、フォーム再構築でも選択を保持すること、新しい所有Itemがベース素材の所有Itemから独立していることを、統合・単独画面の共通生成処理で確認します。

Gridのページ切り替えは、同じ行の項目数が減る場合と増える場合を確認します。空slotは子要素を含めて非表示とし、別タブの画像・名前が残らず、元のタブへ戻ると必要なカードが再表示されることを確認します。

マイアセットタブの追加カードは既存`AssetItemGridViewTests`で通常の選択の回帰を確認し、手動では空一覧・検索・並び替え時の先頭配置、クリックとEnter／Spaceからの作成フォーム表示、追加カードが範囲選択・右クリック操作の対象に含まれないことを確認します。統合ee4vと単独AssetManagerの両方からベースなしの作成を確認し、Storyでは作成フォームの表示だけを行ってProjectへ保存しません。

`src/Editor`のUnity 2022.3 EditModeテストは、次の対象を保証します。

- 壊れた場合の影響が大きい利用者向けの振る舞い
- 通常操作では原因を特定しにくい状態遷移やデータ保全
- Unity、SQLite、ファイル形式など外部境界との契約

計算式、定数、具象型、USSの記述方法といった実装詳細だけを検査するテストは置きません。同じ判断への入力違いは一つのテストへまとめます。

## AssetManager

改変画面の差分確認は、大きいZIPを参照する保存済みVariantで、初回のハッシュ確認中も詳細の表示・componentの有効切り替え・Windowの操作が続くことを実Editorで確認します。計測には`Scheduler.Tick`とEditor更新間隔を使用します。同じ元Fileの再確認ではハッシュを再利用し、同期と非同期の差分結果が一致すること、確認中の編集・対象切り替え・画面破棄で古い結果を反映しないことを確認します。元Fileとmetaの変更時は再計算し、版の保存・復元では元File全体を検証します。新規テストケースは追加しません。

アーカイブ済みItem・マイアセットは、アーカイブ一覧のダブルクリックや使用素材のリンクから詳細へ入れないことを手動で確認します。未アーカイブの詳細を開いた後で対象をアーカイブし、戻る／進むの履歴を操作しても一覧へ戻ること、詳細表示中のアーカイブで一覧へ戻ること、一覧での選択・情報表示・解除・削除は引き続き利用できることを確認します。

ベースなしの制作物のItem扱いは既存の`AssetManagerApiTests`、`DerivedAssetCreatorTests`と`AssetItemGridViewTests`で関連範囲の回帰を確認します。手動ではItem一覧とマイアセット一覧からの名前・説明・タグ編集、タグ一覧・検索・コレクション一致、インポート済み一覧、アーカイブの重複防止、Itemからの履歴表示と派生作成、保存・復元後のItem情報の維持を確認します。構成Prefabの追加Pickerでは制作物自身と自身を参照するPrefabを候補から除き、候補が空になるItemを表示せず、別の制作物には追加できることを確認します。ベースを持つマイアセットはタグを持たず、所有Itemや作成元素材のタグを継承しないことを確認します。新規テストケースは追加しません。

マイアセットのタグ・情報表示は既存の`DerivedAssetCreatorTests`と`AssetManagerApiTests`で関連範囲の回帰を確認します。手動では未保存と保存済みのベースなしの制作物へのタグ追加・削除、連続編集、名前変更・アーカイブ・過去版復元後のタグと作成日時の維持を確認します。親を持つマイアセットにはタグ欄がなく、APIでタグを指定しても付与されず、既存のProject内metadataやGitにタグがあっても一覧・検索・復元で使用しないことと、元の素材や所有Itemのタグが変わらないことを確認します。情報セクションでは専用フォルダー内Assetと作業Sceneの合計サイズ、ローカル日時表示、別ProjectでPrefabがない場合の保存情報、BOOTH情報を表示しないことを確認します。新規テストケースは追加しません。

ベースなしの制作物は既存の`AssetManagerApiTests`と`DerivedAssetCreatorTests`で共有のItem管理・Prefab生成の回帰を確認します。手動確認では制作物の作成、複数Prefabの追加・保存後の削除・Undo、版の保存と復元、完成Prefabの別アバターへの追加を確認します。Source切り替えとDB再生成後も制作物の所有ID・Tag・Archive状態と履歴を維持すること、試着アバターを変更・解除して再度Sceneを開けること、制作物Prefabに試着アバターを含めないこと、外部Scene参照が保存時に失われる前にエラーとなることを確認します。新規テストケースは追加しません。

ee4v・BLM・Eagleの契約は [datasources.md](./datasources.md) を参照してください。新規テストケースを追加せず、既存AssetManagerApiTestsと新規隔離DBへの手動取り込み・loopback疎通で確認します。Catalogの排他表示は3SourceとSourceなしのItem・Tag・Fileを同じ隔離DBへ入れ、検索件数・ページ分割・Collection一致・Tag候補・所属/未所属Fileを確認します。手動Itemはee4vだけに表示し、Fileの拡張子条件とアーカイブ表示もSourceを越えないこと、切替後も全データを保持することを確認します。ee4vでは手動pairと複数downloadのItemが同じ同期で共存すること、metadata編集、部分File削除の退避・復元・確定を隔離ライブラリで確認します。

| テスト | 保証する契約 | 残す理由 |
| --- | --- | --- |
| `CollectionSearch_EvaluatesNestedLogicAndHierarchicalTags` | SQLite検索が入れ子条件と階層Tagを評価する | Collection結果の誤りは通常の単純検索では検出できない |
| `MatchesCollection_ReevaluatesOnlyTheRequestedItem` | 指定ItemだけのCollection一致を返す | UIの差分更新に使う公開API契約である |
| `CollectionName_MustBeUnique` | 重複Collection名を`Duplicate`として拒否する | DBの一意制約を利用側のエラーへ変換する境界である |
| `ChangeSubscriberFailure_DoesNotStopOtherSubscribers` | 一購読者の例外が更新と他購読者を止めない | 購読者間の障害分離は通常操作では原因を特定しにくい |
| `ItemAndCollectionChanges_IdentifyMutationAndSubjects` | ItemとCollectionの通知が種別と対象IDを含む | UIの差分更新が依存する公開イベント契約である |
| `FileChanges_IdentifyFilesAndAffectedItems` | unitypackageのFile通知が対象Fileと影響Itemを含み、Item Target通知が対象Itemと参照Fileを含む | 関連Itemだけを更新する公開イベント契約である。直接Import対象にできないZIPを通知検証のfixtureには使用しない |
| `EagleSync_ReusesIdsAndDeletesMissingFile` | Eagleの再同期でItem IDを維持し、消えたFileを除く | 外部SourceとDBの同一性を保つ接続契約である |
| `EagleSync_PreservesManagedTagsAndTargets` | Booth商品・ストア情報とEagle由来Tagを取り込み、AssetManagerで設定したTagとTargetを再同期後も保持する | 外部Sourceのメタデータ変換とAssetManager管理状態の所有境界である |
| `EagleSync_MissingTargetPreservesData` | 対象rootがない同期で既存データを保持する | 設定誤りによる全削除を防ぐデータ保全である |
| `ThumbnailApis_ReturnMissingWithoutAThumbnailSource` | サムネイル元がない場合に単体・一括APIがmissingを返す | 非同期画像取得の欠落結果を統一する公開契約である |
| `FileRegistration_RejectsDirectoriesFromEverySource` | Eagleとee4vのどちらでもdirectoryをFileにしない | 外部Sourceから扱えない実体がDBへ入るのを防ぐ |
| `Ee4vImport_PairRestoresCatalogAfterDatabaseRebuild` | ee4v pairからItem情報を再構築する | DB再生成時のSource境界とデータ保全を確認する |
| `RegisterFile_RestoresUnassignedPairAfterDatabaseRebuild` | 未所属ee4v pairをDB再生成後も未所属Fileとして戻す | Itemを持たないSource情報が失われるのを防ぐ |
| `Ee4vDeleteStaging_RestoresSourceUntilCommitted` | 削除準備の破棄ではSourceを戻し、確定時だけ消す | DB更新失敗時の実体消失を防ぐ |
| `ArchiveOperations_UpdateMultipleTargetsAndCanRestore` | 複数Item・Fileをアーカイブして復元できる | 一括操作で一部だけ状態が変わる不具合を検出する |
| `DeleteItem_DeletesMultipleItemsAndTheirEe4vFiles` | 未アーカイブItemの削除を拒否し、アーカイブ済みItemと所属ee4v実体を削除する | 破壊的操作の事前条件とDB・Sourceの整合性を確認する |
| `DeleteFile_DeletesMultipleEe4vSourcePairs` | 未アーカイブFileの削除を拒否し、アーカイブ済みee4v pairを削除する | 破壊的操作の事前条件とSourceだけ残る不整合を確認する |
| `FileDependencies_RejectCyclesWithoutChangingData` | 循環依存を拒否して既存依存を保持する | 取り込み順を解決不能にする状態と部分更新を防ぐ |
| `ImportItemTargets_ImportsDependencyTargetsBeforeDependent` | 指定した依存実体を依存元より先にUnity Projectへ取り込む | 実体選択と依存順序を結ぶ公開契約である |
| `ImportFileEntries_ImportsTemporaryZipSelectionWithoutSavingTarget` | 一時選択だけを取り込み、保存済みItem Targetを変更しない | UIの一時操作と永続設定の境界を確認する |
| `ImportItemTargets_ImportsOneChoicePerGroupAndEveryUngroupedTarget` | Group選択1件とGroup外の全Item Targetを取り込む | Item Targetの選択取り込み契約を確認する |
| `FileDependencies_RejectInvalidTargetsWithoutChangingData` | 親directoryへのpathとZIP Targetを拒否して既存依存を保持する | 書き込み範囲逸脱、ZIPの二重取り込み、部分更新を防ぐ安全契約である |
| `FileDependencies_AllowMultipleEntriesInsideZip` | 同じ依存File内のUnityPackageを含む複数実体を指定できる | 実体単位の依存Targetを保持する公開契約である |
| `TargetImporter_CopiesFileAndZipEntry` | 通常FileとZIP内要素を指定先へ書き込む | ファイルシステムとZIPの接続部を確認する |
| `TargetImporter_ReportsPackageFailureAfterCleanup` | UnityPackage失敗を返して一時Fileを削除する | Unityの非同期完了契約と一時データ保全を確認する |
| `AnalyzeFile_ReadsZipAndUnityPackageContents` | 同期・非同期APIでZIPとUnityPackageを共通の解析結果へ変換する | 外部形式を公開モデルへ変換し、UIスレッド外からも利用する接続契約である |
| `Ee4vSync_InvalidMetadataDoesNotChangeCatalog` | 壊れたmetadataで既存Catalogを変更しない | 外部入力不正時のデータ保全を確認する |
| `Selection_CtrlTogglesItems` | Ctrl操作でItemを選択集合へ追加または削除する | 単一選択への巻き戻りと選択解除漏れを検出する |
| `Selection_ShiftUsesAnchorAndCtrlShiftAddsRange` | Shiftで基準Itemから範囲選択し、Ctrl併用時は既存選択へ追加する | 仮想Gridの表示行をまたぐ範囲と基準点の消失を検出する |
| `Selection_EscapeReturnsGridToUnselectedState` | Escapeで選択集合と主選択を空へ戻す | 選択解除後に詳細表示だけが残る問題を検出する |
| `ImagesWithTheSameSource_ReuseDecodedTexture` | 同じ画像Sourceを表示する要素がデコード済みTextureを共有する | スクロール中の再デコードは最終表示だけでは検出できない |
| `Create_CreatesOnlyMaterialVariantsAndSharesOtherAssets` | ルートPrefabをVariant、Materialを`Assets/Materials`のVariantにし、Animator Controllerを含むその他の参照アセットはコピーせず元を共有する | UnityのVariantとSerializedObjectをまたぐ参照差し替えで、未使用コピーの残存や共有対象の誤りは生成Prefabの存在だけでは検出できない |
| `FindPrefabCandidates_ResolvesOnlyImportedPrefabs` | Itemの取り込み済みGUIDからProject内のPrefabだけを重複なく候補へ変換する | GUID、Project path、Asset型をまたぐ候補抽出は入力画面の見た目だけでは誤りを特定できない |

## Face Expression

ライブラリの右クリック「割り当てを解除」は、同じClipの複数ジェスチャー条件とメニュー登録を1操作で解除することを手動確認します。別Clip・素材Clip・候補の選択・ジェスチャーのまばたき／口固定／名前を維持し、アバター未設定・未割り当てでは無効、右クリックだけでは選択を変えないことを確認します。実行・1回のUndo／Redoでバッジ、条件群、8×8表、メニュー欄と生成物が連動することも確認します。新規テストケースは追加しません。

初回割り当ては、未生成アバターで左右の手を初めて確定した場合に、Clipカーブの一括生成とMAへの反映が完了することを確認します。カーブ削除・まばたき・口固定・Loop・既定値の契約は既存`FaceExpressionTests`で確認します。メニュー画像生成はライブラリのサムネイル用コピーを再利用し、描画元の変更時にはコピーを更新すること、画像生成後もライブラリ・表情編集のPreviewを使用できること、対象変更・画面終了時にコピーが解放されることを手動確認します。実アバターでの初回操作時間はController生成と画像生成を分けて計測します。

ライブラリの割り当てバッジ・「+n」にホバーすると、割り当て先だけが行ごとに表示され、Clip名・左右の見出し・現在の選択状態が含まれないことを確認します。バッジ上のクリック・ダブルクリックは従来の候補選択・編集を維持します。「すべて」への切り替え、64通りから片手・個別への縮小、メニュー登録の増減とUndo／Redoで、ジェスチャー状態と遷移の参照が保持され、メニュー状態だけが増減することを確認します。選択だけではバッジを再構築せず、更新のないセルやState・Tracking Controlを再更新しないこと、競合条件のグレーアウトと削除済みClipの未割り当て表示を維持することも確認します。

表情ライブラリは手動で、単クリックとEnter／Spaceで候補を選ぶと左のカードの背景と枠が青くなり、選択先の割り当て済みClipが青枠になることを確認します。ホバー・フォーカス・押下でも選択色を維持し、ライブラリと確認一覧で表情を選ぶだけでは割り当て・Undo・未保存状態を変更しないことを確認します。割り当て済みカードは選択にかかわらずFist+Fistなど左手＋右手順のバッジや「メニュー」を表示し、複数割り当ての残り件数と全件のtooltipを示します。未割り当て・フォルダーにはバッジがなく、割り当て・解除・削除・Undo/Redo・アバター切り替え・検索・リサイズで正しく更新され、バッジ上の操作もカードと同じことを確認します。

ライブラリは常に1行5個で、見出しとサイズ調整欄がなく、リサイズ・検索・フォルダー移動・編集からの復帰でも5列を保ち、最後の行だけカードが大きくならないことを確認します。ライブラリと「割り当て」タブの領域は通常の背景色・外枠なしで、カードとセルの背景・枠線・正方形Preview・中央の名前と選択の強調を保つことを確認します。手の表示名は日本語・英語ともNeutral、Fist、HandOpen、FingerPoint、Victory、RockNRoll、HandGun、ThumbsUpとします。

右ペインの「表情」は条件が1つの場合に「条件 1」と×の行を表示せず、追加して複数になった場合は各条件の「条件 n」と右端の×を同じ行に表示することを確認します。×でその条件だけを削除し、残りの番号を詰め、1つに戻ったら見出し行が消えること、重複した条件の削除でも他の条件の割り当ては維持し、確定済み条件の削除を1回のUndo/Redoで戻すと見出しも再表示されることを確認します。右ペインの「表情」は各条件群で左手を左列、右手を右列に横並びにし、各列の先頭に「すべて」、8候補を2列×4段、その下に「メニュー」を表示することを確認します。選択中のClipがない場合は条件の編集・追加が無効で、Clip選択時は既存の全割り当てを条件群として表示し、全組み合わせ・片手すべて・個別の組を復元することを確認します。Fist+Fistを登録した状態で別条件の左Fistを選ぶと右Fistが無効、右Fistを先に選ぶと左Fistが無効となり、同じClip・別Clipのどちらでも再登録できないこと、片手の選択変更・条件削除・解除・Undo/Redo・アバター切り替えで有効状態が更新されることを確認します。「すべて」が使用済みの組を含む場合も無効とし、現在編集中の条件自身の組は除外して変更でき、拒否された操作が割り当て・Undo・revisionを変えないことを確認します。「条件を追加」で未選択の条件群を追加してもデータとUndoは変えず、最初の片手だけの選択では書き込まないこと、左右が揃ったボタン操作は即時に割り当て、追加条件で複数の組み合わせへ配置できることを確認します。左すべて＋右Fistは8組、左Victory＋右すべては8組、両手すべては64組で、複数条件は和集合として扱い、重複する条件の変更・削除でも他の条件がカバーする組は維持することを確認します。条件の変更で対象外になった元Clipの組だけを解除し、別Clipに割り当て済みの組は触らず、使用済みの組を含む条件はグレーアウトして選択できないこと、条件群の右クリック「条件を削除」で単独の対象を解除し、変更・削除を1回のUndo/Redoで戻せることを確認します。未完成の編集条件は以前の確定済み割り当てを保持することも確認します。

各条件の「メニュー」を押すと手の指定が未選択になり、共通Clip・名前・設定から即時にメニュー項目へ登録され、同じ条件の再押下では項目を増やさず、同じClipの登録があれば登録済み条件自身も含めて「メニュー」が無効となり、別Clipのメニュー登録は許可することを確認します。別条件でジェスチャーとメニューの両方へ登録できること、条件を切り替えたときは他条件の割り当てを保って以前の単独条件だけを解除・削除すること、右ペインに登録済み項目だけの一覧がなく、左の「割り当て」タブから既存項目を選んで条件や設定を編集できることを確認します。Clipを選ぶだけでは割り当てを変更せず、メニュー名の確定入力は同じClipの全ジェスチャー・メニュー項目へ即時に適用し、名前が空なら生成時にClip名を使い、混在名はplaceholderで示し、1回のUndo/Redoで名前を復元できることを確認します。

右ペインの共通入力・「表情／設定」タブと下端の「条件を追加」、左ペインの「ライブラリ／割り当て」タブは本文と独立して固定され、複数の条件群だけを縦スクロールできることを確認します。下部には変更前後のClip・適用ボタン・解除ボタン・区切り線がなく、設定タブでは「条件を追加」が非表示であることを確認します。タブ・画面切り替えだけではデータやUndoを変更せず、候補と条件、入力値、未確定条件を保ち、確認一覧の選択先変更で表情タブへ戻ることを確認します。

「設定」タブはまばたき・口固定と、「グローバル設定」配下の「サムネイルを生成」を四角いチェックボックスで示し、ラベルからも切り替えられ、サムネイル生成がPreferencesのメニューアイコン設定と相互に反映することを確認します。ライブラリ・「割り当て」タブのジェスチャーセル・メニューセルのどちらから選んでも、手の組が未選択でも同じClipの全ジェスチャー割り当てとメニュー項目へ設定が反映されることを確認します。異なる値は混在表示にし、変更した設定だけを統一して別Clip・未割り当て・名前・もう一方の設定を保持し、1回のUndo/Redoで戻せることを確認します。未割り当てClipの設定はまばたきON・口固定OFFから下書きで編集でき、Clipを切り替えると初期値へ戻り、最初のジェスチャー・メニュー条件確定時にClip・名前・設定をまとめて記録することを確認します。

「割り当て」タブでは右ペインを維持し、64通りの8×8表を左寄せでスクロールなしに収め、「メニュー」で登録した項目だけを右端に2列で表示することを確認します。ジェスチャーに割り当てた表情は表のみに表示し、Expression Menuへの生成は続けます。同じClipを両方に登録してもメニュー欄は明示した項目だけを同じ順序・表示名で並べます。メニュー欄が空なら幅・見出し・スクロール領域と間隔を消し、8×8表を再配置し、登録・最後の削除・Undo/Redo・アバター切り替えで表示を切り替えることを確認します。メニュー部分だけを縦スクロールでき、見出し・8×8表・右ペインは固定し、スクロールバー幅を常に確保して2列と正方形セルを保つことを確認します。見出しと手の名前は縦横中央で、左上の方向は2行で見切れないこと、横長・縦長のリサイズと選択枠でもPreviewが正方形を保つことを確認します。

一覧セルのプレビュー・名前・未割り当て領域はクリックとEnter／Spaceで選択でき、押下後も青枠を保ち、登録済みClipは共通入力と候補へ読み込み、未割り当てセルは候補を保持することを確認します。選択変更だけではメニューセルを再生成せず、データ変更・削除・Undo/Redoで参照元を更新し、スクロール後も正しく選択できることを確認します。確認セルのClip入力・Drag & Drop・追加は無効で、右クリックはジェスチャーのClipだけを解除してその他設定を保持し、明示したメニュー項目は削除し、アバター未設定・未割り当ての解除は無効で、メニューを開くだけではデータや選択を変えないことを確認します。解除後の一覧・条件群・ライブラリバッジとUndo/Redo、元Clipアセットの保持も確認します。

確認一覧には戻るボタンとナビバーがなく、左のタブで戻ると候補と条件を保持し、表情編集画面の戻るボタンは押下・フォーカス時も青枠が出ないことを確認します。ダブルクリック・右クリック編集・新規作成・複製は自動割り当てを行わず、作成キャンセルで候補を変えないことを確認します。新規カードは検索中も表示し、サブフォルダーでは「戻る」の次へ置きます。同期設定を表示せず、保存先・MA生成・repo外ライブラリの契約、保存・破棄とUndoを維持します。新規テストケースは追加しません。

| テスト | 保証する契約 | 残す理由 |
| --- | --- | --- |
| `Apply_GeneratesGestureMatrixAndControlsBlinkAndMouthBindings` | 左右ジェスチャーとExpression Menuを排他的に遷移させ、未割り当てをNeutral＋Neutralへ戻し、顔メッシュの未指定BlendShapeを既定値で上書きする。まばたき有効時は3〜8秒間隔で表情のBlink値から100へ往復する。口固定が無効ならVisemeカーブを除外し、`Voice`駆動Blend Treeで発話中だけプリセットの口形状を既定値へ戻し、`Voice`が戻ると元の表情Motionへ戻す | メニューとジェスチャーの競合、未割り当ての既定表情、元FXからの表情値残り、生成物の重複、表情値を基準にした瞬きとリップシンク制御の契約は元クリップの確認だけでは検出できない |
| `MouthMorphBindings_UseVisemeGroupWithoutVisemes` | プリセットの初期候補として、Visemeと同じ区切り見出し内のBlendShapeだけを口形状にし、Viseme自体と次の見出し以降を除外する | 自動分類の範囲が広がると目などの表情まで口形状として保存されるため、生成クリップだけでは誤分類を特定できない |
| `Copy_CreatesIndependentClipWithCurves` | 複製した表情クリップが元のカーブを保持し、その後の編集が元クリップへ影響しない | 複製後に元アセットを書き換える破壊的な参照共有を防ぐ |
| `Read_ConvertsHeadersAndIncludesSelectedMeshes` | 複数の区切り文字をヘッダーとして読み、グループウィンドウで選択した各RendererパスのBlendShapeを列挙する | 複数メッシュの同名BlendShapeを正しいAnimationカーブへ結ぶ契約を維持する |
| `BlendShapeRows_NestSidesUnderNormal` | FBX別プリセットで同じ見出し内の役割について通常版を親行、LとRを子行にし、番号などが異なる役割は別の親子行にする | 左右以外の名前まで誤って同じ親へまとめる問題は実名一覧だけでは検出できない |
| `BlendShapeRows_KeepClipChannelsSeparateWithoutSideControls` | クリップ内表示ではFBX別プリセットで対応した各BlendShapeも個別行にし、左右切替コントロールを生成しない | クリップ内表示で各カーブを独立して扱う契約を守る |
| `BlendShapeClipboard_PastesMatchingNamesWithoutUsingIndices` | 表情のBodyからコピーした値を、並び順ではなく名前が一致するPrefabのBlendShapeだけへ貼り付ける | アバター間でBlendShapeの順序や構成が異なるとき、別の表情値を上書きする問題は同一メッシュへの貼り付けだけでは検出できない |
| `BlendShapePresetClassifier_SeedsSupportedAvatarConventions` | Chiffon、Kipfel、Shinano、Manukaの命名形式から役割と左右指定の初期分類を作る | 左右表記だけを役割名から除き、番号や括弧表記を維持する条件は一覧表示だけでは確認できない |
| `BlendShapePresets_RoundTripManualFbxMapping` | `<FBX名>.json`へFBXとメッシュに結び付いた手動分類と口形状指定を保存して復元し、別FBXには適用しない | 再起動後や複数FBX利用時のファイル形式と対応表の取り違えは設定画面だけでは確認できない |
| `Groups_FilterHeadersAndTreatAddedMeshesAsGroups` | ヘッダー間の分類に加え、追加メッシュをメッシュ名のグループとして絞り込む | 追加メッシュのShapeが別Groupへ混入する問題と、メッシュ全体を選べない問題を防ぐ |

## Core

即時反映と保存集約は手動で、表情の割り当て・解除・まばたき・口固定・メニュー登録が保存ボタンなしでControllerとMAへ反映されること、連続操作の集約、Undo／Redo、Clip編集の保存・破棄、反映失敗の再試行を確認します。ControllerのState・Transitionと変更のない生成Clip・画像・Installerが再利用されること、新しい画像が更新ごとに1枚ずつ反映されること、ExMenuのController・recipeと表情生成assetの保存が入力を受け付けながら進むことを確認します。Scene保存・対象変更・画面終了・Play Mode開始・再読み込み時の確定と、subassetを含む再読込後の一致も確認します。既存`FaceExpressionTests`でGesture、メニュー、Blink、口固定、生成物の整理を検証し、新規テストケースは追加しません。

| テスト | 保証する契約 | 残す理由 |
| --- | --- | --- |
| `SettingsServiceTests.Instances_DoNotShareState` | SettingsService間で状態を共有しない | 独立した利用コンテキストの設定混入を防ぐ |
| `SettingsServiceTests.ProjectAssetSettings_UsesGlobalRootUnlessProjectOverrideEnabled` | グローバルまたはProject上書きのルート配下にScene・Animation/Facialの保存先を組み立てる | スコープ間の優先順位と機能ごとの保存先がずれる問題を防ぐ |
| `SettingsServiceTests.ProjectAssetSettings_RejectsInvalidRootFolderNames` | 空値、相対要素、パス区切りを含むルート名を拒否する | Assets外や意図しない階層への生成を防ぐ |
| `SettingsServiceTests.InvalidPersistedValue_FallsBackToDefault` | 無効な永続値を既定値へ戻す | 保存形式境界の不正値でEditorを壊さないため |
| `SettingsServiceTests.Changed_IsRaisedAfterSuccessfulUpdate` | 成功した更新後に定義と値を通知する | 設定連動機能が依存する公開イベント契約である |
| `LocalizationServiceTests.ScopedLocalizer_UsesCurrentFallbackAndEnglishInOrder` | 現在言語からキー名まで所定順で解決する | 欠落翻訳を含む複数Catalogの優先契約である |
| `LocalizationServiceTests.Reload_DropsCatalogCacheAndRaisesEvent` | 再読み込みが新Catalogを使い通知する | キャッシュと変更イベントをまたぐ状態遷移である |
| `LocalizationServiceTests.CatalogDiagnostics_AreReportedWhenCatalogLoads` | 重複キーを診断境界へ渡す | 外部Catalogの競合を利用側へ伝える契約である |
| `InjectionRegistryTests.GetRegistrations_ReturnsStablePriorityOrder` | 優先度とIDで決定的に並べる | 複数機能の注入順が不定になるのを防ぐ |
| `InjectionRegistryTests.Register_ReplacesSameIdentity` | 同一IDとchannelの再登録を置換する | 再読み込みで処理が重複するのを防ぐ |
| `InjectionRegistryTests.RegistryChanges_ReportAffectedChannel` | 登録変更が対象channelを通知する | 必要なEditor領域だけを再構築する契約である |
| `EditorIntegrationApiTests.PublicApis_DoNotExposeInternalBackendTypes` | 公開APIが内部backend型を露出しない | Unity内部実装を差し替えられる境界を維持する |
| `EditorIntegrationApiTests.PackageAssetApi_ResolvesInstalledPackage` | 開発配置と導入済みPackageのrootを解決する | Unity Package配置との外部境界を確認する |
| `BackgroundActivityTests.BeginAndDispose_TracksLatestActiveOperation` | 並行処理の件数と最新表示を追跡する | 複数処理の終了順に依存する状態遷移である |
| `BackgroundActivityTests.Run_TracksProgressAndSuccessfulCompletion` | 正常完了までの進捗と履歴状態を反映する | 非同期処理の複数状態をまたぐ公開契約である |
| `BackgroundActivityTests.Run_RecordsFailure` | 例外を失敗状態とメッセージへ変換する | 非同期例外をUIが扱う形へ変換する境界である |
| `BackgroundActivityTests.Cancel_RequestsCancellationAndRecordsCanceled` | cancelをtokenへ伝えて取消状態にする | 取消と失敗を分ける非同期契約である |
| `BackgroundActivityTests.BackgroundStatusOverlayHost_ReleaseRemovesWindowRegistration` | Window解除時にhost登録を除く | 閉じたWindowへの参照残りは通常操作で見つけにくい |

## UI

| テスト | 保証する契約 | 残す理由 |
| --- | --- | --- |
| `UiTextFactoryTests.UiButton_UpdatesDisplayedText` | `UiButton`の表示文字を作成後に更新できる | Unityの文字描画問題を避ける共通境界である |
| `UiStoryTests.Catalog_DiscoversExternalStoryProviders` | 別assemblyのStory providerを検出する | reflectionによる拡張境界を確認する |
| `UiStoryTests.CatalogStories_DeclareUsageLocations` | Reference以外のStoryが使用箇所を1件以上宣言する | 空の使用箇所を検出する。登録処理は別途、パスの存在・カテゴリ・ID重複を検証する |
| `UiIconTests.UiBuiltinIconResolver_TryResolve_AllRegisteredIcons` | 全登録アイコンをUnity textureへ解決する | Unity versionで変わり得る外部アイコン契約である |
| `UiIconTests.FluentUiIcons_LoadsBundledRuntimeIcons` | 同梱したFluent UI System IconsをすべてTextureへ解決する | サードパーティーアセットと実行時パスの契約である |

## SQLite

| テスト | 保証する契約 | 残す理由 |
| --- | --- | --- |
| `SqliteBootstrapTests.SqliteBootstrap_Provider_AllowsInMemoryRoundTrip` | native providerでSQLiteの読み書きができる | bundled native pluginとの実接続を確認する |
| `SqliteBootstrapTests.SqliteBootstrap_NonWindows_Initialize_DoesNotThrow` | 非Windowsでは安全に初期化を終える | platform境界の契約でありWindows試験とは同時に動かない |

## Editor機能

Expression Menuは手動で、ツリーにRoot・Submenu・通常項目を表示し、展開ボタン・親子罫線・28pxのアイコン・種類・削除が同じ行へ収まることを確認します。選択変更とフォーム更新で開閉・スクロールを保持し、8項目を超える枝でも全項目を表示すること、各階層末尾の追加行が右の選択と無関係にその階層へ登録されること、通常表示は上へExMenu、下へツリーを常設し、形式タブ・トグルがないこと、ツリーだけをスクロールしてもExMenuが上に固定され、両表示の選択・階層・ページと編集を同期すること、行頭に開閉ボタンがあり、ハンドルがないこと、選択の強調と必要時の動作プレビューを確認します。確認中の左にはプレビューと内側の戻る・動作操作だけが表示され、右のフォームを維持し、戻るとExMenuのページ・選択・開閉・一覧の位置を復元することを確認します。動作なし・表示・BlendShape・MaterialのON／OFFで基準Aポーズの両腕が45度下がり肘が伸び、再描画・開閉でも姿勢が累積せず、直接Transform指定が仮ポーズより優先されること、MA衣装が追従することも確認します。Toggleの初期ONがGestureManagerと同じ画像・位置・回転のExMenu表示とtooltipへ反映され、初期OFFとUndo／Redoでは実際の設定へ戻ること、ExMenuのページ・選択・編集欄の位置を保持すること、保存・再読み込みでも初期ON表示が一致すること、OFF・プレビューとの切り替え・再描画・画面離脱で回転表示とアニメーションを解放することを確認します。空の「項目の設定」だけを消し、SDK／MAの詳細入力は維持することも確認します。別階層の行の移動元判定、ハンドルと移動ボタンがなく、行の名前・アイコン・種類・余白から6px以上動かすとドラッグを開始すること、開閉・削除ボタンを押してもドラッグを開始しないこと、同じ親・別のSubmenuのどちらからもSubmenuの行全体へのドロップでその階層の末尾へ移動し、上端・中央・下端で同じ可否と青い移動先表示になること、自動展開の行再生成・移動元行のdetach・`DragExited`通知の後でもドロップ確定を受け取り、項目が実際に移動先の中へ入ること、確定後に移動先を展開・選択すること、Submenu自体の並び替えは行間で示すこと、閉じた移動先へ600ms留まると選択を変えずに展開してドラッグを継続できること、離脱・取消時の展開待ち解放、名前・開閉・削除・追加のどこへホバーしても行全体の背景が一枚だけ表示されること、名前の通常・ホバー・マウス押下中・クリック後・キーボードフォーカスで内側の背景画像・背景色・枠線が表示されず、非選択・選択行とも行全体で示すこと、`UiButton`へclassの状態指定が適用されること、削除がホバー・押下・フォーカスを含め枠線と背景のないアイコンであること、Item・空のSubmenu・中身のあるSubmenuすべてで対象名を示す確認を挟み、取消・Escでは削除せず、確定・Undo／Redo・保存を維持すること、選択・ドロップ先の色と開閉トグル・削除以外のキーボードフォーカス枠を維持すること、開閉トグルの通常・ホバー・押下・フォーカスで青い枠線を表示せず、行からのUnity標準ドラッグを維持すること、表示内に収まる短いツリーを上端・下端へドラッグしてもスクロール値が0のまま余白や高さが増えず、長いツリーは先頭・末尾で止まること、同じ挿入位置の上下端・行間を通っても一本の線が動かず行高を変えないこと、展開中のSubmenuの子領域を含めた挿入位置、取消・画面離脱時の表示解放、保護項目の閲覧と入力無効、コピー・移動・削除・Undo／Redo・保存を確認します。統合460px・単独380pxの右ペイン、880px未満の縦配置、Dark／Lightと長い名前のtooltipも確認します。新規テストケースは追加しません。

componentの有効状態をOFFにして名前・アイコンがグレーアウトし、ONへ戻せること、初期OFF・Undo/Redo・外部Inspectorからの変更でもヘッダーが同期することを手動確認します。RendererとBehaviourのON/OFFを繰り返して記録された編集以外から保存通知を発生させず、変更を止めた後に作業Sceneの保存・再Importを繰り返さないこと、Scheduler.Tickの待機画面を連続表示しないこと、Preview・Prefab override・保存・変更破棄を維持することを確認します。Mesh・Material・boneの変更ではPreview用キャッシュを更新することも確認します。

パーツの全体一覧でPrefab rootと別のPrefabグループ見出しを二重表示せず、通常のroot行へPrefabアイコンだけを追加することを手動確認します。nested Prefab rootのアイコン、rootからの開閉・詳細選択・表示切り替え、部位の絞り込みとPreviewからの選択も確認します。Material・体型のPrefabグループはそれぞれの表示を維持します。

パーツ詳細のcomponentヘッダーは、左端の丸いスイッチ・アイコン・名前が縦中央へ揃い、Dark・LightテーマでON/OFFとfocusを識別できることを手動確認します。スイッチのクリックとSpace操作、Undo/Redoによるthumb位置と有効状態の同期、編集不可時の無効化、Transformなど有効状態を持たないcomponentの表示、長いcomponent名と狭いペインでの配置も確認します。Inspector本文のチェックボックスは標準の外観を維持します。

外見の内部タブをパーツ→マテリアル→体型→パーツと繰り返し切り替え、タブボタンと文字要素を保持したまま選択だけを更新すること、3ボタンが均等幅・最小高さ32px・透明背景・青い2pxの選択下線で、主文字色・補助文字色・ホバー背景と下端の区切り線が表情の「表情／設定」タブと同じになり、外枠や丸角、追加の区切り線がなく、部位選択とタブを含むヘッダーの背景色が本文と一致し、明暗両テーマでヘッダーに背景の重ね描きがないこと、単独Shape and Partsの2タブも同じスタイルになること、右ペインの幅とスクロール領域が安定することを手動確認します。3タブを部位選択欄の下へ配置し、Material詳細でも部位選択欄と3タブを表示し続け、全身・手・足などの切り替えが一覧絞り込みとPreviewのフレーミングへ反映されることを確認します。

統合ee4vのEdit Modeでは外側カテゴリが「総合」「外見」「表情」「Expression Menu」となり、「外見」内に「パーツ」「体型」「マテリアル」の3タブだけを表示することを手動確認します。Material詳細でも3タブと部位選択欄の操作を維持し、単独Materials Windowでも詳細を開いて部位選択欄が消えず、一覧へ戻る操作でも表示を維持すること、内部タブ切り替え前に保留変更を確定すること、Prefab・部位の切り替えで現在の内部タブを維持すること、Window再構築とPlay Modeからの復帰で内部タブを復元することを確認します。単独Shape and PartsとMaterials Windowの画面構成も確認します。

多数のBlendShapeを持つ体型画面を一度開いた後、パーツとMaterialの一覧を繰り返し切り替えます。非表示の内容がpanelから外れること、再表示時に一覧の要素を再生成しないこと、タブ入力から次の描画までの時間とWindowの再描画時間を実Editorで確認します。右ペインとPreviewの位置・幅、一覧の開閉状態、スクロールの先頭復帰、詳細Inspectorの解放、離れた体型UIのお気に入り通知解除も確認します。新規テストケースは追加しません。

体型の初回表示ではお気に入りだけを開き、部位と役割を閉じ、未展開グループの編集行を生成しないことを実Editorで確認します。全身の「その他」と高度な骨格スケール、同期BlendShapeの個別Renderer行も展開時に生成し、開閉を繰り返して要素と描画callbackが増えないこと、親の閉鎖・タブ切り替え・範囲変更・再構築で開閉状態を維持すること、対象変更でお気に入りだけを開く初期状態へ戻ることを確認します。遅延生成前の値変更と、閉じたお気に入りへの追加・解除を再展開時に反映し、展開だけでSceneを変更しないことも確認します。新規テストケースは追加しません。

パーツ詳細は統合ee4vと独立版Shape and Partsで手動確認します。名前・pathとPreviewのシングルクリックでは一覧の選択とPreviewのハイライトだけを更新し、ダブルクリックで詳細へ移動すること、「一覧へ戻る」で一覧のスクロール位置と階層の開閉状態を復元することを確認します。閉じたPrefab root・nested Prefab root・子を持つ通常パーツの名前またはpathをダブルクリックして詳細へ入り、戻っても閉じたままであること、開いた行から戻っても開いたままであること、左端の矢印だけで開閉できて詳細へ移動しないことも確認します。Transform、Renderer、MAなどのcomponentのUI Toolkit・IMGUI Inspectorから値と有効状態を変更し、入力中のfocusを保持しながらPreviewに反映すること、Undo/RedoとホストのPrefab保存・変更破棄へ接続することを確認します。部位・Prefab範囲変更、対象切替、Window終了でInspectorを解放し、編集不可のPrefabでは変更できないこと、Missing Scriptが他のcomponentの表示を妨げないことも確認します。Catalogの`Domain/AvatarParts/Containers/Part details`は読み取り専用の詳細サンプルです。

パーツ詳細のヘッダは戻る矢印、名前・path、目アイコン、EditorOnlyチェックボックスの並びを確認します。長い名前・pathでも右端の操作が収まり、pathを折り返すこと、Meshを持たないパーツでは目アイコンを表示しないことを確認します。目アイコンでPreviewだけを隠して戻せること、チェックボックスでactiveSelf・EditorOnly・Previewが一覧と同様に変わり、詳細とInspectorを保持することを確認します。非アクティブな自身・祖先では目アイコンを無効にしてTooltipを消し、EditorOnlyは操作できること、再有効化で一時非表示の状態を復元することを確認します。componentの編集後も両操作が即座にヘッダへ反映され、Undo/Redo・外部Inspectorの変更・一覧へ戻ったときに状態が一致し、Prefab保存・変更破棄へ反映されることを確認します。

子を持つPrefab root・nested Prefab root・通常パーツの詳細では、ヘッダとcomponentの間に直下の「子要素」だけをHierarchy順で表示し、子がない詳細では領域を表示しないことを確認します。Prefab範囲・部位・名前の除外条件を維持し、子の名前・pathのシングルクリックではハイライトだけを更新し、ダブルクリックで子の詳細、さらに孫の詳細へ進めること、「一覧へ戻る」で最初の一覧の開閉状態とスクロール位置を維持することを確認します。子行の目アイコン・EditorOnly・右クリック操作、親の非アクティブ化と復元、component編集後・Undo/Redo・外部変更での状態同期も確認します。

パーツ・Materialの一覧とPreviewは統合版と各単独Windowでシングルクリック・ダブルクリックを確認します。シングルクリックでは一覧の選択とハイライトだけを更新し、スクロール・開閉状態・カメラ・Inspectorを維持すること、続くダブルクリックではクリックした対象の詳細を一度だけ開くことを確認します。Materialが複数Prefabに重複表示される場合は同じMaterialの行をすべて選択表示にします。詳細表示中に別対象や空白をシングルクリックしてもInspectorの編集対象とMaterialの「他を非表示」の対象を変えず、ダブルクリックだけで編集対象を切り替えることを確認します。目アイコン・チェックボックス・開閉矢印・右クリックでは詳細を開かず、「一覧へ戻る」とPrefab・部位の切り替えで選択と詳細の状態が整合することも確認します。自動テストは追加しません。

パーツ一覧・詳細内の子要素一覧・Material一覧では、左ボタンを押した時点で選択とハイライトを更新すること、押下から解放までにカーソルを少し動かしても選択が取り消されないことを確認します。行内の名前・pathをまたぐ移動も確認します。各行の上端・下端・区切り線付近・左右の余白・深い階層の字下げ・開閉矢印のない空き領域・名前とpathの間・右端の操作間の余白・MaterialのBadgeからも対象行を選択できることを確認します。ダブルクリックだけが詳細を開き、実際の目アイコン・EditorOnly・開閉矢印とその子のアイコンでは行選択や詳細移動を発生させないこと、Material行のキーボード選択も維持することを確認します。

| テスト | 保証する契約 | 残す理由 |
| --- | --- | --- |
| `ItemStyleServiceTests.Scopes_KeepProjectAndHierarchyValuesSeparate` | ProjectとHierarchyの同一IDを別設定として扱う | 異なる保存scope間のデータ混入を防ぐ |
| `ItemStyleInteractionTests.Selection_UsesGroupOnlyWhenHoveredFolderIsSelected` | 選択中項目からだけ一括編集する | 意図しない複数対象の変更を防ぐ |
| `ProjectTabsTests.PinnedTab_RejectsNavigationToAnotherFolder` | 固定tabが別folderへ移動しない | 固定位置を失う状態変更を防ぐ |
| `HierarchyStyleTests.VisibilityApi_RestoresActiveStateAndTag` | 非表示解除時にactive stateとtagを戻す | GameObjectの状態を失う破壊的副作用を防ぐ |
| `HierarchyStyleTests.VisibilityService_RestoresPersistedHiddenState` | 保存したGlobalObjectIdからHierarchyの非表示状態を復元し、未変更のシーンをdirtyにしない | エディターやシーンを開き直した後に非表示状態が失われる問題と、起動直後に不要な保存要求が出る問題を検出する |
| `WindowGroupTests.EnteringGroup_FocusesPeersAndRestoresOriginalFocus` | グループ外から入ると同じグループのほかのWindowを前面化し、起点へfocusを戻す | 複数dockとfocus変更にまたがる状態遷移は最終focusだけでは検出できない |
| `WindowGroupTests.FollowerRole_IsEvaluatedPerGroup` | 同じWindowが通常メンバーのGroupだけを起動し、FollowerのGroupには起点にならず追従だけする | 複数Group間の所属役割が混同される問題を防ぐ |
| `WindowGroupTests.RegisteringWindowAsRegularInMultipleGroups_IsRejected` | 同じWindowを複数Groupの通常メンバーとして登録できない | 複数Groupが意図せず同時に起動する構成を防ぐ |
| `WindowGroupTests.ReplacingSameGroupRegistration_IgnoresOldDisposal` | 同じGroupへの再登録後に古い登録を破棄しても新しい所属を維持する | lifecycle順序の前後で有効な登録が消える問題を防ぐ |
| `WindowGroupTests.AdditionalMembership_BecomesFollowerAndPersists` | 通常所属があるWindow種類を別Groupへ追加するとFollowerになり、通常所属への変更を拒否して保存する | 通常所属の重複と設定再読込後の制約消失を防ぐ |

## 実行と判定

Codexからの実行コマンド、Licensing ClientのIPC待ちを避ける条件、結果XMLによる成否判定は、リポジトリルートの`AGENTS.md`を正とします。

隔離検証Projectではee4vを`Packages/dev.4of.ee4v`または名前が`src`で終わるlocal packageとして配置します。local packageの場合は検証Projectの`Packages/manifest.json`の`testables`へ`dev.4of.ee4v`を指定し、既存のテストassemblyを読み込みます。結果XMLの`total`が0なら対象テストを実行できていません。VRChat SDKとGestureManagerを使う範囲では元Projectと同じ`VRC_SDK_VRCSDK3`のコンパイル定義を使用します。

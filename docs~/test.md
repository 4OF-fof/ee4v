# テスト一覧

`src/Editor`のUnity 2022.3 EditModeテストは、`tiny-code`の次の対象だけに限定します。

- 壊れた場合の影響が大きい利用者向けの振る舞い
- 通常操作では原因を特定しにくい状態遷移やデータ保全
- Unity、SQLite、ファイル形式など外部境界との契約

計算式、定数、具象型、USSの記述方法といった実装詳細だけを検査するテストは置きません。同じ判断への入力違いは一つのテストへまとめます。

## AssetManager

| テスト | 保証する契約 | 残す理由 |
| --- | --- | --- |
| `CollectionSearch_EvaluatesNestedLogicAndHierarchicalTags` | SQLite検索が入れ子条件と階層Tagを評価する | Collection結果の誤りは通常の単純検索では検出できない |
| `MatchesCollection_ReevaluatesOnlyTheRequestedItem` | 指定ItemだけのCollection一致を返す | UIの差分更新に使う公開API契約である |
| `CollectionName_MustBeUnique` | 重複Collection名を`Duplicate`として拒否する | DBの一意制約を利用側のエラーへ変換する境界である |
| `ChangeSubscriberFailure_DoesNotStopOtherSubscribers` | 一購読者の例外が更新と他購読者を止めない | 購読者間の障害分離は通常操作では原因を特定しにくい |
| `ItemAndCollectionChanges_IdentifyMutationAndSubjects` | ItemとCollectionの通知が種別と対象IDを含む | UIの差分更新が依存する公開イベント契約である |
| `FileChanges_IdentifyFilesAndAffectedItems` | File通知が対象Fileと影響Itemを含み、Item Target通知が対象Itemと参照Fileを含む | 関連Itemだけを更新する公開イベント契約である |
| `EagleSync_ReusesIdsAndDeletesMissingFile` | Eagleの再同期でItem IDを維持し、消えたFileを除く | 外部SourceとDBの同一性を保つ接続契約である |
| `EagleSync_PreservesManagedTagsAndTargets` | Booth商品・ストア情報を取り込み、AssetManagerで設定したTagとTargetを再同期後も保持する | 外部Sourceのメタデータ変換とAssetManager管理状態の所有境界である |
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

| テスト | 保証する契約 | 残す理由 |
| --- | --- | --- |
| `Apply_GeneratesGestureMatrixAndControlsBlinkAndMouthBindings` | 左右ジェスチャーとExpression Menuを排他的に遷移させ、未割り当てをNeutral＋Neutralへ戻し、顔メッシュの未指定BlendShapeを既定値で上書きする。まばたき有効時は3〜8秒間隔で表情のBlink値から100へ往復する。口固定が無効ならVisemeカーブを除外し、`Voice`駆動Blend Treeで発話中だけプリセットの口形状を既定値へ戻し、`Voice`が戻ると元の表情Motionへ戻す | メニューとジェスチャーの競合、未割り当ての既定表情、元FXからの表情値残り、生成物の重複、表情値を基準にした瞬きとリップシンク制御の契約は元クリップの確認だけでは検出できない |
| `MouthMorphBindings_UseVisemeGroupWithoutVisemes` | プリセットの初期候補として、Visemeと同じ区切り見出し内のBlendShapeだけを口形状にし、Viseme自体と次の見出し以降を除外する | 自動分類の範囲が広がると目などの表情まで口形状として保存されるため、生成クリップだけでは誤分類を特定できない |
| `Read_ConvertsHeadersAndIncludesSelectedMeshes` | 複数の区切り文字をヘッダーとして読み、グループウィンドウで選択した各RendererパスのBlendShapeを列挙する | 複数メッシュの同名BlendShapeを正しいAnimationカーブへ結ぶ契約を維持する |
| `BlendShapeRows_GroupOnlyNormalAndSides` | FBX別プリセットで同じ見出し内の役割について通常、L、Rだけを1行へまとめ、番号などが異なる役割は別行にする | 左右以外の名前まで誤って同じカーブ選択へまとめる問題は実名一覧だけでは検出できない |
| `BlendShapeRows_KeepClipChannelsSeparate` | FBX別プリセットで対応した各BlendShapeを個別行にし、左右指定を読み取り専用のトグルで示す | クリップ内表示で使用中カーブが再集約される問題と、固定値の表示位置が通常表示からずれる問題を防ぐ |
| `BlendShapeClipboard_PastesMatchingNamesWithoutUsingIndices` | 表情のBodyからコピーした値を、並び順ではなく名前が一致するPrefabのBlendShapeだけへ貼り付ける | アバター間でBlendShapeの順序や構成が異なるとき、別の表情値を上書きする問題は同一メッシュへの貼り付けだけでは検出できない |
| `BlendShapePresetClassifier_SeedsSupportedAvatarConventions` | Chiffon、Kipfel、Shinano、Manukaの命名形式から役割と左右指定の初期分類を作る | 左右表記だけを役割名から除き、番号や括弧表記を維持する条件は一覧表示だけでは確認できない |
| `BlendShapePresets_RoundTripManualFbxMapping` | `<FBX名>.json`へFBXとメッシュに結び付いた手動分類と口形状指定を保存して復元し、別FBXには適用しない | 再起動後や複数FBX利用時のファイル形式と対応表の取り違えは設定画面だけでは確認できない |
| `Groups_FilterHeadersAndTreatAddedMeshesAsGroups` | ヘッダー間の分類に加え、追加メッシュをメッシュ名のグループとして絞り込む | 追加メッシュのShapeが別Groupへ混入する問題と、メッシュ全体を選べない問題を防ぐ |

## PhysBone Collider

| テスト | 保証する契約 | 残す理由 |
| --- | --- | --- |
| `Create_UsesOnlyVisibleMajorBonesInDirectArmature` | Prefab直下のArmatureにある主要な体ボーンだけを使い、短いSpine区間をまとめた上下2つの胴体Colliderを体幅から提案する | 衣装、目、EditorOnly階層の混入と、しきい値により胴体がChestだけになる問題は、単純な一列のボーン表示だけでは判別しにくい |
| `Create_PresetsBalanceColliderCountAndBoneCoverage` | 軽量では四肢を統合し、通常では主要ボーン、フルでは主要部位外の変形ボーンまで候補へ含める | プリセットごとの削減と追従範囲の違いは、単一の骨格や最終Prefabだけでは判別しにくい |
| `Apply_ReplacesOwnedCollidersAndAssignsSelectedPhysBoneTransforms` | 複数指定したGameObject直下のArmature内だけから、重複なしでPhysBoneと表示対象Transformを検出する。さらに深いArmatureとArmature外は除外する。生成Colliderをアバター直下の1 Prefabへまとめ、MA Bone Proxyとカプセル形状を設定する。選択したPhysBone Transformだけへ参照を追加し、設定を読み戻して再適用時に旧参照を置き換える | 直下に限定した複数階層の検出範囲と、VRChat SDK型・Modular Avatar型・Prefabをまたぐ個別参照のシリアライズ境界は、画面上の配置案だけでは範囲外や重複の混入、欠落参照、旧Colliderの残存を検出できない |

## Core

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
| `UiTextFactoryTests.FactoryButton_UpdatesDisplayedText` | Factoryで作ったButtonの表示文字を作成後に更新できる | Unityの文字描画問題を避ける共通境界である |
| `UiStoryTests.Catalog_DiscoversExternalStoryProviders` | 別assemblyのStory providerを検出する | reflectionによる拡張境界を確認する |
| `UiStoryTests.CatalogStories_DeclareUsageLocations` | 機能Storyが実使用箇所を宣言する | 未使用componentの残存を通常の表示確認では検出できない |
| `UiIconTests.UiBuiltinIconResolver_TryResolve_AllRegisteredIcons` | 全登録アイコンをUnity textureへ解決する | Unity versionで変わり得る外部アイコン契約である |
| `UiIconTests.FluentUiIcons_LoadsBundledRuntimeIcons` | 同梱したFluent UI System IconsをすべてTextureへ解決する | サードパーティーアセットと実行時パスの契約である |

## SQLite

| テスト | 保証する契約 | 残す理由 |
| --- | --- | --- |
| `SqliteBootstrapTests.SqliteBootstrap_Provider_AllowsInMemoryRoundTrip` | native providerでSQLiteの読み書きができる | bundled native pluginとの実接続を確認する |
| `SqliteBootstrapTests.SqliteBootstrap_NonWindows_Initialize_DoesNotThrow` | 非Windowsでは安全に初期化を終える | platform境界の契約でありWindows試験とは同時に動かない |

## Editor機能

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
| `WindowGroupTests.LoadingMultipleRegularMemberships_ConvertsExtrasToFollowers` | 通常所属が重複した保存設定を読み込むと2つ目以降をFollowerへ補正する | 旧設定による起動時の制約違反と複数Groupの同時起動を防ぐ |

## 実行と判定

Codexからの実行コマンド、Licensing ClientのIPC待ちを避ける条件、結果XMLによる成否判定は、リポジトリルートの`AGENTS.md`を正とします。

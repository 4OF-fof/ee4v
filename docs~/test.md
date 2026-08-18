# テスト一覧

`src/Editor` にあるUnity 2022.3のEditModeテストを、テストが保証する振る舞いと維持する理由に分けて記載する。

## AssetManager

| テスト | 保証する振る舞い | 理由 |
| --- | --- | --- |
| `AssetManagerApiTests.CollectionSearch_UsesNestedLogicAndHierarchicalTags` | SQLite上の入れ子条件、階層Tag、総件数、ページングが完全なItemを返す | Catalog全件を復元せず検索結果ページだけを読み込むため |
| `AssetManagerApiTests.MatchesCollection_ReevaluatesOnlyTheRequestedItem` | 指定ItemだけでCollection条件への一致を再評価できる | 変更通知後にCatalog全体を再取得せずCollection表示を更新するため |
| `AssetManagerApiTests.ItemAndCollectionChanges_IdentifyMutationAndSubjects` | ItemとCollectionの各変更通知が操作種別と対象IDを含む | UIがCatalog全体を再取得せず、変更対象だけを更新できるようにするため |
| `AssetManagerApiTests.FileChanges_IdentifyFilesAndAffectedItems` | Fileの各変更通知が対象File IDと影響を受けるItem IDを含む | File所属やアーカイブの変更時に関連Itemだけを再取得できるようにするため |
| `AssetManagerApiTests.EagleSync_ReusesIdsAndDeletesMissingFile` | Eagle由来Itemの値を正本へ合わせ、Tagだけの更新とFile削除を同期結果・対象ID通知へ反映する | UIが同期後もCatalog全体を再取得せず、Sourceにない行を残さないため |
| `AssetManagerApiTests.EagleSync_MissingTargetPreservesDataAndDeletesRemovedItem` | 存在しない対象rootではDBを変更せず理由を返し、存在する空rootでは消えたItemとFileのIDを返して削除する | 設定誤りによる全削除を防ぎ、正常な完全同期ではEagle側の削除をDBへ反映するため |
| `AssetManagerApiTests.ThumbnailApis_ReturnMissingWithoutAThumbnailSource` | 非同期の単体・一括取得がmissing結果を返し、キャンセル済み要求を中止する | 画像がないItemと画面破棄時の中止をUIが同じ非同期契約で扱うため |
| `AssetManagerApiTests.Ee4vDeleteStaging_RestoresSourceUntilCommitted` | ee4v Sourceの削除準備を確定前に破棄すると実体を元へ戻し、確定時だけ削除する | DB更新失敗時にSourceだけが失われることを防ぐため |
| `AssetManagerApiTests.FileRegistration_RejectsDirectoriesFromEverySource` | Eagleとee4vのどちらからもdirectoryをFileとして登録しない | 取り込み処理が扱えない実体種別をDBへ保存しないため |
| `AssetManagerApiTests.AnalyzeFile_ReadsZipAndUnityPackageContents` | ZIPのroot省略後pathとUnityPackageのAsset path・GUIDを返す | UIが外部形式を直接解析せずTarget候補を構築できるようにするため |
| `AssetManagerApiTests.FileDependencies_UpdateMultipleFilesAndRejectCycles` | 複数Fileの依存先を同時置換し、全置換後の循環を拒否し、依存先削除で影響を受けたFileを通知する | Version／Variant Groupなしで依存設定を一括操作し、連鎖変更をUIへ伝えるため |
| `AssetManagerApiTests.ImportFileTargets_ImportsDependenciesBeforeDependent` | 非同期に依存順で取り込んだFileとGUIDを結果・変更通知・逆引きAPIから取得できる | 取り込み完了をUIへ返し、Project上のGUIDから管理元を特定するため |
| `AssetManagerApiTests.ImportFileEntries_ImportsTemporaryZipSelectionWithoutSavingTarget` | ZIP内の指定要素だけを取り込み、保存済みTargetを変更せず、Target取り込みも同じ経路で動作する | File Treeから一時的に選んだ要素と保存済み設定の取り込み経路が分岐することを防ぐため |
| `AssetManagerApiTests.TargetImporter_ReportsPackageFailureAfterCleanup` | UnityPackage完了待ち中は一時Fileを維持し、失敗結果を返して完了後に削除する | 非同期取り込みの失敗を呼び出し側へ確実に返し、一時Fileを残さないため |

## Core

| テスト | 保証する振る舞い | 理由 |
| --- | --- | --- |
| `SettingsServiceTests.Instances_DoNotShareState` | `SettingsService` のインスタンス間で設定値を共有しない | テストや複数コンテキストの状態が相互に漏れるのを防ぐため |
| `SettingsServiceTests.InvalidPersistedValue_FallsBackToDefault` | 永続化値がバリデーションに失敗した場合は既定値を使う | 壊れた設定でEditor機能が不正な状態になるのを防ぐため |
| `SettingsServiceTests.Changed_IsRaisedAfterSuccessfulUpdate` | 設定更新後に、対象定義と新しい値を含む変更イベントを送る | 設定に連動する表示や処理を確実に更新するため |
| `LocalizationServiceTests.ScopedLocalizer_UsesCurrentFallbackAndEnglishInOrder` | 現在言語、フォールバック言語、英語、キー名の順で翻訳を解決する | 翻訳が欠けていても安定した表示を維持するため |
| `LocalizationServiceTests.Reload_DropsCatalogCacheAndRaisesEvent` | 再読み込み時にカタログキャッシュを破棄し、イベントを1回送る | 翻訳ファイルの変更を利用側へ反映するため |
| `LocalizationServiceTests.CatalogDiagnostics_AreReportedWhenCatalogLoads` | カタログ読み込み時に重複キーを診断へ渡す | 翻訳の上書きや競合を検出できるようにするため |
| `InjectionRegistryTests.GetRegistrations_ReturnsStablePriorityOrder` | 登録を優先度順、同順位ではID順で返す | 描画や処理の順序を決定的にするため |
| `InjectionRegistryTests.Register_ReplacesSameIdentity` | 同じ識別子とチャンネルの登録を新しい登録で置き換える | 再登録で処理が重複するのを防ぐため |
| `InjectionRegistryTests.RegistryChanges_ReportAffectedChannel` | 登録と解除の変更イベントが対象チャンネルを通知する | 必要なEditor領域だけを再描画できるようにするため |
| `EditorIntegrationApiTests.PublicApis_DoNotExposeInternalBackendTypes` | 公開APIのシグネチャに内部実装型を露出しない | 外部モジュールを内部実装から分離するため |
| `EditorIntegrationApiTests.PackageAssetApi_ResolvesInstalledPackage` | 開発配置とPackage Manager配置のどちらでもパッケージルートを解決する | インストール形態に依存せずアセットを参照するため |
| `BackgroundActivityTests.BeginAndDispose_TracksLatestActiveOperation` | 複数処理の件数と最新メッセージを追跡し、破棄時に前の処理へ戻る | 同時実行中のバックグラウンド処理を正しく表示するため |
| `BackgroundActivityTests.Run_TracksProgressAndSuccessfulCompletion` | 進捗、成功状態、完了時刻、アクティブ解除、完了履歴の削除を反映する | 正常系の状態遷移を一貫させるため |
| `BackgroundActivityTests.Run_RecordsFailure` | 例外発生時に失敗状態とエラーメッセージを記録する | 非同期処理の失敗を利用側が診断できるようにするため |
| `BackgroundActivityTests.Cancel_RequestsCancellationAndRecordsCanceled` | キャンセル要求をトークンへ伝え、キャンセル状態として完了する | 中断操作を失敗と区別して扱うため |
| `BackgroundActivityTests.BackgroundStatusOverlayHost_ReleaseRemovesWindowRegistration` | ウィンドウからオーバーレイを外すとホスト登録も削除する | 閉じたウィンドウの登録や参照が残るのを防ぐため |

## UI

| テスト | 保証する振る舞い | 理由 |
| --- | --- | --- |
| `UiTextFactoryTests.FactoryButton_RoutesTextThroughUiTextElement` | ボタン文字列の作成と更新を `UiTextElement` 経由で行う | IMGUIフォントを使う文字描画経路を迂回させないため |
| `UiStoryTests.StoryContract_RejectsMissingIdentity` | 識別子がないStoryを拒否する | カタログ内でStoryを一意に管理するため |
| `UiStoryTests.Catalog_DiscoversExternalStoryProviders` | 外部のStory providerからStoryを検出する | UIカタログを機能アセンブリから拡張できるようにするため |
| `UiStoryTests.CatalogStories_DeclareUsageLocations` | Foundation以外のStoryが実際の使用箇所を宣言する | 使用されていないUI componentを把握できるようにするため |
| `UiStoryTests.FeatureStory_IsRegisteredAndBuilds` | SceneSwitcher、ProjectStyle、HierarchyStyleのStoryをカタログから検出して構築する | 機能UIのStory登録漏れと構築失敗を検出するため |
| `UiIconTests.UiBuiltinIconResolver_TryResolve_AllRegisteredIcons` | 登録済みの組み込みアイコンをすべてテクスチャへ解決する | アイコン名の変更やUnityバージョン差による欠落を検出するため |
| `UiDesignTokenTests.UssAndCSharpPrimitiveTokens_AreInSync` | USSとC#のプリミティブなデザイントークン値を一致させる | UI ToolkitとIMGUIで寸法や文字サイズがずれるのを防ぐため |
| `UiDesignTokenTests.UssFiles_UseSharedDesignTokens` | USSが対象プロパティへ未定義の生値を直接指定しない | 寸法、余白、角丸、文字サイズを共通トークンで管理するため |
| `UiDesignTokenTests.CommonStyle_ImportsAggregateDesignTokens` | 共通スタイルが集約デザイントークンを読み込む | component側で共通トークンを利用可能にするため |
| `UiDesignTokenTests.CommonStyle_DefinesPopupSurfaceBorder` | ポップアップ表面の境界線が共通トークンで定義される | ポップアップの輪郭をテーマとデザイントークンに追従させるため |
| `UiColorTokenTests.UssFiles_UseSharedColorTokens` | パレットと色トークン定義以外のUSSに色の生値を置かない | テーマ差分と色設計をパレット層へ集約するため |
| `SearchFieldTests.Placeholder_UsesSearchFieldLayoutAndImguiTypography` | 検索欄のプレースホルダーが所定のクラスとIMGUI文字要素を使う | レイアウトとフォントキャッシュ対策を維持するため |
| `SearchFieldTests.SearchableTreeEmptyText_UsesImguiTypography` | 検索可能ツリーの空表示がIMGUI文字要素を使う | 空状態でも文字描画経路を統一するため |
| `InfoCardTests.HeaderTypography_UsesImguiFontCacheWorkaround` | 情報カード内の文字要素がIMGUI文字要素を使う | 情報カードでフォントキャッシュ由来の表示問題を避けるため |

## DepthIndicator

| テスト | 保証する振る舞い | 理由 |
| --- | --- | --- |
| `DepthIndicatorTests.Hierarchy_HiddenFollowingSiblingIsIgnored` | 非表示の後続兄弟を除外して最後の可視兄弟を判定する | Hierarchyに見えない要素でガイド線が余分に継続するのを防ぐため |
| `DepthIndicatorTests.Hierarchy_HiddenOnlyChildIsIgnored` | 非表示の子だけを持つ場合は可視の子なしと判定する | 表示上の階層構造とインジケーターを一致させるため |

## FolderContentOverlay

| テスト | 保証する振る舞い | 理由 |
| --- | --- | --- |
| `FolderContentOverlayTests.AssetPostprocessor_CollectsAncestorFolders` | 変更されたフォルダーから `Assets` までの祖先を収集する | 子孫の変更を上位フォルダーの集計へ反映するため |
| `FolderContentOverlayTests.RepresentativeIcon_RequiresStrictMajorityToPropagate` | 親へ伝播するアイコンには過半数を要求する | 少数派や同数の内容を親フォルダーの代表として誤表示しないため |
| `FolderContentOverlayTests.RepresentativeIcon_ReturnsNullWhenMostCommonIsTied` | 最多アイコンが同数の場合は表示アイコンを返さない | 同順位から不定な代表を選ぶのを防ぐため |

## ItemStyle

| テスト | 保証する振る舞い | 理由 |
| --- | --- | --- |
| `ItemStyleServiceTests.Update_DeduplicatesIdentityAndSeparatesScopes` | 重複した識別子を一度だけ更新し、Project と Hierarchy の同じ識別子を別の値として扱う | 共有化によって異なる表示領域の設定が混ざるのを防ぐため |
| `ItemStyleServiceTests.SetIcon_UsesBoundedRecentHistoryContract` | アイコン更新時に対象スコープと最大8件の履歴契約を保存先へ渡す | 最近使ったアイコンが無制限に増えるのを防ぐため |
| `ItemStyleInteractionTests.Selection_UsesGroupOnlyWhenHoveredFolderIsSelected` | 指している項目が選択中の場合だけ複数項目を一括編集する | 選択外の項目から意図せず複数対象を変更するのを防ぐため |

## Editor 機能

| テスト | 保証する振る舞い | 理由 |
| --- | --- | --- |
| `HierarchyStyleTests.Inheritance_UsesNearestExplicitColor` | 対象に明示色があれば親の色より優先する | 背景色の継承順を維持するため |
| `HierarchyStyleTests.VisibilityApi_RestoresActiveStateAndTag` | 公開 API で非表示にした GameObject を再表示すると active state と tag が復元される | HierarchyStyle へ統合した HiddenObjects の永続化境界と副作用を確認するため |
| `ProjectTabsTests.PinnedTab_RejectsNavigationToAnotherFolder` | 固定タブが別フォルダーへの移動を履歴へ記録しない | 固定したフォルダーが移動操作で置き換わるのを防ぐため |
| `SceneSwitcherTests.View_OrdersOpenFavoriteThenOtherAndAllowsNewName` | シーンを読み込み中、お気に入り、その他の順に並べ、一致しない有効名の作成を許可する | 一覧の優先順と作成入口を同時に維持するため |
| `HiddenObjectsTests.Exclusion_RemovesMatchedObjectAndItsDescendants` | 名前で除外した GameObject の子孫も一覧から除く | 除外対象の内部オブジェクトだけが一覧へ残るのを防ぐため |

## 実行と判定

Codexからの実行コマンド、Licensing ClientのIPC待ちを避ける条件、結果XMLによる成否判定は、リポジトリルートの `AGENTS.md` を正とする。

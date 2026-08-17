# テスト一覧

`src/Editor` にあるUnity 2022.3のEditModeテストを、テストが保証する振る舞いと維持する理由に分けて記載する。

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

## 実行と判定

Codexからの実行コマンド、Licensing ClientのIPC待ちを避ける条件、結果XMLによる成否判定は、リポジトリルートの `AGENTS.md` を正とする。

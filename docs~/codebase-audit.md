# コードベース監査

最終更新日: 2026-09-27

## 対象と方法

`src`配下から同梱Third Partyを除いたC# 343 files、85,074 linesと、Editor assembly 40個を対象にした。内訳はproduction 292 files／75,202 lines、test 18 files／4,374 lines、Story 33 files／5,498 linesである。

次の境界を横断して確認した。

- `ARCHITECTURE.md`と各asmdefの依存方向
- `UiTextFactory`を通す文字描画、Storyの実使用箇所、Fluent UI System Iconsの利用
- 非同期処理、cancel、event購読と`IDisposable`のライフサイクル
- HTTP、filesystem、JSON、SQLite、Archive Importの外部入力境界
- 例外の握りつぶし、`async void`、静的mutable state、reflection、直接filesystem mutation
- 800 lines以上のproduction classと機能横断の重複

## 結果

### 依存とUI規約

- 機能assemblyから別機能assemblyへの依存は、正本で許可された`ItemStyle`共有、AssetManager内部依存、MCP composition root、NDMF接続に収まっている。
- 文字を持つUI要素の生成と表示文字更新は`UiTextFactory`内部を除いてFactory境界を通っている。
- Unity Editor内部APIのreflection接続はCore backend、Asset Protection、任意導入のVRChat／Modular Avatar adapterへ局所化されている。
- Subscriber例外、cleanup失敗、cache読取失敗を無視するcatchは、他処理の継続またはbest-effort rollbackという境界に限定されている。通常処理の例外を無条件に成功扱いする箇所は確認されなかった。

### 適用済み改善

1. `Ee4vMcpHttpServer`は、`Content-Length`だけでなく実際に読んだbyte数で8 MiB上限を適用する。chunked requestから無制限に本文を確保しない。
2. MCPの`Host`をURIとして解析し、IPv4／IPv6 loopbackを同じ規則で検証する。起動失敗時は途中まで作成したlistenerを閉じ、停止中のlistenerとcancel sourceを先に切り離す。
3. Client切断中のerror response失敗を最終境界で処理し、fire-and-forget request taskへ未観測例外を残さない。
4. 外部サムネイルとcacheは1画像16 MiBに制限する。Item IDはcache file名へ直接連結せずhash化し、path separatorや無効文字をfile pathへ持ち込まない。
5. AssetManager UIの`async void`を0件にした。UI callbackは例外処理を内包する`Task`を明示的に開始し、cancel sourceは実行中taskが`finally`で破棄する。cancel側は破棄済みsourceをcontinuationが参照する競合を作らない。

## 2026-09-25の整理

- AssetManagerの改変Windowと派生Prefab選択Previewに重複していた、名前の単語分割とPrefab内選択範囲の判定を`AssetManagerPrefabUtility`へ統合した。部位分類と選択範囲の条件は維持する。
- AssetManager、Face Expression、MCPのPreviewで重複していた複製階層の`HideFlags`設定を`EditorSceneApi.HidePreviewHierarchy`へ統合した。
- 同梱Fluent UI System Iconsから、コード、USS、資料、GUID参照のない`code.png`、`document.png`、`music_note_2.png`、`video.png`と対応する`.meta`、元SVGを除き、選定リストとベンダー資料を更新した。
- EditModeテストは公開契約、状態遷移、外部境界に対応しており、同一保証を重ねたテストは確認されなかった。`test.md`の表と実際のテスト名のずれを修正し、クリップ複製の保証を追記した。
- 公開API型、UnityのMenuItem・初期化・AssetPostprocessorは、単純な参照数だけで削除しない。

## 2026-09-27の互換処理の整理

- AssetManager DBはschema v1に固定し、バージョン差の検出と自動削除を除去した。Collectionアイコンは本体の必須列へ統合し、アイコン・順序行がない旧DBの補完も除去した。schema変更後はDBを削除して再生成する。
- Projectサムネイルの旧保存先からの移動、旧Sceneアイコンの消去を除去した。
- Face Expressionの旧メニューレイヤー読み取り、旧口変形キャンセラーの除去、旧生成クリップ・アイコン・MA Menu Item・メニュー子Objectの除去を撤去した。現在生成する単一レイヤー、Motion、アイコンとMAコンポーネントの再適用だけを維持する。
- WindowGroupの旧所属設定のFollower変換を除去した。
- 廃止した互換動作だけを検証するテストとassertionを削除した。新しいテストは追加していない。Unity 2022.3と現在のVRChat／Modular Avatar SDKへの接続、入力検証、現在の生成物の再適用は引き続き対象とする。
- Unity 2022.3の関連する既存EditModeテスト57件の成功と、コンパイルエラー0件を確認した。変更通知テストのfixtureを現在のZIP拒否仕様に合うunitypackageへ修正した。新規DBでCollectionのアイコン変更、未指定時の維持、既定値、再オープン、並び替え、削除後の末尾追加も確認した。

## 残る構造課題

production classのうち19 filesが800 lines以上である。特に次は変更理由が複数集まっており、以後の機能変更と同時に段階分割する。

| File | Lines | 集約されている責務 | 分割境界 |
|---|---:|---|---|
| `AssetModificationWorkflowWindow.cs` | 6,012 | 派生Prefab操作、部位編集、Material、Preview、保存 | 操作状態、Preview、カテゴリ別編集 |
| `AssetManagerView.cs` | 3,560 | navigation、一覧、詳細、編集、import、非同期画像 | page presenter、detail presenter、async operation owner |
| `SqliteAssetManagerStore.cs` | 2,940 | schema、query、row mapping、transaction | schema bootstrap、item/file repository、collection repository |
| `DerivedAssetPrefabPickerWindow.cs` | 2,150 | 候補選択、Preview、部位絞り込み | 候補一覧、Preview操作 |
| `FaceExpressionView.cs` | 1,613 | clip UI、channel UI、filter、interaction state | clip header、channel list、pose controls |
| `FaceExpressionApi.cs` | 1,602 | inspect、write、validate、preview | query API、command API、preview adapter |
| `AssetManagerService.cs` | 1,446 | application command全般、同期、import、通知 | item/file command、source sync、import orchestration |
| `FaceExpressionClipEditor.cs` | 1,368 | clip読取、pose編集、binding変換 | reader、pose editor、binding mapper |
| `GestureMatrixControllerWriter.cs` | 1,332 | controller読取と生成、menu状態変換 | reader、plan builder、controller writer |

Unity Package内で新しい`.cs`を追加するとUnity生成の`.meta`も必要になるため、この監査では機械的なfile分割だけを行っていない。公開契約や保存形式を変えず、対象機能へ変更が入る時に責務単位で移動する。

## 継続時の判定基準

- 外部入力は宣言された長さだけを信用せず、streamまたは展開後の実量にも上限を持たせる。
- cancel sourceはoperationが所有して`finally`で破棄し、呼び出し側はcancelと参照解除だけを行う。
- 新しい機能間依存を追加せず、共有が必要なら`Feature/Shared`へ置いて利用機能と理由を`features/README.md`へ記録する。
- 巨大classの分割は行数だけを目的にせず、公開API、Unity object lifecycle、transaction、UI stateの所有者が一つになる境界で行う。

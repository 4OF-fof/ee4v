# コードベース監査

最終監査日: 2026-09-23

## 対象と方法

`src`配下から同梱Third Partyを除いたC# 339 files、75,439 linesと、Editor assembly 40個を対象にした。内訳はproduction 289 files／65,693 lines、test 18 files／4,325 lines、Story 32 files／5,421 linesである。

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

## 残る構造課題

production classのうち16 filesが800 lines以上である。特に次は変更理由が複数集まっており、以後の機能変更と同時に段階分割する。

| File | Lines | 集約されている責務 | 分割境界 |
|---|---:|---|---|
| `AssetManagerView.cs` | 3,504 | navigation、一覧、詳細、編集、import、非同期画像 | page presenter、detail presenter、async operation owner |
| `SqliteAssetManagerStore.cs` | 2,839 | schema、query、row mapping、transaction | schema bootstrap、item/file repository、collection repository |
| `FaceExpressionView.cs` | 1,608 | clip UI、channel UI、filter、interaction state | clip header、channel list、pose controls |
| `FaceExpressionApi.cs` | 1,602 | inspect、write、validate、preview | query API、command API、preview adapter |
| `AssetManagerService.cs` | 1,445 | application command全般、同期、import、通知 | item/file command、source sync、import orchestration |
| `FaceExpressionClipEditor.cs` | 1,368 | clip読取、pose編集、binding変換 | reader、pose editor、binding mapper |
| `GestureMatrixControllerWriter.cs` | 1,332 | controller読取と生成、menu状態変換 | reader、plan builder、controller writer |

Unity Package内で新しい`.cs`を追加するとUnity生成の`.meta`も必要になるため、この監査では機械的なfile分割だけを行っていない。公開契約や保存形式を変えず、対象機能へ変更が入る時に責務単位で移動する。

## 継続時の判定基準

- 外部入力は宣言された長さだけを信用せず、streamまたは展開後の実量にも上限を持たせる。
- cancel sourceはoperationが所有して`finally`で破棄し、呼び出し側はcancelと参照解除だけを行う。
- 新しい機能間依存を追加せず、共有が必要なら`Feature/Shared`へ置いて利用機能と理由を`features/README.md`へ記録する。
- 巨大classの分割は行数だけを目的にせず、公開API、Unity object lifecycle、transaction、UI stateの所有者が一つになる境界で行う。

# ee4v MCP

`src/Editor/Mcp`は、VRChatアバターの調査、表情改変、ee4vのAssetManagerをAIクライアントから操作するための統合層です。Hierarchy、Component、Scene、Prefab、Animator Controller、Materialなどの汎用操作は既存のUnity MCPへ任せ、ee4v MCPは複数のUnity APIとVRChatの規則をまとめて扱う高水準操作を所有します。

## 接続

Unity Editorを通常起動すると、既定で`http://127.0.0.1:48884/mcp`にStreamable HTTP形式のMCP endpointを開始します。バッチモードでは自動起動しません。

Unity menuから次の操作を行えます。

- `ee4v/MCP/Start Server`: 停止中のserverを現在のEditor sessionで開始する
- `ee4v/MCP/Stop Server`: serverを現在のEditor sessionで停止する。次回の通常起動では再び自動開始する
- `ee4v/MCP/Log Connection Information`: endpointとCodex用設定例をConsoleへ出力する

Codexの`config.toml`には次を追加します。

```toml
[mcp_servers.ee4v]
url = "http://127.0.0.1:48884/mcp"
```

portはEditorPrefsの`ee4v.mcp.port`へ保存し、既定値は`48884`です。通常のUnity Editor起動時は常に自動開始し、バッチモードでは開始しません。

## Transportと安全境界

- Listenerは`127.0.0.1`だけへbindし、remote endpoint、`Host` header、指定されている場合の`Origin`もloopbackか検証する。IPv4、IPv6をURIとして解析してloopbackを判定する。1 requestは8 MiBまでとし、`Content-Length`がないchunked requestもstream読取時に上限を適用する。
- 認証は持たない。同じ端末のprocessはendpointへ接続できるため、外部interfaceへproxyまたは公開しない。
- `/mcp`はJSON-RPCの`initialize`、`ping`、`tools/list`、`tools/call`とnotificationを処理する。`/health`はserverの生存確認だけに使用する。
- Unity objectへ触れるtool実行は`EditorApplication.update`からmain threadへdispatchする。
- dispatch待ちの操作がないEditor frameではqueueの配列を生成しない。
- tool結果は機械処理用の`structuredContent`と、同じ内容のtext contentを返す。preview PNGはimage contentも返す。
- tool annotationでread-only、破壊性、冪等性、open-world accessを宣言する。
- AssetManagerではmetadata編集と、明示的な`ee4v_asset_import`によるUnity ProjectへのImportを公開する。同期、Item／Fileの削除、File登録、新規Item作成は公開しない。Collectionの作成・削除も公開し、削除toolは削除前の名前と条件を返す。Importは既存Project Assetを上書きする可能性があるため、破壊性あり・冪等性なしのannotationを付ける。
- AssetManagerのPrefab調査は、既にUnity Projectへ取り込まれたPrefab assetとAssetManagerで作成済みの派生Prefabだけを対象にする。Prefab調査・previewからImportを暗黙には開始しない。必要な場合は登録済みFileを明示的なImport toolで取り込む。
- Prefab previewはUnityの内部Preview sceneへだけinstanceを作成し、Play Mode、利用者のScene、Prefab assetを変更しない。外部APIへ画像を送信しない。
- 表情Clipと表情アニメーションの更新は`expectedRevision`で競合を検出できる。静止Clipは作成・部分更新・完全置換を一つのtoolで扱う。アニメーションはポーズ単位の追加・更新・並び替え・Loop変更を一つの編集toolへまとめ、削除は破壊性ありの別toolとする。`dryRun`で書き込み前の結果を確認できる。

## Object参照

`ee4v_find_avatars`は、loaded SceneとPrefab Modeから後続toolへ渡す`avatarRef`を返します。参照にはUnityの`GlobalObjectId`を優先します。Project内Prefabの検索は汎用Unity MCPへ任せます。

書き込みtoolは取得済みの参照を再解決し、対象がAvatar配下にあること、永続Prefab assetを直接編集しようとしていないこと、必要なVRChat SDK型が存在することを再検証します。

## Tool設計

現行catalogは28 tools。対象と編集単位が同じ操作は、部分更新または明示的なactionへ統合する。読み取り、書き込み、破壊的な削除はannotationと承認単位が異なるため分離する。検索／一覧と詳細取得は返す情報量と用途が異なるため維持する。Avatar inventoryとauditも、事実の取得と問題の検出を分ける。

Item編集へ名前・説明・Tag・Archive・Import Targetを統合し、一括編集を維持する。Collectionの作成と更新はupsertへ、ポーズ追加・更新・移動・Loop変更はanimation編集へ、Clipの調査とvalidationはClip調査へ統合する。左右Gestureの単独更新はFacialSetのpatch適用で扱う。

## Tool catalog

### 接続とAvatar監査

| tool | 動作 |
|---|---|
| `ee4v_server_status` | Unity version、Project、Play／compile状態を返す |
| `ee4v_find_avatars` | loaded SceneとPrefab ModeからAvatarを探す |
| `ee4v_inspect_avatar` | 判定を加えず、rig、mesh、triangle、material、texture、animation、PhysBone、Constraint、Expression Parametersの事実と数量を返す |
| `ee4v_audit_avatar` | inventoryを重複して返さず、Descriptor、Humanoid、Body、各上限、Constraint、PhysBoneについて対処可能なfindingだけを返す |

監査で使うtriangle数の目安はPC `70,000`、Android `20,000`です。これはupload可否を断定する値ではなく、一般的な制作時の注意としてwarningを返します。

### Face Expression

| tool | 動作 |
|---|---|
| `ee4v_list_blendshape_presets` | 保存済みFBX別BlendShapeプリセットのGUID、path、Mapping数を列挙する |
| `ee4v_get_blendshape_preset` | 指定FBXの全Mappingとrevisionを返す |
| `ee4v_update_blendshape_preset` | Mappingの役割名、左右、口形状、見た目の部位・グループ名を部分更新する |
| `ee4v_inspect_face` | Avatarの編集可能なBlendShape channelとpreset分類を列挙する。`clipPath`指定時はClip値、revision、validation結果も返す |
| `ee4v_upsert_expression_clip` | 静止表情Clipを`create`、`patch`、`replace`のいずれかで作成・更新する。複数ポーズClipの更新は拒否する |
| `ee4v_inspect_expression_clip` | 静止・animation Clipのrevision、validationと件数、ポーズ順・時刻・遷移・名前・参照Clip・Loopを取得する。`includeAnimation: false`はvalidationだけ、`includeChannels: true`はポーズ値も返す |
| `ee4v_edit_expression_animation` | `action`の`add`、`update`、`move`、`setLoop`でポーズ追加・部分更新・隣接移動・Loop変更を行う |
| `ee4v_remove_expression_pose` | ポーズを削除して時間の空きを詰める。最後の1ポーズは削除しない |
| `ee4v_render_expression_preview` | AvatarへClipを指定時刻で適用したpreviewをPNGとして返す。描画可能なSkinnedMeshがなければ`expression_preview_unavailable`を返す |
| `ee4v_remap_expression_clip` | Renderer pathとBlendShape名の完全一致を優先し、一致しないchannelはFBX別presetのroleとsideで別Avatarへ対応付ける |
| `ee4v_get_facial_configuration` | Gesture matrix、menu専用表情、Blink、口固定の現在値を返す |
| `ee4v_plan_facial_set_apply` | FacialSet適用の入力、生成物、前提条件を検証する |
| `ee4v_apply_facial_set` | FX Controller、Expression Parameters、Expression Menu、Modular Avatar installerを生成・更新する。`mode: patch`は指定Gesture／Menuだけ更新し、`mode: replace`は全設定を置き換える。`dryRun`では設定と前提条件だけを検証する |

AnimationClipの一般検索は汎用Unity MCPへ任せます。`ee4v_upsert_expression_clip`の`patch`は未指定channelを維持し、`replace`は未指定BlendShape curveを削除します。同じ内容を再指定した場合は`changed: false`としてAssetを書き直しません。複数ポーズClipはこの静止表情toolで誤って単一frameへ戻さず、アニメーション専用toolだけで更新します。

自動生成済みのFBX別プリセットをagentが整える場合は、一覧から`assetGuid`を選び、詳細の`revision`、`meshLocalId`（文字列）、`shapeName`を取得します。更新toolの`changes`には編集するMappingと項目だけを指定します。`role`と`appearanceGroup`は空文字で消去でき、`side`は空文字／`L`／`R`、`appearancePart`は空文字（自動）／`expression`／`head`／`chest`／`waist`／`shoulders`／`arms`／`hands`／`legs`／`feet`／`other`を指定できます。区切り見出しは編集できません。`expectedRevision`が一致しない場合は再取得が必要です。`dryRun: true`は変更件数と適用後のrevisionを返しますが、保存しません。書き込みは既存プリセットに限り、他のMappingと未指定項目を維持します。保存時はUIと同じ保存先・通知を使用します。

アニメーション編集は`ee4v_inspect_expression_clip`でポーズindexとrevisionを取得し、`ee4v_edit_expression_animation`のactionを選び、書き込みtoolをまず`dryRun: true`で呼んでから同じ入力で適用します。ポーズ追加は指定ポーズの表情を複製し、`transitionDuration`後へ挿入します。ポーズ更新では参照Clipと明示的なchannel値を同時に指定できません。参照Clipをローカル編集へ戻して値も変更する場合は、空の`sourceClipPath`と`channels`を同じ呼び出しへ指定します。参照Clipとポーズ名はUIと同じ`.anim`内のsub-assetへ保存され、移動と削除にも追従します。更新用revisionはアニメーション調査の結果を使用します。

`ee4v_remap_expression_clip`はRenderer pathとBlendShape名が完全一致するchannelを直接対応付け、それ以外だけpreset roleとsideを使用します。対応不能channelは結果へ残し、無言で別名へ割り当てません。表情previewにはMeshが割り当てられた描画可能な`SkinnedMeshRenderer`が1つ以上必要で、存在しない場合は空画像を成功扱いにしません。アニメーションClipでは`time`を指定して各ポーズや遷移途中を描画できます。

### AssetManager

| 分類 | tools |
|---|---|
| 検索・詳細 | `ee4v_asset_search`、`ee4v_asset_get_item`。詳細は取り込み済みGUIDと派生Prefabから解決した`prefabCandidates`も返す |
| Item編集 | `ee4v_asset_edit_items`。名前・説明・Tag・Archive・Import Targetを部分更新する |
| File解析 | `ee4v_asset_analyze_file` |
| Import | `ee4v_asset_import`。Itemの登録済み対象と依存、またはFile内の指定エントリーを取り込む |
| 依存設定 | `ee4v_asset_set_dependencies` |
| Collection | `ee4v_asset_list_collections`、`ee4v_asset_upsert_collection`、`ee4v_asset_delete_collection` |
| Prefab調査 | `ee4v_asset_inspect_prefab`、`ee4v_asset_render_prefab_preview` |

`ee4v_asset_edit_items`は`itemIds`に1件以上を渡し、`name`、`description`、`tags`、`archived`、`targets`のうち変更する項目だけを指定する。未指定項目は保持する。空のdescriptionは消去、空のtags／targets配列は全解除、`archived: false`はArchive解除を意味する。targets変更は1 Itemだけを対象とし、各要素はfileId、targetPath、任意のgroupNameを持つ。groupNameを省略すると既存Groupを保持し、空文字はGroup解除を意味する。Eagle由来Itemの名前・説明は変更できず、Eagle由来Tagは維持する。名前・説明・Tag・Archiveの一括指定も可能。複合編集は複数の公開APIを順に実行するため全体transactionではなく、途中失敗時はerrorの`details.appliedChanges`に完了した編集を返す。

```json
{"itemIds":["item-id"],"description":"更新する説明","tags":["avatar/example"],"archived":false}
```

`ee4v_asset_upsert_collection`はcollectionId未指定なら作成し、nameとfilterを必須とする。ID指定なら既存Collectionを部分更新し、未指定のname、icon、filterを保持する。削除は別toolに維持し、削除前のCollectionを返す。

`ee4v_asset_set_dependencies`はdependentTargetsとdependencyTargetsの両方にfileIdとtargetPathを渡す。ZIPは空pathで指定せず、解析で得た内部実体を選ぶ。

MCPはUIと同じ公開API、validation、change notificationを使用し、独自のDB接続・schema・Import処理は持たない。UIとMCPは`AssetManagerFactory.OpenSession`で同じDBのmanagerを共有するため、MCPからのImportや編集も画面、Project Thumbnail、AssetProtectionへ通知される。DBは共通data rootの`asset-manager-v1.db`。同期、新規Item／File登録、Item／File削除はUIから実行する。

### 明示的なImport

`ee4v_asset_import`は次のいずれか一方を指定する。両方を同時に渡すことはできない。

| 入力 | 動作 |
|---|---|
| `itemId`、任意の`selectedTargets` | `ImportItemTargets`で登録済み対象を取り込む。groupNameが空の対象はすべて含め、名前付きGroupは各Groupから1件を選ぶ。実体ごとの依存を先に取り込む |
| `fileId`、1件以上の`paths` | `ImportFileEntries`で指定したFile内のエントリーを一時的に取り込む。保存済みImport Targetは変更しない |

```json
{"itemId":"item-id","selectedTargets":[{"fileId":"file-id","targetPath":"variant-a.unitypackage"}]}
```

```json
{"fileId":"file-id","paths":["materials/example.png"]}
```

File全体を取り込む場合はpathsへ空文字列を1件指定する。ただしZIP自体の直接Importは拒否する。未登録File、所属のないFile、Archive済みFile、無効な選択やpathは既存APIの検証で拒否する。出力はstate、fileIds、assetGuids、errorMessage。失敗・キャンセルはMCPのerrorとして返し、途中まで取り込んだ情報も保持する。Prefab調査からImportは自動開始しない。

### 実Prefabを比較する流れ

1. `ee4v_asset_search`の`query`と`tags`でItemを5〜10件へ絞る。
2. 各Itemを`ee4v_asset_get_item`で取得し、`prefabCandidates`からProject内に実在するPrefab GUIDを選ぶ。候補はItemの取り込み済みAsset GUIDと、親Item IDを持つAssetManager派生Prefabから解決する。
3. `ee4v_asset_render_prefab_preview`の同じview、size、backgroundで候補を描画し、image contentを比較する。`turntable`は8方向を4×2へ並べる。
4. 選んだPrefabを`ee4v_asset_inspect_prefab`で調査し、Renderer path、Material slot、Material path、Shader、Texture、Mesh、BlendShape、参照切れを得る。
5. `unityMcpHandoff`のRenderer pathとMaterial pathを既存Unity MCPへ渡し、Material編集は既存Unity MCPで行う。

未ImportのZIPまたはunitypackageしか存在しない場合、`prefabCandidates`は空になり、`prefabCandidateResolution.emptyReason`または`unresolvedImportedAssets`に理由を返します。Prefab調査はImportを開始しません。必要な場合は`ee4v_asset_import`を明示的に呼び、完了後にItem詳細を再取得します。

### Prefab toolの契約

| tool | 主な入力 | 主な出力 | Projectへの書き込み |
|---|---|---|---|
| `ee4v_asset_get_item` | `itemId` | Item、File、取り込み済みGUID、`prefabCandidates`。各候補はPrefab GUID／path／name／type、Variant親、dependency hash、preview可否・不可理由・警告を持つ | なし |
| `ee4v_asset_inspect_prefab` | `prefabGuid`、または`Assets/`／`Packages/`から始まる`prefabPath` | hierarchy、Renderer、Meshとtriangle、bounds、Material slot、Shader、Texture、BlendShape、Missing Script、参照切れ、preview可否・警告、Unity MCP handoff | なし |
| `ee4v_asset_render_prefab_preview` | `prefabGuid`または`prefabPath`、`viewPreset`、`width`、`height`、`background`、`forceRefresh` | 固定撮影条件のPNG image content、dependency hash、cache path、cache hit、警告、Unity／Render Pipeline情報 | 再生成可能なcacheだけ |

`prefabGuid`と`prefabPath`を両方渡す場合は同じAssetを指す必要があります。絶対path、`Assets/`と`Packages/`以外のpath、PrefabでないAssetは拒否します。`ee4v_asset_inspect_prefab`とpreviewはいずれもPrefab、Scene、AssetManager DBを変更しません。

利用例:

```json
{
  "prefabGuid": "0123456789abcdef0123456789abcdef",
  "viewPreset": "turntable",
  "width": 1024,
  "height": 1024,
  "background": "neutral",
  "forceRefresh": false
}
```

### Preview cache

PNGはSQLiteへ格納せず、共通data rootの`asset-preview/<prefab-guid>/<dependency-hash>/`以下へ保存します。共通data rootがProjectの`Assets`内に設定されている場合だけ、`Library/ee4v-cache`へ退避します。file名にはrender profile version、view、size、backgroundを含め、同じ条件の再呼び出しでは再利用します。隣接JSONにはPrefab GUID、dependency hash、render profile version、view、image path、size、Unity version、Render Pipeline、作成日時、警告、errorを記録します。cache directory名と描画条件の世代は分離し、現在のrender profile versionは`v2`です。

dependency hashには`AssetDatabase.GetAssetDependencyHash`を使用します。Prefab、Variant親、Mesh、Material、Texture、ShaderなどUnityが依存関係として追跡するAssetが変化すると保存先hashが変わるため、古い画像を使用しません。`forceRefresh: true`は同じ条件を再撮影します。cacheは再生成可能であり、Unity Project Assetの状態には含めません。

撮影は`PreviewRenderUtility`の内部Preview sceneへPrefabを直接instance化し、Renderer boundsを基準にscaleを正規化し、各viewへ投影した横幅と縦幅を使ってcamera framingを決めます。照明、背景、camera FOVは固定し、instance上の`Behaviour`を無効化してParticle Systemを停止します。描画後はinstanceを破棄します。空のMaterial slotやMaterial slotなしは`missing_material`警告として返し、撮影を続けます。Missing Mesh、Shader、描画可能Rendererなし、無効なboundsは構造化errorとして返します。

RAG、Embedding、Vector DB、Semantic Search、自動caption、全Prefabの事前indexは持ちません。検索metadataで候補を絞り、要求されたPrefabだけをオンデマンド撮影します。

## 汎用Unity MCPとの使い分け

ee4v MCPは、Avatarの発見と監査、表情ClipとFacialSet、AssetManagerのようにドメイン規則をまとめて検証できる操作へ使用します。次は既存Unity MCPを使用します。

- 任意GameObjectの作成、削除、Transform変更
- 任意ComponentとSerializedPropertyの読み書き
- Scene、Prefab、Animator Controller、Material、Shaderの汎用編集。`ee4v_asset_inspect_prefab`で得たMaterialの実編集もこちらで行う
- Console、Play Mode、screenshot、build、test、任意Editor C#実行

ee4v MCPは任意C#実行toolを公開しません。高水準toolで扱っていない変更は汎用Unity MCPで実行し、最後に`ee4v_audit_avatar`でAvatar固有の前提を確認します。

## 実装境界

- `Ee4v.Mcp.Editor`はMCP protocol、loopback HTTP server、tool registry、Unity main-thread dispatch、各moduleへのadapterを所有する。
- Face Expressionは機能moduleの公開APIをMCP adapterから呼ぶ。MCPのJSON DTOを機能moduleへ持ち込まない。
- AssetManagerは既存のContractsとInfrastructureを利用する。
- VRChat SDKの型は任意依存としてreflectionで解決し、未導入時は構造化errorを返す。
- MCPは複数機能を束ねる外部integration／composition rootであり、機能module同士の依存を追加しない。

## 未実装範囲

- remote接続、認証、TLS
- Unity Editor未起動時のheadless処理
- VRChat upload、Blueprint ID管理、VRChat API操作
- shader固有設定、Mesh Cutter／Shape Changerの自動構成
- Unity 6での検証

これらは汎用Unity MCPで個別に編集できる場合もありますが、ee4v MCPとしてはdomain validationと安全な再適用方法が定義できるまで専用writerを公開しません。

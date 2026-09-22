# ee4v MCP

`src/Editor/Mcp`は、VRChatアバターの調査、表情改変、ee4vのAssetManagerをAIクライアントから操作するための統合層です。Hierarchy、Component、Scene、Prefab、Animator Controller、Materialなどの汎用操作は既存のUnity MCPへ任せ、ee4v MCPは複数のUnity APIとVRChatの規則をまとめて扱う高水準操作を所有します。

## 接続

Unity Editorを通常起動すると、既定で`http://127.0.0.1:48884/mcp`にStreamable HTTP形式のMCP endpointを開始します。バッチモードでは自動起動しません。

Unity menuから次の操作を行えます。

- `ee4v/MCP/Start Server`: serverを開始し、次回以降の通常起動でも自動開始する
- `ee4v/MCP/Stop Server`: serverを停止し、次回以降の自動開始を無効にする
- `ee4v/MCP/Log Connection Information`: endpointとCodex用設定例をConsoleへ出力する

Codexの`config.toml`には次を追加します。

```toml
[mcp_servers.ee4v]
url = "http://127.0.0.1:48884/mcp"
```

portはEditorPrefsの`ee4v.mcp.port`、自動開始は`ee4v.mcp.enabled`へ保存します。portの既定値は`48884`です。

## Transportと安全境界

- Listenerは`127.0.0.1`だけへbindし、remote endpoint、`Host` header、指定されている場合の`Origin`もloopbackか検証する。1 requestは8 MiBまでとする。
- 認証は持たない。同じ端末のprocessはendpointへ接続できるため、外部interfaceへproxyまたは公開しない。
- `/mcp`はJSON-RPCの`initialize`、`ping`、`tools/list`、`tools/call`とnotificationを処理する。`/health`はserverの生存確認だけに使用する。
- Unity objectへ触れるtool実行は`EditorApplication.update`からmain threadへdispatchする。
- tool結果は機械処理用の`structuredContent`と、同じ内容のtext contentを返す。preview PNGはimage contentも返す。
- tool annotationでread-only、破壊性、冪等性、open-world accessを宣言する。
- AssetManagerでは同期、削除、File登録、Unity ProjectへのImport、新規Item／Collection作成をMCPへ公開しない。書き込みは、事前に読み取った値を再設定すれば元へ戻せるmetadata編集だけに限定する。
- 表情Clipの更新は`expectedRevision`で競合を検出できる。作成、部分更新、完全置換を分け、`dryRun`で書き込み前の結果を確認できる。

## Object参照

`ee4v_find_avatars`は、loaded SceneとPrefab Modeから後続toolへ渡す`avatarRef`を返します。参照にはUnityの`GlobalObjectId`を優先します。Project内Prefabの検索は汎用Unity MCPへ任せます。

書き込みtoolは取得済みの参照を再解決し、対象がAvatar配下にあること、永続Prefab assetを直接編集しようとしていないこと、必要なVRChat SDK型が存在することを再検証します。

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
| `ee4v_inspect_face` | Avatarの編集可能なBlendShape channelとpreset分類を列挙する。`clipPath`指定時はClip値、revision、validation結果も返す |
| `ee4v_upsert_expression_clip` | 単一frame表情Clipを`create`、`patch`、`replace`のいずれかで作成・更新する |
| `ee4v_validate_expression_clip` | binding、keyframe、値、object curve、Animation Eventを検査する |
| `ee4v_render_expression_preview` | AvatarへClipを適用したpreviewをPNGとして返す |
| `ee4v_remap_expression_clip` | FBX別presetのroleとsideを使って別Avatarへchannelを対応付ける |
| `ee4v_get_facial_configuration` | Gesture matrix、menu専用表情、Blink、口固定の現在値を返す |
| `ee4v_set_gesture_expression` | 他の割り当てを維持し、左右Gestureの1組へ表情Clip、Blink、口固定、menu名を設定する |
| `ee4v_plan_facial_set_apply` | FacialSet適用の入力、生成物、前提条件を検証する |
| `ee4v_apply_facial_set` | FX Controller、Expression Parameters、Expression Menu、Modular Avatar installerを生成・更新する |

AnimationClipの一般検索は汎用Unity MCPへ任せます。`ee4v_upsert_expression_clip`の`patch`は未指定channelを維持し、`replace`は未指定BlendShape curveを削除します。更新用revisionは`clipPath`付きの`ee4v_inspect_face`から取得します。`ee4v_remap_expression_clip`は対応不能channelを結果へ残し、無言で別名へ割り当てません。

### AssetManager

| 分類 | tools |
|---|---|
| 検索・詳細 | `ee4v_asset_search`、`ee4v_asset_get_item` |
| Item metadata | `ee4v_asset_update_item`、`ee4v_asset_set_tags`、`ee4v_asset_set_archived` |
| File解析 | `ee4v_asset_analyze_file` |
| Import設定 | `ee4v_asset_set_targets`、`ee4v_asset_set_dependencies` |
| Collection | `ee4v_asset_list_collections`、`ee4v_asset_update_collection` |

MCPは`AssetManager`の公開APIを通してDB内のmetadataを読み取り・編集します。DB fileはUser Settingsの共通data rootにある`asset-manager-v1.db`です。MCP独自のDB書き込みやschemaは持ちません。同期、Import、新規登録、作成、削除はAssetManager UIで利用者が明示的に実行します。

## 汎用Unity MCPとの使い分け

ee4v MCPは、Avatarの発見と監査、表情ClipとFacialSet、AssetManagerのようにドメイン規則をまとめて検証できる操作へ使用します。次は既存Unity MCPを使用します。

- 任意GameObjectの作成、削除、Transform変更
- 任意ComponentとSerializedPropertyの読み書き
- Scene、Prefab、Animator Controller、Material、Shaderの汎用編集
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

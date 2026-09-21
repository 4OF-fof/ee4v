# ee4v MCP

`src/Editor/Mcp`は、VRChatアバター改変とee4vのAssetManagerをAIクライアントから操作するための統合層です。Hierarchy、Component、Scene、Prefab、Animator Controller、Materialなどの汎用操作は既存のUnity MCPへ任せ、ee4v MCPは複数のUnity APIとVRChat／Modular Avatarの規則をまとめて扱う高水準操作を所有します。

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
- `ee4v_asset_delete_items`だけを破壊的toolとして公開する。`confirm=true`とarchive済みであることを要求し、Eagleまたはee4v以外のFileを含むItemを拒否する。ee4v FileはSource実体も削除する。
- Scene／Prefab Mode上のModular Avatar書き込みはUnity Undoへまとめる。Project内のPrefab assetへ直接書き込まず、Prefab ModeまたはSceneへの配置を要求する。
- 表情Clipの更新は`expectedRevision`で競合を検出できる。作成、部分更新、完全置換を分け、`dryRun`で書き込み前の結果を確認できる。

## Object参照

`ee4v_find_avatars`は後続toolへ渡す`avatarRef`を返します。SceneとPrefab ModeのobjectにはUnityの`GlobalObjectId`を優先し、Project内assetにはasset pathを使用します。曖昧なHierarchy pathは拒否します。

書き込みtoolは取得済みの参照を再解決し、対象がAvatar配下にあること、永続Prefab assetを直接編集しようとしていないこと、必要なVRChat SDK／Modular Avatar型が存在することを再検証します。

## Tool catalog

### 接続とAvatar監査

| tool | 動作 |
|---|---|
| `ee4v_server_status` | Unity version、Project、Play／compile状態を返す |
| `ee4v_find_avatars` | loaded scene、Prefab Mode、またはProject内PrefabからAvatarを探す |
| `ee4v_inspect_avatar` | rig、mesh、triangle、material、texture、animation、PhysBone、Contact、Constraint、Modular Avatar、Expression Parametersを集計する |
| `ee4v_audit_avatar` | Descriptor、Humanoid、Body、triangle目安、Expression Parametersの256-bit上限、Menuの8-control上限、Unity Constraint、Humanoid boneをrootにしたPhysBoneを監査する |
| `ee4v_plan_outfit_setup` | 衣装のbone対応、同名BlendShape、material構成を読み取り専用で分析する |
| `ee4v_list_contacts` | Contact Sender／Receiverのshape、tag、parameterを列挙する |
| `ee4v_upsert_contact` | Avatar内の指定bone下へee4v所有のSender／Receiverを作成または更新する |

監査で使うtriangle数の目安はPC `70,000`、Android `20,000`です。これはupload可否を断定する値ではなく、一般的な制作時の注意としてwarningを返します。

### Modular Avatarによる一般改変

| tool | 動作 |
|---|---|
| `ee4v_list_avatar_controls` | Menu Itemと同じGameObject上のreaction componentを列挙する |
| `ee4v_apply_outfit_setup` | 衣装ArmatureへMerge Armatureを設定し、Bodyと同名のBlendShapeを各衣装RendererへBlendshape Syncとして追加する |
| `ee4v_upsert_object_toggle` | ee4v所有のToggle controlを作成または更新し、複数Objectのactive stateを切り替える |
| `ee4v_upsert_material_toggle` | ee4v所有のToggle controlを作成または更新し、Rendererの1 material slotを置換する |

生成controlはAvatar直下の`ee4v MCP Controls`へまとめ、`controlName`を安定IDとして再利用します。同名controlへ別reaction typeを上書きしません。同期ToggleはExpression Parametersを1 bit消費することをplan結果へ含めます。衣装適用は既存の無関係なBlendshape Sync bindingを維持し、同じsource／local名のbindingを重複追加しません。

Contactは`parentRef`直下の`ee4v Contact - <contactName>`を安定した生成Objectとして再利用します。SphereとCapsule、collision tag、Sender／Receiver、Receiver parameter、Constant／OnEnter／Proximity、local-only、self／othersを設定できます。書き込み前に`dryRun`を使用でき、kindが異なる同名Contactは上書きしません。

### Face Expression

| tool | 動作 |
|---|---|
| `ee4v_inspect_face` | Avatarの編集可能なBlendShape channelとpreset分類を列挙する |
| `ee4v_list_expression_clips` | 表情libraryまたは指定folderのAnimationClipを列挙する |
| `ee4v_inspect_expression_clip` | Clip内channel、revision、validation結果を返す |
| `ee4v_upsert_expression_clip` | 単一frame表情Clipを`create`、`patch`、`replace`のいずれかで作成・更新する |
| `ee4v_validate_expression_clip` | binding、keyframe、値、object curve、Animation Eventを検査する |
| `ee4v_render_expression_preview` | AvatarへClipを適用したpreviewをPNGとして返す |
| `ee4v_remap_expression_clip` | FBX別presetのroleとsideを使って別Avatarへchannelを対応付ける |
| `ee4v_get_facial_configuration` | Gesture matrix、menu専用表情、Blink、口固定の現在値を返す |
| `ee4v_plan_facial_set_apply` | FacialSet適用の入力、生成物、前提条件を検証する |
| `ee4v_apply_facial_set` | FX Controller、Expression Parameters、Expression Menu、Modular Avatar installerを生成・更新する |

`ee4v_upsert_expression_clip`の`patch`は未指定channelを維持し、`replace`は未指定BlendShape curveを削除します。`ee4v_remap_expression_clip`は対応不能channelを結果へ残し、無言で別名へ割り当てません。

### PhysBone Collider

| tool | 動作 |
|---|---|
| `ee4v_plan_physbone_colliders` | 軽量、通常、フルのpresetから配置候補とPhysBone割り当て候補を返す |
| `ee4v_apply_physbone_colliders` | 編集済み候補を生成Prefabへ反映し、選択したPhysBoneへ割り当てる |

適用は既存のPhysBone Collider機能と同じgatewayを使うため、Avatar直下のee4v所有Prefab、MA Bone Proxy、既存生成物の置換というUI操作と同じ契約を持ちます。

### AssetManager

| 分類 | tools |
|---|---|
| 検索・詳細 | `ee4v_asset_search`、`ee4v_asset_get_item` |
| Item編集 | `ee4v_asset_create_item`、`ee4v_asset_update_item`、`ee4v_asset_set_tags`、`ee4v_asset_set_archived`、`ee4v_asset_delete_items` |
| File | `ee4v_asset_register_file`、`ee4v_asset_analyze_file` |
| Import構成 | `ee4v_asset_set_targets`、`ee4v_asset_set_dependencies`、`ee4v_asset_import` |
| Collection | `ee4v_asset_list_collections`、`ee4v_asset_upsert_collection`、`ee4v_asset_delete_collection` |
| Source同期 | `ee4v_asset_sync_library` |

MCPは`AssetManager`の公開APIを通してDB、ee4v Source、Eagle Source、Import処理を操作します。DB fileはUser Settingsの共通data rootにある`asset-manager-v1.db`です。MCP独自のDB書き込みやschemaは持ちません。

## 汎用Unity MCPとの使い分け

ee4v MCPは、Avatarの発見と監査、表情ClipとFacialSet、PhysBone Collider、Modular Avatar衣装・Toggle、AssetManagerのようにドメイン規則をまとめて検証できる操作へ使用します。次は既存Unity MCPを使用します。

- 任意GameObjectの作成、削除、Transform変更
- 任意ComponentとSerializedPropertyの読み書き
- Scene、Prefab、Animator Controller、Material、Shaderの汎用編集
- Console、Play Mode、screenshot、build、test、任意Editor C#実行

ee4v MCPは任意C#実行toolを公開しません。高水準toolで扱っていない変更は汎用Unity MCPで実行し、最後に`ee4v_audit_avatar`でAvatar固有の前提を確認します。

## 実装境界

- `Ee4v.Mcp.Editor`はMCP protocol、loopback HTTP server、tool registry、Unity main-thread dispatch、各moduleへのadapterを所有する。
- Face ExpressionとPhysBone Colliderは各機能moduleに公開APIを置き、MCP adapterから呼ぶ。MCPのJSON DTOを機能moduleへ持ち込まない。
- AssetManagerは既存のContractsとInfrastructureを利用する。
- VRChat SDKとModular Avatarの型は任意依存としてreflectionで解決し、未導入時は構造化errorを返す。
- MCPは複数機能を束ねる外部integration／composition rootであり、機能module同士の依存を追加しない。

## 未実装範囲

- remote接続、認証、TLS
- Unity Editor未起動時のheadless処理
- VRChat upload、Blueprint ID管理、VRChat API操作
- shader固有設定、Mesh Cutter／Shape Changerの自動構成
- Unity 6での検証

これらは汎用Unity MCPで個別に編集できる場合もありますが、ee4v MCPとしてはdomain validationと安全な再適用方法が定義できるまで専用writerを公開しません。

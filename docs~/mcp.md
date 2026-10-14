# ee4v MCP

`src/Editor/Mcp`は、VRChatアバターの調査、表情・Expression Menu改変、Play Modeの操作・ライティング確認、性能測定、ee4vのAssetManagerをAIクライアントから操作する統合層です。Hierarchy、Component、Scene、Prefab、Animator Controller、Materialなどの汎用操作は既存のUnity MCPへ任せ、ee4v MCPは複数のUnity APIとVRChatの規則をまとめて扱う高水準操作を所有します。

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
- `/mcp`はJSON-RPCの`initialize`、`ping`、`tools/list`、`tools/call`とnotificationを処理する。`/health`はserverの生存確認だけに使用する。対応protocol versionは`2025-11-25`、`2025-06-18`、`2025-03-26`。initializeで未対応versionを要求された場合は最新の対応versionを返し、不正・未対応の`MCP-Protocol-Version` headerはHTTP 400で拒否する。
- initializeの応答で`MCP-Session-Id`を発行する。以後のtool要求とnotificationにはこのheaderを付け、protocol versionは同じsessionの交渉結果に合わせる。終了済みsessionはHTTP 404、header欠落はHTTP 400。`DELETE /mcp`はsessionを終了し、実行中の要求をキャンセルする。
- request IDはsessionごとに追跡し、同一sessionの実行中IDの重複を拒否する。`notifications/cancelled`とserver停止は待機中のmain-thread操作を取り消す。Importでは既存APIへキャンセルトークンを渡す。開始済みの同期生成・保存は中断や巻き戻しを保証しない。
- Unity objectへ触れるtool実行は`EditorApplication.update`からmain threadへdispatchする。
- dispatch待ちの操作がないEditor frameではqueueの配列を生成しない。
- tool結果は機械処理用の`structuredContent`と、同じ内容のtext contentを返す。preview PNGはimage contentも返す。
- AssetManagerとVariantの必須文字列入力は`McpJson.RequireString`で前後の空白を除き、空なら`invalid_request`を返す。
- tool annotationでread-only、破壊性、冪等性、open-world accessを宣言する。
- registryは実行前にcatalogのinputSchemaを検証する。必須項目、型、enum、未知の項目、配列要素と最小件数、oneOf／anyOfによる操作別入力を検証し、欠落・null・不正入力を`invalid_request`として拒否する。数値は有限値に限る。
- 通常の書き込みはtool registryでPlay Mode中・切り替え中に`edit_mode_required`として拒否する。実行中のGestureManager操作とライティングプリセット保存・削除だけは明示的な例外とする。read-onlyな画像描画は一時状態を必ず復元する。
- AssetManagerはUIと同じ公開APIでItem／Fileの登録・編集・Archive・削除、明示的なSource同期、Import、Collection、制作物と履歴・画像を扱う。Libraryへの登録・取り込みとUnity ProjectへのImportは別toolとし、暗黙にImportしない。削除には既存APIのArchive・所有権の検証を使用する。Import・同期・履歴復元・変更破棄・削除は破壊性ありとして宣言する。
- AssetManagerのPrefab調査は、既にUnity Projectへ取り込まれたPrefab assetとAssetManagerで作成済みの派生Prefabだけを対象にする。Prefab調査・previewからImportを暗黙には開始しない。必要な場合は登録済みFileを明示的なImport toolで取り込む。
- Prefab previewはUnityの内部Preview sceneへだけinstanceを作成し、Play Mode、利用者のScene、Prefab assetを変更しない。外部APIへ画像を送信しない。
- 表情Clipと表情アニメーションの更新は`expectedRevision`で競合を検出できる。静止Clipは作成・部分更新・完全置換を一つのtoolで扱う。アニメーションはポーズ単位の追加・更新・並び替え・Loop変更を一つの編集toolへまとめ、削除は破壊性ありの別toolとする。`dryRun`で書き込み前の結果を確認できる。

## Object参照

`ee4v_find_avatars`は、loaded Sceneと現在のPrefab Modeから後続toolへ渡す`avatarRef`を返します。Prefab Stage以外の内部Preview Sceneは除外します。参照にはUnityの`GlobalObjectId`を優先し、Play Modeの実体と未保存ObjectはEditor session限定の`instance:<session>:<id>`で参照します。対象破棄、Play Mode停止、Domain Reload後は再取得します。Project内Prefabの検索は汎用Unity MCPへ任せます。

書き込みtoolは取得済みの参照を再解決し、対象がAvatar配下にあること、永続Prefab assetを直接編集しようとしていないこと、必要なVRChat SDK型が存在することを再検証します。

Avatar参照は現在のdomain sessionでAvatar一覧またはruntime toolが発行したものだけを受け入れ、loaded SceneのVRChat Avatar rootであることを再確認します。任意のAsset path、Hierarchy path、未発行のGlobalObjectIdは受け付けません。Prefab Modeは調査と別Clip assetの編集に使用でき、FacialSetとExpression MenuのAvatar設定の書き込みには利用者のScene上のinstanceを使用します。

## Tool設計

現行catalogは80 tools。対象と編集単位が同じ操作は、部分更新または明示的なactionへ統合する。読み取り、書き込み、破壊的な削除はannotationと承認単位が異なるため分離する。検索／一覧と詳細取得は返す情報量と用途が異なるため維持する。Avatar inventoryとauditも、事実の取得と問題の検出を分ける。静止Clipの部分更新・完全置換、リマップとFacialSet適用は既存内容の削除・置換を含み得るため、破壊性ありとして宣言する。

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

inventoryの数量は`scope: currentHierarchy`で、最終ビルドの性能ではありません。Expression ParametersはAvatarInfoのSDK計算とNDMF供給元のビルド前推定を使用し、`parameterSource`で`authoringEstimate`／`descriptor`／`runtimeDescriptor`／`unavailable`を区別します。未取得の使用量・上限はnullで返し、0にしません。MA合成メニューの件数と編集用parameter候補も返し、解決失敗は`authoringError`へ残します。Menu監査は合成元のSDK assetを確認し、MAの自動ページ分割をassetの8項目上限違反として扱いません。最後のNDMF性能測定は`lastMeasurement`として日時・失敗情報とともに返し、現在の数量へ混ぜません。

### Avatar authoring

| tool | 動作 |
|---|---|
| `ee4v_inspect_avatar_authoring` | 元Prefab基準の部位倍率・対象path、身長・ViewPosition基準値、体型BlendShape分類と同期元、既存装着設定と推奨先を取得する |
| `ee4v_edit_avatar_shape` | `setScale`、`setHeight`、`setBlendshape`、`setBlendshapeGroup`、`prepareBlendshapeSync`をUIの体型処理で実行する |
| `ee4v_edit_avatar_attachment` | Merge Armature／Bone Proxyの追加・設定変更をUIと共有する処理で行う |

authoringは利用者のScene上の取得済みAvatarを対象とする。`scopePath`は空文字（全体）または直下のPrefab instanceのAvatar相対path。調査・dryRunは保存済み分類だけを使用し、初期presetを暗黙に作らない。未作成のFBX分類は`ee4v_ensure_blendshape_presets`で明示作成する。

Scaleの`part`は調査で返す`wholeBody`、`head`、`chest`、`waist`、`shoulders`、`arms`、`hands`、`legs`、`feet`。`multipliers`はXYZの正の有限値3要素で、元Prefabの100%を1とする。Avatar全体のwholeBodyではViewPositionも補正し、個別Prefabではroot倍率だけを変更する。`setHeight`の`value`はメートルで、UIと同じ0.01m単位と基準身長±1mの範囲を使用する。

体型BlendShapeは調査結果の`shapeName`、`category`、個別の場合は`rendererPath`を指定し、`value`はUIと同じ0〜100へ制限する。Group更新は同期元とMA BlendshapeSyncを構成し、個別更新は必要なremapを維持する。制作物の保存時も共通の同期準備を実行する。書き込みはUndoとPrefab override・Sceneのdirty通知へ接続し、実体のPrefab適用・Scene保存・版保存はホストの保存操作へ任せる。

装着は直下のPrefabの`partPath`と`kind: auto`／`MergeArmature`／`BoneProxy`を指定する。既存設定は調査の`ownerPath`と`kind`を使用し、複数同型componentや別方式への暗黙の置換を拒否する。`targetPath`省略時はUIの推奨先を使い、Avatar内かつパーツ外だけを許可する。Merge Armatureのbone名設定を保持する。新規Bone Proxyは`AsChildKeepWorldPose`とし、既存の`mode`と`matchScale`は未指定なら保持する。調査の`exists`がfalseの行は現在のcomponentではなく追加時の推奨案である。

```json
{"avatarRef":"<取得した参照>","action":"setScale","part":"head","multipliers":[1.05,1.05,1.05],"dryRun":true}
```

### Expression Menu

| tool | 動作 |
|---|---|
| `ee4v_inspect_expression_menu` | MA合成後の編集用階層、出所、編集・コピー可否、タイプ、初期値、動作とrevisionを取得する |
| `ee4v_create_expression_menu_item` | 指定pageへToggle ItemまたはChildren Submenuを作成・登録する |
| `ee4v_edit_expression_menu_item` | `patch`で名前・アイコン・初期値、`setType`で生成Itemの操作タイプを変更する |
| `ee4v_replace_expression_menu_actions` | 生成Itemの全動作を置換し、UIと同じ検証・Undo・Controller生成・保存を実行する。`dryRun`は保存せず入力を検証する |
| `ee4v_move_expression_menu_item` | 同じ出所内で並び替える、または別pageの末尾へ移動する |
| `ee4v_remove_expression_menu_item` | 既存Item／Submenuは元のComponent・Controlを保持して除外し、ee4v生成項目はギミック本体を削除する。中身のあるSubmenuは`includeChildren: true`を必須とする |
| `ee4v_restore_expression_menu_item` | 調査結果の`excluded`にある`exclusionId`で項目または登録全体の除外を解除する |
| `ee4v_exclude_expression_menu_registration` | 調査結果の`registrations`の`sourceObjectId`を指定し、既存MA Menu Installerの登録全体を除外する |
| `ee4v_copy_expression_menu` | SDK Menuと必要な子・親Menuをコピーし、対象Avatar内の参照だけを差し替える |
| `ee4v_render_expression_menu_preview` | AvatarEvaluationで指定Itemの操作値を評価し、PNGと計算したparameter出力を返す |

rootの`pagePath`は空文字、`entryPath`は調査で返す`0/1`形式のindex pathです。書き込みには最新の`expectedRevision`を必須とし、変更後に返る`menu.revision`を次の操作に使用します。共有・循環Submenuは祖先を再展開せず、最大32階層・2048項目を超える調査はエラーにします。編集にはScene上の書き込み可能なAvatar Prefab instanceを使用します。Prefab assetを直接書き換えません。Sceneをdirtyにし、機能側の生成物だけ保存します。Avatar Prefabと作業Sceneの確定保存はホストの保存操作です。

調査結果の`excluded`は除外ID・表示名・元オブジェクトID・参照消失・元Control変更の状態を返します。`registrations`は既存InstallerのオブジェクトIDと登録の除外状態、各項目の`canExclude`は元assetを書き換えず除外できるかを返します。除外・復元はアバター上のEditorOnly Componentに保持し、Scene・Prefabの保存はホストが行います。SDK Controlの設定が変わった除外は誤った項目へ適用せず、復元・再除外するまでビルドを停止します。

`patch`は指定項目だけ更新します。`iconPath: ""`はアイコン解除、初期値はToggleの0／1またはRadialの0〜100です。タイプはToggle、RadialPuppet、Button、TwoAxisPuppet、FourAxisPuppet。設定を失う変更は`discardSettings: true`を明示し、名前・アイコン・登録先・共通parameter IDを保持します。並び替えの`relativeEntry`はその項目の現在のindexへ移動する指定です。別pageへの移動は`destinationPage`を使用し、保護境界・異なる出所・満杯・自己／子孫への移動を既存処理で拒否します。

動作の置換は`actions`全体を渡し、空配列で全動作を除去します。KindはObjectToggle、MaterialSwap、ShapeChanger、ParameterValue、MaterialValue、Transform、Componentです。既存Clip動作は編集・削除だけを許可します。同期は各動作の`synced`、対象はAvatar相対`path`、Puppetは`axis`で指定します。Material／Clip等の参照は`{path, localId}`を使い、同一pathに複数sub-assetがある場合はlocalIdを必須にします。曖昧path、モード不一致、範囲外、propertyの重複はUIと同じ生成処理で拒否します。previewは各軸のnormalized値（通常0〜1、TwoAxisPuppetは−1〜1）を受け取ります。外部Animator遷移・PhysBone・SDK assetだけのControlはPlay Modeで確認します。

### Play Mode確認と性能測定

Expression Menuの同期PNG撮影は`scope: isolatedAuthoringPreview`の隔離した表示用コピーを使用し、MA骨対応とClip／Puppet再生を評価します。Scene用NDMF Preview Pluginの加工は含みません。最終的な処理結果・実行時動作はPlay Modeの撮影で確認します。

| tool | 動作 |
|---|---|
| `ee4v_get_avatar_runtime` | GestureManager接続、表示中Avatar参照と全実行時parameterを取得する |
| `ee4v_control_avatar_runtime` | `connect`でScene内の既存GestureManagerへ接続、`setParameters`で実行時値を設定する |
| `ee4v_list_lighting_presets` | 組み込み・保存済み照明とVRCLV利用可否を取得する |
| `ee4v_upsert_lighting_preset` | 名前付き照明を共通storeへ保存・上書きする |
| `ee4v_delete_lighting_preset` | 名前付き照明を削除する |
| `ee4v_render_lighting_preview` | Play Modeの実Avatarを同一Cameraで1〜6条件撮影し、各PNGを返す |
| `ee4v_get_avatar_performance` | 最後のNDMF／AAO測定、PC／Quest性能、parameter内訳、SDKサイズと別々の日時を取得する |
| `ee4v_measure_avatar_performance` | Edit Modeで隔離コピーをNDMF全フェーズへ通し、性能を明示測定する |
| `ee4v_get_playmode_suppression` | 設定済み型（未導入も含む）と利用可能な具象MonoBehaviour型を取得する |
| `ee4v_set_playmode_suppression` | Projectの除去対象型を全置換する。新規には利用可能な型だけを許可する |

Play Modeの開始・停止は汎用Unity MCPで行います。開始後に`ee4v_find_avatars`で実体を再取得します。接続は保存済みSceneにあるGestureManagerを使い、追加・保存・既存Moduleの削除をしません。parameter入力は`[{name,value}]`。すべての値を事前検証し、Gestureは整数0〜7、強さは0〜1、Bool／Triggerは0／1を指定します。入力は実行中Moduleだけに渡し、Prefab・Scene assetへ保存しません。

照明設定は`preset`へ組み込みkey（day、night、warm、cold、backlight、lv）または保存名を指定し、RGB3要素の色、環境光・反射・光源の強度、pitch／yawを任意に上書きできます。VRCLV未導入時は撮影を拒否しますが保存済み設定は維持します。PNGは64〜1024pxで、Cameraのyaw／pitchとdistanceScale（0.25〜4）を共通に指定できます。描画中だけSceneのLight、環境光・反射とVRCLV globalを変更し、成功・失敗とも復元します。一時Camera・Light・Textureを解放し、UIの画面選択・比較設定は変更しません。通常のPrefab撮影とは別の実行時照明確認です。

照明の保存は`LightingPresetStore`をUIと共有し、組み込み名と破損ファイルの上書きを拒否します。変更通知で表示中の保存一覧も更新します。性能測定は画面と同じ`AvatarPlayModePerformanceCache.Bake`を使い、失敗時は前回成功値と日時を保持した構造化エラーを返します。サイズは公式SDKで利用者が行ったビルドの記録だけです。uploadや自動ビルドは開始しません。除去型の全置換では既存の未導入型を保持でき、Play ModeのNDMF専用という設定の適用範囲は変えません。

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
| `ee4v_remap_expression_clip` | 静止ClipのRenderer pathとBlendShape名の完全一致を優先し、一致しないchannelはFBX別presetのroleとsideで別Avatarへ対応付ける。複数ポーズ入力は拒否する |
| `ee4v_get_facial_configuration` | Gesture matrix、menu専用表情、Blink、口固定の現在値を返す |
| `ee4v_plan_facial_set_apply` | FacialSet適用の前提条件と生成先を検証する。任意の`configuration`と`mode`を渡すと、applyと同じpatch／replace後の設定を検証する |
| `ee4v_apply_facial_set` | FX Controller、Expression Parameters、Expression Menu、Modular Avatar installerを生成・更新する。`mode: patch`は指定Gesture／Menuだけ更新し、`mode: replace`は全設定を置き換える。`dryRun`では設定と前提条件だけを検証する |

AnimationClipの一般検索は汎用Unity MCPへ任せます。`ee4v_upsert_expression_clip`の`patch`は未指定channelを維持し、`replace`は未指定BlendShape curveを削除します。同じ内容を再指定した場合は`changed: false`としてAssetを書き直しません。複数ポーズClipはこの静止表情toolで誤って単一frameへ戻さず、アニメーション専用toolだけで更新します。

自動生成済みのFBX別プリセットをagentが整える場合は、一覧から`assetGuid`を選び、詳細の`revision`、`meshLocalId`（文字列）、`shapeName`を取得します。更新toolの`changes`には編集するMappingと項目だけを指定します。`role`と`appearanceGroup`は空文字で消去でき、`side`は空文字／`L`／`R`、`appearancePart`は空文字（自動）／`expression`／`head`／`chest`／`waist`／`shoulders`／`arms`／`hands`／`legs`／`feet`／`other`を指定できます。区切り見出しは編集できません。`expectedRevision`が一致しない場合は再取得が必要です。`dryRun: true`は変更件数と適用後のrevisionを返しますが、保存しません。書き込みは既存プリセットに限り、他のMappingと未指定項目を維持します。保存時はUIと同じ保存先・通知を使用します。

アニメーション編集は`ee4v_inspect_expression_clip`でポーズindexとrevisionを取得し、`ee4v_edit_expression_animation`のactionを選び、書き込みtoolをまず`dryRun: true`で呼んでから同じ入力で適用します。ポーズ追加は指定ポーズの表情を複製し、`transitionDuration`後へ挿入します。ポーズ更新では参照Clipと明示的なchannel値を同時に指定できません。参照Clipをローカル編集へ戻して値も変更する場合は、空の`sourceClipPath`と`channels`を同じ呼び出しへ指定します。参照Clipとポーズ名はUIと同じ`.anim`内のsub-assetへ保存され、移動と削除にも追従します。更新用revisionはアニメーション調査の結果を使用します。

`ee4v_remap_expression_clip`はRenderer pathとBlendShape名が完全一致するchannelを直接対応付け、それ以外だけpreset roleとsideを使用します。対応不能channelは結果へ残し、無言で別名へ割り当てません。表情previewにはMeshが割り当てられた描画可能な`SkinnedMeshRenderer`が1つ以上必要で、存在しない場合は空画像を成功扱いにしません。アニメーションClipでは`time`を指定して各ポーズや遷移途中を描画できます。

FacialSetのplanとapplyは同じGesture名・重複・Clip参照の検証を使用します。割り当て要素の`clipPath`は必須で、空文字でその組を解除します。planとapplyの前提不足・適用失敗は`isError: true`と詳細を返します。リマップは静止Clip専用で、アニメーションのキー・ポーズ・Loopを単一frameへ変換しません。既存の静止リマップ先は置換するため、適用前に`dryRun`で確認します。

`ee4v_ensure_blendshape_presets`はAvatarが使用するFBXの未作成分類をUIと同じ初期化処理で作り、既存分類を保持する。`ee4v_set_expression_channel_enabled`は静止・animation Clipの曲線全体を有効化／除去する。`expectedRevision`は必須。新規有効化は既存ポーズ時刻へ既定値を埋め、指定`poseIndex`へ任意の`value`を設定する。既に有効な曲線はそのまま保持する。無効化は全ポーズの該当曲線を除去し、最後の曲線を除くとポーズ時刻も失われ得るため、破壊性ありとして宣言しdryRunを提供する。

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
| Variant改変 | `ee4v_asset_list_variants`、`ee4v_asset_create_variant`、`ee4v_asset_add_prefab_to_variant`、`ee4v_asset_create_material_variant`、`ee4v_asset_save_variant` |

`ee4v_asset_edit_items`は`itemIds`に1件以上を渡し、`name`、`description`、`tags`、`archived`、`targets`のうち変更する項目だけを指定する。未指定項目は保持する。空のdescriptionは消去、空のtags／targets配列は全解除、`archived: false`はArchive解除を意味する。targets変更は1 Itemだけを対象とし、各要素はfileId、targetPath、任意のgroupNameを持つ。groupNameを省略すると既存Groupを保持し、空文字はGroup解除を意味する。外部Source由来Itemの名前・説明は変更できず、外部Source由来Tagは維持する。名前・説明・Tag・Archiveの一括指定も可能。複合編集は複数の公開APIを順に実行するため全体transactionではなく、途中失敗時はerrorの`details.appliedChanges`に完了した編集を返す。

```json
{"itemIds":["item-id"],"description":"更新する説明","tags":["avatar/example"],"archived":false}
```

`ee4v_asset_upsert_collection`はcollectionId未指定なら作成し、nameとfilterを必須とする。ID指定なら既存Collectionを部分更新し、未指定のname、icon、filterを保持する。削除は別toolに維持し、削除前のCollectionを返す。

`ee4v_asset_set_dependencies`はdependentTargetsとdependencyTargetsの両方にfileIdとtargetPathを渡す。ZIPは空pathで指定せず、解析で得た内部実体を選ぶ。

MCPはUIと同じ公開API、validation、change notificationを使用し、独自のDB接続・schema・Import処理は持たない。UIとMCPは`AssetManagerFactory.OpenSession`で同じDBのmanagerを共有し、Variant履歴には`OpenVariantSession`で同じmanagerを共有するため、MCPからのImport、編集、履歴保存も画面へ通知される。DBは共通data rootの`asset-manager-v1.db`。Source同期は明示的な要求だけで実行し、UIの選択Sourceとユーザー設定を変更しない。

### Libraryと制作物の管理

| 分類 | tools |
|---|---|
| Collection・Tag・画像 | `ee4v_asset_search`の`collectionId`／`filter`、`ee4v_asset_list_tags`、`ee4v_asset_reorder_collections`、`ee4v_asset_get_thumbnail` |
| Item／File | `ee4v_asset_create_item`、`ee4v_asset_delete_items`、`ee4v_asset_inspect_files`、`ee4v_asset_register_file`、`ee4v_asset_edit_files`、`ee4v_asset_delete_files` |
| Source・Libraryへの取り込み | `ee4v_asset_sync_source`、`ee4v_asset_ingest_file` |
| 新しい所有Itemを持つ制作物 | `ee4v_asset_create_work` |
| 制作物の詳細・metadata | `ee4v_asset_get_variant`、`ee4v_asset_edit_variant`、`ee4v_asset_delete_variant` |
| 履歴復元・変更破棄 | `ee4v_asset_restore_variant`、`ee4v_asset_discard_variant_changes` |
| 試着 | `ee4v_asset_set_fitting_avatar` |
| 保存画像 | `ee4v_asset_get_variant_image`、`ee4v_asset_add_variant_images`、`ee4v_asset_delete_variant_image`、`ee4v_asset_move_variant_image_to_front` |

検索はquery／tags／extension、filter、collectionIdのいずれかを使用する。filterは`type`、`conditionType`、`value`、`children`のtreeで、enum名または取得結果のenum値を受け付ける。未知のfield・enum、非objectのnode、不正なNot、32階層超を拒否する。collection検索は既存Collection APIのArchive範囲を使用するため`includeArchived`を指定しない。その他の検索では同fieldを使用でき、すべての検索はoffset／limitでページ分割する。

Fileの登録・ee4v Libraryへの取り込みとProject Importは分離する。register_fileはitemId省略／空文字で未所属Fileを登録でき、edit_filesはitemId（空文字で所属解除）かarchivedの一方を指定する。Source由来データの所有権と削除前のArchive要件はUIと同じAPIで検証する。sync_sourceはkind（Eagle／BoothLibraryManager／Ee4v）とlibraryPath、必要なdatabasePath／targetRootを指定し、部分失敗は完了情報を保持したerrorとして返す。

create_workはname、任意descriptionとsourcePrefabGuidを受け、新しい所有Itemを作る。元Prefabを省略するとベースなしの通常Prefab、指定すると取り込み済みPrefabまたは所有される制作物をベースにしたVariantを作る。Projectへの作成と作業Sceneを開く処理はUIと同じcreatorを使用し、作成失敗時は所有Itemを片付ける。履歴の版保存は別操作。

list_variantsはitemIdを省略するとProject内と保存済みの全制作物を返す。get_variantは安定した`variantId`で、ProjectにPrefabがない制作物も参照できる。metadata、現在の編集元revision、版一覧と依存、保存・破棄差分、gallery IDを取得する。差分は保存済みProject assetの内容で、未確定のScene変更は`sceneDirty`として別に返す。未保存のProject assetがある場合は`unsavedAssetPaths`と`changesUnavailableReason`を返し、UIの差分APIによる暗黙の保存を避けて`changes`をnullにする。保存は明示的に行う。edit_variantの未指定fieldは保持し、ベースを持つ制作物のTag制約もUIと同じとする。metadataとArchiveの複合編集は全体transactionではなく、途中失敗時は`details.appliedChanges`を返す。

restore_variantは明示revisionId、discard_variant_changesは現在の編集元revisionを使用する。保存していない制作物の変更破棄は拒否する。復元は履歴managerの素材依存確認とImport、Project内の独自Assetの置換・削除を使用する。対象作業Sceneだけを閉じ、dirtyなSceneは`discardWorkingChanges: true`を明示しない限り拒否する。制作物削除にも同じScene条件とArchive要件を適用し、取り込み済み元素材は削除しない。

試着toolはvariantIdとavatarPrefabGuidを指定し、空文字で解除する。Scene上の環境・試着Avatarと保存対象の制作物を区別し、循環検証、Undo、local Transform保持はUIと共有する。試着環境は制作物Prefabへの適用に含めない。

galleryはget_variantのIDから選択する。追加は明示的なローカルPNG／JPEGのpathsを読み、UIの16 MiB制限・画像検証・内容重複除去・保存順序を使用する。削除・先頭への移動は追加画像だけを対象にする。get_variant_imageはimageIdかrevisionIdの一方を指定し、元のMIME typeを持つimage contentを返す。

### ItemからVariantを作成して改変する

`ee4v_asset_get_item`でベースItemの`prefabCandidates`を確認し、そのItemへ取り込み済みの元Prefab GUIDを選ぶ。オリジナルItemでは、そのItemに所有されるArchiveされていないProject内制作物もベースにできる。`ee4v_asset_create_variant`へ`itemId`、`sourcePrefabGuid`、`name`、任意の`description`を渡す。AssetManager画面と同じ`DerivedAssetCreator`が`Assets/!ee4vAsset/Variant/<name>/<name>.prefab`と`<name>.unity`を作成し、SceneへVariant Prefabインスタンスを配置して開き、対象インスタンスを選択する。Material依存のMaterial Variantと所有Item・元Prefabのmetadataも登録する。元Prefabは変更しない。ItemのPrefab候補に含まれないPrefab、無効な名前、既存のVariantフォルダーは拒否する。作成はProject内への保存であり、履歴revisionの保存は別操作になる。新しい所有Itemを持つ制作物とベースなしの通常Prefabは`ee4v_asset_create_work`を使用する。

`ee4v_asset_list_variants`へ`itemId`を渡すと、そのItemに登録されたProject内制作物のGUID、Prefab path、作業Scene path、元Prefab GUID、Material Variant pathを取得できる。`isOriginal`がtrueの制作物はベースなしの通常Prefabで、元Prefab GUIDは空になる。作成直後は一覧と`ee4v_asset_get_item`の`prefabCandidates`で所有Itemと制作物を確認してから改変する。追加・Material派生作成・保存はベースなしの制作物にも使用できる。

追加素材のPrefabを接続を保ったまま組み合わせるには、`ee4v_asset_add_prefab_to_variant`へ`variantGuid`とAssetManagerから取り込み済み、またはオリジナルItemに所有されるArchiveされていない制作物の`sourcePrefabGuid`を渡す。追加先は作業Scene内の制作物Prefabインスタンス直下で、local Transformを維持する。Prefab循環参照は拒否する。追加素材のMaterialを編集する場合は`ee4v_asset_create_material_variant`へ`variantGuid`と依存Materialの`sourceMaterialPath`を渡し、作業SceneのPrefabインスタンスにある参照を派生フォルダーのMaterial Variantへ差し替える。同じpathに複数Materialがあるときは、Prefab調査結果の`materialLocalId`を`sourceMaterialLocalId`へ指定する。元Materialおよび元Prefabを直接変更しない。TransformやBlendShapeなどの任意のAvatar編集には作業SceneのPrefabインスタンスを指定してUnity MCPを使用する。Material値の編集には派生フォルダー内のMaterial Variantを指定する。`ee4v_asset_save_variant`はScene上のPrefab overrideを制作物Prefabへ適用し、Sceneを保存してからAssetManagerの履歴revisionへ保存する。

```json
{"itemId":"item-id","sourcePrefabGuid":"0123456789abcdef0123456789abcdef","name":"My Avatar Variant","description":"改変内容"}
```

Prefabの追加はUIとMCPが共通の`DerivedAssetCreator.AddPrefabToWorkingRoot`を使用し、配置・Undo・作業Sceneのdirty通知と、素体の現在の部位倍率に合わせる`AvatarPartsEditor.MatchAddedPrefabBodyScale`を実行します。Prefab rootのlocal Transformは維持し、対応できる衣装boneだけへ元Prefab基準のlocal倍率を適用します。元Prefabと素体を変更せず、同じ倍率を累積しません。

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
5. `unityMcpHandoff`のRenderer pathとMaterial pathを確認する。改変時はItemからee4v Variantを作成し、作業SceneのPrefabインスタンスとMaterial Variantのpathを既存Unity MCPへ渡して編集する。

未ImportのZIPまたはunitypackageしか存在しない場合、`prefabCandidates`は空になり、`prefabCandidateResolution.emptyReason`または`unresolvedImportedAssets`に理由を返します。Prefab調査はImportを開始しません。必要な場合は`ee4v_asset_import`を明示的に呼び、完了後にItem詳細を再取得します。

### Prefab toolの契約

| tool | 主な入力 | 主な出力 | Projectへの書き込み |
|---|---|---|---|
| `ee4v_asset_get_item` | `itemId` | Item、File、取り込み済みGUID、`prefabCandidates`。各候補はPrefab GUID／path／name／type、Variant親、dependency hash、preview可否・不可理由・警告を持つ | なし |
| `ee4v_asset_inspect_prefab` | `prefabGuid`、または`Assets/`／`Packages/`から始まる`prefabPath` | hierarchy、Renderer、Meshとtriangle、bounds、Material slot・path・local ID、Shader、Texture、BlendShape、Missing Script、参照切れ、preview可否・警告、Unity MCP handoff | なし |
| `ee4v_asset_render_prefab_preview` | `prefabGuid`または`prefabPath`、`viewPreset`、`width`、`height`、`background`、`forceRefresh` | 固定撮影条件のPNG image content、dependency hash、cache path、cache hit、警告、Unity／Render Pipeline情報。`width`と`height`の既定値は各1536px、指定範囲は128〜3072px | 再生成可能なcacheだけ |

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

PNGはSQLiteへ格納せず、共通data rootの`asset-preview/<prefab-guid>/<dependency-hash>/`以下へ保存します。共通data rootがProjectの`Assets`内に設定されている場合だけ、`Library/ee4v-cache`へ退避します。file名にはrender profile version、view、size、backgroundを含め、同じ条件の再呼び出しでは再利用します。隣接JSONにはPrefab GUID、dependency hash、render profile version、view、image path、size、Unity version、Render Pipeline、作成日時、警告、errorを記録します。cache directory名と描画条件の世代は分離し、現在のrender profile versionは`v4`です。

dependency hashには`AssetDatabase.GetAssetDependencyHash`を使用します。Prefab、Variant親、Mesh、Material、Texture、ShaderなどUnityが依存関係として追跡するAssetが変化すると保存先hashが変わるため、古い画像を使用しません。`forceRefresh: true`は同じ条件を再撮影します。cacheは再生成可能であり、Unity Project Assetの状態には含めません。

撮影は`PreviewRenderUtility`の内部Preview sceneへPrefabを直接instance化します。SkinnedMeshRendererは現在の姿勢をBakeしたMeshのbounds、その他のRendererは通常のboundsを使い、実際に描画する形状を囲んでscaleを正規化します。各viewへ投影した横幅と縦幅でcamera framingを決め、大きなアニメーション用SkinnedMeshRenderer boundsによる余白を避けます。BakeできないMeshはRenderer boundsへ戻します。照明、背景、camera FOVは固定し、instance上の`Behaviour`を無効化してParticle Systemを停止します。描画後はinstanceを破棄します。空のMaterial slotやMaterial slotなしは`missing_material`警告として返し、撮影を続けます。Missing Mesh、Shader、描画可能Rendererなし、無効なboundsは構造化errorとして返します。

RAG、Embedding、Vector DB、Semantic Search、自動caption、全Prefabの事前indexは持ちません。検索metadataで候補を絞り、要求されたPrefabだけをオンデマンド撮影します。

## 汎用Unity MCPとの使い分け

ee4v MCPは、Avatarの発見と監査、表情ClipとFacialSet、AssetManagerのようにドメイン規則をまとめて検証できる操作へ使用します。次は既存Unity MCPを使用します。

- 任意GameObjectの作成、削除、Transform変更
- 任意ComponentとSerializedPropertyの読み書き
- Scene、Prefab、Animator Controller、Material、Shaderの汎用編集。`ee4v_asset_inspect_prefab`で得たMaterialの実編集もこちらで行う
- Console、Play Mode、screenshot、build、test、任意Editor C#実行

ee4v MCPは任意C#実行toolを公開しません。高水準toolで扱っていない変更は汎用Unity MCPで実行し、最後に`ee4v_audit_avatar`でAvatar固有の前提を確認します。

## UIとの機能対応

MCPは画面状態の操作ではなく、保存対象とdomain操作を公開します。UIにある操作がすべて専用toolで提供されるわけではありません。

| UIの操作 | MCPの対応・制約 |
|---|---|
| Avatar調査、性能測定、実行時parameter、照明保存・比較 | 専用toolで提供。Clip数はAnimator、DescriptorのPlayable Layers、MA Merge Animatorが参照するClipを重複除去して集計する |
| 静止表情・ポーズ・遷移・参照Clip・Loop、Gestureとメニュー専用表情 | 専用toolで提供。メニュー専用表情の削除・改名は現在の全設定を取得して`mode: replace`を使用する。UIの手の「すべて」は64組の明示的な割り当てへ展開する |
| 表情アニメーションのBlendShape曲線の有効／無効切り替え | `ee4v_set_expression_channel_enabled`で提供。ポーズ値の更新とは別の曲線全体の編集で、revision・dryRunと破壊性annotationを使用する |
| 表情Rendererの選択、Group絞り込み、Clip複製 | Rendererは`rendererPaths`と各channelのpathで指定できる。Groupの画面状態はUI専用。Clip複製は汎用Unity操作を使用する |
| FBX別分類の編集 | presetの一覧・詳細・Mapping更新と`ee4v_ensure_blendshape_presets`による初期化を提供。調査は未作成分類を暗黙に作らない |
| お気に入り、区切り文字、サムネイル生成設定、画面選択・折りたたみ | ユーザー設定／UI状態として専用toolを持たない |
| Expression Menuの生成・編集・動作・移動・コピー・除外・復元 | 専用toolで提供。新規Clip動作の追加はUIとMCPの両方で提供せず、既存Clip動作を編集・除去する |
| Item検索・情報・Tag・Archive・Import Target、依存・Collection編集 | 専用toolで提供。検索のcollectionId／filter、Tag候補・画像取得、Collection並び替えもUI APIへ接続する |
| 同期、新規Item／File登録、未所属Fileの所属変更、Item／Fileの削除・FileのArchive | 専用toolで提供。登録・同期とProject Importを分離し、所有権・Archive・通知はUIと同じAPIを使用する |
| Item由来Variantの作成、Prefab追加、Material Variant作成、版保存 | 専用toolで提供。Prefab追加時の素体倍率追従もUIと同じ公開処理を使用する |
| 新しい所有Itemを持つマイアセットの作成、ベースなしの通常Prefab作成 | `ee4v_asset_create_work`で提供。独立した所有Itemと通常Prefab／Variantを作り、作成失敗時の片付けも共通creatorで扱う |
| 制作物の名前・説明・Tag・Archive・削除、ギャラリー追加・削除・並び替え | get／edit／delete_variantとgallery toolで提供。Itemとは区別し、UIの履歴managerを共有する |
| 版一覧・版詳細・現在の編集元・保存差分・破棄差分・過去版復元・変更破棄 | get_variant／restore_variant／discard_variant_changesで提供。Projectにない制作物もvariantIdで参照し、依存確認と復元は履歴managerを使用する |
| ベースなし制作物の試着Avatar設定・変更・解除 | `ee4v_asset_set_fitting_avatar`で提供。作業Sceneと保存対象Prefabを分離するUI処理を使用する |
| Avatar身長・ViewPosition連動、部位倍率、体型BlendShape同期 | inspect_avatar_authoring／edit_avatar_shapeで提供。元Prefab基準、ViewPosition、同期とremap、Undo・Scene変更をUIの体型処理で扱う |
| MA Merge Armature／Bone Proxyの追加・装着先・位置回転・scale追従 | edit_avatar_attachmentで提供。推奨方式、装着先範囲、既存bone名、world姿勢の扱いとUndoをUIと共有する |
| パーツactive、Component Inspector、Material値・割り当て、制作物からPrefabを除去 | 汎用Unity操作へ委ねる。改変対象は作業Sceneのinstanceと派生Materialを指定し、最後に`ee4v_asset_save_variant`で保存する |
| Project／Hierarchyの色・アイコン・タブ、Window Group、言語・data root設定 | Editorの表示・ユーザー設定として専用toolを持たない |

復元・変更破棄・曲線除去・削除は、読み取り・通常編集と区別する破壊的操作として公開する。体型・装着・試着は汎用SerializedProperty操作へ薄く転送せず、UIと共通のdomain validation・Undo・保存境界を公開APIとして使用する。

## 実装境界

- `Ee4v.Mcp.Editor`はMCP protocol、loopback HTTP server、tool registry、Unity main-thread dispatch、各moduleへのadapterを所有する。
- Face Expressionは機能moduleの公開APIをMCP adapterから呼ぶ。MCPのJSON DTOを機能moduleへ持ち込まない。
- AssetManagerは既存のContractsとInfrastructureを利用する。
- Avatar調査のSDK型はreflectionで解決する。MA合成、Expression MenuとSimulationには導入済み機能moduleの公開APIを使い、実際に使用するassemblyだけを直接参照する。解決失敗・未導入・実行モード不一致は構造化errorとして返す。
- MCPは複数機能を束ねる外部integration／composition rootであり、機能module同士の依存を追加しない。

## 未実装範囲

- remote接続、認証、TLS
- Unity Editor未起動時のheadless処理
- VRChat upload、Blueprint ID管理、VRChat API操作
- shader固有設定、Mesh Cutter／Shape Changerの自動構成
- Unity 6での検証

これらは汎用Unity MCPで個別に編集できる場合もありますが、ee4v MCPとしてはdomain validationと安全な再適用方法が定義できるまで専用writerを公開しません。

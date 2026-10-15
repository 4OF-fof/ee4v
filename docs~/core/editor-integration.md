# EditorIntegration

## 機能

`Ee4v.Core.EditorIntegration` は Unity Editor 操作の公開窓口です。reflection、Unity の非公開型、serialized property 名は `Core/Internal/EditorAPI/Backends` に隔離します。

backend は公開状態型を直接生成します。公開 API と backend の間に同じ操作を転送するだけの層や、同じ値を複製する内部 snapshot は置きません。

`Try` で始まる API は操作できない場合に `false` を返します。公開 signature に内部 backend 型は含まれません。

## `AssetPickerCatalog`と`MaterialUvUsageIndex`

`Find(typeof(UnityEngine.Object), origins)`は種類「すべて」の列挙です。型指定なしでProject内を検索し、フォルダーを除いてmain assetとProjectに表示されるsub assetを取得します。Material・Texture・AnimationClipだけでなく、Prefab、Mesh、Shader、Controller、Script、Audioなども含めます。GUIDの所属を適用してItemごとに確認できるようにし、取り込み済みGUIDを先に取得します。未取り込みのzip内のファイルは対象外です。キャッシュには実際のasset型名も保持し、種類横断の一覧表示では候補を読み込まずに型名を表示します。

`AssetPickerCatalog.Find(type, origins)`はProjectのmain/sub assetを型で列挙します。候補のidentityはGUIDとlocal file IDの組で、同じFBXに入った複数ClipやMaterialを区別します。候補には名前・path・主Textureのidentity、Materialの全Texture property参照、AnimationClipの分類を保持し、保存済み候補の検索・所属分類・関連度判定では実assetを読み込みません。`Asset`への初回アクセスでmain assetまたは対応するsub assetを解決し、プレビュー・サムネイル・ドラッグへ実参照を渡します。`UpdateMatches`は参照元のpath・所属・Texture参照を一度だけ取得し、候補のmetadataとGUID/local ID単位のUV使用実績で順位を判定します。Texture候補の場合は追加引数`textureMaterials`へMaterial候補metadataを渡します。`RegisterOrigins`でGUIDから`AssetPickerOrigin`（所属ID・表示名）へのsnapshot取得を登録し、返されたleaseの破棄で同じ登録を解除します。CoreとUIはAssetManagerへ依存せず、AssetManagerのsessionが取り込みGUIDの関連を提供します。所属snapshotは初回参照で取得して共有し、providerの登録変更・解除または`InvalidateOrigins`で破棄します。providerの利用側は元データ変更時に無効化します。未登録GUIDには所属を付けません。

Materialの関連順位は、同じUV0配置での使用実績、同一主Texture参照、参照元と同一フォルダー、同一Item所属の順です。TextureもMaterialを参照元にし、同じUV0配置の使用実績があるMaterialのTexture propertyから候補を逆引きします。順位はそのUV使用実績、参照元の主Textureそのもの、参照元の他のTexture property参照、同一フォルダー、同一Item所属の順です。Textureの同一フォルダー判定には参照元Materialと、そのMaterialが参照するTextureの直近の親フォルダーを使います。所属は参照元MaterialのGUIDから得たOrigin IDの一致です。主Texture以外のmaskやnormalなども候補に含めますが、shaderのUV channel・propertyの役割・画像内容による対応は保証しません。参照元がない場合やどの条件にも一致しない候補は関連アセットに含めません。

AnimationClipは参照元と関連順位を持ちません。`ClassifyClip`は`AnimationUtility`からbindingとイベントを読み、1本以上のfloatカーブを持ち、すべてが`SkinnedMeshRenderer`の`blendShape.` propertyかつbinding pathの末尾が大文字小文字を含め`Body`であるClipを`AssetPickerClipKind.Facial`とします。`Body`と`Armature/Body`のようなpathを対象にし、他のproperty、オブジェクト参照カーブ、AnimationEventを含むClipや空のClipは`Other`です。Clip名・フォルダー名・接続Avatarは分類に使いません。FBX内のClipもlocal file IDごとに分類します。分類は候補metadataとして永続キャッシュへ保存し、ファイルの依存hashが変わった場合に再取得します。MaterialのTexture参照またはClip分類が欠けるキャッシュ項目は再取得します。

`AssetPickerOrigin`は任意の画像取得delegateを受け取り、`GetThumbnailAsync(CancellationToken)`でencoded画像を返します。AssetManagerのproviderはPrefab追加画面と同じ`IAssetManager.GetThumbnail(Item ID)`を接続します。取得完了した画像と画像なしの結果は所属snapshot内で保持し、次回Popupで再利用します。失敗・キャンセルは完了結果として保持しません。UIは画像のデコードとTextureの寿命を管理し、閉じると進行中の取得をキャンセルします。

`MaterialUvUsageIndex`は入力された編集RootとProject内のPrefab/ModelにあるRendererのMaterial slotから、参照Materialと各候補の使用先を調べます。対応submeshのUV0三角形を点順・三角形順に依存しない形で正規化し、座標の完全一致をhashで照合します。Mesh/submeshごとの解析結果を共有し、`CreateContextIndex`でProject indexを参照する独立したcontextを生成して、`ScanContext`で編集中のRootだけを追加調査します。Project indexの更新時はcontextの照合結果を再計算します。Scene上のMaterial割り当てや使用先はProject indexへ混入しません。RendererはSkinnedMeshRendererのsharedMeshまたはMeshFilterのsharedMeshを使用し、MeshFilterの有無をUnityのnull判定で確認します。Particle・TrailなどMeshを取得できないRendererはスキップします。座標・topologyの近似一致、画像サイズや色からのUV推測は行いません。Edit ModeのEditor callbackではRead/Write無効のMeshも参照します。Play Modeの非readable MeshではUVを新たに読み出さず、変更のないEdit Modeの解析結果を再利用します。UV0なし、非triangleは根拠として使いません。

`AssetPickerCache`はEditor session内の型別候補・調査完了したProject UV indexと、プロジェクトの`Library/ee4v/asset-picker-v1.json`に保存する永続キャッシュを併用します。session内の再表示はAssetDatabaseの全件検索を省き、最新の所属snapshotを候補へ再適用します。domain reload・Editor再起動後は型別のGUID一覧を取得し、候補metadata、Prefab/Model単位のMaterial使用実績、Mesh/submesh単位のUV hashをディスクから再利用します。保存したasset pathと`AssetDatabase.GetAssetDependencyHash`を現在値と照合し、追加・移動・変更されたファイルだけ読み込み・解析します。削除されたGUIDは検索結果から除外されます。依存hashの取得もsession内で共有します。所属名・所属画像は永続ファイルへ複製せず、現在のproviderから再適用します。

Project変更とPlay Mode遷移ではsessionの結果と依存hashを無効化し、永続結果を次回調査時に検証します。永続assetへのUndo変更では当該GUIDの候補・Mesh解析と、そのGUIDを依存先に持つPrefab/Modelの解析を破棄します。Undo/Redoでもsession中に編集された永続assetを再検証し、SceneだけのUndoで新たに全Projectを解析することはありません。調査開始時の`Version`と保存時のversionが一致する場合だけ結果を登録します。調査中の変更を検出したPopupは取得を再開し、変更前の途中結果を使いません。

ファイル単位で取得・解析が完了した結果だけを永続化し、途中でPopupを閉じても完了済みファイルは再利用できます。更新の書き込みは1秒間の待機で集約し、候補／Project UVの取得完了・assembly reload直前・Editor終了時にも保存します。一時ファイルからの置き換えで書き込み、破損・読み書き失敗・形式またはUnityバージョン不一致はキャッシュなしとして扱います。Libraryの削除で再生成できます。未保存の永続assetの編集、Play Modeで得た結果、SceneのMaterial割り当てや一時参照は永続化しません。編集中のRootはPopupを開くたびに別contextとして確認し、Projectの使用実績へ混ぜません。

UV hashは各点の32bit浮動小数点座標を数値で保持し、三角形内の点順と三角形順を正規化したbinary列からSHA-256を生成します。三角形ごとの座標文字列を作らず、同じMeshのUV座標取得もsubmesh間で共有します。座標0の正負は同じ点として扱い、座標の近似一致は行いません。

Rootに接続されたAnimationClipのMaterial参照カーブも使用実績に含めます。Rendererへのbinding pathと`m_Materials.Array.data[n]`から対象slotを解決し、色切り替えClipにだけ登場するMaterialも同じUV配置へ関連付けます。未接続ClipやVRChatのDescriptorにだけ登録されたcontroller、MAのbuild時path変換は解決しません。

`DescribeUsage(material)`およびidentity文字列を受け取るoverloadは検出したMesh名とslot番号を返し、Pickerの補足とtooltipへ表示します。Mesh名を部位名へ推測変換せず、実際の使用先を役割の手掛かりにします。

同じUV0配置での使用実績は別Textureの色違いMaterialを優先する手掛かりです。使用されていないMaterialの対応や、shaderが使うUV channel・tiling・offset・独自変換まで保証する情報ではありません。未知の候補を除外せず、根拠がない場合は主Texture参照とフォルダーを補助に使います。調査は読み取り専用で、Meshのimport設定やMaterialの割り当てを変更しません。

## `PrefabEditingChanges`

`HasContentOverrides(instance)`はPrefabインスタンスの追加・削除と、元Prefabの現在値と異なるserialized propertyを判定します。Sceneのdirtyフラグや、値を元に戻したまま残るoverrideだけでは変更扱いにしません。配置に使うUnityのdefault overrideは除外します。AssetManagerの保存・変更を戻すボタンが使用します。

`SerializedValuesEqual(current, saved, include)`は表示用hideFlagsを除いたserialized値を比較します。任意のproperty filterを指定でき、インスタンス内の参照は対応するPrefab側の参照へ揃えて比較します。serialized propertyの操作は`PrefabEditingChangesBackend`へ隔離し、どちらのAPIも比較対象を保存・変更しません。

## `ProjectBrowserApi`

### 状態型

`ProjectBrowserState` は `FolderGuid`、`FolderPath`、`SearchText`、`HasSearch`、`ViewMode`、`Orientation` を持つ読取専用 snapshot です。

### メンバー

| メンバー | 戻り値・動作 | 副作用 |
|---|---|---|
| `TryGetState(out state)` | 対象 window を自動選択して状態を返す | なし |
| `TryGetState(window, out state)` | 指定 Project Browser の状態を返す | なし |
| `TryGetState(selectionRect, out state)` | 項目領域を向きの判定材料にして状態を返す | なし |
| `TryShowFolder(window, folderGuid, reveal)` | 2列表示の指定Project Browserでfolderを表示する。1列表示では`false`を返す | Project Browser の表示 folder を変更する |
| `TrySetSearch(window, searchText)` | 検索文字列を設定する | Project Browser の検索状態を変更する |
| `TryClearSearch(window)` | 検索を解除する | Project Browser の検索状態を変更する |
| `TryGetOpenWindows(out windows)` | 開いている Project Browser を返す | なし |

対象を自動選択する場合は focused window、mouse over window、検出した Project Browser の順に探します。

状態取得は `SerializedObject` から Project Browser の内部 property を読みます。対象型や property が存在しない場合、または reflection が失敗した場合は `false` と `null` を返します。

## `ProjectFavoritesApi`

### 状態型

`ProjectFavoriteFolder` は `FolderGuid` と `FolderPath` を持つ読取専用値です。

### メンバー

| メンバー | 戻り値・動作 | 副作用 |
|---|---|---|
| `TryGetFolders(out folders)` | Favorite folder の snapshot を返す | なし |
| `TryAddFolder(folderPath)` | folder を追加できれば `true` | Unity の Project Favorites を変更する |
| `TryRemoveFolder(folderPath)` | folder を削除できれば `true` | Unity の Project Favorites を変更する |
| `TryAddChangedListener(callback)` | 変更通知を登録できれば `true` | Unity 内部の変更イベントへ callback を追加する |
| `RemoveChangedListener(callback)` | 登録済み callback を解除する | Unity 内部の変更イベントから callback を削除する |

内部型、対象 folder、必要な member を解決できない場合は `Try` API が `false` を返します。`TryGetFolders` の失敗時は空の配列を返します。

## `InspectorApi`

### 状態型

`InspectorState` は次の参照を持ちます。

| property | 内容 |
|---|---|
| `Window` | 対象 Inspector window |
| `InspectedObjects` | Inspector が現在扱う object |
| `EditorTargets` | 実際に描画中のEditorが扱うobject |
| `EditorsElement` | editor 本体を含む要素。状態生成に必須 |
| `EditorsViewportRect` | editor 本体を表示するviewport領域 |
| `PreviewAndLabelElement` | preview と label の要素。取得できない場合は `null` |
| `VersionControlElement` | version control の要素。取得できない場合は `null` |

### メンバー

| メンバー | 戻り値・動作 | 副作用 |
|---|---|---|
| `TryGetStates(out states)` | 開いている Inspector の snapshot を返す | なし |
| `TryGetState(window, out state)` | 指定 Inspector の snapshot を返す | なし |

返される `EditorWindow` と `VisualElement` は live object です。API 自体は変更しませんが、呼び出し側が要素を変更すると Inspector の表示へ影響します。

Inspector全体の選択と個別Editorのtargetが異なる場合は`EditorTargets`を使用します。

## `EditorPopupApi`

| メンバー | 戻り値・動作 | 副作用 |
|---|---|---|
| `FocusMouseOverWindow()` | マウスオーバー中のEditorWindowが未フォーカスならフォーカスする | 対象windowのフォーカスを変更する |
| `TryGetDesktopBounds(screenPosition, out bounds)` | 指定位置を含む desktop bounds を返す | なし |
| `TrySetBackgroundColor(window, color)` | container window の背景色を設定する | 対象 window の表示色を変更する |
| `TryReadScreenPixels(screenRect, out pixels, out width, out height)` | 画面領域の pixel を読み取る | 画面を読み取り、`Color[]` を割り当てる |
| `IsTransientPicker(window)` | `ColorPicker` または `ObjectSelector` かを返す | なし |
| `HasOpenTransientPicker()` | 全 EditorWindow を列挙し、開いている transient picker の有無を返す | なし |
| `IsEyeDropperOpen()` | EyeDropper の内部状態を返す | なし |

内部型または member が存在しない場合、`Try` API は `false` を返します。状態判定 API も取得失敗時は `false` です。

## `AssetImportApi`

| メンバー | 戻り値・動作 | 副作用 |
|---|---|---|
| `AssetsDirectory` | `Application.dataPath` を返す | なし |
| `ImportPackage(packagePath, interactive, onFinished)` | import 要求を受け付ける | queue、AssetDatabase event、project asset を変更する |
| `Refresh()` / `Refresh(bool synchronous)` | `AssetDatabase.Refresh()` を実行する。`true`では`ForceSynchronousImport`で完了を待つ | asset の再走査と import を発生させる可能性がある |

`ImportPackage` は要求を process 内の queue に追加し、一件ずつ `AssetDatabase.ImportPackage` へ渡します。処理中は completed、cancelled、failed の各 event を購読し、終了時に解除します。

完了後は `EditorApplication.delayCall` で次の要求を開始します。`onFinished` は一要求につき最大一度呼ばれ、成功時は `true`、キャンセルと失敗時は `false` です。

空の package path は `ArgumentException` です。`AssetDatabase.ImportPackage` が同期的に例外を投げた場合は callback に `false` を通知し、event を解除してから例外を再送出します。

`onFinished` が投げた例外は `AssetImportApi` では捕捉しません。

## `HierarchyItemApi`

| メンバー | 戻り値・動作 | 副作用 |
|---|---|---|
| `IsIconSupported` | 必要な Hierarchy 内部 API を利用できるか返す | なし |
| `TrySetIcon(instanceId, icon)` | 開いている各 Hierarchy の対象項目へ icon を設定する | Hierarchy の内部 tree item を変更する |
| `TryGetOpenWindows(out windows)` | 開いている Hierarchy Window を返す | なし |
| `TryGetTreeViewRect(window, out rect)` | GameObjectを表示するtree view領域を返す | なし |

instance ID が `0`、内部 API が非対応、対象項目がない場合は `false` です。一つ以上の Hierarchy で更新できた場合に `true` を返します。tree view領域を取得できない場合は空の`Rect`を返します。永続化は行いません。

## `EditorSceneApi`

| メンバー | 戻り値・動作 | 副作用 |
|---|---|---|
| `TryCreateEmptySceneAsset(scenePath)` | 新しい空のScene Assetを生成できれば`true` | 指定の`Assets/`内pathへSceneを作成し、同期Importする。開いているSceneは変更しない |
| `TryClearDirtiness(scene)` | シーンの dirty 状態を解除できれば `true` | シーンの保存要求を解除する |
| `HidePreviewHierarchy(root)` | Preview用の複製階層を再帰的に非表示・非保存へ設定する | rootと全子GameObjectの`hideFlags`を`HideAndDontSave`へ変更する |

`TryCreateEmptySceneAsset`は既存ファイルへ上書きしません。pathは`Assets/`内の`.unity`とし、親folderが存在する必要があります。無名Sceneを開いている状態でも、別のScene Assetを生成できます。Unityの内部`CreateSceneAsset`への接続はbackendに隔離し、内部APIが非対応または生成・Importに失敗した場合は`false`です。

`TryClearDirtiness`は無効なシーン、Unity内部APIが非対応、reflectionが失敗した場合に`false`を返します。シーン内容は変更しません。

## `EditorTextFieldApi`

| メンバー | 戻り値・動作 | 副作用 |
|---|---|---|
| `ConfigureMultilineScroll(textField, useVerticalScroll, maxHeight)` | 内部 ScrollView を設定できれば `true` | 対象 TextField の scroll と style を変更する |

縦スクロールの表示状態、最大高、scroller の幅と余白を変更します。横スクロールと上下 button は非表示にします。

対象が `null`、または Unity 内部の TextField 構造を取得できない場合は `false` です。内部 method の呼び出しで発生した例外はこの API 内では捕捉していません。

## `PackageAssetApi`

| メンバー | 戻り値・動作 | 副作用 |
|---|---|---|
| `GetPackageRootAssetPath()` | ee4v package root の asset path、または `null` を返す | AssetDatabase を検索し、成功した結果を process 内へキャッシュする |

検索は `Ee4vPackageAnchor` を探し、`Editor/Core/Internal/Ee4vPackageAnchor.cs` で終わる asset path から package root を計算します。

package root を解決できなかった場合はキャッシュせず、次回呼び出しでも検索します。

## Unity version 依存

次の backend は reflection、非公開型、serialized property 名を使用します。

- `ProjectBrowserBackend`
- `ProjectFavoritesBackend`
- `InspectorHostBackend`
- `EditorPopupWindowBackend`
- `SceneHierarchyBackend`
- `EditorSceneBackend`
- `TextFieldMultilineScrollBackend`

Unity version の差異は公開 API の利用側ではなく、これらの backend で吸収します。詳細は [Unity 6 移行メモ](../unity6.md)を参照してください。

## Prefabと部位の共通型

`BodyPartCategory`は編集補助ツールとプレビューが共有する部位の分類です。Head、Chest、Waist、Shoulders、Arms、Hands、Legs、Feet、Otherを持ち、機能固有の改変カテゴリやUI状態は含みません。

`PrefabHierarchyUtility.SplitName`は区切り文字とcamel caseで名前を小文字の語へ分割します。`IsInScope`は指定Transformが対象root内の選択範囲へ含まれるかを判定します。選択indexがnullならroot全体、0以上ならその子階層、負なら構成Prefabのindex集合を除いたroot側を対象とします。共通のGameObjectPreviewとAssetManagerの部位・Material分類が使用し、SceneやPrefabを変更しません。

`EditorSceneApi.SaveAfterEdit(scene)`はee4v編集ホストからだけ呼ぶScene保存APIです。UI操作後に対象の名前付きAssets内Sceneがdirtyなら保存し、同一Sceneの連続要求を集約します。Preview Scene・無名Scene・Play Modeを除外し、全Sceneの一括保存やPrefabへの適用は行いません。

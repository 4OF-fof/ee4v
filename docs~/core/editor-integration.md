# EditorIntegration

## 機能

`Ee4v.Core.EditorIntegration` は Unity Editor 操作の公開窓口です。reflection、Unity の非公開型、serialized property 名は `Core/Internal/EditorAPI/Backends` に隔離します。

backend は公開状態型を直接生成します。公開 API と backend の間に同じ操作を転送するだけの層や、同じ値を複製する内部 snapshot は置きません。

`Try` で始まる API は操作できない場合に `false` を返します。公開 signature に内部 backend 型は含まれません。

## `PrefabEditingChanges`

`HasContentOverrides(instance)`はPrefabインスタンスの追加・削除と、元Prefabの現在値と異なるserialized propertyを判定します。Sceneのdirtyフラグや、値を元に戻したまま残るoverrideだけでは変更扱いにしません。配置に使うUnityのdefault overrideは除外します。AssetManagerの保存・変更を戻すボタンが使用します。

`SerializedValuesEqual(current, saved, include)`は表示用hideFlagsを除いたserialized値を比較します。任意のproperty filterを指定でき、インスタンス内の参照は対応するPrefab側の参照へ揃えて比較します。Materials Windowが編集したMaterialの復元前に使用します。serialized propertyの操作は`PrefabEditingChangesBackend`へ隔離し、どちらのAPIも比較対象を保存・変更しません。

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
| `Refresh()` | `AssetDatabase.Refresh()` を実行する | asset の再走査と import を発生させる可能性がある |

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
| `TryClearDirtiness(scene)` | シーンの dirty 状態を解除できれば `true` | シーンの保存要求を解除する |
| `HidePreviewHierarchy(root)` | Preview用の複製階層を再帰的に非表示・非保存へ設定する | rootと全子GameObjectの`hideFlags`を`HideAndDontSave`へ変更する |

無効なシーン、Unity内部APIが非対応、reflectionが失敗した場合は`false`です。シーン内容は変更しません。

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

`PrefabHierarchyUtility.SplitName`は区切り文字とcamel caseで名前を小文字の語へ分割します。`IsInScope`は指定Transformが対象root内の選択範囲へ含まれるかを判定します。選択indexがnullならroot全体、0以上ならその子階層、負なら構成Prefabのindex集合を除いたroot側を対象とします。共通のPrefabScenePreviewとAssetManagerの部位・Material分類が使用し、SceneやPrefabを変更しません。

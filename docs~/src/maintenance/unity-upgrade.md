# Unity Upgrade Notes

Unity バージョン更新時に確認する互換性メモです。UI Toolkit、built-in icon、Unity 内部 API はバージョン差分の影響を受けやすいため、更新後は関連テストを必ず確認します。

## Package Dependencies

`new/package.json` で Unity 2022.3 向けに次を明示しています。

| package | version | 用途 |
|---|---:|---|
| `com.unity.nuget.newtonsoft-json` | `3.0.2` | JSON / JSONC 周辺実装 |
| `com.unity.test-framework` | `1.1.33` | Editor test assembly と Test Runner |

Unity 更新時は manifest の互換性に加え、Core と AssetManager の Editor tests、Eagle plugin の `npm run check`、docs build を再実行します。

## UiTextFactory

Unity 2022.3.22f1 では UIElements のフォントキャッシュに問題があり、bold や italic などのスタイルを持つテキストを描画するとキャッシュが正しく更新されず、古いスタイルのまま描画される。

この問題を解決するために、`UiTextFactory` を使用している。`TypographyStyleResolver` で `RequiresImgui` が有効なテキストは IMGUI で描画し、UIElements のフォントキャッシュ問題を回避する。ContextMenu の label / shortcut も、直前に選択行の bold テキストを描画する場合があるため、normal font style のままこの経路を使用する。

これはバグ回避のための暫定措置であり、Unity のバージョン更新時に UIElements のフォントキャッシュ問題が解決されていれば、`UiTextFactory` を廃止して UIElements に完全移行することが望ましい。

## ThreePaneLayout resize cursor

`ThreePaneLayout` の pane splitter と drag 中の領域では、Unity Editor 標準の `MouseCursor.ResizeHorizontal` を表示するために `IMGUIContainer` と `EditorGUIUtility.AddCursorRect` を使用している。

Unity 2022.3 の UI Toolkit は `VisualElement.style.cursor` で custom cursor texture を指定できる一方、Editor 組み込み cursor を選ぶ `Cursor.defaultCursorId` を public API として公開していない。このため native の resize cursor を UI Toolkit のみで指定できず、cursor 表示に限定して IMGUI を使用する。pane の layout、pointer event、drag 処理は引き続き UI Toolkit で実装する。

Unity のバージョン更新時に UI Toolkit の public API から Editor 組み込み cursor を指定できるようになった場合は、`ThreePaneLayout` の `IMGUIContainer` を削除して UI Toolkit に移行する。

## Builtin Icon

Unity の組み込みアイコンは、バージョンによって利用できるアイコンの種類や名前が異なるため、`Icon` コンポーネントを使用して存在が検証されたアイコンのみを描画する。

使用可能なアイコンは enum で定義されており、アイコンが使用可能なことはテストコードで検証されている。Unity のバージョン更新時はテストの実行結果を参照し、失敗していた場合はアイコンの enum 定義を更新する。

## InternalEditorAPI

Unity の内部 API はバージョンによって利用できる API や挙動が異なるため、Core 配下に `InternalEditorAPI` を設けて Unity 内部 API へのアクセスを一元管理している。

`InternalEditorAPI` は、テストによって利用可能な API が検証されており、利用できない API は例外を投げるのではなく失敗を返す。Unity のバージョン更新時はテストの実行結果を参照し、利用できなくなった API があった場合は実装を更新する。

### ProjectBrowser navigation

`ProjectTabs` とProject item contextは
`Editor/Core/Internal/EditorAPI/ProjectBrowser.cs` のfacadeを利用します。backendは
`UnityEditor.ProjectBrowser`、`ShowFolderContents(int, bool)`、
`SetSearch(string)`、`ClearSearch()` と、SerializedObject上の
`m_SearchFilter.m_Folders`、`m_SearchFilter.m_NameFilter`、`m_LastFolders`、
`m_ViewMode`、`m_ListAreaGridSize` に依存します。

Project tabからの操作では対象 `EditorWindow` を明示し、指定windowが
ProjectBrowserでない場合はfallbackせず失敗を返します。Unity更新時はfolder移動、
検索の復元と解除、one/two column表示、複数のProject windowを開いた状態で操作対象が
混線しないことを確認します。private memberが変更された場合もfeature側へreflectionを
追加せず、backendとfacadeのfallbackを更新します。

### SceneHierarchy item icon

`HierarchyStyle`のicon変更はscene objectのiconへ書き込まず、
`Editor/Core/Internal/EditorAPI/SceneHierarchy.cs`の
`SceneHierarchyItemIcon` facadeを利用してHierarchyの
TreeView itemだけへ適用します。backendは`UnityEditor.SceneHierarchyWindow`の
`m_SceneHierarchy`、`UnityEditor.SceneHierarchy.m_TreeView`、
`TreeViewController.FindItem(int)`、`TreeViewItem.icon`に依存します。

内部APIが利用できない場合はHierarchy item描画callback上のicon overlayへfallbackします。
Unity更新時は`SceneHierarchyEditorApiTests`に加え、Hierarchy上のiconだけが変わり、
Scene Viewのgizmo iconが変わらないこと、Hierarchy windowの再生成後もiconが復元される
ことを確認します。private memberが変更された場合はfeature側へreflectionを追加せず、
`SceneHierarchyBackend`の取得経路とfallbackだけを更新します。

### ContextMenuWindow popup chrome

`ContextMenuWindow` の native popup は矩形であり、Unity 2022.3 では透明化や OS の window region だけで滑らかな角丸を安定して作れない。このため表示直前に `EditorPopupWindow.TryReadScreenPixels` で popup 背面を取得し、UI root の背景へ一時 texture として設定する。USS の角丸外側には実際の背面が描かれるため、選択色など通常と異なる背景でも境界が一致する。右クリックによって TreeView の選択が変わる場合は、選択背景が画面へ反映される次フレームまで popup 表示を遅延させてから取得する。screen read は `Editor/Core/Internal/EditorAPI/Backends/EditorPopupWindowBackend.cs` に閉じ、取得できない場合は透明背景へ fallback する。

`FolderStyleWindow` は領域外clickで閉じる一方、`ColorField` と `ObjectField` が開く
Unity internal windowへのfocus移動では閉じない。`UnityEditor.ColorPicker` と
`UnityEditor.ObjectSelector` の型名判定は
`EditorPopupWindow.IsTransientPicker(...)` facadeを介し、型名は
`EditorPopupWindowBackend` だけが所有する。Color Pickerのスポイト中はpickerが
focusを失うため、`EditorPopupWindow.HasOpenTransientPicker()` でpicker window自体の
生存期間も確認する。`ColorField` 右端から直接起動するスポイトはpicker windowを
作らないため、backendが`UnityEditor.EyeDropper.IsOpened`をreflectionで読み取り、
`EditorPopupWindow.IsEyeDropperOpen()`として公開する。Unity更新時は両pickerを
開いた状態、Color Picker内のスポイト、ColorFieldから直接起動するスポイトのすべてで
popupが維持され、各操作終了後に外側をclickすると閉じることを確認する。

popup の最大幅と配置には `UnityEditorInternal.InternalEditorUtility.GetBoundsOfDesktopAtPoint` から得た表示中モニターの bounds を使う。この API を利用できない場合は従来どおり希望サイズと起点位置で表示する。Unity 更新時は、角丸外側の透過、複数モニター上での配置、長いラベルがモニター幅まで省略されないことを Catalog の `ContextMenuWindow` story でも確認する。

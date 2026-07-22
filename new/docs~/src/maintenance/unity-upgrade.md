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

## Builtin Icon

Unity の組み込みアイコンは、バージョンによって利用できるアイコンの種類や名前が異なるため、`Icon` コンポーネントを使用して存在が検証されたアイコンのみを描画する。

使用可能なアイコンは enum で定義されており、アイコンが使用可能なことはテストコードで検証されている。Unity のバージョン更新時はテストの実行結果を参照し、失敗していた場合はアイコンの enum 定義を更新する。

## InternalEditorAPI

Unity の内部 API はバージョンによって利用できる API や挙動が異なるため、Core 配下に `InternalEditorAPI` を設けて Unity 内部 API へのアクセスを一元管理している。

`InternalEditorAPI` は、テストによって利用可能な API が検証されており、利用できない API は例外を投げるのではなく失敗を返す。Unity のバージョン更新時はテストの実行結果を参照し、利用できなくなった API があった場合は実装を更新する。

### ContextMenuWindow popup chrome

`ContextMenuWindow` の native popup は矩形であり、Unity 2022.3 では透明化や OS の window region だけで滑らかな角丸を安定して作れない。このため表示直前に `EditorPopupWindow.TryReadScreenPixels` で popup 背面を取得し、UI root の背景へ一時 texture として設定する。USS の角丸外側には実際の背面が描かれるため、選択色など通常と異なる背景でも境界が一致する。右クリックによって TreeView の選択が変わる場合は、選択背景が画面へ反映される次フレームまで popup 表示を遅延させてから取得する。screen read は `Editor/Core/Internal/EditorAPI/Backends/EditorPopupWindowBackend.cs` に閉じ、取得できない場合は透明背景へ fallback する。

popup の最大幅と配置には `UnityEditorInternal.InternalEditorUtility.GetBoundsOfDesktopAtPoint` から得た表示中モニターの bounds を使う。この API を利用できない場合は従来どおり希望サイズと起点位置で表示する。Unity 更新時は、角丸外側の透過、複数モニター上での配置、長いラベルがモニター幅まで省略されないことを Catalog の `ContextMenuWindow` story でも確認する。

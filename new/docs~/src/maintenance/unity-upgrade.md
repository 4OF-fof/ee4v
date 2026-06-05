# Unity Upgrade Notes

Unity バージョン更新時に確認する互換性メモです。UI Toolkit、built-in icon、Unity 内部 API はバージョン差分の影響を受けやすいため、更新後は関連テストを必ず確認します。

## UiTextFactory

Unity 2022.3.22f1 では UIElements のフォントキャッシュに問題があり、bold や italic などのスタイルを持つテキストを描画するとキャッシュが正しく更新されず、古いスタイルのまま描画される。

この問題を解決するために、`UiTextFactory` を使用している。テキストに関する UI 要素を生成する際に IMGUI を使用することで、UIElements のフォントキャッシュの問題を回避し、正しいスタイルでテキストを描画できるようにしている。

これはバグ回避のための暫定措置であり、Unity のバージョン更新時に UIElements のフォントキャッシュ問題が解決されていれば、`UiTextFactory` を廃止して UIElements に完全移行することが望ましい。

## Builtin Icon

Unity の組み込みアイコンは、バージョンによって利用できるアイコンの種類や名前が異なるため、`Icon` コンポーネントを使用して存在が検証されたアイコンのみを描画する。

使用可能なアイコンは enum で定義されており、アイコンが使用可能なことはテストコードで検証されている。Unity のバージョン更新時はテストの実行結果を参照し、失敗していた場合はアイコンの enum 定義を更新する。

## InternalEditorAPI

Unity の内部 API はバージョンによって利用できる API や挙動が異なるため、Core 配下に `InternalEditorAPI` を設けて Unity 内部 API へのアクセスを一元管理している。

`InternalEditorAPI` は、テストによって利用可能な API が検証されており、利用できない API は例外を投げるのではなく失敗を返す。Unity のバージョン更新時はテストの実行結果を参照し、利用できなくなった API があった場合は実装を更新する。

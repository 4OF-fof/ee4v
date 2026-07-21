# UI 実装ルール

## 色は共通トークンを使う

UI の配色は `Editor/Core/UI/Foundation/ui-color-tokens.uss` に集約し、component の USS では raw な `#...` / `rgb(...)` / `rgba(...)` を直接指定しません。`common.uss` が color token USS を import するため、`.ee4v-ui` を root class に持つ既存 window では `var(--ee4v-color-...)` をそのまま利用できます。

- 基本 palette は `DESIGN.md` の Unity Editor 2022.3 Dark theme 測定値に合わせる
- surface / text / border / state など、用途を表す token を選ぶ
- selection、focus、active tool の青を同じ token にまとめない
- module 固有の状態色が必要な場合も component 内へ直書きせず、用途が分かる名前で token を追加する
- C# / IMGUI から色が必要な場合は `UiColorTokens` を使い、USS 側の同名 token と値を揃える

色以外の寸法、余白、角丸、typography は color token に含めません。

## `Label` を直接使わない

`new Label(...)` や `Label` 継承は禁止です。テキスト表示は `UiTextFactory.Create(...)` を使います。

理由:

- Unity 2022.3.22f1 のフォントキャッシュ問題を `UiTextFactory` で吸収している
- `UiLabelAuditTests` で direct `Label` 利用が監査されている
- UI テキストの見た目は `UiTextFactory` + `UiClassNames` + `TypographyStyleResolver` の組み合わせで統一している

許可されている例外実装は `Editor/Core/UI/Foundation/Typography/UiTextFactory.cs` のみです。

## class 名は `UiClassNames` に寄せる

`UiClassNames` は typography 用 class 名の入口です。`TypographyStyleResolver` で解決する text 用 class 名は string 直書きせず、`UiClassNames` に定数を追加して使います。

一方で、`TypographyStyleResolver` で使わない class は component 内に閉じます。

- 構造用 class
- state modifier 用 class
- その component の `.uss` でしか使わない非 text class

## テキストは state 経由で差し替える

既存 UI は `*State` を受け取り、`SetState(...)` で見た目を更新する構成が多いです。

- `InfoCardState`
- `SearchFieldState`
- `WindowToastState`

UI 要素の生成時に値を埋め込むより、state を差し替えて再描画できる形を優先します。

## 組み込みアイコンを直参照しない

Unity の built-in icon はバージョン差分があるため、`Icon` / `UiBuiltinIcon` を使います。

- 追加が必要なら `UiBuiltinIcon` と `UiBuiltinIconResolver` を更新する
- `Editor/Core/UI/Test/Editor/UiIconTests.cs` で解決可能か確認される

## ローカライズ

永続的に表示される文言は `I18N.Get("key")` を使います。

- `Ee4v.UI` namespace のコードは `UI` scope として解決される
- 文言は `Editor/Core/UI/Localization/<locale>/*.jsonc` に追加する
- key の重複、未使用、未定義参照は静的監査の対象

# UI 実装ルール

## 見た目の共通値は design token を使う

`Editor/UI/Foundation/ui-design-tokens.uss` が design token の入口です。`common.uss` がこのファイルを import するため、`.ee4v-ui` を root class に持つ window では各 token を利用できます。

| 定義 | 用途 | C# |
|---|---|---|
| `ui-color-tokens.uss` | surface、text、border、state の意味を持つ色 | `UiColorTokens` |
| `ui-spacing-tokens.uss` | padding、margin、gap | `UiSpacingTokens` |
| `ui-shape-tokens.uss` | border 幅、角丸 | `UiBorderTokens` / `UiShapeTokens` |
| `ui-typography-tokens.uss` | font size | `UiTypographyTokens` |
| `ui-size-tokens.uss` | icon、control、compact geometry | `UiSizeTokens` |

実装時は次を守ります。

- USS では、共通 scale に存在する値を raw 値で再指定せず `var(--ee4v-...)` を使う
- C# で同じ値が必要な場合は `UiDesignTokens.cs` の対応する定数を使う
- control height など用途が決まった値は、primitive size より semantic alias を優先する
- 新しい共通値を追加するときは USS と C# の両方を更新する
- `UiDesignTokenTests` が USS / C# の値の一致と、標準値の直書き再混入を監査する

画面固有の大きな window 幅、preview サイズ、内容から計算される可変寸法は component 固有の layout 値として残して構いません。既存名を残すための互換 alias は追加せず、利用側を新しい token へ移行します。

### 色 token

- 基本 palette は `DESIGN.md` の Unity Editor 2022.3 Dark theme 測定値に合わせる
- surface / text / border / state など、用途を表す token を選ぶ
- selection、focus、active tool の青を同じ token にまとめない
- module 固有の状態色が必要な場合も component 内へ直書きせず、用途が分かる名前で token を追加する
- component の USS では raw な `#...` / `rgb(...)` / `rgba(...)` を直接指定しない

## `Label` を直接使わない

`new Label(...)` や `Label` 継承は禁止です。テキスト表示は `UiTextFactory.Create(...)` を使います。

理由:

- Unity 2022.3.22f1 のフォントキャッシュ問題を `UiTextFactory` で吸収している
- `UiLabelAuditTests` で direct `Label` 利用が監査されている
- UI テキストの見た目は `UiTextFactory` + `UiClassNames` + `TypographyStyleResolver` の組み合わせで統一している

許可されている例外実装は `Editor/UI/Foundation/Typography/UiTextFactory.cs` のみです。

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

ここでいう `*State` は描画 snapshot であり、Core UI component が機能上の state 管理責務を持つことを意味しません。

- Core UI component は渡された snapshot の描画と入力 event の通知に限定する
- cache、履歴、非同期処理、永続設定、画面遷移は feature 側の controller または module UI が所有する
- 複数 component の event 配線は window または feature の host で行い、共通 component 同士を直接参照させない
- window ごとに独立すべき表示セッション state は static にしない。複数 window で共有する値は setting や domain API など、共有元を明示する

## リスト入力と並び替えを分ける

- 値を追加・削除・編集する文字列リストは、1行を1要素として扱う複数行 list input を使う
- 値が固定され順序だけを変更する設定は、入力欄や追加・削除操作を持たない reorderable list を使う
- 並び替え可能な行にはドラッグ操作に加えてキーボード操作を用意する

## 組み込みアイコンを直参照しない

Unity の built-in icon はバージョン差分があるため、`Icon` / `UiBuiltinIcon` を使います。

- 追加が必要なら `UiBuiltinIcon` と `UiBuiltinIconResolver` を更新する
- `Editor/UI/Test/Editor/UiIconTests.cs` で解決可能か確認される

## ローカライズ

永続的に表示される文言は `I18N.Get("key")` を使います。

- `Ee4v.UI` namespace のコードは `UI` scope として解決される
- 文言は `Editor/UI/Localization/<locale>/*.jsonc` に追加する
- key の重複、未使用、未定義参照は静的監査の対象

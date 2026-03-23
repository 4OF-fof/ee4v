# UI実装

## 1. カテゴリ

UI コンポーネントは責務ごとにカテゴリを分ける。

- `Display`
  - 情報表示が主目的の汎用UI
- `Interactive`
  - ユーザー操作を伴う汎用UI
- `DataView`
  - データソースの閲覧を扱うUI
- `Overlay`
  - 既存画面の上に重ねて出すUI
- `Domain/<Feature>`
  - 特定ドメイン専用のUI

判断基準:

- 他画面でも再利用できるなら `Display` / `Interactive` / `DataView` / `Overlay`
- 特定機能の文脈がないと成立しないなら `Domain/<Feature>`

## 2. 新規実装

新規コンポーネントは基本的に以下を揃える。

- `Editor/UI/Components/<Category>/<Component>.cs`
- `Editor/UI/Components/<Category>/<component>.uss`

必要に応じて以下も更新する。

- `Editor/UI/Foundation/UiClassNames.cs`
- `Editor/UI/Foundation/Typography/TypographyStyleResolver.cs`
- `Editor/UI/Catalog/CatalogWindow.cs`
- `Editor/UI/Localization/<locale>/*.jsonc`

namespace は `Ee4v.UI` を使う。

## 3. 実装ルール

### 3-1. `Label` を直接使わない

`new Label(...)` や `Label` 継承は禁止。テキスト表示は `UiTextFactory.Create(...)` を使う。

理由:

- Unity 2022.3.22f1 のフォントキャッシュ問題を `UiTextFactory` で吸収している
- `UiLabelAuditTests` で direct `Label` 利用が監査されている
- UI テキストの見た目は `UiTextFactory` + `UiClassNames` + `TypographyStyleResolver` の組み合わせで統一している

許可されている例外実装は `Editor/UI/Foundation/Typography/UiTextFactory.cs` のみ。

### 3-2. class 名は `UiClassNames` に寄せる

`UiClassNames` は単なる class 名の定数置き場ではない。

- component の class 名をコード上で一元管理する
- `UiTextFactory.Create(...)` に渡す text 用 class 名の入口になる
- `TypographyStyleResolver` と組み合わせて、どのテキストがどの style で描画されるかを固定する

そのため、特にテキスト要素の class 名は string 直書きせず、`UiClassNames` に定数を追加して使う。

### 3-3. テキストは state 経由で差し替えられる形にする

既存UI は `*State` を受け取り、`SetState(...)` で見た目を更新する構成が多い。

- 例: `InfoCardState`
- 例: `SearchFieldState`
- 例: `WindowToastState`

UI要素の生成時に値を埋め込むより、stateを差し替えて再描画できる形を優先する。

### 3-4. 組み込みアイコンを直参照しない

Unityのbuilt-in iconはバージョン差分があるため、`Icon` / `UiBuiltinIcon` を使う。

- 追加が必要なら `UiBuiltinIcon` と `UiBuiltinIconResolver` を更新する
- `Editor/UI/Test/Editor/UiIconTests.cs` で解決可能か確認される

## 4. ローカライズ

永続的に表示される文言は `I18N.Get("key")` を使う。

- `Ee4v.UI` namespace のコードは `UI` scope として解決される
- 文言は `Editor/UI/Localization/<locale>/*.jsonc` に追加する

注意:

- keyの重複、未使用、未定義参照は静的監査の対象
- scopeはnamespaceから解決されるため、`Ee4v.UI` 以外に置くと想定外のscopeになる

## 5. Catalog への追加

新規 UI を作ったら、原則 `Debug/UI Catalog` で触れるようにする。

追加箇所は `Editor/UI/Catalog/CatalogWindow.cs`。

### 5-1. `EnsureStories()` に story を追加する

`StoryDefinition` を 1 件追加し、以下を埋める。

- `id`
  - 一意な識別子
- `group`
  - 一覧上のカテゴリ
  - `/` 区切りで階層化される
  - 例: `Display`, `Interactive`, `DataView`, `Overlay`, `Domain/Testing`
- `title`
  - コンポーネント名
- `description`
  - 一覧と詳細の短い説明
- `details`
  - 使いどころ、責務、制約
- `dependencies`
  - 内部利用している UI コンポーネント名
- `implementation`
  - 基本は `UiToolkit`
- `build`
  - プレビュー構築メソッド

### 5-2. `RebuildWindow()` に USS を追加する

Catalog がプレビューを正しく描画できるよう、対象コンポーネントの `.uss` を `UiStyleUtility.AddPackageStyleSheet(...)` で読み込む。

### 5-3. story 用の preview を作る

既存 story と同じ方針で、以下のどちらかを用意する。

- プロパティを触って見た目を確認できる control + preview
- 最小構成の static preview

Catalogは「存在確認」ではなく「使い方の見本」を置く前提で作る。

## 6. 実装チェックリスト

- 配置カテゴリは妥当か
- `Ee4v.UI` namespaceになっているか
- `Label` を直接使っていないか
- text 表示を `UiTextFactory` 経由にし、必要な class 名を `UiClassNames` に追加したか
- 必要なタイポグラフィ定義を `TypographyStyleResolver` に追加したか
- 文言を `I18N.Get(...)` に寄せ、`Editor/UI/Localization` を更新したか
- built-in icon を直接引かず `Icon` 経由にしたか
- `CatalogWindow` にstoryとstylesheetを追加したか
- Catalog 上で最低限の使い方が確認できるか

## 7. 補足

- 既存コンポーネントを組み合わせて作れるなら、まず `Domain/*` ではなく汎用カテゴリへの追加を検討する
- 1画面専用でも、今後の再利用が見込める責務なら先に汎用コンポーネントとして切り出す
- Catalog に載せにくい実装は、責務が曖昧かstateの切り方が不十分なことが多い

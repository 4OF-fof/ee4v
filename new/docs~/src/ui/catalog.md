# UI Catalog

新規 UI を作ったら、原則 `Debug/UI Catalog` で触れるようにします。追加箇所は `Editor/UI/Catalog/CatalogWindow.cs` です。

Catalog は「存在確認」ではなく「使い方の見本」を置く場所です。プロパティを触って見た目を確認できる control + preview、または最小構成の static preview を用意します。

## Story の追加

`EnsureStories()` に `StoryDefinition` を 1 件追加します。

| field | 内容 |
|---|---|
| `id` | 一意な識別子 |
| `group` | 一覧上のカテゴリ。`/` 区切りで階層化できる |
| `title` | コンポーネント名 |
| `description` | 一覧と詳細の短い説明 |
| `details` | 使いどころ、責務、制約 |
| `dependencies` | 内部利用している UI コンポーネント名 |
| `implementation` | 基本は `UiToolkit` |
| `build` | プレビュー構築メソッド |

`group` の例:

- `Display`
- `Interactive`
- `DataView`
- `Overlay`
- `Domain/Testing`

## USS の追加

Catalog がプレビューを正しく描画できるよう、対象コンポーネントの `.uss` を `UiStyleUtility.AddPackageStyleSheet(...)` で読み込みます。

## Preview の作成

既存 story と同じ方針で、以下のどちらかを用意します。

- プロパティを触って見た目を確認できる control + preview
- 最小構成の static preview

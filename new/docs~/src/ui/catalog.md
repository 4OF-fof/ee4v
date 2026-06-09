# UI Catalog

新規 UI を作ったら、原則 `Debug/UI Catalog` で触れるようにします。Catalog window 本体は navigation と共通 helper に寄せ、story の追加は Catalog registrar からの登録で行います。

Catalog は「存在確認」ではなく「使い方の見本」を置く場所です。プロパティを触って見た目を確認できる control + preview、または最小構成の static preview を用意します。

## Story の追加

`ICatalogRegistrar` を実装し、`CatalogRegistry.RegisterStory(...)` で story を 1 件追加します。汎用 UI は component フォルダ、module 専用 UI は各 module の `UI` 配下に registrar と preview 実装を置きます。

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

- `Content`
- `Content/Interactive`
- `Inputs`
- `Inputs/Selection`
- `Collections`
- `Overlays`
- `Layout`
- `Domain/Testing`

## USS の追加

Catalog がプレビューを正しく描画できるよう、対象コンポーネントの `.uss` を registrar から `CatalogRegistry.RegisterStyleSheet(...)` で登録します。

## Preview の作成

既存 story と同じ方針で、以下のどちらかを用意します。

- プロパティを触って見た目を確認できる control + preview
- 最小構成の static preview

Preview 実装は component フォルダの `<Component>.story.cs`、または各 module の `<name>.story.cs` に追加します。共通 UI は `Editor/UI/Catalog/helper` 配下の helper を使います。

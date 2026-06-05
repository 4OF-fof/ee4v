# UI

UI は Unity Editor 上で使う共通 UI コンポーネントと、その実装ルールをまとめる領域です。再利用可能な部品は `Editor/UI/Components` に置き、特定 feature に閉じる UI は `Domain/<Feature>` として分けます。

## カテゴリ

| category | 用途 | 例 |
|---|---|---|
| `Display` | 情報表示が主目的の汎用 UI | info card, badge |
| `Interactive` | ユーザー操作を伴う汎用 UI | `SearchField`, `TabCard` |
| `DataView` | データソースの閲覧を扱う UI | `SearchableTreeView` |
| `Overlay` | 既存画面の上に重ねて出す UI | toast, popup |
| `Domain/<Feature>` | 特定ドメイン専用 UI | testing 用 panel |

判断基準:

- 他画面でも再利用できるなら `Display` / `Interactive` / `DataView` / `Overlay`
- 特定機能の文脈がないと成立しないなら `Domain/<Feature>`

## 新規コンポーネント

新規コンポーネントは基本的に以下を揃えます。

- `Editor/UI/Components/<Category>/<Component>.cs`
- `Editor/UI/Components/<Category>/<component>.uss`

必要に応じて以下も更新します。

- `Editor/UI/Foundation/UiClassNames.cs`
- `Editor/UI/Foundation/Typography/TypographyStyleResolver.cs`
- `Editor/UI/Catalog/CatalogWindow.cs`
- `Editor/UI/Localization/<locale>/*.jsonc`

namespace は `Ee4v.UI` を使います。

## Pages

- [実装ルール](./rules.md)
- [Catalog への追加](./catalog.md)
- [チェックリスト](./checklist.md)

# UI

UI は Unity Editor 上で使う共通 UI コンポーネントと、その実装ルールをまとめる領域です。再利用可能な部品は `Editor/UI/Components` に置き、特定 feature に閉じる UI は各 module の `UI` 配下に置きます。

## カテゴリ

| category | 用途 | 例 |
|---|---|---|
| `Display` | 情報表示が主目的の汎用 UI | info card, badge |
| `Interactive` | ユーザー操作を伴う汎用 UI | `SearchField`, `TabCard` |
| `DataView` | データソースの閲覧を扱う UI | `SearchableTreeView` |
| `Layout` | 複数領域の配置や resize を扱う汎用 UI | `ThreePaneLayout` |
| `Overlay` | 既存画面の上に重ねて出す UI | `WindowToast`, `StatusOverlay`, `DiffConfirmationOverlay`, popup |
| `<Module>/UI` | 特定ドメイン専用 UI | `Editor/Testing/UI`, `Editor/AssetManager/UI` |

判断基準:

- 他画面でも再利用できるなら `Display` / `Interactive` / `DataView` / `Layout` / `Overlay`
- 特定機能の文脈がないと成立しないなら、その module の `UI`

`StatusOverlay` は `IBackgroundActivityTracker` の active state を描画する汎用 component です。background activity が存在する間だけ window 右下に spinner と message を表示し、処理ロジックや datasource には依存しません。

`DiffConfirmationOverlay` は現在値と入力される値を等幅の2 column gridで差分行ごとに並べ、item thumbnail とともに表示し、`Overwrite` / `Cancel` の結果だけを通知する汎用 component です。AssetManager の競合判定や同期処理には依存せず、表示文字列、thumbnail、差分 state は呼び出し側から受け取ります。

`ImageTooltip` は画像とファイル名を縦に並べる汎用 preview component です。画像取得や hover 判定には依存せず、`ImageTooltipState` で受け取った texture の下へファイル名を中央揃えで表示します。desktop 端では `ImageTooltipWindow` が pointer の反対側へ表示位置を補正します。

## 新規コンポーネント

新規コンポーネントは基本的に以下を揃えます。

- `Editor/UI/Components/<Category>/<Component>/<Component>.cs`
- `Editor/UI/Components/<Category>/<Component>/<component>.uss`
- `Editor/UI/Catalog/Stories/<Category>/<Component>/<Component>.story.cs`

必要に応じて以下も更新します。

- `Editor/UI/Foundation/UiClassNames.cs`
- `Editor/UI/Foundation/Typography/TypographyStyleResolver.cs`
- Catalog registrar
- `Editor/UI/Localization/<locale>/*.jsonc`

namespace は `Ee4v.UI` を使います。

## Pages

- [実装ルール](./rules.md)
- [Catalog への追加](./catalog.md)
- [チェックリスト](./checklist.md)

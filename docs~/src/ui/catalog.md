# UI Catalog

新規 UI を作ったら、原則 `ee4v/Debug/Catalog` で触れるようにします。Catalog window 本体は navigation と共通 helper に寄せ、story の追加は Catalog registrar からの登録で行います。

Catalog は「存在確認」ではなく「使い方の見本」を置く場所です。プロパティを触って見た目を確認できる control + preview、または最小構成の static preview を用意します。

## Story の追加

`ICatalogRegistrar` を実装し、`CatalogRegistry.RegisterStory(...)` で story を 1 件追加します。汎用 UI は `Editor/UI/Catalog/Stories`、module 専用 UI は `Editor/UI/Catalog/Stories/Modules/<Module>` に registrar と preview 実装を置きます。

`Content`、`Inputs`、`Collections`、`Navigation`、`Overlays`、`Layout` など
`Domain` 外へ登録する汎用 component / layout は、Catalog上の分類だけでなく本体も
`Editor/UI` 配下、`Ee4v.UI` namespace、`Ee4v.UI.Editor` assemblyが所有します。
module配下の実装をStoryだけ汎用カテゴリへ移すことは禁止します。汎用化する場合は
実装、USS、testをUIへ移してからCatalogへ登録します。

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

Preview 実装は `Editor/UI/Catalog/Stories` の `<Component>.story.cs`、または `Stories/Modules/<Module>` の `<name>.story.cs` に追加します。共通 UI は `Editor/UI/Catalog/helper` 配下の helper を使います。

## 画面と注入 UI の登録

独立した `EditorWindow` は window class 自体を埋め込まず、window が使用する実 component
から画面全体を再構築する screen story を登録します。window は lifecycle、state の接続、
component の配置だけを担当し、画面固有 layout を再利用できない形で `CreateGUI` に
閉じ込めないでください。

Project window / Hierarchy window へ注入する IMGUI renderer も Catalog の対象です。
`ComponentImplementationKind.Imgui` の story と `IMGUIContainer` preview を用意し、
実際の geometry helper と icon resolver を利用して代表状態を描画します。

次の単位は個別 story を持ちます。

- 共通 component
- module 固有 component
- 独立 window の screen
- Settings の user / project screen
- Project / Hierarchy へ注入する renderer

`UiCatalogCoverageTests` は必須 component / screen、IMGUI story、汎用UIの
namespace / assembly所有権、Foundation token以外の全 USS が Catalog registrarへ
登録されていることを監査します。

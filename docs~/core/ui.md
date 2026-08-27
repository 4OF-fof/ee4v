# Core UI

`Ee4v.UI.Editor`は機能横断のUI Toolkitコンポーネントと共通トークンを提供します。

## 共通部品の役割

### Reference

- `Color Palette`はUIとIMGUIで共有する役割別カラートークンの参照です。独自の表示部品ではありません。
- `Default Scrollbars`は`UiComposition.Prepare`配下へ適用される既定のスクロールバー表示です。独自の`ScrollView`型は提供しません。

### Inputs

- `UiButton`は文字、アイコン、強調度を統一した通常の操作ボタンです。Toolbarなど用途固有の小型寸法は利用側USSで指定します。
- `InputField`は1行、複数行、読み取り専用に対応する文字入力です。
- `SearchField`は検索入力、消去、任意の先頭操作をまとめます。先頭操作が有効な場合だけhover反応を表示します。
- `CommaSeparatedListField`はカンマ、セミコロン、改行で入力された文字列を一覧値として扱います。

### Layout

- `ActionBar`はToolbarやFooterの左、中央、右を配置する枠です。
- `LabeledContentRow`はラベル、入力内容、補助操作を並べるフォームの1行です。
- `SectionHeader`はセクション名、説明、右側操作を置く見出しです。
- `ContentRow`はアイコン、名称、補足、末尾操作を持つ汎用的な一覧行です。一覧自体は含みません。
- `InfoCard`は見出しと本文を外枠付きでまとめる情報パネルです。

### Navigation

- `NavigationItem`は`ContentRow`へクリックと選択状態を加えた移動項目です。
- `DisclosureSection`は見出しを押して本文を開閉するセクションです。

### Collections

- `SearchableTreeView`は検索欄と階層一覧をまとめ、絞り込みと展開状態の維持を行います。各行の表示は利用側が構成します。

### Labels

- `Badge`は件数や短い分類値を中立表示し、任意の`UiStatusTone`で処理状態も表示します。
- `TagPill`は任意の選択操作と削除操作を持つタグ表示です。

### Feedback

- `InlineMessage`は処理結果や入力エラーを行内で示すメッセージです。
- `EmptyState`は対象がない領域全体へ理由と次の操作を示します。
- `StatusOverlay`はバックグラウンド処理の進捗をウィンドウ右下へ重ねて表示します。

### Media

- `Icon`はFluent UI System Icons、実使用するUnity固有の組み込みアイコン、任意Textureの表示を共通化します。通常の操作アイコンにはFluent UI System Iconsを使用します。
- `CachedImage`はデコード済みTextureを複数の画像表示で共有します。
- `PreviewContainer`はPreview本体、未表示時のPlaceholder、重ねる操作を置く3層コンテナです。描画処理と一覧は含みません。

`PreviewContainer` Storyはコンテナの範囲を枠で示し、中央のContent、空表示のPlaceholder、右上のOverlay操作を重ねて確認できる構成にします。

### Overlays

- `CustomPopup`はHeader、本文、任意Footerを持つポップアップの外枠です。`CustomPopupWindow`が移動、リサイズ、focus離脱時のCloseを担当します。

### Catalog外の基盤

- `UiComposition.Prepare`は共通USSと機能固有USSを一つの入口で登録します。
- `UiTextFactory`は文字を描画するUI要素の生成と文字更新を統一します。
- `UiDragAndDrop`は型付きpayloadによるドラッグ開始とMove操作の受け入れを共通化します。
- `PreviewOrbitController`は3D Previewの回転、移動、拡縮とCamera配置を共通化します。Bounds計算と描画内容は利用側が扱います。

これらは機能間で重複していた小さな表示パターンをCoreへ昇格したものです。Domain固有の文言、操作、一覧構造は利用側に残します。

## `CustomPopup`

`CustomPopup`は`c31a22adf8a49e6fc91addde1febed94a98ea85b`の`old/Editor/Core/UI/Window/BaseWindow.cs`を基準にしたpopup外枠です。同じ`old`配下の`FolderStyleSelectorWindow`などが使用していたインラインスタイルと操作を移植し、外枠は現行仕様の1pxに変更しています。現行の`UiTextFactory`、`FluentUiIcons`、`EditorPopupApi`へ接続し、画面内への位置補正とUnity標準Pickerのfocus処理を加えています。popup用Windowは`CustomPopupWindow`を継承し、`ShowAsPopup`で表示して`SetPopup(CustomPopup)`で外枠を設定します。

| API | 動作 |
|---|---|
| `HeaderLeading` | ヘッダータイトルの左側へアイコンなどを追加する |
| `HeaderActions` | ヘッダータイトルの右側へ補助操作を追加する |
| `Content` | 機能固有のフォームや一覧を追加する |
| `Footer` | キャンセル、確定などの操作を追加する |
| `SetTitle(string)` | ヘッダーのタイトルを更新する |
| `SetFooterVisible(bool)` | フッターの表示を切り替える |
| `CustomPopupWindow.ShowAsPopup(VisualElement, Vector2)` | anchor直下へ指定サイズで表示する |
| `CustomPopupWindow.ShowAsPopup(VisualElement, Vector2, Vector2)` | panel上の指定位置を起点に表示する |
| `CustomPopupWindow.ShowAsPopup(Vector2, Vector2)` | screen上の指定位置を起点に表示する |
| `CustomPopupWindow.SetPopup(CustomPopup)` | 旧`BaseWindow`形式の外枠、Header、Close操作を設定する |
| `CustomPopupWindow.ConfigureCloseAndSubmitKeys(VisualElement, Action)` | Escapeで閉じ、任意のEnter確定処理を呼び出す |

AssetManagerのタグ選択画面、Target選択画面、コレクション作成画面に加え、Project StyleとHierarchy Styleが共有するItem Style画面で使用します。表示は`ee4v/Debug/Catalog`の`Overlays/CustomPopup`、`Domain/ProjectStyle/Project Style Window`、`Domain/HierarchyStyle/Hierarchy Style Window` Storyで確認できます。

## 機能UIのStory

独立したEditorWindowまたは機能固有のVisualElementは、実使用するViewを`IUiStoryProvider`から組み立てます。Story専用の複製UIは作らず、保存や外部Sourceの変更は行いません。画面全体、一覧、Gridは`Domain/<feature>`に配置し、画面へ組み込む再利用可能な部品だけを`Domain/<feature>/Components`に配置します。

CoreコンポーネントのStoryは、`Reference`を除いて`Controls`と`Preview`を持ちます。`Controls`では公開状態を変更でき、Preview内の操作は結果表示または状態変更へ接続します。表示だけの無反応な操作は置きません。

Catalog上の分類とコード上の分割は別に扱います。コードではWindowホストと表示クラスを分離し、`BlendShapePresetView`、`WindowGroupSettingsView`、`ItemStyleEditor`を各Windowと別ファイルで管理します。

| UI | Story |
|---|---|
| 色と既定の縦横スクロールバー | `Reference/*` |
| ボタンと入力欄 | `Inputs/*` |
| Action Bar、フォーム行、見出し、一覧行、情報パネル | `Layout/*` |
| 移動項目と開閉セクション | `Navigation/*` |
| 検索可能な階層一覧 | `Collections/*` |
| BadgeとTag Pill | `Labels/*` |
| 行内メッセージ、空表示、進捗表示 | `Feedback/*` |
| Icon、CachedImage、PreviewContainer | `Media/*` |
| ポップアップ外枠 | `Overlays/*` |
| Core Settings | `Domain/Core/Settings UI` |
| AssetManagerの画面、Grid、Tree、Popup | `Domain/AssetManager/*` |
| AssetManagerのBreadcrumb、Tag Field、Controls、Grid Card、Filter Editor、Detail parts | `Domain/AssetManager/Components/*` |
| ItemStyleのProject・Hierarchy編集 | `Domain/ProjectStyle/Project Style Window`、`Domain/HierarchyStyle/Hierarchy Style Window` |
| Hidden Objects | `Domain/HierarchyStyle/Hidden Objects`、`Domain/HierarchyStyle/Components/*` |
| Scene Switcher | `Domain/SceneSwitcher/SceneSwitcherView`、`Domain/SceneSwitcher/Components/SceneSwitcherRow` |
| Project Tabs | `Domain/ProjectTabs/Components/Project Tabs` |
| Face Expression、Gesture Assignment、Expression Groups、BlendShape Presets | `Domain/FaceExpression/*`、`Domain/FaceExpression/Components/*` |
| Window Groups | `Domain/WindowGroup/Window Groups` |

# Core UI

`Ee4v.UI.Editor`は機能横断のUI Toolkitコンポーネントと共通トークンを提供します。

## 入力コンポーネント

- `UiButton`は文字、`IconState`、標準状態表現をまとめます。機能固有の色や選択状態は利用側のクラスで追加します。
- `UiComposition.Prepare`を適用したroot配下では、Buttonがフォーカスを得た際の青い枠を表示しません。入力欄の編集フォーカスと、カードなどの選択状態は各コンポーネントの状態表現を維持します。
- `UiComposition.Prepare(root, styleSheetPaths)`は共通USS、共有コンポーネントのUSS、機能固有USSを一つの入口で登録します。`styleSheetPaths`には機能固有USSだけを指定し、EditorWindowや埋め込みViewはroot classとStyleSheetを個別に組み立てません。
- `InputField`は1行入力と複数行入力に対応し、読み取り専用、プレースホルダー、欄内スクロールを共通化します。
- `UiComposition.Prepare`を適用したroot配下では、縦横のスクロールバーを共通の8px幅、4px thumbで表示します。縦のthumbはtrackの右端へ揃えます。個別画面は細幅classや固有USSを追加しません。`UiClassNames.ThinVerticalScrollbar`は共通root外へ縦スクロールバーだけを適用する場合に使用します。
- `SearchField`は検索、消去、プレースホルダーを提供します。`SearchFieldState.SearchActionEnabled`を有効にすると、先頭アイコンから`SearchActionRequested`を通知します。ポップアップなどの配置には`SearchActionAnchor`を使用できます。
- `SearchableTreeView`は検索とTreeViewを共通化します。データ更新時は展開中の項目IDを復元します。行の再構築後もホイール入力時にTreeViewへフォーカスを戻し、Unity標準のScrollView設定でスクロールバーだけを非表示にします。
- `Icon`はFluent UI System Icons、Unity組み込みアイコン、任意Textureの表示を共通化します。汎用操作・状態は`FluentUiIcons`、Unityの実体やEditor概念は`UiBuiltinIconResolver`から取得します。`IconState.FromTexture`では必要に応じてtint色を指定できます。

## 表示コンポーネント

- `EmptyState`は、対象がない領域に表示するアイコン、見出し、説明、任意操作を構成します。
- `SectionHeader`は、セクションの見出し、説明、右側の操作領域を構成します。
- `ContentRow`は、先頭要素、アイコン、名称、補足、末尾操作からなる1件分の行を構成します。一覧やGrid自体は含みません。
- `LabeledContentRow`は、ラベル、入力内容、補助操作からなるフォームの1行を構成します。
- `InlineMessage`は、処理結果や入力エラーを`UiStatusTone`と任意アイコンで表示します。
- `PreviewSurface`は、Preview本体、未表示時のPlaceholder、重ねる操作を構成します。Previewを並べるGridは含みません。
- `PreviewOrbitController`は3D Previewの右ドラッグ回転、中ドラッグ移動、ホイール拡縮とCamera配置を共通化します。Boundsの計算と描画内容は利用側が扱います。
- `CustomPopupWindow`は旧`BaseWindow`のpopup実装を引き継ぎます。1pxの外枠、24pxのHeader、Headerのドラッグ移動、左右と下端のリサイズ、focus離脱時のCloseを共通化します。ColorPicker、ObjectSelector、EyeDropperの使用中はWindowを維持します。`ConfigureCloseAndSubmitKeys`でEscapeによるCloseと任意のEnter確定を設定します。
- `UiDragAndDrop`は、一定距離の左ドラッグ開始と型付きpayloadのMove操作受け入れを共通化します。payloadの生成、drop可否、表示フィードバック、適用処理は利用側が渡します。
- `ActionBar`は、伸縮する左側領域、任意の中央領域、右側の操作領域を構成します。画面固有のToolbarやFooterはこれを継承または内包します。
- `NavigationItem`は、`ContentRow`を内包した選択可能な1件分のNavigationです。Navigation一覧や階層は含みません。
- `DisclosureSection`は、見出し操作と開閉可能な本文を構成します。
- `Badge`は、件数や短い分類値を表示します。処理状態を表す`StatusBadge`とは意味を分けます。
- `TagPill`は短いタグ名をpill形で表示し、任意の先頭アイコン、pill全体の選択操作、右端の削除操作を構成します。選択操作はクリックとキーボードに対応し、削除ボタンはホバーと押下を背景色で示します。

これらはAssetManager、Face Expression、Hierarchy Style、Scene Switcher、Item Style、Window Groupで重複していた小さな表示パターンをCoreへ昇格したものです。Domain固有の文言、操作、一覧構造、配置は利用側に残します。各状態は`ee4v/Debug/Catalog`の`Content/*`または`Inputs/LabeledContentRow` Storyで確認できます。

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

Catalog上の分類とコード上の分割は別に扱います。コードではWindowホストと表示クラスを分離し、`BlendShapePresetView`、`WindowGroupSettingsView`、`ItemStyleEditor`を各Windowと別ファイルで管理します。

| UI | Story |
|---|---|
| 既定の縦横スクロールバー | `Collections/Default Scrollbars` |
| Empty State、Section Header、Content Row、Inline Message、Preview Surface、Action Bar、Navigation Item、Disclosure Section、Badge、Tag Pill | `Content/*` |
| Labeled Content Row | `Inputs/LabeledContentRow` |
| Core Settings | `Domain/Core/Settings UI` |
| AssetManagerの画面、Grid、Tree、Popup | `Domain/AssetManager/*` |
| AssetManagerのBreadcrumb、Tag Field、Controls、Grid Card、Filter Editor、Detail parts | `Domain/AssetManager/Components/*` |
| ItemStyleのProject・Hierarchy編集 | `Domain/ProjectStyle/Project Style Window`、`Domain/HierarchyStyle/Hierarchy Style Window` |
| Hidden Objects | `Domain/HierarchyStyle/Hidden Objects`、`Domain/HierarchyStyle/Components/*` |
| Scene Switcher | `Domain/SceneSwitcher/SceneSwitcherView`、`Domain/SceneSwitcher/Components/SceneSwitcherRow` |
| Project Tabs | `Domain/ProjectTabs/Components/Project Tabs` |
| Face Expression、Gesture Assignment、Expression Groups、BlendShape Presets | `Domain/FaceExpression/*`、`Domain/FaceExpression/Components/*` |
| Window Groups | `Domain/WindowGroup/Window Groups` |

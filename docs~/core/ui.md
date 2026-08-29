# Core UI

`Ee4v.UI.Editor`は機能横断のUI Toolkitコンポーネントと共通トークンを提供します。

## 共通部品の役割

Catalogは利用場面ではなく、部品が公開する責務で分類します。ユーザーの操作や値を受け取る部品は`Inputs`、情報を描画する部品は`Displays`、呼び出し側の子要素を配置する部品は`Containers`、複数項目を管理する部品は`Collections`に置きます。

Catalogのナビゲーションではカテゴリ名も選択できます。カテゴリページには分類の説明と、下位カテゴリを含めて属しているStoryの名称・概要を表示します。一覧から各Storyへ移動できます。

### Reference

- `Color Palette`はUIとIMGUIで共有するUnity Dark・Lightの役割別カラートークンの参照です。独自の表示部品ではありません。
- `Default Scrollbars`は`UiComposition.Prepare`配下へ適用される既定のスクロールバー表示です。独自の`ScrollView`型は提供しません。

### Inputs

- `UiButton`は文字、アイコン、強調度を統一した通常の操作ボタンです。通常ボタンは操作面を薄く塗り、InputFieldと同じ境界線とフォーカス色を使用します。Ghostは通常時の面と境界線を表示しません。Toolbarなど用途固有の小型寸法は利用側USSで指定します。サムネイルなどの複合内容は`Content`へ配置し、操作面そのものは`UiButton`に保ちます。
- `InputField` Storyはテキスト、トグル、Object選択、ドロップダウンを含む単一値入力の標準入口です。文字列には`InputField`コンポーネントを使用し、その他の値には`UiTextFactory`が生成する型付き入力を使用します。単行テキストとドロップダウンは透明背景の下線型、複数行テキストは全周の境界と内側余白を持つテキストエリアとして表示します。トグルは標準入力と同じ境界線と操作色を使う小型チェックボックスとし、選択時は青い面と白いチェックマークで状態を示します。フォーカスの有無では外観を変えません。
- `SearchField`は薄い入力面と全周枠へ検索入力、消去、任意の先頭操作をまとめます。検索アイコンと角丸の全周枠で用途を示し、境界線とフォーカス色はInputFieldと共有します。先頭操作が有効な場合だけhover反応を表示します。
- `ListField<T>`は任意型の値を1項目1行で表示する一覧です。利用側が値から項目要素を生成し、編集項目では変更通知を返します。追加値の生成処理を指定した場合だけ、通常項目と同じ寸法の追加行と各行の削除ボタンを表示します。区切り文字の解釈や保存形式への変換は行いません。
- `FormInput`は任意ラベル、必須の単一入力コンポーネント、任意ボタンを横一列に配置するフォーム入力です。囲み枠は持ちません。文字入力には`InputField`を使用し、`Toggle`と`ObjectField`には`UiTextFactory`の標準入力スタイルを使用します。`ListField<T>`のように複数の入力要素を持つ場合も一つの入力コンポーネントとして渡します。値の保持と検証は入力コンポーネントが担当します。
- `InputGroup`はfieldset風の外枠へ必須見出しを重ね、1個以上の`FormInput`をまとめる入力グループです。子孫の入力へフォーカスが移ると外枠を強調します。各`FormInput`のラベルは省略できます。
- `NavigationItem`は`ItemRow`へクリックと選択状態を加えた操作項目です。
- `TagPill`は任意の選択操作と削除操作を持つタグ入力です。pill形と薄い面を保ち、境界線と選択操作にはInputFieldと同じフォーカス色を使用します。

### Displays

- `Badge`は件数や短い分類値を中立表示し、任意の`UiStatusTone`で処理状態も表示します。
- `EmptyState`は薄い枠面で空領域を示し、対象がない理由と次の操作を中央へ表示します。操作要素自体は利用側が`Actions`へ追加します。
- `StatusOverlay`は浮いた小型パネルとして、バックグラウンド処理の進捗をウィンドウ右下へ重ねて表示します。
- `Icon`はFluent UI System Icons、実使用するUnity固有の組み込みアイコン、任意Textureの表示を共通化します。通常の操作アイコンにはFluent UI System Iconsを使用します。
- `CachedImage`はデコード済みTextureを複数の画像表示で共有します。

### Containers

- `ActionBar`はToolbarやFooterの左、中央、右を配置する枠です。
- `ItemRow`はアイコン、名称、補足、末尾操作を持つ一覧の1項目です。一覧自体は含みません。
- `SectionHeader`はセクション名、説明、右側操作を置く見出しです。
- `DisclosureSection`は見出しを押して本文を開閉するセクションです。
- `InfoCard`は見出しと本文を外枠付きでまとめる情報パネルです。
- `PreviewContainer`はPreview本体、未表示時のPlaceholder、重ねる操作を置く3層コンテナです。描画処理と一覧は含みません。
- `CustomPopup`は細い外枠と面の濃度差でHeader、本文、任意Footerを分けるポップアップです。`CustomPopupWindow`が移動、リサイズ、focus離脱時のCloseを担当します。

`PreviewContainer` Storyはコンテナの範囲を枠で示し、中央のContent、空表示のPlaceholder、右上のOverlay操作を重ねて確認できる構成にします。

### Collections

- `SearchableTreeView`は検索欄と階層一覧をまとめ、絞り込みと展開状態の維持を行います。各行の表示は利用側が構成します。

### Catalog外の基盤

- `UiComposition.Prepare`は共通USSと機能固有USSを一つの入口で登録します。
- `UiColorPalette`はUI ToolkitとIMGUIが共有するテーマ別標準色の契約です。`UiColorPalettes`がUnity Dark・Lightのバリエーションを所有し、`UiColorTokens.Current`と`UiComposition.Prepare`が現在のUnity Editorテーマへ接続します。
- `UiTextFactory`は文字を描画するUI要素の生成と文字更新を統一します。通常の操作、文字入力、検索入力にはそれぞれ`UiButton`、`InputField`、`SearchField`を使用し、低レベルの`TextField`生成は共有入力コンポーネント内部に限定します。Factoryが生成する型付き入力には基盤となる共通クラスを付与します。数値入力とドロップダウンは透明背景の下線型、`Toggle`は標準入力と境界線、操作色、状態遷移を共有する小型チェックボックス型、`ObjectField`は右端の操作領域を分けた選択欄として`FormInput`の内外で共有します。複合入力内の埋め込みフィールドは共通クラスを外し、親コンポーネントが外観を担当します。
- `UiDragAndDrop`は型付きpayloadによるドラッグ開始とMove操作の受け入れを共通化します。
- `PreviewOrbitController`は3D Previewの回転、移動、拡縮とCamera配置を共通化します。Bounds計算と描画内容は利用側が扱います。

これらは機能間で重複していた小さな表示パターンをCoreへ昇格したものです。Domain固有の文言、操作、一覧構造は利用側に残します。

## `CustomPopup`

`CustomPopup`は`c31a22adf8a49e6fc91addde1febed94a98ea85b`の`old/Editor/Core/UI/Window/BaseWindow.cs`を基準にしたpopup外枠です。同じ`old`配下の`FolderStyleSelectorWindow`などが使用していた操作を移植し、外観は共通トークンを使う単純なパネルへ変更しています。外枠は1pxとし、HeaderとFooterは本文との面の濃度差と境界線で区切ります。現行の`UiTextFactory`、`FluentUiIcons`、`EditorPopupApi`へ接続し、画面内への位置補正とUnity標準Pickerのfocus処理を加えています。popup用Windowは`CustomPopupWindow`を継承し、`ShowAsPopup`で表示して`SetPopup(CustomPopup)`で外枠を設定します。

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

AssetManagerのタグ選択画面、Target選択画面、コレクション作成画面に加え、Project StyleとHierarchy Styleが共有するItem Style画面で使用します。表示は`ee4v/Debug/Catalog`の`Containers/CustomPopup`、`Domain/ProjectStyle/Project Style Window`、`Domain/HierarchyStyle/Hierarchy Style Window` Storyで確認できます。

## 機能UIのStory

独立したEditorWindowまたは機能固有のVisualElementは、実使用するViewを`IUiStoryProvider`から組み立てます。Story専用の複製UIは作らず、保存や外部Sourceの変更は行いません。画面全体、一覧、Gridは`Domain/<feature>`に配置し、画面へ組み込む再利用可能な部品だけを`Domain/<feature>/Components`に配置します。

CoreコンポーネントのStoryは、`Reference`を除いて`Controls`と`Preview`を持ちます。`Controls`の入力は`FormInput`へ統一し、公開状態を変更できます。Preview内の操作は結果表示または状態変更へ接続します。表示だけの無反応な操作は置きません。

Catalog上の分類とコード上の分割は別に扱います。コードではWindowホストと表示クラスを分離し、`BlendShapePresetView`、`WindowGroupSettingsView`、`ItemStyleEditor`を各Windowと別ファイルで管理します。

| UI | Story |
|---|---|
| 色と既定の縦横スクロールバー | `Reference/*` |
| 操作、選択、値入力 | `Inputs/*` |
| 文字、状態、画像の表示 | `Displays/*` |
| 子要素の配置と表示切り替え | `Containers/*` |
| 検索可能な階層一覧 | `Collections/*` |
| Core Settings | `Domain/Core/Settings UI` |
| AssetManagerの画面、Grid、Tree、Popup | `Domain/AssetManager/*` |
| AssetManagerのBreadcrumb、Tag Field、Controls、Grid Card、Filter Editor、Detail parts | `Domain/AssetManager/Components/*` |
| ItemStyleのProject・Hierarchy編集 | `Domain/ProjectStyle/Project Style Window`、`Domain/HierarchyStyle/Hierarchy Style Window` |
| Hidden Objects | `Domain/HierarchyStyle/Hidden Objects`、`Domain/HierarchyStyle/Components/*` |
| Scene Switcher | `Domain/SceneSwitcher/SceneSwitcherView`、`Domain/SceneSwitcher/Components/SceneSwitcherRow` |
| Project Tabs | `Domain/ProjectTabs/Components/Project Tabs` |
| Face Expression、Gesture Assignment、Expression Groups、BlendShape Presets | `Domain/FaceExpression/*`、`Domain/FaceExpression/Components/*` |
| Window Groups | `Domain/WindowGroup/Window Groups` |

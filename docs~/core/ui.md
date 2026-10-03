# Core UI

`Ee4v.UI.Editor`は機能横断のUI Toolkitコンポーネントと共通トークンを提供します。

## 共通部品の役割

Catalogは利用場面ではなく、部品が公開する責務で分類します。ユーザーの操作や値を受け取る部品は`Inputs`、情報を描画する部品は`Displays`、呼び出し側の子要素を配置する部品は`Containers`、複数項目を管理する部品は`Collections`に置きます。

Catalogのナビゲーションではカテゴリ名も選択できます。カテゴリページには分類の説明と、下位カテゴリを含めて属しているStoryの名称・概要を表示します。一覧から各Storyへ移動できます。

Storyページは詳細カードと実部品の描画領域を分離し、内容の高さを維持してスクロールします。必要な最小幅がウィンドウを超える部品は横スクロールで確認できます。親のサイズに追従するプレビューや画面全体の部品は、Story側で高さを持つ外枠へ配置します。

Catalogのrootにも実画面と同じ`UiComposition.Prepare`を適用します。共通部品のスタイルはこの入口から読み込み、Storyが別途登録する機能固有スタイルを追加します。

### Reference

- `Color Palette`はUIとIMGUIで共有するUnity Dark・Lightの役割別カラートークンの参照です。独自の表示部品ではありません。
- `Default Scrollbars`は`UiComposition.Prepare`配下へ適用される既定のスクロールバー表示です。スクロール方向の内部Sliderの端の余白をなくし、最小・最大位置のつまみをバーの端へ合わせます。縦横のバーに共通で適用します。独自の`ScrollView`型は提供しません。

### Inputs

- `UiButton`は文字、アイコン、強調度を統一した通常の操作ボタンです。通常ボタンは操作面を薄く塗り、InputFieldと同じ境界線とフォーカス色を使用します。Ghostは通常時の面と境界線を表示しません。Toolbarなど用途固有の小型寸法は利用側USSで指定します。サムネイルなどの複合内容は`Content`へ配置し、操作面そのものは`UiButton`に保ちます。`SetPrimaryActionEnabled`は保存などの主操作の有効状態・青い操作面・ラベル色をまとめて切り替え、統合と単独のAvatar編集Windowが共有します。
- `InputField` Storyはテキスト、トグル、Object選択、ドロップダウンを含む単一値入力の標準入口です。文字列には`InputField`コンポーネントを使用し、その他の値には`UiTextFactory`が生成する型付き入力を使用します。単行テキストとドロップダウンは透明背景の下線型、複数行テキストは全周の境界と内側余白を持つテキストエリアとして表示します。トグルは標準入力と同じ境界線と操作色を使う小型チェックボックスとし、選択時は青い面と白いチェックマークで状態を示します。フォーカスの有無では外観を変えません。
- `SearchField`は薄い入力面と全周枠へ検索入力、消去、任意の先頭操作をまとめます。検索アイコンと角丸の全周枠で用途を示し、境界線とフォーカス色はInputFieldと共有します。先頭操作が有効な場合だけhover反応を表示します。
- `ListField<T>`は任意型の値を1項目1行で表示する一覧です。利用側が値から項目要素を生成し、編集項目では変更通知を返します。追加値の生成処理を指定した場合だけ、通常項目と同じ寸法の追加行と各行の削除ボタンを表示します。区切り文字の解釈や保存形式への変換は行いません。
- `FormInput`は任意ラベル、必須の単一入力コンポーネント、任意ボタンを横一列に配置するフォーム入力です。囲み枠は持ちません。文字入力には`InputField`を使用し、`Toggle`と`ObjectField`には`UiTextFactory`の標準入力スタイルを使用します。`ListField<T>`のように複数の入力要素を持つ場合も一つの入力コンポーネントとして渡します。値の保持と検証は入力コンポーネントが担当します。
- `InputGroup`はfieldset風の外枠へ開閉可能な必須見出しを重ね、1個以上の`FormInput`をまとめる入力グループです。見出しから内容を開閉し、閉状態は高さとシェブロンで示します。枠の内外と見出し背景は通常色へ揃え、シェブロンと文字の領域だけをホバー対象として外枠の色を変えます。枠内の内容上や子孫入力のフォーカスでは外観を変えません。展開状態の変更を通知し、各`FormInput`のラベルは省略できます。
- `NavigationItem`は`ItemRow`へクリックと選択状態を加えた操作項目です。
- `SelectionTab`は名称と選択状態を持つタブです。通常と強調用の`SelectionTabVariant`を持ち、クリック時の操作は利用側が接続します。
- `BodyPartSelector`はCoreの`BodyPartCategory`を使う部位選択です。選択通知と利用可否の判定は利用側が渡します。
- `PrefabSelector`は候補Prefabの選択とドラッグの受け入れを行います。`ShowPicker`は同じ候補を検索付きの一覧・Gridから選択する入口で、候補取得や保存は行いません。PickerのWindowは内部実装です。
- `TagPill`は任意の選択操作と削除操作を持つタグ入力です。pill形と薄い面を保ち、境界線と選択操作にはInputFieldと同じフォーカス色を使用します。

### Displays

- `Badge`は件数や短い分類値を中立表示し、任意の`UiStatusTone`で処理状態も表示します。
- `EmptyState`は薄い枠面で空領域を示し、対象がない理由と次の操作を中央へ表示します。操作要素自体は利用側が`Actions`へ追加します。
- `MessagePanel`はエラー・警告・案内を表示する共通パネルです。`MessagePanelState`で見出し、説明、`MessageSeverity`と任意の対象一覧を渡します。共通の文字・余白・角丸・意味別の色と小型Fluentアイコンを使用し、長文は折り返し、多数の対象は高さ144px以内の一覧で縦スクロールします。説明だけの通知ではアイコンと本文を縦中央へ揃え、本文はアイコンと同じ最小高さを確保します。空のstateでは非表示になります。検証や翻訳は利用側が担当し、Catalogの`Displays`へ登録します。
- `StatusOverlay`はspinnerと状態メッセージを表示する小型パネルです。バックグラウンド処理ではウィンドウ右下へ重ね、統合ee4vの変更破棄では再読み込み完了まで画面中央へ表示します。配置と操作の抑止は使用側が制御します。
- `Icon`はFluent UI System Icons、実使用するUnity固有の組み込みアイコン、任意Textureの表示を共通化します。通常の操作アイコンと`InputGroup`の開閉chevronにはFluent UI System Iconsを使用します。組み込みアイコンはUnityのFolder、Scene、GameObject、Model FileとHierarchyの非表示操作に限定します。Fluent画像が読み込めない場合は組み込みアイコンへ代替せず非表示にします。
- `PrefabThumbnail`はUnityのAssetPreviewを表示し、読み込み中はMiniThumbnailを表示します。取得の再試行は表示中だけ行います。
- `PrefabScenePreview`はCoreの[`AvatarPreviewRenderer`](./avatar-preview.md)へ描画を委ねます。Scene上の対象は複製せず、Edit ModeではNDMF Previewの加工後Renderer、Play Modeでは実際の対象を描画します。Prefabアセットの表示は隔離された表示用コピーを使用します。対象、部位・Material選択、Cameraと破棄を担当し、`SetFlexibleLayout`で親のサイズへ追従し、`SetViewToggleVisible`で視点切り替えトグルだけを非表示にでき、Cameraのフレーミングは変えません。`SetFullBodyFraming`で全体を収める標準表示と編集向けの拡大表示を切り替えます。既定では全体を収めます。保存、DB、Play Modeの切り替えは行いません。表示中は約30fpsで再描画し、NDMFの更新、時間依存Shaderと実行中の姿勢を表示します。
- `CachedImage`はデコード済みTextureを複数の画像表示で共有します。

`PrefabScenePreview.SetScope`は描画対象とboundsを同期的に切り替え、PreviewとNDMF sessionを再生成しません。選択した構成Prefab rootのEditorOnly除外規則も同時に再計算し、scope変更後の最初の描画から選択範囲だけを表示します。

`FocusBodyPart`は通常、選択部位へCameraをアニメーションで合わせます。`preserveView: true`では部位選択と視点ボタンだけを更新し、Cameraの位置・角度・距離を維持して進行中のアニメーションを止めます。構成Prefabの選択解除・切り替え時に使用します。

`PrefabScenePreview`の選択輪郭は、選択中のRenderer・Material slotの加工後submeshをGeometry maskへ投影して生成します。maskは深度判定を行わず、手前のオブジェクトに遮られた部分や背面も含め、輪郭をPreview画像の上へ重ねます。透過・cutoutのテクスチャ形状ではなくMeshの形状を使用します。別Materialのsubmeshや影はmaskに含めません。部位選択では対象Renderer内の表示中slotをすべて含め、表示切替で非表示にした部位・Materialは輪郭にも含めません。Geometry maskは更新ごとに解放し、共有Meshを複製・変更しません。クリック選択では元Materialの描画差分を使用して見えている対象を判定します。

### Containers

- `SelectionTabBar`は固定の先頭タブ、横スクロールする`Tabs`と任意の操作を`Items`に配置します。タブの種類や選択状態は利用側が決めます。
- `ActionBar`はToolbarやFooterの左、中央、右を配置する枠です。
- `ItemRow`はアイコン、名称、補足、末尾操作を持つ一覧の1項目です。一覧自体は含みません。
- `SectionHeader`はセクション名、説明、右側操作を置く見出しです。
- `InfoCard`は見出しと本文を外枠付きでまとめる情報パネルです。
- `PreviewContainer`はPreview本体、未表示時のPlaceholder、重ねる操作を置く3層コンテナです。描画処理と一覧は含みません。
- `PreviewPane`はプレビューの見出し、見出し右側の`Actions`、描画要素を置く`Content`をまとめる外枠です。`SetTitle`で見出しを更新します。描画、対象の保持、Camera、Play Modeの制御と破棄は利用側の責務です。AssetManagerの編集用プレビューと実行確認で共有します。
- `ScenePreviewViewport`は3D Preview向けにグリッド背景、IMGUI描画領域、背景の明暗切り替え、表示リセット、未表示時のPlaceholderをまとめます。機能固有のColliderやBone表示操作は`FeatureOverlay`へ重ね、3D描画自体とCamera制御は利用側が扱います。`PreviewOrbitController`はtarget・距離・yaw・pitchの即時設定と時間補間を提供し、アニメーション中の再描画スケジュールは利用側が管理します。右ドラッグ、中央ドラッグ、ホイール操作を始めた場合は補間を中断します。
- `CustomPopup`は細い外枠と面の濃度差でHeader、本文、任意Footerを分けるポップアップです。`CustomPopupWindow`が移動、リサイズ、focus離脱時のCloseを担当します。

`PreviewContainer` Storyはコンテナの範囲を枠で示し、中央のContent、空表示のPlaceholder、右上のOverlay操作を重ねて確認できる構成にします。

`PreviewPane` Storyは実部品へ内容と操作ボタンを配置し、見出しの更新を確認します。共通の3D描画は`Displays/PrefabScenePreview`、機能固有の描画内容はDomain側のStoryで確認します。

`ScenePreviewViewport` Storyは共有グリッド、右上の背景切り替えと表示リセット、左上の機能固有オーバーレイを一つのPreview内で確認できる構成にします。AssetManagerのAppearance PreviewとPlay Modeの操作確認は`PrefabScenePreview`を介してこの部品を使用し、表情エディターは自身の顔向けCamera制御と組み合わせます。描画画像は透過で重ね、グリッド背景と明暗切り替えを表示します。`PreviewGridBackground`は同じグリッドの描画とTextureの破棄を担当し、表情サムネイルでも使用します。

### Collections

- `SearchableTreeView`は検索欄と階層一覧をまとめ、絞り込みと展開状態の維持を行います。各行の表示は利用側が構成します。

### Catalog外の基盤

- `UiLocalization`は共通部品の文字をUI scopeから取得する内部基盤です。部品は利用機能の翻訳catalogへ依存しません。
- `UiComposition.Prepare`は共通USSと機能固有USSを一つの入口で登録します。
- `UiColorPalette`はUI ToolkitとIMGUIが共有するテーマ別標準色の契約です。`UiColorPalettes`がUnity Dark・Lightのバリエーションを所有し、`UiColorTokens.Current`と`UiComposition.Prepare`が現在のUnity Editorテーマへ接続します。
- `UiTextFactory`は文字を描画するUI要素の生成と文字更新を統一します。通常の操作、文字入力、検索入力にはそれぞれ`UiButton`、`InputField`、`SearchField`を使用し、低レベルの`TextField`生成は共有入力コンポーネント内部に限定します。FactoryのIMGUI文字は実際の描画時にも寸法を計測し、フォントの初期化後の寸法を反映します。折り返し時は配置済みのContent幅を上限に描画幅を再計測し、必要な高さを縮めません。文字・フォントサイズ・折り返しの変更と表示幅の変更を反映し、寸法が変わらない場合はレイアウトを更新しません。Factoryが生成する型付き入力には基盤となる共通クラスを付与します。数値入力とドロップダウンは透明背景の下線型、`Toggle`は標準入力と境界線、操作色、状態遷移を共有する小型チェックボックス型、`ObjectField`は右端の操作領域を分けた選択欄として`FormInput`の内外で共有します。複合入力内の埋め込みフィールドは共通クラスを外し、親コンポーネントが外観を担当します。
- `UiDragAndDrop`は型付きpayloadによるドラッグ開始とMove操作の受け入れを共通化します。
- `PreviewOrbitController`は3D Previewの回転、移動、拡縮とCamera配置を共通化します。Bounds計算と描画内容は利用側が扱います。

これらは機能間で重複していた小さな表示パターンをCoreへ昇格したものです。Domain固有の文言、操作、一覧構造は利用側に残します。

Fluent UIのPNGは`FluentUiIconPostprocessor`でGUI用Textureとして取り込み、非圧縮、ミップマップなし、透過エッジ補正あり、Bilinear、Clamp、最大512pxに固定します。小さな操作アイコンの輪郭を圧縮やミップマップでぼかさないための設定です。`FluentUiIcons.CreateState`とサイズ付き`LoadTexture`は18px以下に16px用SVGから生成した画像を選びます。12px以下のAdd、Subtract、Arrow Clockwise、Arrow Left、Arrow Right、Checkmark、Chevron Down、Chevron Right、Dismiss、Pinには12px用を選びます。Mail Inboxも16px用の画像を使用します。サイズを付けない`LoadTexture`とサイズ接尾辞を明示したファイル名は、そのファイルを読み込みます。アイコンの表示寸法、tooltip、tintは呼び出し側の指定を維持します。File TreeとTarget一覧、Toggleのチェックマーク、Scene Switcherのお気に入りなど、Textureを直接表示する箇所にもサイズ付き読み込みを使用します。

小サイズ用の`*_12.png`と`*_16.png`は12px・16pxで生成し、512px画像を極小表示するときの間引きで細い線や穴が消えることを防ぎます。SVGを512pxで描画してから画素の被覆率を保持するBoxフィルターで縮小します。取り込み後のTextureも元の12px・16pxを維持します。

## `CustomPopup`

`CustomPopup`は`c31a22adf8a49e6fc91addde1febed94a98ea85b`の`old/Editor/Core/UI/Window/BaseWindow.cs`を基準にしたpopup外枠です。同じ`old`配下の`FolderStyleSelectorWindow`などが使用していた操作を移植し、外観は共通トークンを使う単純なパネルへ変更しています。外枠は1pxとし、HeaderとFooterは本文との面の濃度差と境界線で区切ります。現行の`UiTextFactory`、`FluentUiIcons`、`EditorPopupApi`へ接続し、画面内への位置補正とUnity標準Pickerのfocus処理を加えています。popup用Windowは`CustomPopupWindow`を継承し、`ShowAsPopup`で表示して`SetPopup(CustomPopup)`で外枠を設定します。

Footerに並ぶボタンの間隔は共通スタイルで12px確保します。

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
| `CustomPopupWindow.SetPopup(CustomPopup)` | `BaseWindow`形式の外枠、Header、Close操作を設定する |
| `CustomPopupWindow.ConfigureCloseAndSubmitKeys(VisualElement, Action)` | Escapeで閉じ、任意のEnter確定処理を呼び出す |

AssetManagerのタグ選択画面、Target選択画面、コレクション作成画面に加え、Project StyleとHierarchy Styleが共有するItem Style画面で使用します。表示は`ee4v/Debug/Catalog`の`Containers/CustomPopup`、`Domain/ProjectStyle/Project Style Window`、`Domain/HierarchyStyle/Hierarchy Style Window` Storyで確認できます。

## 機能UIのStory

独立したEditorWindowまたは機能固有のVisualElementは、実使用するViewを`IUiStoryProvider`から組み立てます。Story専用の複製UIは作らず、保存や外部Sourceの変更は行いません。独立画面は`Domain/<feature>`に配置します。画面内で再利用する機能固有UIはCoreと同じ責務で分類し、`Domain/<feature>/Inputs`、`Displays`、`Containers`、`Collections`に配置します。

Storyは独立画面または単独で再利用するVisualElementを一単位とします。同じ部品の状態違いは一つのStoryで扱い、異なる部品や別Windowを確認するためのStoryへまとめません。配置部品の責務を示すために実際の子部品を組み込むことは許容します。Factory、状態型、部品内部だけで使う実装には単独のStoryを作りません。

Domain固有UIもCoreと同じ視覚上の役割へ揃えます。入力と操作は`Button`、`InputField`と同じ境界線、面、focus、hoverの状態を使用します。一覧の行は通常時を透過、hover時を`surface-hover`、選択時を`focus`の境界線と`selection-soft`の面にします。Card、Panel、Collectionは`border-default`、`surface-subtle`、`radius-2xl`を基本とします。機能固有の配置、意味を持つ状態色、3D PreviewやThumbnailの描画面はこの規則の対象外です。

CoreコンポーネントのStoryは、`Reference`を除いて`Controls`と`Preview`を持ちます。`Controls`の入力は`FormInput`へ統一し、公開状態を変更できます。Preview内の操作は結果表示または状態変更へ接続します。表示だけの無反応な操作は置きません。

Catalog上の分類とコード上の分割は別に扱います。コードではWindowホストと表示クラスを分離し、`BlendShapePresetView`、`WindowGroupSettingsView`、`ItemStyleEditor`を各Windowと別ファイルで管理します。

| 機能 | Story |
|---|---|
| Core Settings | `Domain/Core/Settings UI` |
| AssetManagerの画面とPopup | `Domain/AssetManager/*` |
| AssetManagerの操作、選択、値入力、編集行 | `Domain/AssetManager/Inputs/*` |
| AssetManagerの通知、画像、詳細値 | `Domain/AssetManager/Displays/*` |
| AssetManagerの詳細領域と設定一覧 | `Domain/AssetManager/Containers/*` |
| AssetManagerのGrid、File TreeとTag一覧 | `Domain/AssetManager/Collections/*` |
| ItemStyleのProject・Hierarchy編集 | `Domain/ProjectStyle/Project Style Window`、`Domain/HierarchyStyle/Hierarchy Style Window` |
| Hidden Objects | `Domain/HierarchyStyle/Hidden Objects`、`Domain/HierarchyStyle/Inputs/*`、`Domain/HierarchyStyle/Containers/*` |
| Scene Switcher | `Domain/SceneSwitcher/SceneSwitcherView`、`Domain/SceneSwitcher/Inputs/SceneSwitcherRow` |
| Project Tabs | `Domain/ProjectTabs/Inputs/Project Tabs` |
| Face Expressionの各画面と設定画面 | `Domain/FaceExpression/*` |
| パーツ・体型編集 | `Domain/AvatarParts/Containers/AvatarPartsEditor` |
| Material編集と埋め込みInspector | `Domain/AvatarMaterials/Containers/AvatarMaterialsEditor`、`Domain/AvatarMaterials/Inputs/EmbeddedMaterialInspector` |
| アバター詳細と性能結果 | `Domain/AvatarInfo/Displays/AvatarInfoView` |
| Face Expressionの編集CellとRow | `Domain/FaceExpression/Inputs/*` |
| Window Groups | `Domain/WindowGroup/Window Groups` |

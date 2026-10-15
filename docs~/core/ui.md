# Core UI

`Ee4v.UI.Editor`は機能横断のUI Toolkitコンポーネントと共通トークンを提供します。

## 共通部品の役割

Catalogは利用場面ではなく、部品が公開する責務で分類します。ユーザーの操作や値を受け取る部品は`Inputs`、情報を描画する部品は`Displays`、呼び出し側の子要素を配置する部品は`Containers`、複数項目を管理する部品は`Collections`に置きます。

使用する機能や使用箇所の数は、汎用部品をDomainへ置く理由にしません。ID、文字、画像、値、callbackだけを受け取る部品は`Ee4v.UI.Editor`へ置き、Item・File・Gestureなどのドメインデータの取得、変換、保存は利用側に残します。

| 責務 | 汎用部品 |
|---|---|
| Inputs | `EnumFormInput`、`StepperSlider`、`ScalarInput`、`TagField`、`Breadcrumb` |
| Displays | `MetricDisplay`、`KeyValueRow`、`ThumbnailStack`、`MessagePanel` |
| Containers | `DetailHeader`、`DetailSection`、`PropertyList` |
| Inputs / 編集行 | `PropertyRow` |
| Collections / 選択カード | `ImageGridView` / `ImageGridCard` |

`FormInput<TInput>`は型付きの`Input`を公開する配置部品です。文字入力は`FormInput<InputField>`へまとめ、値・読み取り専用・フォーカスは`Input`へ設定します。`EnumFormInput`は標準EnumFieldまたは表示名formatter付きPopupFieldを配置し、独自の入力枠を重ねません。`StepperSlider`は整数値と範囲・増減操作を公開します。`ScalarInput`はSlider、FloatField、任意単位、リセットの配置を共有し、値の同期・丸め・保存は利用側が担当します。

`UiButton.SetTone`は通常・Primary・Dangerを切り替えます。Primaryは青い操作面、Dangerは不透明な赤い操作面と白文字を共有します。無効状態との連動には`SetPrimaryActionEnabled`を使用します。情報通知は`MessagePanel`へ統一し、`MessagePanelDensity.Compact`で小さい余白と強調線を使用します。

本文だけの案内・警告・エラーは`UiTextFactory.CreateMessagePanel(message, severity, classNames)`で生成します。戻り値はCompact表示の`MessagePanel`で、重要度には共通の`MessageSeverity`を使用します。本文と重要度の更新は`SetState(new MessagePanelState(string.Empty, message, severity))`、通知の解除は`SetState(null)`で行います。空の本文は非表示になり、長文は利用可能な幅へ折り返します。機能画面と設定の検証通知にはUnity標準のHelpBoxを使用しません。

`SelectionTabVariant.Underline`と`SelectionTab.CreateStrip`は画面内の下線型タブを共有します。Default・Primaryは構成Prefabなどのカード型タブです。各タブはフォーカスでき、Enter・Spaceからクリックと同じ操作を通知します。Project Tabsのピン留め・並べ替え・閉じる操作は機能側へ残します。`UiTextFactory.CreateSwitch`は標準Toggleと同じ値通知・bindingを持つSwitch表示です。

Catalogのナビゲーションではカテゴリ名も選択できます。カテゴリページには分類の説明と、下位カテゴリを含めて属しているStoryの名称・概要を表示します。一覧から各Storyへ移動できます。

Storyページは詳細カードと実部品の描画領域を分離し、内容の高さを維持してスクロールします。必要な最小幅がウィンドウを超える部品は横スクロールで確認できます。親のサイズに追従するプレビューや画面全体の部品は、Story側で高さを持つ外枠へ配置します。

`InputGroup`のStoryは設定画面と同じ通常のpanel背景で表示し、囲み枠の内外と見出しの背景色を揃えます。プレビューカードの標準背景で色の差を付けません。

`MetricDisplay`のStoryは実使用と同じ横並びのコンテナへ配置し、プレビュー枠が部品の高さを含むようにします。`MessagePanel`のStoryは表示状態の変更後に親の`InfoCard.RefreshLayout`を呼び、初期表示と非表示からの再表示を反映します。

Catalogのrootにも実画面と同じ`UiComposition.Prepare`を適用します。共通部品のスタイルはこの入口から読み込み、機能固有の`styleSheetPaths`はそのStoryの描画領域へだけ追加します。他のStoryが登録した機能スタイルには依存しません。

### Reference

- `Color Palette`はUIとIMGUIで共有するUnity Dark・Lightの役割別カラートークンの参照です。独自の表示部品ではありません。
- `Default Scrollbars`は`UiComposition.Prepare`配下へ適用される既定のスクロールバー表示です。スクロール方向の内部Sliderの端の余白をなくし、最小・最大位置のつまみをバーの端へ合わせます。縦横のバーに共通で適用します。独自の`ScrollView`型は提供しません。

### Inputs

型付きの標準Sliderは`UiTextFactory.CreateSlider`、整数のSliderIntは`UiTextFactory.CreateSliderInt`を使用します。生成した入力の`value`・`SetValueWithoutNotify`・`showInputField`で操作値と数値入力を設定し、ラベルを標準UIの`text`へ直接書き込みません。

`InputField`は左クリックで内部のTextFieldへフォーカスします。文字の上だけでなく入力欄の余白もクリック対象です。無効な入力欄はフォーカスしません。

- `PathField`は手入力とOSのフォルダー・ファイル選択ダイアログをまとめたパス入力です。`PathFieldKind`で選択対象を切り替え、ファイルには拡張子filterを指定できます。入力はEnterまたはフォーカスを外した時に確定し、選択をキャンセルした場合は変更を通知しません。選択ダイアログは現在値から環境変数を展開して最寄りの存在する親フォルダーを開き、空欄や無効な値ではDocumentsを起点にします。内容の必要幅を基準に最小240pxからラベルと余白を除いた枠内の利用可能幅まで伸縮します。文字入力と枠線のないフォルダーアイコンを一つの下線内に配置し、フォーカスとhoverは欄全体の下線色で示します。読み書き・同期は利用側が担当します。

- `UiButton`は文字、アイコン、強調度を統一した通常の操作ボタンです。通常ボタンは操作面を薄く塗り、InputFieldと同じ境界線とフォーカス色を使用します。Ghostは通常時の面と境界線を表示しません。Toolbarなど用途固有の小型寸法は利用側USSで指定します。サムネイルなどの複合内容は`Content`へ配置し、操作面そのものは`UiButton`に保ちます。`SetPrimaryActionEnabled`は保存などの主操作の有効状態・青い操作面・ラベル色をまとめて切り替え、統合と単独のAvatar編集Windowが共有します。
- `InputField` Storyはテキスト、トグル、Object選択、ドロップダウンを含む単一値入力の標準入口です。文字列には`InputField`コンポーネントを使用し、その他の値には`UiTextFactory`が生成する型付き入力を使用します。単行テキストとドロップダウンは透明背景の下線型、複数行テキストは全周の境界と内側余白を持つテキストエリアとして表示します。トグルは標準入力と同じ境界線と操作色を使う小型チェックボックスとし、選択時は青い面と白いチェックマークで状態を示します。フォーカスの有無では外観を変えません。
- `SearchField`は薄い入力面と全周枠へ検索入力、消去、任意の先頭操作をまとめます。検索アイコンと角丸の全周枠で用途を示し、境界線とフォーカス色はInputFieldと共有します。先頭操作が有効な場合だけhover反応を表示します。
- `ListField<T>`は任意型の値を1項目1行で表示する一覧です。利用側が値から項目要素を生成し、編集項目では変更通知を返します。追加値の生成処理を指定した場合だけ、通常項目と同じ寸法の追加行と各行の削除ボタンを表示します。区切り文字の解釈や保存形式への変換は行いません。
- `FormInput`は任意ラベル、必須の単一入力コンポーネント、任意ボタンを横一列に配置するフォーム入力です。囲み枠は持ちません。文字入力には`InputField`を使用し、`Toggle`と`ObjectField`には`UiTextFactory`の標準入力スタイルを使用します。`ListField<T>`のように複数の入力要素を持つ場合も一つの入力コンポーネントとして渡します。値の保持と検証は入力コンポーネントが担当します。
- `InputGroup`はfieldset風の外枠へ開閉可能な必須見出しを重ね、1個以上の`FormInput`をまとめる入力グループです。見出しから内容を開閉し、閉状態は高さとシェブロンで示します。枠の内外と見出し背景は通常色へ揃え、シェブロンと文字の領域だけをホバー対象として外枠の色を変えます。枠内の内容上や子孫入力のフォーカスでは外観を変えません。展開状態の変更を通知し、各`FormInput`のラベルは省略できます。
- `NavigationItem`は`ItemRow`へクリックと選択状態を加えた操作項目です。
- `SelectionTab`は名称と選択状態を持つタブです。通常と強調用の`SelectionTabVariant`を持ち、クリック時の操作は利用側が接続します。
- `ExclusiveButtonGroup<TValue>`は値とラベルの組を受け取り、選択状態を一つだけ保持するボタン群です。値の型、表示順、ラベル、利用可否は呼び出し側が決めます。値は重複不可で、選択値は候補に含まれる必要があります。クリックでは選択状態を更新してcallbackを通知し、同じ値の再選択も通知します。`SetSelected`と`Refresh`は通知せず、既存のボタンと文字要素を保持します。`Refresh`は利用可否も更新します。部位の定義や翻訳には依存しません。
- `ObjectSelector<TObject>`は呼び出し側が渡したUnity Object候補の選択とドラッグの受け入れを行います。同じファイル内の別sub-assetを候補として受け入れず、Object単位で検証します。constructorの任意の`showPicker`には、anchor・現在値・選択確定callbackを受け取る機能固有Pickerを指定できます。callbackで受け取る値にも同じ候補の検証を適用し、ドラッグの受け入れと値変更通知を共有します。`placeholder`で未選択時の表示文字を指定できます。未指定時は共通のPickerと中立な文言を使用します。`ShowPicker`は同じ候補を検索付きの一覧・Gridから選択する入口で、候補取得や保存は行いません。共通Pickerの`ObjectPickerWindow`は内部実装です。
- `AssetPicker.Show(anchor, type, reference, usageContext)`はMaterial・Texture・AnimationClipなどを探して既存の入力欄へドラッグするProject assetブラウザーPopupの入口です。`CreateButton(type, getReference, getUsageContext)`は同じPopupの起動ボタンを返し、押した時点の参照元・contextを取得します。`GetReferenceType(type)`はMaterial・Textureに対してMaterial、AnimationClipと種類「すべて」に対してnull、それ以外に対して候補の型を返します。起動時の参照元はその型の保存済みassetだけを受け入れます。統合ee4vでは編集中画面の左のカテゴリ一覧の最下部に「AssetManagerを開く」と縦に並べて配置します。Popupで型を変更でき、Material・Textureの参照元にはMaterialの標準ObjectFieldを使用します。MaterialとTextureの切り替えでは同じ参照Materialを保持します。AnimationClipでは参照欄を表示せず、すべて・表情の分類ドロップダウンを表示します。分類は候補metadataを使い、所属カード・候補一覧・件数のすべてへ適用します。分類選択肢はenumと表示名で定義し、追加分類を拡張できます。型と参照元・分類のラベル幅は文字に合わせ、入力欄との間隔は共通の余白に揃えます。名前・所属名・path・GUIDで検索できます。Materialの順位は同じUV0配置でのMaterial使用、同一主Texture参照、参照元自身の同一フォルダー、同一Item所属の順です。Textureの順位は同じUV0のMaterialが参照するTexture、参照元の主Texture、参照元の他のTexture property参照、同じフォルダー、同じItemの順です。Textureのフォルダー判定には参照元Materialと参照Textureの親フォルダーを使います。複数に該当する場合は最優先の理由を使い、いずれかに一致した候補を関連アセットに含めます。AnimationClipは関連順位と関連アセットカードを持ちません。関連アセットの一覧では関連順位を先に適用し、同順位では所属・フォルダー・名前で並べます。通常の所属内一覧と分類なしの一覧は同一フォルダーごとの見出しでまとめ、フォルダーpathと各グループ内の候補名の順に並べます。未登録の所属を推測で補いません。候補はvirtualized ListViewへ表示し、ファイルごとの候補読み込みとPrefab/ModelのUV調査をEditor tickへ分割します。一つの大きなファイルの解析中は処理時間がtickの目標を超える場合があります。Catalogの`Inputs/AssetPicker`へ登録します。
- `TagPill`は任意の選択操作と削除操作を持つタグ入力です。pill形と薄い面を保ち、境界線と選択操作にはInputFieldと同じフォーカス色を使用します。

種類の選択肢は「すべて」「Material」「Texture」「AnimationClip」です。「すべて」は`UnityEngine.Object`で指定し、`GetReferenceType`はnullを返します。参照元と表情分類は表示せず、Item内の取り込み済みアセットを種類横断で一覧にします。候補には型名も表示します。種類変更は同じPopup内で候補取得を切り替え、ウィンドウと主要UI要素を作り直しません。位置・サイズ・検索文字・開いているItemを保持し、切り替え後の候補がないItemは取得完了後にカード一覧へ戻します。取得中の旧型の列挙は破棄し、候補キャッシュと取得済みサムネイルを再利用します。Material・Texture間は参照Materialを保持し、関連アセットは参照元がなくなる種類へ切り替えた際に解除します。右ペインは選択アセットの型に応じてMaterial・Texture・AnimationClipなどの表示を切り替えます。「すべて」で選択したMaterialにも大きなMaterialプレビューと主Texture切り替えを使用し、Material下の情報欄は表示しません。

`AssetPicker.Show`の末尾の`animationFilter`引数で`AssetPickerAnimationFilter.All`または`Facial`を初期設定できます。省略時はAllです。`CreateButton`は型を固定するoverloadと`Func<Type>`から取得するoverloadを持ち、`getAnimationFilter`も参照元・contextと同じくクリック時に評価します。起動元のタブや詳細画面が変わってもボタンを再生成する必要はありません。AnimationClip以外の種類では分類をAllへ戻し、分類によってMaterialやTextureの候補を除外しません。

`PathField`の幅は入力中も文字の描画幅を計測して更新します。文字幅・入力余白・選択アイコンの幅を足した必要幅を最小幅と利用可能幅で制限し、入力内容の変更、パス選択、Window幅の変更に追従します。

`AssetPicker`のPopupはPrefab追加画面と同じ、所属アセットのカード一覧から候補の詳細画面へ進む構成です。初期1040×720pxとし、詳細画面では先頭に戻るボタン・所属名・フォルダー絞り込みの行を置き、その下に型・参照元または分類、検索欄を配置します。候補は左の幅300pxの一覧と右の大きな`PreviewPane`で確認し、下にドロップ操作の案内と閉じるボタンを配置します。幅720px未満では候補一覧を上、プレビューを下へ並べます。参照元があれば関連候補グループを開き、関連候補がなければカード一覧を表示します。カード一覧への戻り操作は検索文字を復元します。先頭に「関連アセット」グループを置き、通常の所属はOrigin IDでまとめて所属名・型別の件数・所属アセットのサムネイルを表示します。未登録の候補は末尾の「分類なし」カードへまとめます。カード幅と正方形に近い画像領域の高さはPrefab追加画面と同じ列数・余白の計算で決めます。

上部の戻る行・種類と参照元または分類の行・検索欄は共通のtoolbarへまとめます。コンテンツ外周は16px、toolbarの行間とメインコンテンツまでの間隔は12pxとし、戻るボタン・検索欄の高さと種類行の最小高さを28pxに揃えます。種類・参照元・分類・フォルダーは共通の標準Fieldスタイルを使用し、入力部分の高さ・枠・背景・ホバーやフォーカスの表示を独自に上書きしません。標準Fieldが持つ外側の余白はこのtoolbar内の行に限って除き、左右端を検索欄とメインコンテンツへ揃えます。戻るボタンは枠と背景のあるSolidボタン、Item名は13pxの太字見出しとし、両者の間隔を16pxにします。戻る行がないカード一覧ではその行の余白も残しません。

AnimationClipの右ペインは画像領域を表示せず、表情・その他の分類に共通して再生時間（秒）・Loopの有効状態・キーフレーム数と、扱うプロパティを表示します。LoopはClipの`isLooping`、キーフレーム数は全数値カーブのキー数と全オブジェクト参照カーブのキー数の合計です。同じ時刻の別プロパティのキーもそれぞれ数え、イベントは含めません。プロパティ一覧は対象の階層pathとコンポーネント型ごとにまとめて名前順に並べ、ルートの空pathも表示します。カーブがないClipには空状態を表示します。再生情報と選択中プロパティのカーブは上に固定し、プロパティ一覧をスクロールできます。候補一覧の調査更新では同じClipの表示要素を再生成せず、Clipの選択またはassetキャッシュの無効化時に更新します。種類「すべて」で選択したAnimationClipにも同じ表示を使用します。

プロパティ行には固定値、またはClipの開始時刻から終了時刻への値の変化、数値カーブのキー値の最小・最大、個別のキー数を表示します。キー値の範囲は補間によるovershootを含みません。`m_IsActive`・`m_Enabled`の0/1は有効・無効を併記し、オブジェクト参照値には名前またはNoneを表示します。単一キーと、区間内に変化がない数値・オブジェクト参照カーブを固定状態として扱います。同じ値の数値キーでも区間の接線が変化を作る場合は固定状態にしません。

プロパティを選ぶと、数値カーブは`AnimationCurve.Evaluate`をサンプリングした補間曲線、オブジェクト参照カーブは値が切り替わる階段状のタイムラインを表示し、キー位置と現在時刻のカーソルを重ねます。時間Sliderまたはグラフのクリックで時刻とその時点の値を確認でき、キー近くのクリックはそのキーの時刻へ合わせます。長さ0のClipは固定状態と0秒の値を表示し、Sliderを無効にします。グラフ・Sliderは読み取り専用で、Clip・キー・シーンを変更しません。実装は`AssetPickerWindow.Animation.cs`へまとめます。

関連アセットの候補一覧にはグループ見出しを挿入せず、関連順位で並べた候補を連続して表示し、候補行へ関連理由とフォルダーを表示します。それ以外の候補一覧は同一フォルダーごとに、パスと表示候補件数を記した見出しを置きます。見出しは折り返し可能なテキストと下線で表示し、各グループの候補を同じ字下げで連続して配置します。ホバー・選択の背景は候補部分だけに付け、見出しの背景は変えません。選択中の候補はホバー中も選択色を維持します。見出しのない行では空の見出し要素を非表示にし、余白を残しません。候補行はMaterialなど対象そのもののサムネイルと名前を表示し、pathはtooltipへまとめます。検索時は一致候補のあるグループだけを表示します。右上のフォルダー絞り込みは実際の候補フォルダーから生成し、候補の関連度判定とは独立です。

候補のクリックと一覧の選択移動ではプレビューだけを切り替えます。候補行またはプレビューの「Projectで表示」ボタンからドラッグを開始し、`DragAndDrop.objectReferences`に実際のassetを渡します。FBX内のClipなどのsub assetもその参照を保持し、ファイルpathへ置き換えません。参照先への代入、型検証、Undoはドロップ先の入力欄が処理します。「Projectで表示」のクリックはassetをPingします。Popupはfocus離脱やドロップ完了で閉じず、Esc・Headerの×・閉じるボタン・新しいブラウザーの起動・assembly reloadで閉じます。Materialの初期プレビューは参照元、Textureでは参照Materialの主Textureとし、それがなければ詳細画面の最初の候補です。AnimationClipは詳細画面の最初の候補を表示します。検索や調査による一覧更新ではプレビュー中の参照を保持し、別の所属やAnimationClipの分類を選んだときは表示範囲の候補を選びます。関連候補は高さ76pxの固定高さ仮想化、フォルダーごとのグループ表示は高さ76pxの候補に各グループの先頭だけ見出しを添え、可変高さ仮想化で表示します。見出しは選択・ドラッグの対象に含めません。解析中もitemsSource・行・Imageを作り直さず既存要素を更新します。取得済みのAssetPreviewはPopup内で保持し、同じ参照の再bindでは型アイコンへ戻しません。行が別の候補へ再利用されたときは、その参照に対応する画像と取得要求へ切り替えます。カード一覧も所属IDごとに要素と画像を保持して件数・順序を更新します。

画像は縦横比を維持して領域へ収め、Texture2Dは元画像を表示します。Materialの詳細は`MaterialEditor.OnInteractivePreviewGUI`を表示領域の実サイズで描画し、ドラッグによる回転と主Texture表示の切り替えに対応します。Materialのプレビュー下には情報欄を設けず、画像領域を広く使います。所属、path、GUID・local IDとMesh使用先は候補のtooltipで確認します。その他の型ではプレビュー下に所属、path、関連理由と型別の情報を表示し、長い情報は高さを制限したScrollViewへ収めます。Materialの候補一覧は主TextureではなくUnityのMaterial AssetPreviewを使用します。

「関連アセット」は検索アイコン、「分類なし」はフォルダーアイコンを固定表示し、候補のAssetPreviewやTextureへ切り替えません。アイコンはFluentUiSystemIconsを使用します。通常の所属カードは`AssetPickerOrigin.GetThumbnailAsync`から取得した画像を使い、画像未登録は参照そのもののAssetPreviewまたは型アイコンで補います。所属画像は画面近傍のカードだけ最大4件並列で取得し、デコードしたTextureはPopup内で共有して閉じると破棄します。候補サムネイルの再試行もPopup終了時に停止します。MaterialEditorも選択変更・Popup終了時に破棄します。画像が取得できないアセットは案内を表示します。

`AssetPicker`の調査中は`StatusOverlay`の回転スピナーと読み込みメッセージをカード一覧または候補一覧の中央へ重ね、完了・失敗で隠します。一覧画面の切り替えでは同じスピナーを表示中の一覧paneへ移します。オーバーレイはpointerを受けず、取得済み候補の選択・ドラッグを妨げません。調査はPopup生成後のEditor tickから始めます。検索・フォルダー分類・関連度判定は候補metadataを使用し、実assetは表示中の候補のサムネイル・プレビュー・ドラッグ時に読み込みます。所属カードのfallbackも、画面付近で所属画像が取得できない場合にだけ読み込みます。Coreの`AssetPickerCache`はsession内とLibraryの永続ファイルへ候補情報・Project UV解析を保持します。再表示ではsessionの結果を使用し、Editor再起動後は保存済みファイルと依存hashを検証して、変更されたファイルだけ再取得・解析します。現在の編集Rootは毎回別contextで最初に確認し、候補取得・ProjectのUV調査が進むにつれて関連候補を更新します。contextのUV照合結果はProject indexの更新に追従します。開いているPopupでProjectキャッシュの変更を検出した場合は候補取得を再開します。キャッシュの寿命と無効化条件は[EditorIntegration](./editor-integration.md#assetpickercatalogとmaterialuvusageindex)を参照します。

### Displays

- `Badge`は件数や短い分類値を中立表示し、任意の`UiStatusTone`で処理状態も表示します。
- `EmptyState`は薄い枠面で空領域を示し、対象がない理由と次の操作を中央へ表示します。操作要素自体は利用側が`Actions`へ追加します。
- `MessagePanel`はエラー・警告・案内を表示する共通パネルです。`MessagePanelState`で見出し、説明、`MessageSeverity`と任意の対象一覧を渡します。共通の文字・余白・角丸・意味別の色と小型Fluentアイコンを使用し、長文は折り返し、多数の対象は高さ144px以内の一覧で縦スクロールします。`Actions`には通知本文の末尾へ、`DetailsActions`には対象一覧の右側へボタンなどを配置でき、stateの更新でも保持します。右側の操作欄は縦中央に揃え、対象一覧が残りの幅を使用します。対象一覧がある場合は説明との区切り線を操作欄まで含めた全幅へ表示し、対象一覧がない場合は区切り線とその余白を表示しません。利用側が操作欄の表示条件と余白を管理します。説明だけの通知ではアイコンと本文を縦中央へ揃え、本文はアイコンと同じ最小高さを確保します。空のstateでは非表示になります。検証や翻訳は利用側が担当し、Catalogの`Displays`へ登録します。
- `ConfirmationOverlay`は指定host全体へ確認カードを重ね、既存の子要素の操作を無効にします。hostはEditorWindowのrootVisualElementまたはその配下とし、Editorのフォントとテーマを継承します。FaceExpressionの未保存変更とExpressionMenuのタイプ切り替えで共有します。状態は`SetState`、操作ボタンは`Notification.Actions`を通じて利用側が設定します。カードは最大幅480pxの不透明な中立色のraised surfaceとし、余白20pxの本文に通常色の見出しを配置します。確認カードには重要度にかかわらずアイコンを表示せず、見出しは本文の横幅を使用して最小高さを確保します。本文の下へ区切り線付きの操作欄を全幅で配置し、`AddDiscardAction`の赤い破棄ボタンを左端、通常の取消とprimaryの保存を右端へ並べます。`Close`・Esc・detachで各要素の元の有効状態とフォーカスを復元し、`Closed`を通知します。データの保存・破棄・画面遷移は利用側が成功を確認してから行います。
- `StatusOverlay`はspinnerと状態メッセージを表示する小型パネルです。バックグラウンド処理ではウィンドウ右下へ重ね、統合ee4vの変更破棄では再読み込み完了まで、AvatarInfoの手動測定では測定完了まで画面中央へ表示します。AssetPickerでは候補・UV使用先の調査中にPopup内へ表示します。配置と操作の抑止は使用側が制御します。
- `Icon`はFluent UI System Icons、実使用するUnity固有の組み込みアイコン、任意Textureの表示を共通化します。通常の操作アイコンと`InputGroup`の開閉chevronにはFluent UI System Iconsを使用します。組み込みアイコンはUnityのFolder、Scene、GameObject、Model FileとHierarchyの非表示操作に限定します。Fluent画像が読み込めない場合は組み込みアイコンへ代替せず非表示にします。
- `ObjectThumbnail`はUnityのAssetPreviewを表示し、読み込み中はMiniThumbnailを表示します。取得の再試行は表示中だけ行います。
- `GameObjectPreview`はCoreの[`AvatarPreviewRenderer`](./avatar-preview.md)へ描画を委ねます。Scene上の対象は複製せず、Edit ModeではNDMF Previewの加工後Renderer、Play Modeでは実際の対象を描画します。Prefabアセットは隔離された表示用コピーを使用します。対象、部位・Material選択、Cameraと破棄を担当し、`SetFlexibleLayout`で親のサイズへ追従し、`SetViewToggleVisible`で視点切り替えとポーズの操作を非表示にでき、Cameraのフレーミングは変えません。`FocusBodyPart`で部位ポーズを適用し、右下のFluent Accessibilityアイコンまたは`SetTPose`で基準姿勢と切り替えます。ポーズ操作はHumanoidを持つEdit Modeの対象だけに表示します。`SetFullBodyFraming`で全体を収める標準表示と編集向けの拡大表示を切り替えます。既定では全体を収めます。保存、DB、Play Modeの切り替えは行いません。表示中は約30fpsで再描画し、NDMFの更新、時間依存Shaderと実行中の姿勢を表示します。
- `CachedImage`はデコード済みTextureを複数の画像表示で共有します。

`GameObjectPreview.FeatureOverlay`はプレビュー画像上の機能固有操作の配置先です。Expression Menuは正面・背面切り替えを隠した左上へ戻るボタン、下部中央へ枠付きの動作操作を配置します。`MatchFullBodyFraming`は参照プレビューの全身boundsと拡大表示設定を取得し、初期表示・リセット・サイズ変更時のカメラ計算に使用します。参照のCamera操作値はコピーせず、通常の全身フレーミングを適用します。`SetAnimationSampler`はAvatarEvaluationのMA対応サンプリングを登録し、Scene参照と隔離コピーの両方で使用できます。UIの再構築・再接続でもcallbackを引き継ぎ、描画資源は表示中だけ保持します。null指定は再生用コピーを解放し、再描画を要求します。

`GameObjectPreview.SetScope`は描画対象とboundsを同期的に切り替え、PreviewとNDMF sessionを再生成しません。選択した構成Prefab rootのEditorOnly除外規則も同時に再計算し、scope変更後の最初の描画から選択範囲だけを表示します。

`FocusBodyPart`は通常、選択部位へCameraをアニメーションで合わせます。`preserveView: true`では部位選択と視点ボタンだけを更新し、Cameraの位置・角度・距離を維持して進行中のアニメーションを止めます。構成Prefabの選択解除・切り替え時に使用します。部位フォーカス、表示方向の切り替え、表示リセットは描画scopeにかかわらずアバター全体のboneとboundsを基準にし、同じ部位・表示方向では注視点・角度・距離を一致させます。全身の余白調整も全体表示と個別Prefab表示で共通です。Cameraのboundsと顔のMesh判定には全体表示時と同じ非表示・EditorOnly規則を適用します。描画対象と部位ボタンの有効／無効判定は選択中のscopeに従います。

`GameObjectPreview`の選択輪郭は、選択中のRenderer・Material slotの加工後submeshをGeometry maskへ投影して生成します。maskは深度判定を行わず、手前のオブジェクトに遮られた部分や背面も含め、輪郭をPreview画像の上へ重ねます。透過・cutoutのテクスチャ形状ではなくMeshの形状を使用します。別Materialのsubmeshや影はmaskに含めません。部位選択では対象Renderer内の表示中slotをすべて含め、表示切替で非表示にした部位・Materialは輪郭にも含めません。Geometry maskは更新ごとに解放し、共有Meshを複製・変更しません。クリック選択では元Materialの描画差分を使用して見えている対象を判定します。

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

`PreviewPane` Storyは実部品へ内容と操作ボタンを配置し、見出しの更新を確認します。共通の3D描画は`Displays/GameObjectPreview`、機能固有の描画内容はDomain側のStoryで確認します。

`ScenePreviewViewport` Storyは共有グリッド、右上の背景切り替えと表示リセット、左上の機能固有オーバーレイを一つのPreview内で確認できる構成にします。AssetManagerのAppearance PreviewとPlay Modeの操作確認は`GameObjectPreview`を介してこの部品を使用し、表情エディターは自身の顔向けCamera制御と組み合わせます。描画画像は透過で重ね、グリッド背景と明暗切り替えを表示します。`PreviewGridBackground`は同じグリッドの描画とTextureの破棄を担当し、表情サムネイルでも使用します。

### Collections

- `SearchableTreeView`は検索欄と階層一覧をまとめ、絞り込みと展開状態の維持を行います。各行の表示は利用側が構成します。縦スクロールバーの表示方式はconstructorの`verticalScrollerVisibility`で指定でき、既定は`Hidden`です。ボーンの詳細ツリーは`AlwaysVisible`を指定し、検索や枝の開閉でもスクロールバーの幅を確保します。

### Catalog外の基盤

- `UiLocalization`は共通部品の文字をUI scopeから取得する内部基盤です。部品は利用機能の翻訳catalogへ依存しません。
- `UiComposition.Prepare`は共通USSと機能固有USSを一つの入口で登録します。
- `UiColorPalette`はUI ToolkitとIMGUIが共有するテーマ別標準色の契約です。`UiColorPalettes`がUnity Dark・Lightのバリエーションを所有し、`UiColorTokens.Current`と`UiComposition.Prepare`が現在のUnity Editorテーマへ接続します。

`warning-surface-soft-opaque`と`error-surface-soft-opaque`は、テーマ別のpanel色に薄い警告・エラー色を合成した不透明なUSS tokenです。背後の画面を透かさずに意味別の色を表示する通知で使用します。

`danger-action`、`danger-action-hover`、`danger-action-pressed`は、破棄などの操作を示す不透明な赤いボタン面のUSS tokenです。Dark・Lightで同じ赤を使用し、ラベルは`UiColorTokens.TextOnState`で表示します。

`overlay-surface`はPreview画像に重ねる操作ボタンの半透明背景です。Dark・Lightで背景色を切り替え、ホバー・押下時は`overlay-hover`を使用します。

- `UiTextFactory`は文字を描画するUI要素の生成と文字更新を統一します。通常の操作、文字入力、検索入力にはそれぞれ`UiButton`、`InputField`、`SearchField`を使用し、低レベルの`TextField`生成は共有入力コンポーネント内部に限定します。FactoryのIMGUI文字は実際の描画時にも寸法を計測し、フォントの初期化後の寸法を反映します。折り返し時は配置済みのContent幅を上限に描画幅を再計測し、必要な高さを縮めません。文字・フォントサイズ・折り返しの変更と表示幅の変更を反映し、寸法が変わらない場合はレイアウトを更新しません。Factoryが生成する型付き入力には基盤となる共通クラスを付与します。数値入力とドロップダウンは透明背景の下線型、`Toggle`は標準入力と境界線、操作色、状態遷移を共有する小型チェックボックス型、`ObjectField`は右端の操作領域を分けた選択欄として`FormInput`の内外で共有します。複合入力内の埋め込みフィールドは共通クラスを外し、親コンポーネントが外観を担当します。
- `UiDragAndDrop`は型付きpayloadによるドラッグ開始とMove操作の受け入れを共通化します。
- `PreviewOrbitController`は3D Previewの回転、移動、拡縮とCamera配置を共通化します。Bounds計算と描画内容は利用側が扱います。

これらは機能間で重複していた小さな表示パターンをCoreへ昇格したものです。Domain固有の文言、操作、一覧構造は利用側に残します。

Fluent UIのPNGは`FluentUiIconPostprocessor`でGUI用Textureとして取り込み、非圧縮、ミップマップなし、透過エッジ補正あり、Bilinear、Clamp、最大512pxに固定します。小さな操作アイコンの輪郭を圧縮やミップマップでぼかさないための設定です。`FluentUiIcons.CreateState`とサイズ付き`LoadTexture`は18px以下に16px用SVGから生成した画像を選びます。12px以下のAdd、Subtract、Arrow Clockwise、Arrow Left、Arrow Right、Checkmark、Chevron Down、Chevron Right、Dismiss、Pinには12px用を選びます。Mail Inboxも16px用の画像を使用します。サイズを付けない`LoadTexture`とサイズ接尾辞を明示したファイル名は、そのファイルを読み込みます。アイコンの表示寸法、tooltip、tintは呼び出し側の指定を維持します。File TreeとTarget一覧、Toggleのチェックマーク、Scene Switcherのお気に入りなど、Textureを直接表示する箇所にもサイズ付き読み込みを使用します。

小サイズ用の`*_12.png`と`*_16.png`は12px・16pxで生成し、512px画像を極小表示するときの間引きで細い線や穴が消えることを防ぎます。SVGを512pxで描画してから画素の被覆率を保持するBoxフィルターで縮小します。取り込み後のTextureも元の12px・16pxを維持します。

`UiTextFactory.CreateInspector(Editor)`はUnity標準の`InspectorElement`を生成する入口です。component固有のUI Toolkit・IMGUI Inspector内の文字とプロパティ描画はUnityまたはcomponentのEditorが担当します。利用側はEditorの生成・破棄、bindingの解放、変更通知と編集可否を管理します。

## `CustomPopup`

`CustomPopup`は共通トークンを使うpopup外枠です。外枠は1pxとし、HeaderとFooterは本文との面の濃度差と境界線で区切ります。タイトルは`UiTextFactory`と`UiClassNames.SectionTitle`を使用して、共通の太字見出しとして描画します。`FluentUiIcons`と`EditorPopupApi`を使用し、画面内への位置補正とUnity標準Pickerのfocus処理を行います。popup用Windowは`CustomPopupWindow`を継承し、`ShowAsPopup`で表示して`SetPopup(CustomPopup)`で外枠を設定します。

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

Storyは独立画面または単独で再利用するVisualElementを一単位とします。同じ部品の状態違いは一つのStoryで扱い、異なる部品や別Windowを確認するためのStoryへまとめません。配置部品の責務を示すために実際の子部品を組み込むことは許容します。Factory、状態型、部品内部だけで使う実装には単独のStoryを作りません。登録時にはカテゴリ、IDの重複、実在する使用ファイルを検証し、Story自身を使用箇所として受け入れません。

Domain固有UIもCoreと同じ視覚上の役割へ揃えます。入力と操作は`Button`、`InputField`と同じ境界線、面、focus、hoverの状態を使用します。一覧の行は通常時を透過、hover時を`surface-hover`、選択時を`focus`の境界線と`selection-soft`の面にします。Card、Panel、Collectionは`border-default`、`surface-subtle`、`radius-2xl`を基本とします。機能固有の配置、意味を持つ状態色、3D PreviewやThumbnailの描画面はこの規則の対象外です。

CoreコンポーネントのStoryは、`Reference`を除いて`Controls`と`Preview`を持ちます。`Controls`の入力は`FormInput`へ統一し、公開状態を変更できます。Preview内の操作は結果表示または状態変更へ接続します。表示だけの無反応な操作は置きません。

Catalog上の分類とコード上の分割は別に扱います。コードではWindowホストと表示クラスを分離し、`BlendShapePresetView`、`WindowGroupSettingsView`、`ItemStyleEditor`を各Windowと別ファイルで管理します。

| 機能 | Story |
|---|---|
| Core Settings | `Domain/Core/Settings UI` |
| AssetManagerの画面とPopup | `Domain/AssetManager/*` |
| AssetManagerの操作、選択、値入力、編集行 | `Domain/AssetManager/Inputs/*` |
| AssetManagerの統合編集、Prefab選択、検索条件、Target Popup | `Domain/AssetManager/*` |
| AssetManagerのFile TreeとTag一覧 | `Domain/AssetManager/Collections/*` |
| ItemStyleのProject・Hierarchy編集 | `Domain/ProjectStyle/Project Style Window`、`Domain/HierarchyStyle/Hierarchy Style Window` |
| Hidden Objects | `Domain/HierarchyStyle/Hidden Objects`、`Domain/HierarchyStyle/Inputs/*`、`Domain/HierarchyStyle/Containers/*` |
| Scene Switcher | `Domain/SceneSwitcher/SceneSwitcherView`、`Domain/SceneSwitcher/Inputs/SceneSwitcherRow` |
| Project Tabs | `Domain/ProjectTabs/Inputs/Project Tabs` |
| Face Expressionの各画面と設定画面 | `Domain/FaceExpression/*` |
| パーツ・体型編集 | `Domain/AvatarParts/Containers/AvatarPartsEditor` |
| Material編集と埋め込みInspector | `Domain/AvatarMaterials/Containers/AvatarMaterialsEditor`、`Domain/AvatarMaterials/Inputs/EmbeddedMaterialInspector` |
| アバター詳細の表示・名前とIDの編集 | `Domain/AvatarInfo/AvatarInfoView` |
| Expression Menuの画面・項目設定・動作Preview | `Domain/ExpressionMenu/*` |
| Face Expressionの編集CellとRow | `Domain/FaceExpression/Inputs/*` |
| Window Groups | `Domain/WindowGroup/Window Groups` |

編集・SimulationのStoryは専用Preview Sceneのダミーアバター、一時的な画像・Clip・Mesh、メモリ上のManager・設定・ライティングプリセットを使います。Project assetを検索する部品の通常動作とは区別します。表情変換の`previewOnly`は変換結果をcallbackへ渡した後に破棄し、保存ダイアログとAsset作成を行いません。統合編集画面のプレビューは実Managerの取得と書き込み操作を無効にします。SimulationのプレビューはPlay Modeへ入らず、実GestureManagerやプリセット保存先へ接続しません。

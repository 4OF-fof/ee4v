# Core UI

`Ee4v.UI.Editor`は機能横断のUI Toolkitコンポーネントと共通トークンを提供します。

## 入力コンポーネント

- `UiButton`は文字、`IconState`、標準状態表現をまとめます。機能固有の色や選択状態は利用側のクラスで追加します。
- `InputField`は1行入力と複数行入力に対応し、読み取り専用、プレースホルダー、欄内スクロールを共通化します。
- `SearchField`は検索、消去、プレースホルダーを提供します。`SearchFieldState.SearchActionEnabled`を有効にすると、先頭アイコンから`SearchActionRequested`を通知します。ポップアップなどの配置には`SearchActionAnchor`を使用できます。
- `Icon`はUnity組み込みアイコンと任意Textureの表示を共通化します。`IconState.FromTexture`では必要に応じてtint色を指定できます。

## `CustomPopup`

`CustomPopup`はドロップダウン型`EditorWindow`の外枠を構成します。外枠は`ee4v-popup-surface`の1px borderを使用し、ヘッダー、本文、任意フッターの背景と区切り線を共通化します。

| API | 動作 |
|---|---|
| `HeaderActions` | ヘッダー右側へ閉じるボタンなどを追加する |
| `Content` | 機能固有のフォームや一覧を追加する |
| `Footer` | キャンセル、確定などの操作を追加する |
| `SetTitle(string)` | ヘッダーのタイトルを更新する |
| `SetFooterVisible(bool)` | フッターの表示を切り替える |
| `ShowAsDropDown(EditorWindow, VisualElement, Vector2)` | anchor直下へ固定サイズで表示する |

AssetManagerのタグ選択画面、Target選択画面、コレクション作成画面が使用します。表示は`ee4v/Debug/Catalog`の`Overlays/CustomPopup` Storyで確認できます。

# Settings

setting 定義は原則 `Editor/<Feature>/<Feature>Definitions.cs` に置きます。`SettingDefinition<T>` は source file の namespace から localization scope を解決するため、別 namespace の util file へ逃がさないでください。

## 定義項目

| field | 内容 |
|---|---|
| `key` | 例: `injector.projectToolbar.enabled` |
| `scope` | `User` または `Project` |
| `sectionKey` | settings 画面のグループ見出し |
| `displayNameKey` | 項目名 |
| `descriptionKey` | tooltip |
| `defaultValue` | 既定値 |
| `order` | section 内並び順 |
| `validator` | 入力制約 |
| `customDrawer` | 標準 field で足りない場合に UI Toolkit の `VisualElement` を生成する callback |
| `keywords` | settings 検索補助 |

## 登録と参照

- `RegisterAll()` には `_registered` guard を入れる
- bootstrap から `RegisterAll()` を呼ぶ
- 値参照は `SettingApi.Get(...)`
- 更新は `SettingApi.Set(...)`
- background task から値を読む場合は、task を開始する前に main thread で `SettingApi.Preload(scope)` を呼ぶ

同じ key に異なる `SettingDefinition` instance を登録した場合は例外になります。設定 cache と登録表は同期化されていますが、`EditorPrefs` や project file の初回読み込み自体は Unity main thread で行う前提です。

## 保存先

| scope | 保存先 | 用途 |
|---|---|---|
| `SettingScope.User` | `EditorPrefs` | ユーザー単位の設定 |
| `SettingScope.Project` | `ProjectSettings/ee4v.settings.json` | プロジェクト固有の設定 |

保存 JSON が破損している場合は起動を止めず、該当 scope を空の設定として読み込み、各定義の default 値へ戻します。現在は破損ファイルの自動修復・退避は行いません。

## 設定画面

`Preferences/4OF/ee4v` と `Project/4OF/ee4v` は `RegisteredSettingsProviders` が提供します。通常の feature 実装では provider を追加せず、`SettingApi.Register(...)` した定義を既存 provider に載せます。

grouping は `localizationScope + sectionKey` 単位です。section を増やす場合は localization key も揃えます。

設定画面は `SettingsProvider.activateHandler` から UI Toolkit で構築します。`customDrawer` は `VisualElement` を返し、値の変更を `SettingDrawerContext<T>.NotifyValueChanged(...)` で通知してください。IMGUI drawer は使用しません。

## バリデーション

- invalid 値は `validator` で弾く
- エラーメッセージは `SettingValidationResult.Error(...)`
- validation 文言も `I18N.Get(...)` 経由にする

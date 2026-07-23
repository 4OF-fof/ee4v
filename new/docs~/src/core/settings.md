# Settings

setting 定義は原則 `Editor/<Feature>/<Feature>Definitions.cs` に置きます。`SettingDefinition<T>` は Unity 非依存の contract であり、localization scope を constructor へ明示します。

## 定義項目

| field | 内容 |
|---|---|
| `key` | 例: `injector.projectToolbar.enabled` |
| `scope` | `User` または `Project` |
| `localizationScope` | 例: `AssetManager`。表示keyを解決するscope |
| `sectionKey` | settings 画面のグループ見出し |
| `displayNameKey` | 項目名 |
| `descriptionKey` | tooltip |
| `defaultValue` | 既定値 |
| `order` | section 内並び順 |
| `validator` | 入力制約 |
| `range` | 数値など順序付け可能な設定値の最小値・最大値 |
| `keywords` | settings 検索補助 |

## 登録と参照

- `RegisterAll(ISettingsService)` は渡された service へ定義を登録する
- feature の Composition が `CoreSettings.Current` を取得し、definitions と adapter へ渡す
- 値参照は注入された `ISettingsService.Get(...)`
- 更新は注入された `ISettingsService.Set(...)`
- background task から値を読む場合は、task を開始する前に main thread で `Preload(scope)` を呼ぶ

同じ key に異なる `SettingDefinition` instance を登録した場合は例外になります。設定 cache と登録表は `SettingsService` instance ごとに分離されます。`EditorPrefs` や project file の初回読み込み自体は Unity main thread で行う前提です。

feature の Domain / Application / UI は `CoreSettings.Current` を直接参照しません。Composition だけが現在の service を取得し、必要な adapter へ `ISettingsService` を constructor injection します。

## 保存先

| scope | 保存先 | 用途 |
|---|---|---|
| `SettingScope.User` | `EditorPrefs` | ユーザー単位の設定 |
| `SettingScope.Project` | `ProjectSettings/ee4v.settings.json` | プロジェクト固有の設定 |

保存 JSON が破損している場合は起動を止めず、該当 scope を空の設定として読み込み、各定義の default 値へ戻します。現在は破損ファイルの自動修復・退避は行いません。

## 設定画面

`Preferences/4OF/ee4v` と `Project/4OF/ee4v` は `RegisteredSettingsProviders` が提供します。通常の feature 実装では provider を追加せず、`ISettingsService.Register(...)` した定義を既存 provider に載せます。

grouping は `localizationScope + sectionKey` 単位です。section を増やす場合は localization key も揃えます。

設定画面は `SettingsProvider.activateHandler` から UI Toolkit で構築します。標準fieldで足りないdrawerは `Editor/Core/Presentation/Settings` の `SettingDrawerRegistry` へpresentation側から登録し、`SettingDefinition<T>` にはUI型を持たせません。IMGUI drawerは使用しません。

## assembly 境界

- `Ee4v.Core.Contracts.Editor`: 定義と `ISettingsService`。Unity非依存
- `Ee4v.Core.Services.Editor`: `SettingsService` とstore/serializer port。Unity非依存
- `Ee4v.Core.Unity.Editor`: EditorPrefs、project file、Newtonsoft adapterと `CoreSettings`
- `Ee4v.Core.Presentation.Editor`: SettingsProvider、drawer、field renderer

## バリデーション

- invalid 値は `validator` で弾く
- 最小値・最大値を持つ設定は `SettingRange<T>` を定義へ持たせる。範囲を使う UI は同じ `range` を参照し、別の制約値を持たない
- エラーメッセージは `SettingValidationResult.Error(...)`
- validation 文言も `I18N.Get(...)` 経由にする

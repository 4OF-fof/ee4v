# Setting

PreferencesとProject Settingsで使用する実画面は`ee4v/Debug/Catalog`の`Domain/Core/Settings UI` Storyで確認できます。Storyはメモリ上のサンプル設定だけを変更し、設定ファイルを保存しません。セクション見出しには背景を持たない`DisclosureSection`のGhost Headerを使用します。

## 機能

Setting は設定定義の登録、値の検証、読込、保存、変更通知を扱います。既定実装は User と Project の二つのスコープを持ちます。

## 公開型

| 型 | 役割 |
|---|---|
| `SettingDefinitionBase` | 型に依存しない設定定義 |
| `SettingDefinition<T>` | 既定値、検証、範囲を持つ型付き設定定義 |
| `SettingScope` | `User` または `Project` を表す |
| `SettingRange<T>` | 最小値と最大値を含む範囲を表す |
| `SettingValidationResult` | 検証の成否とメッセージを表す |
| `ISettingsService` | 設定の登録、読込、更新、保存を定義する |
| `SettingChangedEventArgs` | 変更された定義と値を通知する |
| `CoreSettings` | 既定の `ISettingsService` を公開する |
| `SettingDrawerApi` | 設定定義ごとの入力 UI を登録する |
| `SettingDrawerContext<T>` | 入力 UI へ現在値と変更通知を渡す |
| `GlobalDataSettings` | ee4v共有データのルートと変更通知を公開する |
| `ProjectAssetSettings` | Project内で生成するAssetの共通ルートを解決する |
| `CommaSeparatedListSettingDrawer` | 行編集式の一覧設定UIと区切り文字付き保存値の解析を提供する |

## `SettingDefinitionBase`

| メンバー | 内容 |
|---|---|
| `Key` | Settings 全体で設定を識別する文字列 |
| `Scope` | 保存先のスコープ |
| `LocalizationScope` | 表示名などを取得する翻訳スコープ |
| `SectionKey` | Settings 画面の区分名に使う翻訳 key |
| `DisplayNameKey` | 表示名に使う翻訳 key |
| `DescriptionKey` | tooltip に使う翻訳 key |
| `Order` | 同一区分内の表示順 |
| `Keywords` | Settings 検索に使う追加語句 |
| `ValueType` | 値の `Type` |
| `DefaultValue` | 保存値がない場合に使う値 |
| `Validate(object)` | 値を検証して `SettingValidationResult` を返す |

空の `Key` または `LocalizationScope` は許可されません。

## `SettingDefinition<T>`

`SettingDefinition<T>` は `SettingDefinitionBase` に型付きの既定値、任意の validator、任意の `SettingRange<T>` を追加します。

`Validate` は先に範囲を検証し、範囲内の場合だけ validator を実行します。範囲外の既定値を持つ定義は生成できません。

## `ISettingsService`

| メンバー | 戻り値・動作 |
|---|---|
| `Changed` | 有効な値が変更された後に発生するイベント |
| `Register(definition)` | 設定定義を登録する |
| `GetDefinitions(scope)` | 指定スコープの定義を表示順で返す |
| `Preload(scope)` | 指定スコープの保存値を先に読み込む |
| `Get<T>(definition)` | 型付きの現在値を返す |
| `Get(definition)` | `object` として現在値を返す |
| `Set<T>(definition, value, saveImmediately)` | 型付きの値を検証して更新する |
| `Set(definition, value, saveImmediately)` | `object` の値を検証して更新する |
| `Save(scope)` | 指定スコープまたは全 dirty スコープを保存する |

`Set` の `saveImmediately` は既定で `true` です。`false` の更新はメモリ上で dirty 状態となり、後続の `Save` で保存されます。

## 既定実装

`CoreSettings.Current` は `SettingsService` の共有 instance を返します。保存先と serializer は次の構成です。

| スコープ | 画面 | 保存先 |
|---|---|---|
| `User` | `Preferences/4OF/ee4v` | `EditorPrefs` の `dev.4of.ee4v.settings.user` |
| `Project` | `Project/4OF/ee4v` | `ProjectSettings/ee4v.settings.json` |

各値は Newtonsoft.Json で JSON 文字列へ変換されます。Project の保存ファイルは設定 key と JSON 文字列の辞書です。

### 生成アセットの共通ルート

Preferencesの「生成アセットのルートフォルダー名」は、すべてのプロジェクトで使う共通ルート名です。既定値は`!ee4vAsset`です。Project設定の「このプロジェクトで上書き」は既定で無効です。有効にした場合だけ「プロジェクトのルートフォルダー名」を使用し、入力欄も有効になります。

`!`による通常の名前順でProject Windowの先頭付近へ表示します。新規シーンは`Assets/<有効なルート名>/Scene`、新規表情クリップは`Assets/<有効なルート名>/Animation/Facial`を使用します。以前の新規シーン専用の保存先設定は使用しません。

ルート名を変更しても既存フォルダーは移動または改名しません。変更後に新しく作成するアセットから新しいルート名を使用します。空値、`.`、`..`、前後の空白、パス区切り文字、OSで無効なファイル名文字は指定できません。

`ProjectAssetSettings.GetAssetFolder`は生成先を変更せずにパスを返します。`EnsureAssetFolder`は不足するフォルダーを作成します。`IsAssetRootDefinition`は変更された設定定義が生成Assetのルートへ影響するかを返します。

`GlobalDataSettings.RootDirectory`は環境変数を展開した絶対パスを返します。ルート設定が変更されると`PathChanged`が発生します。

`CommaSeparatedListSettingDrawer.Register`は文字列設定へ`ListField<string>`を登録します。項目の追加はリスト末尾の追加行から行い、削除は各行のボタンから行います。空一覧では表示用の空行を1行維持します。設定値の保存形式は変更せず、Drawerの`ParseItems`と`SerializeItems`がカンマ、セミコロン、改行による変換と空項目の除外を担当します。

## 副作用

| 操作 | 副作用 |
|---|---|
| `Register` | 共有サービスの定義一覧を変更する。既に読込済みのスコープでは対象値も読み込む |
| `GetDefinitions`、`Preload`、最初の `Get` | 対応する保存先を読み、スコープ全体をメモリへ保持する |
| `Set` | メモリ上の値と dirty 状態を変更する。即時保存時は保存先も更新する |
| 値が実際に変わった `Set` | 保存処理の後に `Changed` を発生させる |
| `Save` | スコープに登録された全定義の現在値を保存先へ書き込む |
| Settings 画面を開く | 登録済み定義から UI を生成し、値変更時に `Set` を呼ぶ |
| `SettingDrawerApi.Register` | key ごとの drawer を process 内の共有辞書へ登録する。同じ key は置き換える |

Project の保存では `ProjectSettings` フォルダがなければ作成します。

## 検証と失敗時の動作

| 条件 | 動作 |
|---|---|
| 同じ定義 instance を再登録 | 状態を変更しない |
| 同じ key の異なる定義を登録 | `InvalidOperationException` |
| `Set` した値が検証に失敗 | `InvalidOperationException`。値と保存先は変更しない |
| 非 generic の `Set` に異なる実行時型を渡す | `InvalidCastException` が発生する場合がある |
| 保存値を deserialize できない | 定義の既定値を使う |
| 復元した値が検証に失敗 | 定義の既定値を使う |
| User または Project の保存 JSON が壊れている | 空の辞書として扱う |
| `SettingRange<T>` の最小値が最大値を超える | `ArgumentException` |

保存先の読込・書込で発生した一般的なファイル I/O 例外は `SettingsService` では捕捉しません。

## Settings UI

標準 drawer は `bool`、`int`、`float`、`double`、`string`、`Color`、enum に対応します。未対応型は警告用 `HelpBox` を表示します。

独自 drawer は `SettingDrawerContext<T>` の `Value` を表示し、変更時に `NotifyValueChanged` を呼びます。通知された値は通常の `ISettingsService.Set` と同じ検証と保存を通ります。

現行の Unity 2022.3 実装では、文字を持つ Settings UI を `UiTextFactory` 経由で作成します。

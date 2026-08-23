# Core

Core は feature 横断の契約と接続部を提供します。
機能固有の domain model や DB schema は所有しません。

## 主な公開 API

- `ISettingsService` と `SettingDefinition<T>`
- `ILocalizationService` と `I18N`
- `InjectorApi` と注入 registration
- `IBackgroundTaskManager`、`CoreBackgroundActivities`、`BackgroundStatusOverlay`
- `CachedImage` と `CachedImageCache`
- `Ee4v.Core.EditorIntegration` の用途別 API

`EditorIntegration` の公開型は Unity の内部型や reflection backend を返しません。
Unity 版による差異は `Core/Internal/EditorAPI/Backends` で吸収します。
公開 API は backend から公開状態型を直接受け取り、同じ状態を表す内部 snapshot や
静的な中継層は設けません。

独自 Settings UI が必要な場合は `SettingDrawerApi.Register` を使います。
機能 assembly を Core の `InternalsVisibleTo` へ追加する必要はありません。

## 依存規則

`Contracts` と `Services` から Unity を参照しません。
機能の中心処理から static facade を直接呼ばず、composition root で必要な契約を渡します。
static API は Editor lifecycle と UI の入口だけで使用します。

## background task

`IBackgroundTaskManager.Run` は非同期処理の開始、進捗、完了、失敗、キャンセルを管理します。
処理を別threadへ移すことはせず、呼び出し元の実行contextを保ちます。
Unity APIを使う処理はmain threadで開始し、外部commandやI/O待機も同じ契約へ載せられます。

`Begin` は既存の同期処理を表示へ載せるための互換APIです。
新しい非同期処理では `Run` と `IBackgroundTaskContext` を使用します。

# Background

## 機能

Background は処理の開始、進捗、成功、失敗、キャンセルを一つのライフサイクルとして管理します。既定の共有 manager は `CoreBackgroundActivities.Current` です。

## 公開型

| 型 | 役割 |
|---|---|
| `IBackgroundTaskManager` | 同期・非同期処理の追跡、現在状態、履歴を管理する |
| `IBackgroundTaskContext` | 処理へキャンセル token と進捗通知を渡す |
| `IBackgroundTaskHandle` | 実行中または完了済みタスクの操作窓口 |
| `BackgroundTaskState` | 一つのタスクの snapshot |
| `BackgroundActivityState` | 表示用に集約した実行状態 |
| `BackgroundTaskStatus` | タスクの状態 enum |
| `BackgroundActivityTracker` | Unity 非依存の標準実装 |
| `BackgroundStatusOverlay` | EditorWindow に共有状態を表示する |

## `IBackgroundTaskManager`

| メンバー | 戻り値・動作 |
|---|---|
| `Begin(message)` | 同期処理の開始を登録し、終了用 `IDisposable` を返す |
| `Run(message, operation)` | タスクを登録して処理を開始し、`IBackgroundTaskHandle` を返す |
| `GetState()` | 実行中件数と最新メッセージを返す |
| `GetTasks()` | 保持中の全タスクを ID 順で返す |
| `TryGetTask(id, out state)` | 指定 ID の snapshot があれば返す |
| `Clear()` | 実行中処理をキャンセル要求状態にして全履歴を削除する |
| `ClearCompleted()` | active ではない履歴を削除する |

`Begin` が返す handle を破棄すると `Succeeded` になります。処理中の例外を自動判定しないため、失敗とキャンセルを記録する処理には `Run` を使います。

`Run` は `operation` を別スレッドへ移しません。呼び出し元の実行 context で処理を開始します。

## `IBackgroundTaskContext`

| メンバー | 動作 |
|---|---|
| `CancellationToken` | manager が所有するキャンセル token を返す |
| `Report(message)` | active な間だけタスクの現在メッセージを更新する |

## `IBackgroundTaskHandle`

| メンバー | 戻り値・動作 |
|---|---|
| `Id` | manager 内で増加するタスク ID |
| `Completion` | operation の完了を表す `Task` |
| `State` | 現在の `BackgroundTaskState` を返す |
| `Cancel()` | キャンセルを要求する |
| `Dispose()` | `Cancel()` と同じくキャンセルを要求する。完了は待たない |

`Completion` は operation の例外と `OperationCanceledException` を呼び出し側へ伝えます。

## 状態

| `BackgroundTaskStatus` | 意味 |
|---|---|
| `Running` | operation を実行中 |
| `CancellationRequested` | token をキャンセル済みだが operation は未完了 |
| `Succeeded` | operation が例外なく完了 |
| `Failed` | operation が例外で終了 |
| `Canceled` | operation が `OperationCanceledException` で終了 |

`BackgroundTaskState` は ID、状態、メッセージ、エラーメッセージ、UTC の開始・終了時刻を持ちます。

`BackgroundActivityState` は active の有無、最新の active タスクのメッセージ、active 件数を持ちます。

## 副作用

| 操作 | 副作用 |
|---|---|
| `Run` | manager 内へタスクを追加し、`CancellationTokenSource` を作成して operation を開始する |
| `Report` | manager 内のメッセージを変更する |
| `Cancel`、handle の `Dispose` | 状態を `CancellationRequested` にし、token をキャンセルする |
| operation の完了 | 状態、エラー、終了時刻を更新する |
| `ClearCompleted` | 完了済み履歴を manager から削除する |
| `Clear` | active な token をキャンセルし、active を含む全履歴を直ちに削除する |
| `BackgroundStatusOverlay.Attach` | EditorWindow の root に stylesheet と表示 host を追加する |
| `BackgroundStatusOverlay.Detach` | 対象 window から表示 host と登録情報を削除する |

`BackgroundActivityTracker` は完了済み履歴を最大64件保持します。上限を超えると古い完了済み履歴から削除します。内部状態の操作は lock で保護されています。

## `BackgroundStatusOverlay`

`Attach(window)` は window ごとに host を一つ登録します。同じ window への再実行では既存 host を再利用します。host は `CoreBackgroundActivities.Current.GetState()` を100ミリ秒ごとに読み、active 状態とメッセージを表示します。

window から host が外れた場合は `DetachFromPanelEvent` で共有登録も削除します。

## 失敗時とキャンセル時の動作

| 条件 | 動作 |
|---|---|
| `Run` の operation が `null` | `ArgumentNullException` |
| operation が例外で終了 | 状態を `Failed` にし、例外メッセージを保存して `Completion` を faulted にする |
| operation が `OperationCanceledException` で終了 | 状態を `Canceled` にして `Completion` を canceled にする |
| operation が token を監視しない | `Cancel` 後も operation の完了まで `CancellationRequested` が続く |
| 完了後の `Report` または `Cancel` | 状態を変更しない |
| `Attach` に `null` または root のない window | 状態を変更しない |

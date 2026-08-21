# ProjectTabs

## 機能

ProjectTabs は Project ウィンドウのツールバーへフォルダータブを追加します。タブごとにフォルダー、検索文字列、最大50件の移動履歴を保持します。

固定タブは Unity の Project Favorites と同期します。固定タブから別のフォルダーへ移動する UI 操作では通常タブを追加し、固定タブの位置を維持します。

タブ追加はFluent UI System Iconsの12pxアイコンを使用します。ツールバーから追加した新規タブは、現在の階層を引き継がず `Assets` を初期位置として開きます。

実際のtoolbarは`ee4v/Debug/Catalog`の`Domain/ProjectTabs/Components/Project Tabs` Storyで、通常タブ、固定タブ、選択状態、移動履歴を含むサンプル状態として確認できます。各タブ内のアイコンと名前はCoreの`ContentRow`を組み合わせます。閉じる操作はタブ右上へ重ね、タブ一覧とDrag操作はDomain側に残します。

## 公開型

| 型 | 役割 |
|---|---|
| `ProjectTabsApi` | タブの状態参照と操作を行う |
| `ProjectTabLocation` | フォルダーと検索文字列を表す |
| `ProjectTabState` | 1つのタブの読み取り専用 snapshot |
| `ProjectTabsState` | 全タブの読み取り専用 snapshot |

## `ProjectTabsApi`

| メンバー | 戻り値・動作 |
|---|---|
| `Changed` | 保存するタブ状態が変更された後に通知する |
| `State` | 現在の `ProjectTabsState` を返す |
| `Add(location)` | 通常タブを末尾へ追加し、生成したタブ ID を返す |
| `AddRange(locations)` | 通常タブを末尾へ追加し、生成したタブ ID を入力順で返す |
| `Move(tabId, targetIndex)` | タブを指定 index へ移動する。変更した場合は `true` |
| `Remove(tabId)` | タブを削除する。変更した場合は `true` |
| `SetPinned(tabId, isPinned)` | 固定状態を変更する。変更した場合は `true` |
| `GoBack(tabId, steps = 1)` | 履歴を戻り、移動先を返す。移動できない場合は `null` |
| `GoForward(tabId, steps = 1)` | 履歴を進み、移動先を返す。移動できない場合は `null` |

`State` と `Changed` は process 内で共有する ProjectTabs session を初期化して使用します。API による変更は保存後に `Changed` を通知します。

## `ProjectTabLocation`

constructor は `ProjectTabLocation(folderGuid, folderPath, searchText = "")` です。

| メンバー | 内容 |
|---|---|
| `FolderGuid` | フォルダーの GUID。`null` は空文字列になる |
| `FolderPath` | `/` 区切りへ正規化したフォルダーパス。末尾の `/` は除く |
| `SearchText` | Project Browser の検索文字列。`null` は空文字列になる |
| `DisplayName` | `FolderPath` の末尾要素 |

等価比較では `FolderGuid`、`FolderPath`、`SearchText` の3項目を ordinal 比較します。

## `ProjectTabState`

| メンバー | 内容 |
|---|---|
| `Id` | タブを識別する ID |
| `History` | 移動履歴 |
| `HistoryIndex` | 現在位置の index |
| `IsPinned` | 固定タブか |
| `CurrentLocation` | 現在位置。index が範囲外の場合は `null` |
| `CanGoBack` | 前の履歴があるか |
| `CanGoForward` | 次の履歴があるか |

タブを固定すると履歴を現在位置の1件へ縮めます。固定タブでは `GoBack` と `GoForward` を実行できません。

## `ProjectTabsState`

| メンバー | 内容 |
|---|---|
| `Tabs` | 現在の全タブ |
| `Find(tabId)` | ordinal 比較で ID が一致するタブを返す。見つからない場合は `null` |
| `FindByCurrentLocation(location)` | 現在位置が等しい最初のタブを返す。見つからない場合は `null` |

## 永続化と連携

タブ一覧、履歴、固定状態は `UserSettings/ee4v.project-tabs.asset` へ保存します。固定状態は Core の `ProjectFavoritesApi` を接続部として Unity の Project Favorites と同期します。

選択中のタブは Project ウィンドウごとに保持する UI 状態であり、`ProjectTabsState` には含みません。

## 副作用

| 操作 | 副作用 |
|---|---|
| 変更に成功した API | 状態を保存し、`Changed` を通知する |
| `SetPinned` | Project Favorites との同期対象になる |
| 最後のタブを `Remove` | 既定の `Assets` タブを1つ作成する |

## 失敗時の動作

| 条件 | 動作 |
|---|---|
| `Add` の `location` が `null` またはフォルダー情報を欠く | 既定の `Assets` location でタブを追加する |
| `AddRange` の `locations` が `null` または空 | 空の ID 一覧を返し、状態を変更しない |
| `Move` の ID または index が無効 | `false` を返し、状態を変更しない |
| `Remove`、`SetPinned` の ID が見つからない | `false` を返し、状態を変更しない |
| `SetPinned` の指定値が現在値と同じ | `false` を返し、状態を変更しない |
| `GoBack`、`GoForward` の `steps` が `0` 以下 | `null` を返し、状態を変更しない |
| `GoBack`、`GoForward` を移動不能なタブへ実行 | `null` を返し、状態を変更しない |

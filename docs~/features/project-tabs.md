# ProjectTabs

## 機能

ProjectTabs は Project ウィンドウのツールバーへフォルダータブを追加します。タブごとにフォルダー、検索文字列、最大50件の移動履歴を保持します。

ProjectTabsは2列表示のProject ウィンドウだけで有効にします。1列表示ではタブを非表示にし、現在位置の追跡とフォルダー移動を行いません。判定はウィンドウごとに行うため、1列表示と2列表示のProject ウィンドウを同時に使用できます。表示方式を2列へ戻すと、そのウィンドウのProjectTabsを自動的に再有効化します。

タブ領域は Project ウィンドウの幅に追従し、左側に36pxと右側に470pxの固定余白を確保して Unity 標準の操作領域との重なりを防ぎます。同じ DockArea の別タブが選択されると Unity は Project ウィンドウの表示階層をパネルから一時的に外します。ProjectTabs は再表示時に状態監視を再開し、同じウィンドウのタブを更新します。

固定状態の正本は Unity の Project Favorites です。タブのフォルダーが Favorite に含まれると固定タブとして扱います。固定タブから別のフォルダーへ移動する UI 操作では通常タブを追加し、固定タブの位置を維持します。

タブ追加はFluent UI System Iconsの12pxアイコンを使用します。ツールバーから追加した新規タブは、現在の階層を引き継がず `Assets` を初期位置として開きます。

実際のtoolbarは`ee4v/Debug/Catalog`の`Domain/ProjectTabs/Inputs/Project Tabs` Storyで、通常タブ、固定タブ、選択状態、移動履歴を含むサンプル状態として確認できます。各タブ内のアイコンと名前はCoreの`ItemRow`を組み合わせます。タブと操作ボタンはProject Windowへ収める小さい寸法を維持しながら、境界線、面、角丸、hover、選択状態をCore UIへ揃えます。閉じる操作は通常タブの右上へ常時表示して重ね、固定タブでは選択状態にかかわらず非表示にします。タブ一覧とDrag操作はDomain側に残します。

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
| `Changed` | タブ状態または Favorites に由来する固定状態が変更された後に通知する |
| `State` | 現在の `ProjectTabsState` を返す |
| `Add(location)` | 通常タブを末尾へ追加し、生成したタブ ID を返す |
| `AddRange(locations)` | 通常タブを末尾へ追加し、生成したタブ ID を入力順で返す |
| `Move(tabId, targetIndex)` | タブを指定 index へ移動する。変更した場合は `true` |
| `Remove(tabId)` | タブを削除する。固定タブなら Favorite も削除する。変更した場合は `true` |
| `SetPinned(tabId, isPinned)` | 対象フォルダーを Favorites に追加または削除する。変更した場合は `true` |
| `GoBack(tabId, steps = 1)` | 履歴を戻り、移動先を返す。移動できない場合は `null` |
| `GoForward(tabId, steps = 1)` | 履歴を進み、移動先を返す。移動できない場合は `null` |

`State` と `Changed` は process 内で共有する ProjectTabs session を初期化して使用します。タブ一覧や履歴の変更は保存後に `Changed` を通知します。Favorites の変更も `Changed` を通知します。

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
| `IsPinned` | 現在の Project Favorites にフォルダーが含まれるか |
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

タブ一覧と履歴は `UserSettings/ee4v.project-tabs.asset` へ保存します。固定フラグと削除記録は保存しません。固定状態は Core の `ProjectFavoritesApi` から取得します。起動時と Favorites の変更時は、Favorite に対応するタブがなければ追加し、既存タブがあればそのタブを使用します。保存済みタブの旧固定フラグは読み込みません。

選択中のタブは Project ウィンドウごとに保持する UI 状態であり、`ProjectTabsState` には含みません。

Project ウィンドウの復元時は、Unity側で開いているフォルダーと一致する保存済みタブを選択します。検索文字列だけが異なる場合も同じフォルダーのタブを再利用し、復元処理だけを理由に新しいタブを追加しません。

## 副作用

| 操作 | 副作用 |
|---|---|
| タブ一覧・履歴を変更した API | 状態を保存し、`Changed` を通知する |
| `SetPinned` | Project Favorites を変更し、`Changed` を通知する |
| 固定タブの `Remove` | 対応する Favorite を削除してからタブを削除する |
| 最後のタブを `Remove` | 既定の `Assets` タブを1つ作成する |

## 失敗時の動作

| 条件 | 動作 |
|---|---|
| `Add` の `location` が `null` またはフォルダー情報を欠く | 既定の `Assets` location でタブを追加する |
| `AddRange` の `locations` が `null` または空 | 空の ID 一覧を返し、状態を変更しない |
| `Move` の ID または index が無効 | `false` を返し、状態を変更しない |
| `Remove`、`SetPinned` の ID が見つからない | `false` を返し、状態を変更しない |
| `SetPinned` の指定値が現在値と同じ | `false` を返し、状態を変更しない |
| Favorites を取得できない、または Favorite の変更に失敗した `Remove`・`SetPinned` | `false` を返し、タブを削除・変更しない |
| `GoBack`、`GoForward` の `steps` が `0` 以下 | `null` を返し、状態を変更しない |
| `GoBack`、`GoForward` を移動不能なタブへ実行 | `null` を返し、状態を変更しない |

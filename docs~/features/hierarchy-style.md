# HierarchyStyle

## 機能

HierarchyStyle は GameObject の背景色と Hierarchy 項目アイコンを設定します。`GlobalObjectId` を識別子として使用します。

背景色は対象から親へ向かって明示設定を探し、最初に見つかった色を使用します。アイコンは対象の GameObject だけに適用します。Alt キーを押した状態で GameObject を指すと編集ウィンドウを開きます。

Hidden Objects も HierarchyStyle に含まれます。非表示にした GameObject をシーン単位のツリーで確認し、元の active state と tag を復元できます。

## 公開型

| 型 | 役割 |
|---|---|
| `HierarchyStyleApi` | GameObject のスタイルと Hierarchy 上の表示状態を操作する |
| `ItemStyleValue` | 背景色とアイコンの設定値を表す |

## `HierarchyStyleApi`

| メンバー | 戻り値・動作 |
|---|---|
| `Get(target)` | GameObject に直接設定された `ItemStyleValue` を返す |
| `SetColor(targets, color)` | 指定した GameObject へ背景色を設定する |
| `SetIcon(targets, iconGuid)` | 指定した GameObject へアセット GUID でアイコンを設定する |
| `Clear(targets)` | 指定した GameObject の背景色とアイコンを解除する |
| `Hide(instanceIds, undoOperationName)` | 指定した instance ID の GameObject を Hierarchy から非表示にし、処理数を返す |
| `Reveal(instanceIds, undoOperationName)` | 指定した instance ID の GameObject を再表示し、処理数を返す |
| `OpenHiddenObjects(sceneHandle = 0)` | Hidden Objects ウィンドウを開く。`0` は全シーン、それ以外は該当するシーンを表示する |
| `RefreshHiddenObjects()` | 開いている Hidden Objects ウィンドウを更新する |

`SetColor` にアルファ値が `0` 以下の色を渡すと背景色だけを解除します。`SetIcon` に `null` または空文字列を渡すとアイコンだけを解除します。

## `ItemStyleValue`

| メンバー | 内容 |
|---|---|
| `Identity` | HierarchyStyle では GameObject の `GlobalObjectId` |
| `HasColor` | 背景色が設定されているか |
| `Color` | 設定色。未設定の場合は `Color.clear` |
| `IconGuid` | アイコンとして使うアセットの GUID |
| `HasIcon` | `IconGuid` が空でないか |
| `IsEmpty` | 背景色とアイコンがどちらも未設定か |

`Get` が返す値は対象へ直接保存された設定です。親から継承する背景色は描画時に解決するため、この戻り値には反映しません。

## Hidden Objects

`Hide` は対象の active state と tag を保存してから、非アクティブ化、`EditorOnly` tag の設定、`HideFlags.HideInHierarchy` の追加を行います。

`Reveal` は `HideFlags.HideInHierarchy` を外します。保存した状態がある場合は tag と active state も復元します。保存した tag が現在の Tag Manager に存在しない場合は `Untagged` を設定します。

Hidden Objects ウィンドウでは `HideInHierarchy` が設定された GameObject をシーン単位で表示します。設定で除外したシーンと GameObject は一覧に含めず、除外対象の GameObject の子孫も除きます。

## 永続化

| 内容 | 保存先 |
|---|---|
| 背景色、アイコン、最近使ったアイコン | `UserSettings/ee4v.item-styles.asset` の `hierarchy` スコープ |
| 非表示前の active state と tag | `UserSettings/ee4v.hidden-object-restore-states.asset` |

## 副作用

| 操作 | 副作用 |
|---|---|
| `SetColor`、`SetIcon`、`Clear` | 設定を保存し、Hierarchy 項目の再描画を要求する |
| `Hide`、`Reveal` | Undo を記録し、対象 GameObject、Prefab instance、対象シーンを dirty にして Hierarchy を再描画する |
| `OpenHiddenObjects` | EditorWindow を開く |
| `RefreshHiddenObjects` | 開いている Hidden Objects ウィンドウの一覧を再構築する |

## 失敗時の動作

| 条件 | 動作 |
|---|---|
| `Get` の対象が `null` または有効なシーンに属さない | 空の `ItemStyleValue` を返す |
| スタイル操作の `targets` が `null` または空 | 設定を変更しない。再描画要求は行う |
| `targets` に無効な対象または重複がある | 無効な対象を無視し、同じ `GlobalObjectId` は1回だけ処理する |
| `Hide`、`Reveal` の `instanceIds` が `null` または空 | `0` を返す |
| instance ID が GameObject を指さない | 対象から除外する |
| `Hide` の対象が無効なシーンに属するか、すでに非表示 | 対象から除外する |
| `Reveal` の対象が非表示ではない | 対象から除外する |
| `OpenHiddenObjects` の `sceneHandle` に該当する対象がない | 全シーン表示へ切り替える |

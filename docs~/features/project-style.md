# ProjectStyle

## 機能

ProjectStyle は Project のフォルダーへ背景色とアイコンを設定します。フォルダー GUID を識別子として使用します。

Alt キーを押した状態でフォルダーを指すと編集ウィンドウを開きます。編集ウィンドウはCore UIの`CustomPopup`を使用します。色とアイコンの選択肢、hover、選択、解除操作もCore UIと同じ境界線、面、角丸、状態色を使用します。対象が複数選択に含まれる場合は、選択中のフォルダーへ同じ変更を適用します。カスタムアイコンを描画した項目では FolderContentOverlay の重ね表示を抑制します。

未設定時と色だけを設定したフォルダーには、Unity組み込みのFolderアイコンを使用します。編集ウィンドウ先頭のアイコンプレビューは背景色を持ちません。

AssetManagerはImport時に、アイコンが未設定の取り込み先フォルダーへItemサムネイルを初期アイコンとして設定できます。この設定も通常のProject Styleとして保存するため、利用者が後から別のTextureへ変更または解除できます。

## 公開型

| 型 | 役割 |
|---|---|
| `ProjectStyleApi` | フォルダーのスタイルを取得、更新、解除する |
| `ItemStyleValue` | 背景色とアイコンの設定値を表す |

## `ProjectStyleApi`

| メンバー | 戻り値・動作 |
|---|---|
| `Get(folderGuid)` | フォルダー GUID に対応する `ItemStyleValue` を返す |
| `GetIconGuid(folderGuid)` | フォルダーのアイコン GUID を返す。未設定なら空文字列。共有`ItemStyle`型を参照しない連携用 |
| `SetColor(folderGuids, color)` | 指定したフォルダーへ背景色を設定する |
| `SetIcon(folderGuids, iconGuid)` | 指定したフォルダーへアセット GUID でアイコンを設定する |
| `Clear(folderGuids)` | 指定したフォルダーの背景色とアイコンを解除する |

`SetColor` にアルファ値が `0` 以下の色を渡すと背景色だけを解除します。`SetIcon` に `null` または空文字列を渡すとアイコンだけを解除します。

## `ItemStyleValue`

| メンバー | 内容 |
|---|---|
| `Identity` | ProjectStyle ではフォルダー GUID |
| `HasColor` | 背景色が設定されているか |
| `Color` | 設定色。未設定の場合は `Color.clear` |
| `IconGuid` | アイコンとして使うアセットの GUID |
| `HasIcon` | `IconGuid` が空でないか |
| `IsEmpty` | 背景色とアイコンがどちらも未設定か |

## 永続化

スタイルは `UserSettings/ee4v.item-styles.asset` の `project` スコープへ保存します。設定変更後は同じファイルを直ちに保存します。

## 副作用

| 操作 | 副作用 |
|---|---|
| `SetColor` | 設定を保存し、Project 項目の再描画を要求する |
| `SetIcon` | 設定と最近使ったアイコンを保存し、Project 項目の再描画を要求する |
| `Clear` | 設定を解除して保存し、Project 項目の再描画を要求する |

## 失敗時の動作

| 条件 | 動作 |
|---|---|
| 未登録のフォルダー GUID を `Get` | 空の `ItemStyleValue` を返す |
| `folderGuids` が `null` または空 | 設定を変更しない。再描画要求は行う |
| `folderGuids` に空文字列または重複がある | 空文字列を無視し、同じ GUID は1回だけ処理する |

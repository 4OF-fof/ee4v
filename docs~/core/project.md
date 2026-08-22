# Project

## 機能

Project は Project Browser の各項目へ IMGUI 描画を追加し、各 Project Browser window へ UI Toolkit の toolbar 要素を追加します。登録の窓口は `InjectorApi` です。

Project Browser の状態取得や操作は [EditorIntegration](./editor-integration.md#projectbrowserapi) に記載します。

## 公開型

| 型 | 役割 |
|---|---|
| `InjectorApi` | registration の登録、解除、再描画 |
| `InjectionChannel` | `ProjectItem` と `ProjectToolbar` を識別する |
| `ItemInjectionRegistration` | Project 項目の描画 callback を表す |
| `VisualElementInjectionRegistration` | toolbar 要素の生成 callback を表す |
| `ItemInjectionContext` | asset と項目の表示状態を描画 callback へ渡す |
| `VisualHostContext` | 対象 channel と Project Browser window を渡す |
| `ProjectItemLayout` | Project 項目の icon 領域を計算する |
| `IInjectionRegistry` | registration の登録、解除、列挙、変更通知を定義する |
| `InjectionRegistry` | Unity 非依存の registry 実装 |

## `InjectorApi`

| メンバー | 戻り値・動作 |
|---|---|
| `Register(registration)` | 共有 registry へ登録し、解除用 `IDisposable` を返す |
| `Unregister(registration)` | 同じ registration instance が登録中なら解除する |
| `Repaint(channel)` | channel に対応する Project Browser 表示を更新する |

`ProjectItem` には `ItemInjectionRegistration`、`ProjectToolbar` には `VisualElementInjectionRegistration` を使用します。現在の presenter は異なる組み合わせを実行しません。

## registration

共通メンバーは `Id`、`Channel`、`Priority`、`IsEnabled()` です。

| 型 | 固有メンバー |
|---|---|
| `ItemInjectionRegistration` | `Draw: Action<ItemInjectionContext>` |
| `VisualElementInjectionRegistration` | `CreateElement: Func<VisualHostContext, VisualElement>` |

registration は `Priority` の昇順、その後 `Id` の ordinal 順で実行されます。同じ channel と ID の登録は新しい registration へ置き換わります。

`IsEnabled()` は Project 項目の描画ごと、または toolbar host の再構築時に確認されます。

`IInjectionRegistry.Changed` は登録、解除、または `Clear` の後に対象 channel を通知します。`InjectionRegistry.GetRegistrations` は登録順に snapshot を返します。

## `ItemInjectionContext`

| メンバー | 内容 |
|---|---|
| `Channel` | `ProjectItem` |
| `Guid` | 対象 asset の GUID |
| `Target` | GUID から読み込んだ main asset。解決できなければ `null` |
| `SelectionRect` | Unity が渡した元の項目領域 |
| `CurrentRect` | registration 間で共有する変更可能な領域 |
| `ProjectViewMode` | `Unknown`、`OneColumn`、`TwoColumns` |
| `ProjectOrientation` | `Unknown`、`Horizontal`、`Vertical` |
| `SuppressProjectItemIconOverlay` | registration 間で共有できる icon overlay 抑制フラグ |

表示 mode と向きは Project Browser の内部状態から取得します。取得できない場合は領域の高さなどから補完し、判定できなければ `Unknown` になります。

## `VisualHostContext`

| メンバー | 内容 |
|---|---|
| `Channel` | `ProjectToolbar` |
| `Window` | 要素を追加する Project Browser の `EditorWindow` |

## `ProjectItemLayout`

`GetIconRect(itemRect, viewMode, orientation)` は入力から icon 領域を計算して返します。外部状態を変更しません。

縦向きでは項目幅を基準に正方形の領域を作ります。横向きでは項目高を基準にし、1列表示の場合だけ左端の inset を加えます。返す領域の高さには内部 scale が適用されます。

## 副作用

| 操作 | 副作用 |
|---|---|
| 最初の Injector 初期化 | `EditorApplication.projectWindowItemOnGUI`、`update`、`modifierKeysChanged` へ callback を登録する |
| `Register` | process 内の共有 registry を変更し、Project Browser を再描画する |
| 同じ channel・ID の再登録 | 古い registration を新しい instance へ置き換える |
| registration lease の `Dispose` | 同じ registration instance が残っている場合だけ解除する |
| `ProjectItem` の描画 | 有効な `Draw` を順番に呼び、同じ `ItemInjectionContext` を共有する |
| `ProjectToolbar` の同期 | 開いている各 Project Browser の root へ host 要素を追加する |
| toolbar host の再構築 | 有効な `CreateElement` を順番に呼び、返された要素を host へ追加する |
| `Repaint(ProjectItem)` | Project window の再描画を要求する |
| `Repaint(ProjectToolbar)` | toolbar host を dirty にし、Project window の再描画を要求する |

toolbar host の同期は dirty 時、または前回確認から1秒経過した後の Editor update で行われます。閉じた window の内部記録は同期時に削除されます。Unity が表示階層を作り直して host が失われた場合は、同じ Project Browser window でも新しい host の内容を再構築します。host は Project Browser の幅に追従し、左側に36pxと右側に470pxの固定余白を確保します。

modifier key が変化すると Project window と Hierarchy window の再描画を要求します。toolbar の `IsEnabled` 結果だけが変わった場合は `Repaint(ProjectToolbar)` が必要です。

現行の Unity 2022.3 実装では、文字を持つ toolbar 要素を `UiTextFactory` 経由で作成します。

## 失敗時の動作

| 条件 | 動作 |
|---|---|
| `Register` に `null` | `ArgumentNullException` |
| 空の registration ID | registry 登録時に `ArgumentException` |
| `ItemInjectionRegistration.Draw` が `null` | constructor が `ArgumentNullException` |
| `VisualElementInjectionRegistration.CreateElement` が `null` | constructor が `ArgumentNullException` |
| `CreateElement` が `null` を返す | 要素を追加せず、次の registration を処理する |
| Project Browser の内部状態を取得できない | toolbar 同期を行わないか、context の表示状態を `Unknown` とする |
| 置換前の registration lease を破棄 | 新しい registration は解除しない |

`IsEnabled`、`Draw`、`CreateElement` が投げた例外は presenter では捕捉しません。呼び出し元の Editor callback へ伝わります。

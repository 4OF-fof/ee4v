# Hierarchy

## 機能

Hierarchy は各行への IMGUI 描画追加を提供します。描画には `InjectorApi` を使います。

## 描画の公開型

| 型 | 役割 |
|---|---|
| `InjectorApi` | registration の登録、解除、再描画 |
| `HierarchyItemApi` | 項目アイコンの設定と開いているHierarchy Windowの取得 |
| `InjectionChannel.HierarchyItem` | Hierarchy 項目の channel |
| `ItemInjectionRegistration` | 項目の描画 callback を表す |
| `ItemInjectionContext` | 対象 object、scene、描画領域を callback へ渡す |
| `HierarchyItemKind` | `Unknown`、`SceneHeader`、`GameObject` を表す |
| `IInjectionRegistry` | registration の登録、解除、列挙、変更通知を定義する |
| `InjectionRegistry` | Unity 非依存の registry 実装 |

## `InjectorApi`

| メンバー | 戻り値・動作 |
|---|---|
| `Register(registration)` | 共有 registry へ登録し、解除用 `IDisposable` を返す |
| `Unregister(registration)` | 同じ registration instance が登録中なら解除する |
| `Repaint(HierarchyItem)` | Hierarchy window の再描画を要求する |

Hierarchy では `InjectionChannel.HierarchyItem` と `ItemInjectionRegistration` を組み合わせます。

`HierarchyItemApi.TryGetOpenWindows`はUnityの内部型をCore内で解決し、開いているHierarchy Windowのsnapshotを返します。`TryGetTreeViewRect`は指定WindowでGameObjectを表示するtree view領域を返します。内部型や必要なmemberを解決できない場合、前者は`false`と空の一覧を返し、後者は`false`と空の`Rect`を返します。

registration は `Priority` の昇順、その後 `Id` の ordinal 順で実行されます。同じ channel と ID の登録は新しい instance へ置き換わります。

`IInjectionRegistry.Changed` は登録、解除、または `Clear` の後に対象 channel を通知します。`InjectionRegistry.GetRegistrations` は登録順に snapshot を返します。

## `ItemInjectionContext`

| メンバー | 内容 |
|---|---|
| `Channel` | `HierarchyItem` |
| `InstanceId` | Unity が渡した項目 ID |
| `Target` | `EditorUtility.InstanceIDToObject` で解決した object |
| `SelectionRect` | Unity が渡した元の項目領域 |
| `CurrentRect` | registration 間で共有する変更可能な領域 |
| `HierarchyItemKind` | scene header、GameObject、未判定の区別 |
| `HierarchyScene` | scene header の場合に解決した `Scene` |
| `IsHierarchySceneHeader` | scene header の簡易判定 |
| `IsHierarchyGameObject` | GameObject の簡易判定 |

`Target` が `GameObject` なら `GameObject` と判定します。それ以外では instance ID と読み込み済み scene の handle を比較し、一致すれば `SceneHeader` と判定します。

## 描画の副作用

| 操作 | 副作用 |
|---|---|
| 最初の Injector 初期化 | `EditorApplication.hierarchyWindowItemOnGUI` と `modifierKeysChanged` へ callback を登録する |
| `Register` | process 内の共有 registry を変更し、Hierarchy を再描画する |
| registration lease の `Dispose` | 同じ registration instance が残っている場合だけ解除する |
| Hierarchy の描画 | 有効な `Draw` を順番に呼び、同じ `ItemInjectionContext` を共有する |
| `Repaint(HierarchyItem)` | Hierarchy window の再描画を要求する |

描画 callback が `CurrentRect` を変更すると、後続 registration が受け取る領域も変わります。`SelectionRect` は変更されません。

modifier key が変化すると Hierarchy window と Project window の再描画を要求します。

## 失敗時の動作

| 条件 | 動作 |
|---|---|
| 描画 registration が `null` | `ArgumentNullException` |
| 空の registration ID | registry 登録時に `ArgumentException` |
| `Draw` が `null` | constructor が `ArgumentNullException` |

`IsEnabled` と描画 callback が投げた例外は facade では捕捉しません。

# Injector

`InjectorApi` は Unity Editor の以下 3 箇所へ差し込めます。

- `HierarchyItem`
- `ProjectItem`
- `ProjectToolbar`

## 登録

- IMGUI 描画なら `ItemInjectionRegistration`
- `VisualElement` を返すなら `VisualElementInjectionRegistration`

各 registration では以下を必ず決めます。

| field | 内容 |
|---|---|
| `id` | channel 内で一意な識別子 |
| `channel` | 差し込み先 |
| `priority` | 同一 channel 内の並び順 |
| `isEnabled` | Setting 連動などの有効条件 |

`InjectorApi.Register(...)` は同じ `id + channel` があれば上書きします。複数登録で押し込む用途ではないため、`id` は安定させます。

## `ItemInjectionContext`

`HierarchyItem` / `ProjectItem` では `ItemInjectionContext` を受け取ります。

| property | 用途 |
|---|---|
| `SelectionRect` | 元の行全体 |
| `CurrentRect` | 他 registration と余白を分け合うための現在の描画可能領域 |
| `Target` | 対象 `Object` |
| `HierarchyItemKind` | scene header / game object 判定 |
| `ProjectViewMode` | one column / two columns |
| `ProjectOrientation` | horizontal / vertical |

右側へ badge を足す場合などは、描画後に `CurrentRect` を狭めて次の registration と競合しないようにします。

## `VisualHostContext`

`ProjectToolbar` では `VisualHostContext` を受け取ります。

- `Window` から host window を参照できる
- 返した `VisualElement` は host にそのまま追加される
- `null` を返すと何も追加しない

host 自体の生成や再構築は `InjectorApi` 側が管理します。feature 側で host を探して差し込む実装は避けます。

## 再描画

Injector 表示に影響する setting を変えたら、該当 channel に対して `InjectorApi.Repaint(...)` を呼びます。

- `HierarchyItem` が変わるなら `HierarchyItem`
- `Project` 側だけなら `ProjectItem` / `ProjectToolbar`

setting 変更監視は注入された `ISettingsService.Changed` に乗せます。`CoreSettings.Current` の取得はfeatureのCompositionに限定します。

## ルール

- draw / create callback は idempotent に保つ
- callback 内で registration を再登録しない
- feature の有効 / 無効は `isEnabled` に寄せる
- channel ごとの描画差を `ItemInjectionContext` で吸収する

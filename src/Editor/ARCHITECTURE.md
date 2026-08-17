# Core / UI architecture

## 目的

Core は機能固有の判断を持たず、Editor 機能を組み立てるための契約と実装を提供します。
UI は業務状態を持たず、渡された状態の表示と操作通知だけを担当します。

```text
Feature composition
  ├─ Feature domain / application
  ├─ Core contracts and services
  ├─ Core editor integration
  └─ Feature UI
       └─ Ee4v.UI
```

DB、VRChat 固有仕様、個別機能の状態遷移は Core に置きません。
Unity、filesystem、reflection の都合は接続部へ閉じ、中心の判断へ持ち込みません。

## assembly 境界

| assembly | 役割 |
|---|---|
| `Ee4v.Core.Contracts.Editor` | Settings、多言語化、注入、バックグラウンドタスク管理の契約 |
| `Ee4v.Core.Services.Editor` | Unity 非依存の契約実装 |
| `Ee4v.Core.Unity.Editor` | EditorPrefs、ProjectSettings、JSON の接続部 |
| `Ee4v.Core.Editor` | package 基盤と Unity 内部 API の隔離 |
| `Ee4v.Core.Presentation.Editor` | UI Toolkit、Settings 画面、Editor lifecycle への接続 |
| `Ee4v.UI.Editor` | 公開 UI 部品、状態、トークン、パレット |
| `Ee4v.UI.Catalog.Editor` | Story の発見と対話確認 |

内側の二つの assembly は `noEngineReferences: true` です。
Unity 6 への移行では `Core.Unity`、`Core.Editor`、`Core.Presentation` の接続部を優先して交換します。

## master 機能の再構成

| 機能 | Core / UI の組み合わせ | 機能側に残すもの |
|---|---|---|
| DepthIndicator、HierarchyDecoration | `InjectorApi`、`ItemInjectionContext` | 幾何計算と対象判定 |
| FolderContentOverlay | `InjectorApi`、`ProjectItemLayout` | AssetDatabase 接続と cache 方針 |
| FolderStyle、HierarchyStyle | Settings | GUID と継承規則 |
| HiddenObjects | visibility 契約、Settings、`SearchableTreeView` | 復元規則と tree 構築 |
| ProjectTabs | Project Browser API、Favorites API、toolbar 注入 | tab と履歴の状態遷移 |
| SceneSwitcher | Hierarchy 注入、検索部品、Settings | Scene 切替規則 |
| SaveAndBackup | background task lifecycle、Settings | 保存順序と外部 command |
| AvatarModify | Status UI | Avatar と VRChat の判断 |

この表の右端は Core へ移さない責務です。
標準の `Dictionary` や LINQ で足りる cache と変換には共通基底型を追加しません。

## 新機能の構成

1. 中心の判断を Unity 非依存の class として作ります。
2. Unity 操作を小さな port と adapter に分けます。
3. Settings、Injector、EditorIntegration を composition root で渡します。
4. 表示は `Ee4v.UI` の公開部品へ状態を渡して構築します。
5. Story は `IUiStoryProvider` として別 assembly に置けます。

Settings の値型には複合 state も使えます。
DB が必要な機能は専用 Infrastructure を所有し、Core Settings を DB の代用にしません。

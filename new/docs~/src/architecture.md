# Architecture

ee4v は機能単位のModule分離を維持し、その内側をClean Architectureの依存方向で
構成します。旧API、旧namespace、旧DBとの互換レイヤは置きません。

```text
AssetManager.Composition
  -> AssetManager.UI
  -> AssetManager.Infrastructure
       -> AssetManager.Application
            -> AssetManager.Domain
            -> AssetManager.Contracts

Testing.UI / Testing.Infrastructure
  -> Testing.Application
       -> Testing.Contracts

Core.Unity / Ee4v.UI / Core.Editor adapters
  -> Core.Services
       -> Core.Contracts
```

`Contracts`、`Domain`、`Application`、`Services` は内側です。Unity、SQLite、
filesystem、Editor lifecycle、UI Toolkitは外側の `Infrastructure`、`Unity`、
`UI`、`Composition` に限定します。

## Module

| Module | 役割 |
|---|---|
| `AssetManager` | asset catalog、datasource同期、import、専用UIを所有する業務Module |
| `Core` | Settings、Localization、Injector、background activity、Unity internal facadeなどfeature横断のfoundation |
| `Testing` | test metadata、descriptor構築、Unity Test Runner adapter、静的監査、Test List UIを所有する独立Module |
| `ThirdParty/SQLite` | SQLite配布物と初期化だけを隔離する外部技術adapter |

`Phase1` stubと、コードを持たず参照を束ねるだけだった `Ee4v.Editor` assemblyは
削除済みです。利用側は必要なModule assemblyを明示的に参照します。

## AssetManager namespace

| namespace | 役割 |
|---|---|
| `Ee4v.AssetManager.Contracts` | 公開DTO、request / result、error code、`IAssetManager` |
| `Ee4v.AssetManager.Domain` | invariant、path・command policy、技術非依存の判断 |
| `Ee4v.AssetManager.Application` | use case、transaction境界、通知順序、instance facade |
| `Ee4v.AssetManager.Application.Ports` | Applicationが外側へ要求するread/write/import/sync port |
| `Ee4v.AssetManager.Infrastructure` | port実装の構成と外部技術共通処理 |
| `Ee4v.AssetManager.Infrastructure.Persistence.SQLite` | schema、SQL、row mapping、transaction |
| `Ee4v.AssetManager.Infrastructure.Datasources` | datasource snapshotと共通読取処理 |
| `Ee4v.AssetManager.Infrastructure.Datasources.Blm` | BLM database adapter |
| `Ee4v.AssetManager.Infrastructure.Datasources.Eagle` | Eagle library / bridge adapter |
| `Ee4v.AssetManager.Infrastructure.Files` | filesystem、ZIP、cache |
| `Ee4v.AssetManager.Infrastructure.Unity` | Unity package importなどUnity固有adapter |
| `Ee4v.AssetManager.UI` | window、controller、view state、UI Toolkit presentation |
| `Ee4v.AssetManager.Composition` | settingsを含む依存構築とEditor lifecycleの唯一の入口 |

## Core namespace

Coreではnamespaceを機能境界、asmdefとdirectoryを依存レイヤ境界として使います。
たとえば `Ee4v.Core.Settings` はContracts、Services、Unity adapter、UI adapterに
またがりますが、依存方向はasmdefで固定されます。

| namespace | 役割 |
|---|---|
| `Ee4v.Core.Settings` | setting定義・service契約、instance service、永続化adapter、Settings UI |
| `Ee4v.Core.Localization` | catalog model、localizer契約、Unity非依存resolver |
| `Ee4v.Core.I18n` | catalog sourceとcomposition、UI向け `I18N` presentation facade |
| `Ee4v.Core.Injector` | registry契約、登録順service、Hierarchy / Project presentation |
| `Ee4v.Core.Background` | background activity契約、instance tracker、共有composition |
| `Ee4v.Core.Internal` | package pathやbootstrapなど公開対象外のfoundation |
| `Ee4v.Core.Internal.EditorAPI` | Unity internal APIのfallback付きfacade |
| `Ee4v.Core.Internal.EditorAPI.Backends` | reflection、SerializedObject、非公開型access |
| `Ee4v.UI` | feature非依存のUI Toolkit component、state、overlay |

## Testing namespace

| namespace | 役割 |
|---|---|
| `Ee4v.Testing.Contracts` | registrar、suite / case metadata、category |
| `Ee4v.Testing.Application` | test case発見、descriptor構築、run state model |
| `Ee4v.Testing.Infrastructure.Unity` | TypeCache、Unity Test Runner、SessionState |
| `Ee4v.Testing.Infrastructure.StaticAnalysis` | localizationとUI sourceの監査 |
| `Ee4v.Testing.UI` | Test List window、run stateのlocalizationと表示 |

各featureのtest namespaceは対応レイヤの末尾を `.Tests` とし、原則
`Ee4v.Testing.Contracts.Editor` だけを追加参照します。

## 依存ルール

- Domain / Application / ServicesからUnity、UI、SQLite、filesystemを参照しない
- UIはview stateの表示と入力通知に限定し、use caseをContracts経由で呼ぶ
- InfrastructureはUIを参照せず、Compositionだけが具象を組み立てる
- Unity internal APIは `Core.Internal.EditorAPI.Backends` 以外からreflectionしない
- static facadeはpresentationまたはcomposition入口に限定し、状態はinstance serviceが所有する
- Module間の旧namespace転送型や互換assemblyは追加しない

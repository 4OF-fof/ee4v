# Architecture

ee4v は機能単位のModule分離を維持し、その内側をClean Architectureの依存方向で
構成します。旧API、旧namespace、旧DBとの互換レイヤは置きません。

```text
AssetManager.Composition
  -> AssetManager.UI
       -> Ee4v.UI
  -> Core.Presentation
  -> AssetManager.Infrastructure
       -> AssetManager.Application
            -> AssetManager.Domain
            -> AssetManager.Contracts

Testing.UI
  -> Testing.Application / Testing.Contracts
  -> Ee4v.UI / Core.Presentation

Testing.Composition
  -> Testing.UI
  -> Testing.Infrastructure.Unity
       -> Testing.Application
            -> Testing.Contracts

UI.Catalog
  -> Ee4v.UI / AssetManager.UI / Testing.UI
  -> Core.Presentation

Core.Presentation
  -> Ee4v.UI
  -> Core.Unity / Core.Editor adapters
       -> Core.Services
            -> Core.Contracts

Ee4v.UI
  -> Core.Editor facade

Core.Unity / Core.Editor
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
| `DepthIndicator` | Hierarchyの親子関係を示す分岐ガイドを所有する小規模Module |
| `FolderContentOverlay` | Project folder直下の主要asset種別を示すoverlayを所有する小規模Module |
| `Core` | Settings、Localization、Injector、background activity、Unity internal facadeと、それらをEditor UIへ接続するPresentation |
| `UI` | feature非依存のUI Toolkit component、state、resource。Catalogは開発支援用の別assembly |
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
たとえば `Ee4v.Core.Settings` はContracts、Services、Unity adapter、
Presentationにまたがります。依存方向はasmdefで固定されます。

| namespace | 役割 |
|---|---|
| `Ee4v.Core.Settings` | setting定義・service契約、instance service、永続化adapter、Settings UI adapter |
| `Ee4v.Core.Localization` | catalog model、localizer契約、Unity非依存resolver |
| `Ee4v.Core.I18n` | catalog sourceとcomposition、UI向け `I18N` presentation facade |
| `Ee4v.Core.Injector` | registry契約、登録順service、Hierarchy / Project presentation |
| `Ee4v.Core.Background` | background activity契約、instance tracker、共有composition |
| `Ee4v.Core.Internal` | package pathやbootstrapなど公開対象外のfoundation |
| `Ee4v.Core.Internal.EditorAPI` | Unity internal APIのfallback付きfacade |
| `Ee4v.Core.Internal.EditorAPI.Backends` | reflection、SerializedObject、非公開型access |
| `Ee4v.UI` | feature非依存のUI Toolkit component、state、overlay |

## EditorEnhancements category

`Editor/EditorEnhancements` は小規模なEditor拡張ModuleがEditor直下へ散らばることを
防ぐための物理カテゴリです。`EditorEnhancements` 自体はModuleではなく、asmdef、
namespace、bootstrap、setting、Localization、Testを所有しません。

各子directoryが独立Moduleです。カテゴリ名はnamespaceとassembly名へ含めず、
LocalizationとTestも各Moduleの配下へ配置します。

| 配置 | namespace / assembly | 役割 |
|---|---|---|
| `Editor/EditorEnhancements/DepthIndicator` | `Ee4v.DepthIndicator` / `Ee4v.DepthIndicator.Editor` | Hierarchyの親子関係を示す分岐ガイド |
| `Editor/EditorEnhancements/FolderContentOverlay` | `Ee4v.FolderContentOverlay` / `Ee4v.FolderContentOverlay.Editor` | Project folder直下の主要asset種別を示すoverlay |

両Moduleは兄弟Moduleを参照せず、それぞれが `Core.Injector` を利用して描画callbackを
登録します。描画中にfilesystem走査やreflectionを行わず、
`FolderContentOverlay` のAssetDatabase検索結果はModule内でcacheします。子folderの
代表iconは同一iconが候補の過半数を占める場合だけ親へ伝播します。Texture、Material、
Mesh、Prefab、Modelの内容previewは安定した種別iconへ置換し、それ以外はasset固有の
iconを優先します。

`Ee4v.Core.Settings`、`Ee4v.Core.I18n`、`Ee4v.Core.Injector` のpresentation実装は
namespaceを機能境界として維持しつつ、物理配置とassemblyは
`Editor/Core/Presentation` / `Ee4v.Core.Presentation.Editor` に分離します。
Presentationだけが `Ee4v.UI.Editor` を参照し、CoreのContracts、Services、
Unity adapterはUIを参照しません。

## UI assembly

| assembly | 配置 | 役割 |
|---|---|---|
| `Ee4v.UI.Editor` | `Editor/UI` | Foundation、共通Component、resource。Core固有presentationやstoryを持たない |
| `Ee4v.Core.Presentation.Editor` | `Editor/Core/Presentation` | Settings UI、`I18N` facade、Injector presenter、background overlay host |
| `Ee4v.UI.Catalog.Editor` | `Editor/UI/Catalog` | Catalog window、helper、共通・module固有Componentのstory |

`StatusOverlay` のstateと表示Componentは `Ee4v.UI.Editor`、background activityを
監視するhostは `Ee4v.Core.Presentation.Editor` に置きます。共通Componentは
ローカライズserviceを直接呼ばず、表示文言をstateまたはconstructor引数で受け取ります。

## Testing namespace

| namespace | 役割 |
|---|---|
| `Ee4v.Testing.Contracts` | registrar、suite / case metadata、category |
| `Ee4v.Testing.Application` | test case発見、descriptor構築、run state model、catalog / runner port |
| `Ee4v.Testing.Infrastructure.Unity` | TypeCache、Unity Test Runner、SessionState |
| `Ee4v.Testing.Infrastructure.StaticAnalysis` | localizationとUI sourceの監査 |
| `Ee4v.Testing.UI` | Test List window、run stateのlocalizationと表示 |
| `Ee4v.Testing.Composition` | Unity実装をApplicationのportとしてUIへ注入するcomposition root |

各featureのtest namespaceは対応レイヤの末尾を `.Tests` とし、原則
`Ee4v.Testing.Contracts.Editor` だけを追加参照します。

`Testing.UI` は `Testing.Infrastructure.Unity` を直接参照しません。
catalogとrunnerは `Testing.Application` のportとして受け取り、具象の生成と注入は
`Testing.Composition` だけが行います。

## 依存ルール

- Domain / Application / ServicesからUnity、UI、SQLite、filesystemを参照しない
- UIはview stateの表示と入力通知に限定し、use caseをContracts経由で呼ぶ
- 共通UIはCore固有のI18N、Settings、Injector、background serviceを参照しない
- Catalogとstoryは本番の共通UI assemblyへ含めない
- AssetManager.UIとTesting.UIはCatalogを参照せず、Catalogが外側から各UIを参照する
- InfrastructureはUIを参照せず、Compositionだけが具象を組み立てる
- Unity internal APIは `Core.Internal.EditorAPI.Backends` 以外からreflectionしない
- static facadeはpresentationまたはcomposition入口に限定し、状態はinstance serviceが所有する
- Module間の旧namespace転送型や互換assemblyは追加しない

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
| `FolderStyle` | Project folderの色・アイコン装飾とAlt操作の編集入口を所有する小規模Module |
| `HierarchyStyle` | Hierarchy itemの継承背景色・アイコン・Alt操作による非表示入口を所有する小規模Module |
| `HiddenObjects` | HierarchyのScene見出しから非表示objectを検索・選択し、Undo対応で再表示する管理機能を所有する小規模Module |
| `ProjectTabs` | Project windowのfolder tab、tab別navigation history、現在位置追跡を所有する小規模Module |
| `SceneSwitcher` | HierarchyのScene見出しからSceneを検索・切替・作成する小規模Module |
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
| `Editor/EditorEnhancements/FolderStyle` | `Ee4v.FolderStyle` / `Ee4v.FolderStyle.Editor` | Project folderの色・アイコン装飾とAlt操作の編集入口 |
| `Editor/EditorEnhancements/HierarchyStyle` | `Ee4v.HierarchyStyle` / `Ee4v.HierarchyStyle.Editor` | Hierarchy itemの継承背景色・アイコン・Alt操作による非表示入口 |
| `Editor/EditorEnhancements/HiddenObjects` | `Ee4v.HiddenObjects` / `Ee4v.HiddenObjects.Editor` | `HideInHierarchy` objectの管理画面とScene見出しの入口 |
| `Editor/EditorEnhancements/ProjectTabs` | `Ee4v.ProjectTabs` / `Ee4v.ProjectTabs.Editor` | Project windowのfolder tabとtab別navigation history |
| `Editor/EditorEnhancements/SceneSwitcher` | `Ee4v.SceneSwitcher` / `Ee4v.SceneSwitcher.Editor` | Sceneの検索、優先表示、切替、作成 |

各Moduleは兄弟Moduleを参照せず、それぞれが `Core.Injector` を利用して描画callbackを
登録します。Hierarchyの非表示操作はCoreのinstance IDベース契約を介して連携します。
描画中にfilesystem走査やreflectionを行わず、
`FolderContentOverlay` のAssetDatabase検索結果はModule内でcacheします。子folderの
代表iconは同一iconが候補の過半数を占める場合だけ親へ伝播します。Texture、Material、
Mesh、Prefab、Modelの内容previewは安定した種別iconへ置換し、それ以外はasset固有の
iconを優先します。

`DepthIndicator` は `HideInHierarchy` が設定された子と兄弟を枝線の接続判定から除外し、
Hierarchyに表示されているobjectだけで終端と縦線を決定します。

`HiddenObjects` はScene走査と `HideFlags` / active state / tag / Undo操作をUnity adapterへ
閉じ込め、Applicationのcontrollerとtree builderはinstance IDとsnapshotだけを扱います。
非表示前の `activeSelf` とtagは `GlobalObjectId` ごとにUserSettingsへ保存し、非表示時は
`SetActive(false)` と `EditorOnly` tagを適用します。復帰時は `HideInHierarchy` を解除して
保存値へ戻します。Scene名とobject名の
除外patternはModule所有のUser settingとして保持し、既定ではNDMFのpreview Sceneと
activator objectを一覧から除外します。

`ProjectTabs` はtabごとに最大50件のfolder navigation historyを保持し、検索文字列の
変更は現在の履歴entryへ統合します。tab一覧と履歴は `UserSettings` に保存し、
Project windowごとの選択tabはwindow sessionだけで保持します。現在位置の読取と
folder表示・検索復元は `Core.Internal.EditorAPI.ProjectBrowser` facadeを介し、
対象のProject windowを明示して複数window間の誤操作を防ぎます。tabの並び替えは
Applicationのsessionへ最終indexだけを通知し、Project folderのdropはUnity adapterで
folder pathを検証してから新しいtabとして一括追加します。右クリックでpin留めした
tabは位置を変えずにfolder pathを固定し、別folderへのnavigationは通常tabを新規作成
します。同じfolder内の検索状態はpin tab自身へ反映します。pin tabと通常tabは同じ
並び替え領域で混在できます。AssetsとPackagesのrootを開くHome tabだけは例外として、
解除・削除・移動できない固定tabとしてtab列の左端へ常設します。通常tabが0件になっても
Home自体が残るため、代替のAssets tabは生成しません。HomeではAssetsとPackagesの
rootを切り替えられ、どちらかの配下へ移動した場合は通常tabを新規作成します。

`SceneSwitcher` はHierarchyのScene見出しを入口に、Assets配下のSceneを検索して
切り替えます。複数Sceneを読み込み中はpopupを開いたSceneのhandleを保持し、
一覧で選んだSceneをadditiveで読み込み、起点Sceneの直前へ移動してから起点Sceneだけを
閉じることで、Hierarchy上の位置を維持して置き換えます。対象がすでに
読み込み済みの場合も起点Sceneだけを閉じて対象をactiveにし、ほかのSceneは維持します。
一覧を右クリックした場合は起点Sceneを閉じず、対象Sceneをadditiveで追加してactiveにします。
読み込み中のScene、お気に入り、その他の順で表示し、
各group内では利用者の並び順を保持します。一覧の順序、お気に入り、除外状態は
Module所有のstoreへ保存し、Scene assetの追加・削除・移動時にcatalogを同期します。
検索語と同名のSceneがない場合は設定したAssets配下のfolder（既定値は
`Assets/Scene`）へ新規作成できます。
同じfolderに`TEMPLATE.unity`があればtemplateとして複製します。検索・同期・並び順の
判断はApplicationのpure logicへ置き、AssetDatabase、SceneManager、
EditorSceneManagerはUnity adapterへ隔離します。

`FolderStyle` はfolder GUIDをidentityとして色とicon asset GUIDをUserSettingsへ保存し、
Project item描画中は永続化やasset走査を行いません。Alt操作で開く編集windowは対象選択と
保存だけを担当し、色・iconのfieldは`Ee4v.UI`の`DecorationStyleEditor`へ分離します。
このcomponentは表示stateと変更eventだけを持ち、色presetや最近使ったicon候補の内容と
履歴は所有しません。編集UIは旧版のコンパクトな色paletteとicon fieldの操作順を基準にし、
任意色pickerと最近使ったicon候補を追加します。icon候補のhover previewには共通
`ImageTooltip`を使い、小さいTextureも縦横比を保って拡大します。候補の右clickは
確認menuを介さず履歴だけから即時削除しますが、選択対象のいずれかへ適用中の候補は
削除できません。icon履歴行の先頭には色paletteと同じ解除候補を常設し、履歴がない
場合もpopup高を固定します。
popup表示中の候補順はopen時のsession snapshotへ固定し、icon選択による利用順の更新は
次回open時に反映します。右click削除だけは明示操作としてsnapshotにも即時反映します。
候補間を移動する際は`ImageTooltip`の終了を短時間遅延し、次候補へのhoverでcancelして
focus保護に空白を作りません。
`FolderStyle` は最近使ったicon GUIDを最大8件UserSettingsへ保存し、
Texture候補へ解決してcomponentへ渡します。popupは領域外clickで閉じますが、Color Pickerと
Object Selectorへの一時的なfocus移動はCore EditorAPI facadeで判定して編集を継続します。
Alt押下時はmodifier変更によるProject windowの再描画で対象itemを検出し、マウス下の
Project windowへfocusを移してからpopupを開きます。同じcomponentは`HierarchyStyle`の
Hierarchy item装飾でも再利用します。

`HierarchyStyle` はscene objectの`GlobalObjectId`をidentityとして背景色とicon asset GUIDを
UserSettingsへ保存し、sceneへ専用componentを追加しません。背景色は対象自身から親へ向かって
最初に見つかった明示設定を使うため子孫へ伝播し、子に背景色がある場合は子の値を優先します。
iconは対象自身のHierarchy TreeView itemだけへCore EditorAPI facadeで適用し、
Scene側へは設定せず子へも継承しません。Alt操作で開く編集popupは`Ee4v.UI`の
`DecorationStyleEditor`を再利用します。非表示操作はCoreのinstance IDベース契約を介して
`HiddenObjects` moduleのUnity adapterへ委譲し、`HideInHierarchy`、非アクティブ化、
`EditorOnly` tagをUndo対応で設定します。復帰は同Moduleの管理画面から行い、保存していた
active stateとtagを復元します。Alt押下時はmodifier変更による
Hierarchy windowの再描画で対象itemを検出し、マウス下のHierarchy windowへfocusを
移してからpopupを開きます。

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

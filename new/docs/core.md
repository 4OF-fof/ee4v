# Core Overview

`Editor/Core` は feature から共通利用される基盤をまとめる領域です。  
feature 固有の実装や確認コードは各 feature 配下に置き、`Core` には複数 feature で再利用される API と、その成立を支える内部実装だけを置きます。

## Folder Roles

### `Editor/Core/Debug`
- 基盤 API を検査・診断するためのデバッグ UI を置く
- 現在は i18n の静的解析ウィンドウを提供する

### `Editor/Core/I18n`
- scope 解決付きのローカライズ API を置く
- localization ファイルの読み込み、JSONC 処理、アセット更新検知を担当する

### `Editor/Core/Injector`
- Hierarchy / Project への描画差し込み API を置く
- feature はこの層を経由して item / header / toolbar への描画を登録する

### `Editor/Core/Internal`
- package 内部でのみ使う補助コードを置く
- feature-facing API の裏側にある path 解決、初期化契約、内部 editor API の隔離を担当する

### `Editor/Core/Internal/EditorAPI`
- Unity Editor 内部 API に依存する facade を provider 単位で置く
- feature や他の Core 層は backend ではなくこの層を経由して使う

### `Editor/Core/Internal/EditorAPI/Backends`
- facade の裏側で reflection や serialized property を扱う実装を置く
- Unity 内部 API の不安定な依存点はこの層へ閉じ込める

### `Editor/Core/Localization`
- `Core` scope の翻訳データを置く
- `I18N` や `Settings` など Core 自身の UI 文言だけを保持する

### `Editor/Core/Settings`
- 設定の定義、保存、UI 自動描画を行う設定基盤を置く
- feature は `SettingDefinition` を登録して user / project 設定を利用する

### `Editor/Core/Testing`
- feature 単位の EditMode テスト登録、一覧化、実行管理を行う
- feature の test assembly から registrar を通して suite を登録する
- Unity Test Runner API 依存はこの層に閉じ込める

## File Roles

### `Editor/Core/Debug`
- `I18nAnalyzerWindow.cs`
  `I18N.Get/TryGet` と `SettingDefinition` 参照を静的解析し、missing / unused / duplicate / unresolved を確認するデバッグウィンドウ

### `Editor/Core/I18n`
- `CoreLocalizationDefinitions.cs`
  Core 自身が使う設定項目と翻訳キー定義を登録する
- `I18N.cs`
  呼び出し元 namespace から scope を解決して翻訳を返す公開 API
- `JsoncUtility.cs`
  localization ファイルの JSONC を通常 JSON として読めるように整形する補助
- `LocalizationAssetPostprocessor.cs`
  localization アセット更新時に catalog の再読込を促す
- `LocalizationCatalogLoader.cs`
  localization ファイル群を走査して locale / scope / key の catalog を構築する

### `Editor/Core/Injector`
- `InjectionChannel.cs`
  `HierarchyItem` / `HierarchyHeader` / `ProjectItem` / `ProjectToolbar` の描画チャネル定義
- `InjectionRegistration.cs`
  item 描画と VisualElement 描画の登録モデル定義
- `InjectorApi.cs`
  Unity callback 購読、登録 dispatch、Visual host 再構築、repaint 要求を一元管理する中核 API
- `ItemInjectionContext.cs`
  item 描画時に feature へ渡す context。対象オブジェクト、scene header 判定、Project item の見え方を提供する
- `VisualHostContext.cs`
  header / toolbar の VisualElement 生成時に渡す context

### `Editor/Core/Internal`
- `Ee4vPackageAnchor.cs`
  package root を逆引きするためのアンカーファイル
- `FeatureBootstrapContract.cs`
  feature の scope 名、namespace、definitions 型名の整合性を検証しつつ初期化する契約
- `PackagePathUtility.cs`
  package root、Editor root、Localization root、namespace からの scope 解決を行う

### `Editor/Core/Internal/EditorAPI`
- `ProjectBrowser.cs`
  `ProjectBrowser` provider の facade と DTO。snapshot 取得と folder/search 操作の feature-facing 入口

### `Editor/Core/Internal/EditorAPI/Backends`
- `ProjectBrowserBackend.cs`
  `ProjectBrowser` の内部 API / serialized property / 対象 window 解決を閉じ込める backend

### `Editor/Core/Localization`
- `en-US/core.jsonc`
  Core scope の英語文言
- `ja-JP/core.jsonc`
  Core scope の日本語文言

### `Editor/Core/Settings`
- `EditorPrefsSettingStore.cs`
  user スコープ設定を EditorPrefs に保存する store
- `DefaultEditorPrefsFacade.cs`
  `EditorPrefs` への実アクセスを隔離する facade 実装
- `DefaultFileSystem.cs`
  project 設定ファイル入出力の実ファイルアクセス実装
- `ISettingStore.cs`
  設定保存先の抽象インターフェース
- `IEditorPrefsFacade.cs`
  `EditorPrefs` 操作の抽象
- `IFileSystem.cs`
  ファイル / ディレクトリアクセスの抽象
- `ProjectFileSettingStore.cs`
  project スコープ設定を `ProjectSettings/ee4v.settings.json` に保存する store
- `RegisteredSettingsProviders.cs`
  Preferences / Project Settings に ee4v の設定画面を登録する
- `SettingApi.cs`
  設定定義の登録、値の取得・更新・保存、変更通知を担う中核 API
- `SettingDefinition.cs`
  設定のメタデータ、validator、custom drawer、serialize / deserialize を表現する
- `SettingDrawerContext.cs`
  custom drawer に渡す描画用 context
- `SettingFieldRenderer.cs`
  標準型の設定 UI を自動描画する
- `SettingScope.cs`
  `User` / `Project` の保存スコープ定義
- `SettingsUiRenderer.cs`
  登録済み定義から設定 UI 全体を構築する
- `SettingValidationResult.cs`
  validator の成功 / 失敗結果を表現する

### `Editor/Core/Testing`
- `IFeatureTestRegistrar.cs`
  test assembly 側の登録入口インターフェース
- `FeatureTestCaseAttribute.cs`
  各 `[Test]` メソッドに付ける表示用 metadata 属性
- `FeatureTestCaseDescriptor.cs`
  Test Manager に表示する個別テスト title / description の登録モデル
- `FeatureTestCaseDiscovery.cs`
  test assembly を走査して `[Test]` メソッドと `FeatureTestCaseAttribute` から個別テスト一覧を構築する
- `FeatureTestDescriptor.cs`
  feature test suite の表示名、scope、assembly 名、個別テスト一覧を持つ登録モデル
- `FeatureTestRegistry.cs`
  `TypeCache` で registrar を発見し、重複検証とソートを行う registry
- `UnityFeatureTestRunnerGateway.cs`
  Unity Test Runner API を呼ぶ gateway 実装
- `FeatureTestRunnerService.cs`
  feature 単位 / 全件実行の起動と直近結果の保持を担うサービス
- `FeatureTestManagerWindow.cs`
  suite 一覧、Run、Run All、直近結果を扱う管理ウィンドウ

## Usage Boundaries

- feature は Unity 内部 API に直接依存せず、必要なら `Core/Internal/EditorAPI` の facade を通す
- feature 固有の localization は各 feature 配下に置き、`Core/Localization` には置かない
- feature 固有の debug UI や確認コードは `Core/Debug` ではなく feature 配下に置く
- feature 固有のテストコードは `Editor/<Feature>/Test/Editor` に置き、`Core/Testing` に registrar で登録する
- `Core` に新しい要素を追加する時は、「複数 feature で再利用されるか」を基準に判断する

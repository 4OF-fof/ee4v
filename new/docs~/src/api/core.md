# Core API

Core API は feature 横断で使う公開契約です。feature 固有の状態や domain 仕様は含めず、bootstrap、Injector、Settings、I18N、Test 登録などの共通処理だけを扱います。

各 API は見出しに API 名を置き、直下のコードブロックにシグネチャを記載します。契約は `Parameters`、`Returns`、`Effects`、`Notes` の順で整理します。

## Feature Bootstrap

### `FeatureBootstrapContract.Initialize`

Feature の定義登録と初期化処理を Core の前提に沿って実行します。

```csharp
public static void Initialize(
    string featureScope,
    Type definitionsType,
    Action registerDefinitions,
    Action registerFeature)
```

Parameters:

- `featureScope`: feature の scope 名。`Sample` のような単一 scope 名を渡す。
- `definitionsType`: `<featureScope>Definitions` 型。
- `registerDefinitions`: settings、localization、test descriptor など feature 定義を登録する callback。
- `registerFeature`: Injector など runtime feature を登録する callback。不要なら `null` を渡せる。

Returns:

- `void`

Effects:

- `registerDefinitions` を実行する。
- `registerFeature` が `null` でなければ実行する。
- Core の命名規則に合わない bootstrap は例外で止める。

Notes:

- `featureScope` は空白不可。
- `registerDefinitions` は空不可。
- definitions 型名は `<featureScope>Definitions` にする。
- definitions 型は `Ee4v.<featureScope>` namespace に置く。
- 呼び出し側は `_initialized` guard を置き、domain reload 後の重複初期化を防ぐ。

```csharp
[InitializeOnLoad]
internal static class SampleBootstrap
{
    private static bool _initialized;

    static SampleBootstrap()
    {
        EnsureInitialized();
    }

    public static void EnsureInitialized()
    {
        if (_initialized)
        {
            return;
        }

        _initialized = true;
        var settings = CoreSettings.Current;
        FeatureBootstrapContract.Initialize(
            "Sample",
            typeof(SampleDefinitions),
            () => SampleDefinitions.RegisterAll(settings),
            () => SampleFeatureBootstrap.RegisterAll(settings));
    }
}
```

## Injector

### `InjectorApi.Register`

Hierarchy / Project / ProjectToolbar への描画 registration を登録します。

```csharp
public static IDisposable Register(InjectionRegistration registration)
```

Parameters:

- `registration`: `ItemInjectionRegistration` または `VisualElementInjectionRegistration`。

Returns:

- 登録解除に使う `IDisposable`。同じ `Id + Channel` の新しい登録に置換済みなら、
  古い戻り値を破棄しても新しい登録は解除しない。

Effects:

- `registration.Id + registration.Channel` をキーに registration を追加または上書きする。
- Unity非依存の `IInjectionRegistry` がregistration snapshotを更新する。
- `InjectionPresenter` が対象channelのcacheとhostを更新して再描画する。

Notes:

- `registration` が `null` の場合は `ArgumentNullException`。
- 同じ `Id` と `Channel` の registration は上書きされる。
- `Id` は安定した値にする。
- draw / create callback 内で `InjectorApi.Register(...)` を呼ばない。

```csharp
InjectorApi.Register(new ItemInjectionRegistration(
    id: "sample.hierarchy.badge",
    channel: InjectionChannel.HierarchyItem,
    draw: context =>
    {
        if (context.Target == null)
        {
            return;
        }

        var rect = context.CurrentRect;
        rect.xMin = rect.xMax - 48f;
        GUI.Label(rect, I18N.Get("hierarchy.badge"));
        context.CurrentRect = new Rect(
            context.CurrentRect.x,
            context.CurrentRect.y,
            context.CurrentRect.width - 52f,
            context.CurrentRect.height);
    },
    priority: 10,
    isEnabled: () => settings.Get(SampleDefinitions.HierarchyBadgeEnabled)));
```

### `InjectorApi.Unregister`

指定したregistration instanceが現在登録中なら解除します。

```csharp
public static bool Unregister(InjectionRegistration registration)
```

Returns:

- 解除した場合は `true`。同じkeyの別instanceへ置換済み、または未登録なら `false`。

Notes:

- 一時登録では `Register` が返す `IDisposable` の利用を優先する。
- instance単位の解除なので、古い所有者が新しい登録を誤って解除しない。

### `InjectorApi.Repaint`

Injector の表示に影響する変更後、対象 channel を再描画します。

```csharp
public static void Repaint(InjectionChannel channel)
```

Parameters:

- `channel`: 再描画対象。`HierarchyItem`、`ProjectItem`、`ProjectToolbar` のいずれか。

Returns:

- `void`

Effects:

- `HierarchyItem` の場合は Hierarchy window を再描画する。
- `ProjectItem` または `ProjectToolbar` の場合は Project window を再描画する。
- `ProjectToolbar` の場合は toolbar host を dirty にする。

Notes:

- setting 変更に伴う再描画は注入された `ISettingsService.Changed` から呼ぶ。
- feature 側で host window を直接探して再構築しない。

```csharp
settings.Changed += (_, args) =>
{
    if (ReferenceEquals(args.Definition, SampleDefinitions.HierarchyBadgeEnabled))
    {
        InjectorApi.Repaint(InjectionChannel.HierarchyItem);
    }
};
```

### `ItemInjectionRegistration`

Hierarchy / Project の 1 行に IMGUI 描画を差し込む registration です。

```csharp
public ItemInjectionRegistration(
    string id,
    InjectionChannel channel,
    Action<ItemInjectionContext> draw,
    int priority = 0,
    Func<bool> isEnabled = null)
```

Parameters:

- `id`: channel 内で一意な registration ID。
- `channel`: `HierarchyItem` または `ProjectItem`。
- `draw`: 描画 callback。
- `priority`: 同一 channel 内の実行順。
- `isEnabled`: 有効判定 callback。`null` の場合は常に有効。

Returns:

- `ItemInjectionRegistration`

Effects:

- constructor 自体は登録しない。`InjectorApi.Register(...)` に渡した時点で登録される。

Notes:

- `draw` は idempotent に保つ。
- badge や overlay を描いた後は、必要に応じて `context.CurrentRect` を更新して後続 registration と領域を分ける。

### `VisualElementInjectionRegistration`

ProjectToolbar の host に UI Toolkit element を差し込む registration です。

```csharp
public VisualElementInjectionRegistration(
    string id,
    InjectionChannel channel,
    Func<VisualHostContext, VisualElement> createElement,
    int priority = 0,
    Func<bool> isEnabled = null)
```

Parameters:

- `id`: channel 内で一意な registration ID。
- `channel`: `ProjectToolbar`。
- `createElement`: host に追加する `VisualElement` を作る callback。
- `priority`: 同一 channel 内の実行順。
- `isEnabled`: 有効判定 callback。`null` の場合は常に有効。

Returns:

- `VisualElementInjectionRegistration`

Effects:

- constructor 自体は登録しない。`InjectorApi.Register(...)` に渡した時点で登録される。

Notes:

- `createElement` が `null` を返した場合は何も追加しない。
- host 自体の生成、再構築、window 追跡は `InjectionPresenter` が管理する。
- Unityの非公開ProjectBrowser型へのaccessは `EditorAPI/Backends` に隔離される。

```csharp
InjectorApi.Register(new VisualElementInjectionRegistration(
    id: "sample.project-toolbar.button",
    channel: InjectionChannel.ProjectToolbar,
    createElement: context =>
    {
        var button = new Button(() => Debug.Log(I18N.Get("toolbar.clicked")))
        {
            text = I18N.Get("toolbar.button")
        };
        return button;
    },
    priority: 0,
    isEnabled: () => settings.Get(SampleDefinitions.ProjectToolbarEnabled)));
```

## Settings

### `SettingDefinition<T>`

Unity非依存のSetting定義を作成します。

```csharp
public SettingDefinition(
    string key,
    SettingScope scope,
    string localizationScope,
    string sectionKey,
    string displayNameKey,
    string descriptionKey,
    T defaultValue,
    int order = 0,
    Func<T, SettingValidationResult> validator = null,
    SettingRange<T> range = null,
    IReadOnlyList<string> keywords = null,
)
```

Parameters:

- `key`: 永続化に使う setting key。
- `scope`: `SettingScope.User` または `SettingScope.Project`。
- `localizationScope`: 表示keyを解決するscope。`Sample` などを明示する。
- `sectionKey`: settings 画面の section localization key。
- `displayNameKey`: 項目名 localization key。
- `descriptionKey`: tooltip localization key。
- `defaultValue`: 未保存時、deserialize 失敗時、invalid 保存値の fallback。
- `order`: section 内の並び順。
- `validator`: 値検証 callback。
- `range`: 値の最小値と最大値。
- `keywords`: settings 検索用 keyword。

Returns:

- `SettingDefinition<T>`

Effects:

- constructor 自体は登録も保存もしない。

Notes:

- 定義は原則 `Editor/<Feature>/<Feature>Definitions.cs` に置く。
- `SettingDefinition<T>` はUnity、UI Toolkit、JSON、filesystemを参照しない。
- custom drawerはpresentation側の `SettingDrawerRegistry` に登録する。

```csharp
internal static class SampleDefinitions
{
    public static readonly SettingDefinition<bool> ProjectToolbarEnabled =
        new SettingDefinition<bool>(
            key: "sample.projectToolbar.enabled",
            scope: SettingScope.User,
            localizationScope: "Sample",
            sectionKey: "settings.section.projectToolbar",
            displayNameKey: "settings.projectToolbar.enabled.name",
            descriptionKey: "settings.projectToolbar.enabled.description",
            defaultValue: true,
            order: 10,
            keywords: new[] { "project", "toolbar" });

    public static void RegisterAll(ISettingsService settings)
    {
        settings.Register(ProjectToolbarEnabled);
    }
}
```

### `ISettingsService`

設定の登録、取得、更新、保存を表すinstance契約です。

```csharp
public interface ISettingsService
{
    event EventHandler<SettingChangedEventArgs> Changed;

    void Register(SettingDefinitionBase definition);
    IReadOnlyList<SettingDefinitionBase> GetDefinitions(SettingScope scope);
    void Preload(SettingScope scope);
    T Get<T>(SettingDefinition<T> definition);
    object Get(SettingDefinitionBase definition);
    void Set<T>(SettingDefinition<T> definition, T value, bool saveImmediately = true);
    void Set(SettingDefinitionBase definition, object value, bool saveImmediately = true);
    void Save(SettingScope? scope = null);
}
```

Notes:

- 同じkeyで別instanceを登録すると `InvalidOperationException`。
- 保存値がない、deserialize失敗、validation失敗の場合はdefault値を返す。
- `Changed` は更新成功後に呼ばれ、`SettingChangedEventArgs.Definition` と `Value` を持つ。
- featureのDomain / Application / UIは `CoreSettings.Current` を直接参照しない。
- Compositionで現在のserviceを取得し、adapterへconstructor injectionする。

```csharp
var settings = CoreSettings.Current;
SampleDefinitions.RegisterAll(settings);
var adapter = new SamplePreferencesAdapter(settings);
```

## I18N

`I18N` は `Ee4v.Core.Presentation.Editor` に置くpresentation adapterです。scope解決後は
`CoreLocalization.Current.ForScope(scope)` が返す `ILocalizer` へ委譲します。
解決本体の `LocalizationService` はUnity非依存です。

### `I18N.Get`

caller の source file から scope を解決し、localization 文言を取得します。

```csharp
public static string Get(
    string key,
    [CallerFilePath] string callerFilePath = null)
```

Parameters:

- `key`: localization key。
- `callerFilePath`: scope 解決用。通常は指定しない。

Returns:

- 現在 locale の文言。
- 現在 locale に key がない場合は fallback locale の文言。
- どの locale にも key がない場合は `key` 文字列。

Effects:

- 初回取得時に `ILocalizationCatalogSource` からcatalogを読み込む。
- diagnostics adapterがduplicate keyを報告する。

Notes:

- 永続表示文言はこの API を通す。
- `Ee4v.<Scope>` namespace から呼ぶと `<Scope>` の localization を参照する。

```csharp
var label = I18N.Get("toolbar.button");
```

### `I18N.Get`

caller の stack frame から scope を解決し、localization 文言を format して取得します。

```csharp
public static string Get(string key, params object[] args)
```

Parameters:

- `key`: localization key。
- `args`: `string.Format(...)` に渡す format arguments。

Returns:

- format 済みの文言。
- key が未定義の場合は `key` 文字列。
- format に失敗した場合も `key` 文字列。

Effects:

- 初回取得時に localization catalog を読み込む。
- caller scope が解決できない場合は Unity console に warning を出す。

Notes:

- args なしの通常取得では `I18N.Get(string key, string callerFilePath = null)` が使われる。
- format が必要なときだけこの overload を使う。

```csharp
var message = I18N.Get("import.completed", importedCount);
```

### `I18N.TryGet`

localization 文言を取得できるか試します。未定義 key を UI にそのまま出したくない場合に使います。

```csharp
public static bool TryGet(
    string key,
    out string value,
    [CallerFilePath] string callerFilePath = null)
```

Parameters:

- `key`: localization key。
- `value`: 取得に成功した文言。失敗時は `null`。
- `callerFilePath`: scope 解決用。通常は指定しない。

Returns:

- `true`: 現在 locale または fallback locale から文言を取得できた。
- `false`: scope または key を解決できなかった。

Effects:

- 初回取得時に localization catalog を読み込む。

Notes:

- fallback しても見つからない場合、`Get(...)` と違って key 文字列は返さない。

```csharp
if (I18N.TryGet("optional.tooltip", out var tooltip))
{
    element.tooltip = tooltip;
}
```

### `I18N.GetAvailableLanguages`

読み込まれている localization catalog の locale 一覧を返します。

```csharp
public static IReadOnlyList<string> GetAvailableLanguages()
```

Parameters:

- なし

Returns:

- locale 名の一覧。名前順で返る。

Effects:

- 初回呼び出し時に localization catalog を読み込む。

Notes:

- language selector など、利用可能 locale を表示する UI で使う。

### `I18N.Reload`

localization serviceへreloadを通知します。

```csharp
public static void Reload()
```

Parameters:

- なし

Returns:

- `void`

Effects:

- localization catalog cache を破棄する。
- caller namespace scope cache を消す。
- serviceのreload eventをpresentation adapterが受け取る。
- presentation adapterがProjectToolbarとUnity view全体を再描画する。

Notes:

- `Localization` 配下のasset変更時は `LocalizationAssetPostprocessor` が
  `CoreLocalization.Current.Reload()` を呼ぶ。
- feature 側で独自の localization reload 実装を持たない。

## Testing

Testing API はCore assemblyではなく、Unity非依存の
`Ee4v.Testing.Contracts.Editor` / `Ee4v.Testing.Contracts` にあります。
featureのtest assemblyはこのContractsだけを参照し、Application、
Unity Test Runner adapter、UIを直接参照しません。

### `IFeatureTestRegistrar.CreateDescriptor`

Feature test suite の metadata を `Test List` に提供します。

```csharp
public interface IFeatureTestRegistrar
{
    FeatureTestDescriptor CreateDescriptor();
}
```

Parameters:

- なし

Returns:

- `FeatureTestDescriptor`

Effects:

- registrar 自体は直接登録処理を持たない。
- `FeatureTestRegistry` が `*TestRegistrar` を自動発見して descriptor を収集する。

Notes:

- registrar class 名は `*TestRegistrar` で終わる必要がある。
- `FeatureScope` と `AssemblyName` は全 suite で重複禁止。

```csharp
public sealed class SampleTestRegistrar : IFeatureTestRegistrar
{
    public FeatureTestDescriptor CreateDescriptor()
    {
        return new FeatureTestDescriptor(
            featureScope: "Sample",
            displayName: "Sample",
            assemblyName: "Ee4v.Sample.Tests.Editor",
            description: "Sample feature tests",
            order: 100,
            category: FeatureTestCategory.Standard);
    }
}
```

### `FeatureTestDescriptor`

`Test List` に表示する suite metadata です。

```csharp
public FeatureTestDescriptor(
    string featureScope,
    string displayName,
    string assemblyName,
    string description = "",
    int order = 0,
    IReadOnlyList<FeatureTestCaseDescriptor> testCases = null,
    FeatureTestCategory category = FeatureTestCategory.Standard)
```

Parameters:

- `featureScope`: suite 識別子。
- `displayName`: `Test List` 表示名。
- `assemblyName`: 実行対象 asmdef 名。
- `description`: suite 説明。
- `order`: suite 並び順。
- `testCases`: 明示的に渡す test case descriptor。通常は discovery に任せる。
- `category`: suite 分類。

Returns:

- `FeatureTestDescriptor`

Effects:

- `testCases` を `Order`、`Title` の順に sort して保持する。
- required field が空の場合は例外を投げる。

Notes:

- `testCases` は通常 `FeatureTestCaseAttribute` から自動発見される。

### `FeatureTestCaseAttribute`

NUnit test method に `Test List` 用の case metadata を付与します。

```csharp
public FeatureTestCaseAttribute(
    string title,
    string description = "",
    int order = 0,
    FeatureTestCategory category = FeatureTestCategory.Standard)
```

Parameters:

- `title`: case 表示名。
- `description`: case 説明。
- `order`: case 並び順。
- `category`: case 分類。

Returns:

- `FeatureTestCaseAttribute`

Effects:

- test method metadata として保持され、`FeatureTestCaseDiscovery` から参照される。

Notes:

- attribute を付けなくても NUnit test の実行自体は可能。
- `Test List` 上の説明を揃えるため、feature test には原則付与する。

```csharp
[Test]
[FeatureTestCase(
    title: "Project toolbar registration is valid",
    description: "Sample ProjectToolbar registration can be created.",
    order: 10)]
public void ProjectToolbarRegistrationIsValid()
{
    var registration = new VisualElementInjectionRegistration(
        "sample.project-toolbar.button",
        InjectionChannel.ProjectToolbar,
        _ => new Button());

    Assert.That(registration.Id, Is.EqualTo("sample.project-toolbar.button"));
}
```

# Core API

Core API は feature 横断で使う公開契約です。feature 固有の状態や domain 仕様は含めず、bootstrap、Injector、Settings、I18N、Test 登録などの共通処理だけを扱います。

各 API は見出しに API 名を置き、直下のコードブロックにシグネチャを記載します。契約は `Parameters`、`Returns`、`Effects`、`Notes` の順で整理します。

## Feature Bootstrap

### `FeatureBootstrapContract.Initialize`

Feature の定義登録と初期化処理を Core の前提に沿って実行します。

```csharp
public static void Initialize(
    string featureScope,
    Action registerDefinitions,
    Action registerFeature)
```

Parameters:

- `featureScope`: feature の scope 名。`Sample` のような単一 scope 名を渡す。
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
- `registerDefinitions` は `SampleDefinitions.RegisterAll` のように definitions 型の method group を渡す。
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
        FeatureBootstrapContract.Initialize(
            "Sample",
            SampleDefinitions.RegisterAll,
            SampleFeatureBootstrap.RegisterAll);
    }
}
```

## Injector

### `InjectorApi.Register`

Hierarchy / Project / ProjectToolbar への描画 registration を登録します。

```csharp
public static void Register(InjectionRegistration registration)
```

Parameters:

- `registration`: `ItemInjectionRegistration` または `VisualElementInjectionRegistration`。

Returns:

- `void`

Effects:

- `registration.Id + registration.Channel` をキーに registration を追加または上書きする。
- registration cache を更新する。
- ProjectToolbar host を dirty にする。
- 登録対象 channel を再描画する。

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
    isEnabled: () => SettingApi.Get(SampleDefinitions.HierarchyBadgeEnabled)));
```

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

- setting 変更に伴う再描画は `SettingApi.Changed` から呼ぶ。
- feature 側で host window を直接探して再構築しない。

```csharp
SettingApi.Changed += (definition, _) =>
{
    if (ReferenceEquals(definition, SampleDefinitions.HierarchyBadgeEnabled))
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
- host 自体の生成、再構築、window 追跡は `InjectorApi` が管理する。

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
    isEnabled: () => SettingApi.Get(SampleDefinitions.ProjectToolbarEnabled)));
```

## Settings

### `SettingDefinition<T>`

Setting の定義を作成します。

```csharp
public SettingDefinition(
    string key,
    SettingScope scope,
    string sectionKey,
    string displayNameKey,
    string descriptionKey,
    T defaultValue,
    int order = 0,
    Func<T, SettingValidationResult> validator = null,
    Func<SettingDrawerContext<T>, T> customDrawer = null,
    IReadOnlyList<string> keywords = null,
    [CallerFilePath] string definitionSourceFilePath = "")
```

Parameters:

- `key`: 永続化に使う setting key。
- `scope`: `SettingScope.User` または `SettingScope.Project`。
- `sectionKey`: settings 画面の section localization key。
- `displayNameKey`: 項目名 localization key。
- `descriptionKey`: tooltip localization key。
- `defaultValue`: 未保存時、deserialize 失敗時、invalid 保存値の fallback。
- `order`: section 内の並び順。
- `validator`: 値検証 callback。
- `customDrawer`: 標準 field で足りない場合の描画 callback。
- `keywords`: settings 検索用 keyword。
- `definitionSourceFilePath`: scope 解決用。通常は指定しない。

Returns:

- `SettingDefinition<T>`

Effects:

- constructor 自体は登録も保存もしない。
- `definitionSourceFilePath` から localization scope を解決して保持する。

Notes:

- 定義は原則 `Editor/<Feature>/<Feature>Definitions.cs` に置く。
- localization scope を崩さないため、別 namespace の util file に逃がさない。

```csharp
internal static class SampleDefinitions
{
    public static readonly SettingDefinition<bool> ProjectToolbarEnabled =
        new SettingDefinition<bool>(
            key: "sample.projectToolbar.enabled",
            scope: SettingScope.User,
            sectionKey: "settings.section.projectToolbar",
            displayNameKey: "settings.projectToolbar.enabled.name",
            descriptionKey: "settings.projectToolbar.enabled.description",
            defaultValue: true,
            order: 10,
            keywords: new[] { "project", "toolbar" });

    private static bool _registered;

    public static void RegisterAll()
    {
        if (_registered)
        {
            return;
        }

        _registered = true;
        SettingApi.Register(ProjectToolbarEnabled);
    }
}
```

### `SettingApi.Register`

Setting 定義を登録します。

```csharp
public static void Register(SettingDefinitionBase definition)
```

Parameters:

- `definition`: 登録する setting 定義。

Returns:

- `void`

Effects:

- key が未登録なら定義を追加する。
- scope がすでに load 済みの場合は、その定義の値も cache に読み込む。

Notes:

- `definition` が `null` の場合は `ArgumentNullException`。
- 同じ key で別 instance を登録すると `InvalidOperationException`。
- 同じ instance の再登録は no-op。

### `SettingApi.Get<T>`

登録済み setting の現在値を取得します。

```csharp
public static T Get<T>(SettingDefinition<T> definition)
```

Parameters:

- `definition`: 取得対象の setting 定義。

Returns:

- 保存済みの値。
- 保存値がない、deserialize に失敗した、または validator が invalid を返した場合は `definition` の default value。

Effects:

- 未登録の definition は自動登録される。
- scope が未 load の場合は store から値を読み込んで cache する。

Notes:

- feature 側で `EditorPrefs` や `ProjectSettings/ee4v.settings.json` を直接読まない。

```csharp
if (SettingApi.Get(SampleDefinitions.ProjectToolbarEnabled))
{
    InjectorApi.Repaint(InjectionChannel.ProjectToolbar);
}
```

### `SettingApi.Set<T>`

登録済み setting の値を更新します。

```csharp
public static void Set<T>(
    SettingDefinition<T> definition,
    T value,
    bool saveImmediately = true)
```

Parameters:

- `definition`: 更新対象の setting 定義。
- `value`: 保存する値。
- `saveImmediately`: `true` の場合は即座に store へ保存する。

Returns:

- `void`

Effects:

- 未登録の definition は自動登録される。
- validator が invalid を返した場合は `InvalidOperationException`。
- cache を更新し、scope を dirty にする。
- `saveImmediately` が `true` の場合は保存する。
- `SettingApi.Changed` を発火する。

Notes:

- `User` scope は `EditorPrefs` に保存される。
- `Project` scope は `ProjectSettings/ee4v.settings.json` に保存される。

```csharp
SettingApi.Set(SampleDefinitions.ProjectToolbarEnabled, false);
```

### `SettingApi.Save`

dirty な setting cache を store へ保存します。

```csharp
public static void Save(SettingScope? scope = null)
```

Parameters:

- `scope`: 保存対象 scope。`null` の場合は dirty な全 scope を保存する。

Returns:

- `void`

Effects:

- 対象 scope の値を serialize して store へ保存する。
- 保存済み scope を dirty から外す。

Notes:

- `SettingApi.Set(..., saveImmediately: false)` を使った場合は、後で `Save(...)` を呼ぶ。

```csharp
SettingApi.Set(SampleDefinitions.ProjectToolbarEnabled, true, saveImmediately: false);
SettingApi.Save(SettingScope.User);
```

## I18N

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

- 初回取得時に localization catalog を読み込む。
- duplicate key があれば Unity console に error を出す。

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

localization catalog と scope cache を破棄し、表示を再描画します。

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
- duplicate key warning 状態を消す。
- ProjectToolbar と Unity view 全体を再描画する。

Notes:

- `Localization` 配下の asset 変更時は `LocalizationAssetPostprocessor` が呼び出す。
- feature 側で独自の localization reload 実装を持たない。

## Testing

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

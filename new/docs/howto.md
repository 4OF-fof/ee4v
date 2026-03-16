# How To Add A New Feature

このドキュメントは、`new` 配下に新しい feature を追加するための実装手順書です。  
ここに書かれている手順だけで、`Editor/Core` の基盤 API を使った feature を 1 つ追加できる構成にしています。

例として feature 名を `MyFeature` として説明します。  
実際の scope 名は `Phase1`、`Phase2` などに置き換えてください。

## 1. 追加するもの

新しい feature を追加する時は、最低限次の 4 つを用意します。

1. `Editor/<Feature>/Localization`
2. `Editor/<Feature>/<Feature>Definitions.cs`
3. `Editor/<Feature>/<Feature>Bootstrap.cs`
4. feature 固有の実装コード
5. `Editor/<Feature>/Test/Editor`

`Definitions` と `Bootstrap` は必須です。  
`Bootstrap` から `typeof(<Feature>Definitions)` を参照するため、`<Feature>Definitions` 型が無ければコンパイルエラーになります。
テストを追加する場合は `Test/Editor` 配下に Editor test asmdef と registrar を置きます。

## 2. フォルダと namespace を決める

feature 名を 1 つ決めます。  
この feature 名は次の 3 箇所で一致させます。

- フォルダ名: `Editor/<Feature>`
- localization scope 名: `Editor/<Feature>/Localization`
- namespace 第2セグメント: `Ee4v.<Feature>`

例:

- フォルダ: `Editor/MyFeature`
- namespace: `Ee4v.MyFeature`
- localization scope: `MyFeature`

使う namespace は `Ee4v.<Feature>` または `Ee4v.<Feature>.*` です。  
`Core` 側は `Ee4v.Core.*` を使います。

## 3. Localization を置く

まず翻訳ファイルを置きます。

```text
Editor/
  MyFeature/
    Localization/
      en-US/
        settings.jsonc
        injector.jsonc
      ja-JP/
        settings.jsonc
        injector.jsonc
```

ルール:

- `Core` の文言は `Core/Localization` に置く
- feature の文言は `Feature/Localization` に置く
- 別 scope の文言は参照しない
- fallback は同一 scope 内で `選択言語 -> fallback言語 -> en-US -> key`

例:

```jsonc
{
  "settings": {
    "section": {
      "myFeature": "My Feature"
    },
    "enabled": {
      "label": "Enable My Feature",
      "tooltip": "Turns this feature on."
    }
  }
}
```

## 4. Definitions を作る

`Editor/<Feature>/<Feature>Definitions.cs` を作成します。  
ここに設定定義を集約します。

```csharp
using Ee4v.Core.I18n;
using Ee4v.Core.Settings;
using UnityEngine;

namespace Ee4v.MyFeature
{
    internal static class MyFeatureDefinitions
    {
        private static bool _registered;

        public static readonly SettingDefinition<bool> EnableFeature = new SettingDefinition<bool>(
            "myFeature.enabled",
            SettingScope.User,
            "settings.section.myFeature",
            "settings.enabled.label",
            "settings.enabled.tooltip",
            true,
            order: 0);

        public static void RegisterAll()
        {
            if (_registered)
            {
                return;
            }

            _registered = true;
            SettingApi.Register(EnableFeature);
        }
    }
}
```

ポイント:

- namespace は `Ee4v.<Feature>`
- 型名は必ず `<Feature>Definitions`
- `RegisterAll()` に全設定を登録する
- `SettingDefinition` の localization scope は定義元 namespace から自動決定される
- `displayNameKey` と `descriptionKey` には localization key を渡す

validator が必要な場合:

```csharp
validator: value => string.IsNullOrWhiteSpace(value)
    ? SettingValidationResult.Error(I18N.Get("settings.validation.required"))
    : SettingValidationResult.Success
```

## 5. Bootstrap を作る

`Editor/<Feature>/<Feature>Bootstrap.cs` を作成します。  
このファイルが feature の初期化入口です。

```csharp
using Ee4v.Core.Internal;
using UnityEditor;

namespace Ee4v.MyFeature
{
    [InitializeOnLoad]
    internal static class MyFeatureBootstrap
    {
        private static bool _initialized;

        static MyFeatureBootstrap()
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
                "MyFeature",
                typeof(MyFeatureDefinitions),
                MyFeatureDefinitions.RegisterAll,
                MyFeatureRuntime.RegisterAll);
        }
    }
}
```

ポイント:

- 型名は必ず `<Feature>Bootstrap`
- namespace は `Ee4v.<Feature>`
- `FeatureBootstrapContract.Initialize(...)` を使う
- 第1引数の scope 名は feature 名と一致させる
- 第2引数は `typeof(<Feature>Definitions)`
- 第3引数は `Definitions.RegisterAll`
- 第4引数は feature 固有の初期化処理

第4引数に渡す処理が不要なら、空メソッドを 1 つ作って渡して構いません。

## 6. 通常コードで文言を参照する

feature 内の通常コードでは `I18N.Get(...)` を使います。

```csharp
var label = I18N.Get("settings.enabled.label");
```

この呼び出しは、呼び出し元 namespace の `Ee4v.<Scope>` から scope を自動解決します。

例:

- `Ee4v.MyFeature` から呼ぶと `MyFeature/Localization` を参照する
- `Ee4v.Core.I18n` から呼ぶと `Core/Localization` を参照する

別 scope の localization には fallback しません。

## 7. Settings UI に出したい場合

`Definitions` に `SettingDefinition<T>` を追加して `SettingApi.Register(...)` すれば、Preferences / Project Settings の UI は自動生成されます。

選択ルール:

- ユーザー単位の設定: `SettingScope.User`
- プロジェクト単位の設定: `SettingScope.Project`

通常項目は自動描画されます。  
特殊な UI が必要な時だけ `customDrawer` を使います。

## 8. Injector を使う場合

Hierarchy / Project への描画追加は `InjectorApi.Register(...)` を使います。

例:

```csharp
using Ee4v.Core.Injector;
using Ee4v.Core.Settings;

namespace Ee4v.MyFeature
{
    internal static class MyFeatureRuntime
    {
        private static bool _registered;

        public static void RegisterAll()
        {
            if (_registered)
            {
                return;
            }

            _registered = true;

            InjectorApi.Register(new ItemInjectionRegistration(
                "myFeature.hierarchy.item",
                InjectionChannel.HierarchyItem,
                DrawHierarchyItem,
                priority: 100,
                isEnabled: () => SettingApi.Get(MyFeatureDefinitions.EnableFeature)));
        }

        private static void DrawHierarchyItem(ItemInjectionContext context)
        {
        }
    }
}
```

ポイント:

- 有効 / 無効の判定は feature 側で `SettingApi` を使って決める
- `priority` で描画順を制御する
- `InjectorApi` は登録、dispatch、再描画要求を担当する

`ItemInjectionContext` からは次の情報を取れます。

Hierarchy:

- `context.IsHierarchySceneHeader`
- `context.IsHierarchyGameObject`
- `context.HierarchyItemKind`
- `context.HierarchyScene`

Project:

- `context.ProjectViewMode`
- `context.ProjectOrientation`

## 9. 動作確認用コードを置く

API の成立性確認や判定可視化コードは feature フォルダ内に置きます。  
確認用のコードも feature の一部として扱ってください。

例:

- `Editor/MyFeature/MyFeatureRuntime.cs`
- `Editor/MyFeature/MyFeatureDebugView.cs`

`Core` 側には feature 固有の確認コードを置きません。

## 10. EditMode テストを置く

feature ごとの EditMode テストは `Editor/<Feature>/Test/Editor` に置きます。

最小構成:

```text
Editor/
  MyFeature/
    Test/
      Editor/
        Ee4v.MyFeature.Tests.Editor.asmdef
        MyFeatureTestRegistrar.cs
        MyFeatureTests.cs
```

ルール:

- test asmdef 名は `Ee4v.<Feature>.Tests.Editor`
- test asmdef は `Ee4v.Editor` を参照する
- `optionalUnityReferences` に `TestAssemblies` を入れる
- registrar は `IFeatureTestRegistrar` を実装し、descriptor を 1 つ返す
- feature の suite は Core の `Debug/ee4v Test Manager` に自動表示される

例:

```csharp
using Ee4v.Core.Testing;

namespace Ee4v.MyFeature.Tests
{
    public sealed class MyFeatureTestRegistrar : IFeatureTestRegistrar
    {
        public FeatureTestDescriptor CreateDescriptor()
        {
            return new FeatureTestDescriptor(
                "MyFeature",
                "MyFeature",
                "Ee4v.MyFeature.Tests.Editor",
                "MyFeature edit mode tests.");
        }
    }
}
```

## 11. Analyzer で確認する

`Debug/I18n Analyzer` で次を確認できます。

- 未定義キー
- 未使用キー
- 同一 locale 内の文言重複
- 同一 scope 内の重複キー
- scope 解決できない namespace の参照

初期実装で検査対象になるのは次です。

- 静的文字列リテラルの `I18N.Get("...")`
- 静的文字列リテラルの `I18N.TryGet("...")`
- `SettingDefinition` の `sectionKey`
- `SettingDefinition` の `displayNameKey`
- `SettingDefinition` の `descriptionKey`

動的に連結したキーは検査対象外です。

## 12. 実装チェックリスト

新しい feature を追加したら、次を順に確認します。

1. `Editor/<Feature>` フォルダを作成した
2. namespace を `Ee4v.<Feature>` または `Ee4v.<Feature>.*` にした
3. `Localization/<locale>/*.jsonc` を配置した
4. `<Feature>Definitions.cs` を作成した
5. `<Feature>Bootstrap.cs` を作成した
6. `FeatureBootstrapContract.Initialize(..., typeof(<Feature>Definitions), ...)` を呼んだ
7. 必要な `SettingDefinition` を `RegisterAll()` で登録した
8. 必要なら `InjectorApi.Register(...)` を追加した
9. テストを追加するなら `Test/Editor` と registrar を作成した
10. `Debug/I18n Analyzer` で missing / unused / unresolved が無いことを確認した

## 13. 最小構成

最小構成は次です。

```text
Editor/
  MyFeature/
    Localization/
      en-US/
        settings.jsonc
      ja-JP/
        settings.jsonc
    MyFeatureDefinitions.cs
    MyFeatureBootstrap.cs
```

この構成があれば、Settings ベースの新規 feature を追加できます。  
Injector や確認用コードが必要な場合だけ追加してください。

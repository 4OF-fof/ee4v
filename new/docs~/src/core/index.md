# Core

Core は feature 横断で使う基盤を置く領域です。feature 固有の仕様や状態は `Editor/<Feature>` 側に置き、Core には複数 feature から使う契約と共通処理だけを持たせます。

## 責務

- `Injector`: Hierarchy / Project への描画差し込み
- `I18N`: scope 解決、localization 読み込み、再読込
- `Settings`: 定義登録、保存、Preferences / Project Settings 表示
- `Testing`: `Test List` への suite 登録と実行状態管理
- `Internal`: package ルート解決や Unity 内部 API への薄いラッパー

## Feature 実装の入口

Core 前提で feature を作る場合は、まず以下を揃えます。

- `Editor/<Feature>/<Feature>Bootstrap.cs`
- `Editor/<Feature>/<Feature>Definitions.cs`
- `Editor/<Feature>/Localization/<locale>/*.jsonc`
- 必要なら `Editor/<Feature>/Test/Editor/*`

namespace は `Ee4v.<Feature>` を使います。`<Feature>Bootstrap.cs` では `FeatureBootstrapContract.Initialize(...)` を通し、`featureScope` と definitions の scope を一致させます。

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

## Pages

- [Core API](../api/core.md)
- [可視性と Internal](./internal.md)
- [Injector](./injector.md)
- [Settings](./settings.md)
- [I18N / Localization](./localization.md)
- [Test 登録](./testing.md)
- [実装チェックリスト](./checklist.md)

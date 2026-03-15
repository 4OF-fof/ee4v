# How To Implement Features On Top Of Core

このドキュメントは、`Editor/Core` の基盤 API を使って実際の機能を `Editor/<Feature>` 側へ実装する流れをまとめたものです。  
現状のサンプル実装は `Editor/Phase1` にあります。

## 置き場所

- 基盤 API は `Editor/Core`
  - `I18n`
  - `Settings`
  - `Injector`
  - `Internal`
- 機能実装は `Editor/<Feature>`
  - 例: `Editor/Phase1`
- namespace は `Ee4v.<Scope>` を基準にする
  - Core: `Ee4v.Core.*`
  - feature: `Ee4v.Phase1` / `Ee4v.Phase1.*`

## 1. Localization を置く

機能ごとの翻訳は、その機能フォルダ内に置きます。

```text
Editor/
  Core/
    Localization/
      en-US/
        core.jsonc
      ja-JP/
        core.jsonc
  MyFeature/
    Localization/
      en-US/
        settings.jsonc
        injector.jsonc
      ja-JP/
        settings.jsonc
        injector.jsonc
```

- `Core/Localization` は Core 自身が使う文言だけを置く
- `Feature/Localization` はその feature 自身が使う文言だけを置く
- `I18N` は `Editor/<Scope>/Localization/<locale>/*.jsonc` を scope ごとに読み分ける
- `I18N.Get("...")` は呼び出し元 namespace の `Ee4v.<Scope>` から scope を自動解決する
- 別 scope への fallback はしない
- `Core` の文言を feature から共有利用する前提ではない
- `Localization` フォルダ名の `<Scope>` と namespace 第2セグメントは一致させる

## 2. SettingDefinition を作る

機能側で必要な設定は `Editor/<Feature>/<Feature>Definitions.cs` にまとめます。

```csharp
internal static class MyFeatureDefinitions
{
    public static readonly SettingDefinition<bool> EnableFeature =
        new SettingDefinition<bool>(
            "myFeature.enabled",
            SettingScope.User,
            "settings.section.myFeature",
            "settings.enableFeature.label",
            "settings.enableFeature.tooltip",
            true);
}
```

- 保存先は `SettingScope.User` / `SettingScope.Project` で決める
- `displayNameKey` と `descriptionKey` は i18n key を渡す
- validator や custom drawer が必要なら `SettingDefinition<T>` に渡す
- 言語 / fallback言語のような基盤設定は `Core` 側で定義し、feature 側へ持ち込まない
- `SettingDefinition` の localization scope は定義元 namespace から自動決定される

## 2.1 文言参照

通常のコードではこれまで通り `I18N.Get("settings.foo")` を使います。

```csharp
var label = I18N.Get("settings.enableFeature.label");
```

- scope は呼び出し元 namespace から自動推定される
- `Ee4v.Phase1...` からの呼び出しは `Phase1/Localization` だけを見る
- `Ee4v.Core...` からの呼び出しは `Core/Localization` だけを見る
- 共通文言が必要でも現時点では各 scope に明示的に持つ

## 3. Bootstrap で登録する

機能ごとに `Editor/<Feature>/<Feature>Bootstrap.cs` を作り、必要な定義や stub を登録します。

```csharp
[InitializeOnLoad]
internal static class MyFeatureBootstrap
{
    static MyFeatureBootstrap()
    {
        MyFeatureDefinitions.RegisterAll();
        MyFeatureInjector.RegisterAll();
    }
}
```

- feature 固有 bootstrap は feature フォルダ内に置く
- `Core` 側には feature bootstrap を置かない

## 4. Injector に登録する

描画拡張は `InjectorApi.Register(...)` で登録します。

```csharp
InjectorApi.Register(new ItemInjectionRegistration(
    "myFeature.hierarchy.item",
    InjectionChannel.HierarchyItem,
    DrawHierarchyItem,
    priority: 100,
    isEnabled: () => SettingApi.Get(MyFeatureDefinitions.EnableFeature)));
```

- 有効 / 無効の判定は `SettingApi` の値を使って feature 側で決める
- `InjectorApi` は登録、dispatch、callback 購読、再描画要求だけを担当する

## 5. ItemInjectionContext を使う

`HierarchyItem` と `ProjectItem` では `ItemInjectionContext` に判定済みの情報が入っています。

### Hierarchy

- `context.IsHierarchySceneHeader`
- `context.IsHierarchyGameObject`
- `context.HierarchyItemKind`
- `context.HierarchyScene`

### Project

- `context.ProjectViewMode`
  - `OneColumn`
  - `TwoColumns`
- `context.ProjectOrientation`
  - `Horizontal`
  - `Vertical`

## 6. 確認用コードの置き方

API の成立性確認や UI 判定の可視化コードは、feature フォルダ内に置きます。  
今の確認用コードは以下です。

- `Editor/Phase1/Phase1ContextVerification.cs`
- `Editor/Phase1/Phase1StubBootstrap.cs`

Hierarchy では `SCENE` / `GO`、Project では `1C` / `2C-H` / `2C-V` のバッジを出して判定確認しています。

## 7. I18n Debug

`Debug/I18n Analyzer` で以下を確認できます。

- 未定義キー
- 未使用キー
- 同一 locale 内の文言重複
- 同一 scope 内の重複キー
- scope 解決できない namespace の参照

初期対象は静的文字列リテラルの `I18N.Get("...")` / `I18N.TryGet("...")` です。  
動的に連結したキーは検査対象外です。

## 実装時の基本方針

- `Core` は再利用可能な基盤だけを置く
- feature 固有の設定、文言、bootstrap、stub は feature フォルダに置く
- `old` は挙動確認用であり、そのまま構成を持ち込まない

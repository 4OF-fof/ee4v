# Test Guide

`new/Editor` 配下の EditMode テストは、feature ごとに `Editor/<Feature>/Test/Editor` にまとめます。  
Core の `Debug/ee4v Test Manager` では feature 単位の suite と、その中に含まれる各テストの title / description を表示します。

## Folder Layout

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
- `references` には `Ee4v.Editor` を入れる
- `optionalUnityReferences` には `TestAssemblies` を入れる
- registrar 名は `<Feature>TestRegistrar`
- test namespace は `Ee4v.<Feature>.Tests`

## Registrar

registrar は Test Manager の一覧表示に使う feature 単位メタデータを返します。  
各テストの title / description は registrar ではなく、テストメソッド側の `FeatureTestCaseAttribute` に書きます。

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
                "MyFeature の EditMode テスト全体を管理します。",
                order: 100);
        }
    }
}
```

ポイント:

- `FeatureScope` は feature 名と合わせる
- `AssemblyName` は test asmdef 名と合わせる
- `order` は feature 間、feature 内どちらも昇順で並ぶ

## Test Metadata

各 `[Test]` メソッドには `FeatureTestCaseAttribute` を付けます。  
Test Manager は test assembly を走査して、実際のテスト件数分だけ title / description を表示します。

```csharp
using Ee4v.Core.Testing;
using NUnit.Framework;

namespace Ee4v.MyFeature.Tests
{
    public sealed class MyFeatureTests
    {
        [Test]
        [FeatureTestCase(
            "定義登録の確認",
            "MyFeature の SettingDefinition が必要なキーで登録されることを確認します。",
            order: 0)]
        public void RegisterAll_RegistersExpectedDefinitions()
        {
        }
    }
}
```

ポイント:

- title / description は Test Manager でそのまま表示されるので、日本語で具体的に書く
- `FeatureTestCaseAttribute` を付けない場合はメソッド名がそのまま表示される
- 表示件数は実際の `[Test]` メソッド数と一致する

## Test Code

テスト本体は通常の NUnit / Unity Test Framework で書きます。

```csharp
using Ee4v.Core.Settings;
using NUnit.Framework;

namespace Ee4v.MyFeature.Tests
{
    public sealed class MyFeatureTests
    {
        [SetUp]
        public void SetUp()
        {
            MyFeatureTestReset.ResetAll();
        }

        [TearDown]
        public void TearDown()
        {
            MyFeatureTestReset.ResetAll();
            MyFeatureTestReset.RecoverEditorState();
        }

        [Test]
        public void RegisterAll_RegistersExpectedDefinitions()
        {
            MyFeatureDefinitions.RegisterAll();

            var definitions = SettingApi.GetDefinitions(SettingScope.User);
            Assert.That(definitions, Is.Not.Empty);
        }
    }
}
```

## Static State Reset

このコードベースでは `SettingApi` や feature bootstrap が static 状態を持つため、テストごとに状態を戻す必要があります。

方針:

- production assembly に test 用 API は追加しない
- reset helper は test asmdef 側にだけ置く
- reflection で private static field を初期状態へ戻す
- `TearDown` では reset のあとに `RecoverEditorState()` を呼び、本番 Editor 状態を復元する

対象になりやすい static 状態:

- `SettingApi`
- `InjectorApi`
- `PackagePathUtility`
- `CoreLocalizationDefinitions`
- 対象 feature の `Definitions`
- 対象 feature の `Bootstrap`

## Mock Policy

副作用を持つ処理は、まず薄い抽象を差し替えられる形にします。

現状の例:

- `ProjectFileSettingStore` は `IFileSystem`
- `EditorPrefsSettingStore` は `IEditorPrefsFacade`

テストでは fake 実装を作って roundtrip や validation を確認します。  
実ファイルを使う必要がある場合だけ temp directory ベースの統合寄りテストを追加します。

## Writing Tips

- 1 テストは 1 つの責務に絞る
- 各 `[Test]` メソッドに `FeatureTestCaseAttribute` を付ける
- `saveImmediately: false` を使える箇所では不要な永続化を避ける
- テスト後に feature の通常機能が消えないよう、`RecoverEditorState()` を忘れない
- Test Manager に表示する説明と実際のテスト内容がずれないように保つ

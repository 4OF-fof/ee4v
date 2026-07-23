# Test 登録

Testing は `Editor/Testing` に置く独立Moduleです。Coreの内部機能ではありません。

| assembly | namespace | 役割 |
|---|---|---|
| `Ee4v.Testing.Contracts.Editor` | `Ee4v.Testing.Contracts` | suite / case metadataとregistrar契約 |
| `Ee4v.Testing.Application.Editor` | `Ee4v.Testing.Application` | test case発見とdescriptor構築規則 |
| `Ee4v.Testing.Infrastructure.Unity.Editor` | `Ee4v.Testing.Infrastructure.Unity` | TypeCache、Unity Test Runner、SessionState adapter |
| `Ee4v.Testing.Infrastructure.StaticAnalysis.Editor` | `Ee4v.Testing.Infrastructure.StaticAnalysis` | source / localization監査 |
| `Ee4v.Testing.UI.Editor` | `Ee4v.Testing.UI` | Test List windowと結果presentation |

依存方向は `UI / Infrastructure -> Application -> Contracts` です。
通常のfeature test assemblyは `Ee4v.Testing.Contracts.Editor` だけを参照します。

feature test は原則 `Editor/<Scope>/Test/Editor` に置きます。

必要になるもの:

- test asmdef
- `<Scope>TestRegistrar.cs`
- NUnit test class
- 必要なら `Editor/AssemblyInfo.cs` の `InternalsVisibleTo`

## Registrar

`Test List` に suite を出すには `Ee4v.Testing.Contracts.IFeatureTestRegistrar`
を実装します。クラス名は `*TestRegistrar` で終わる必要があります。
Unity adapterの `FeatureTestRegistry` がこの命名で自動発見し、
Applicationの `FeatureTestDescriptorBuilder` へ渡します。

`FeatureTestDescriptor` では以下を決めます。

| field | 内容 |
|---|---|
| `FeatureScope` | suite 識別子 |
| `DisplayName` | `Test List` 表示名 |
| `AssemblyName` | 実行対象 asmdef 名 |
| `Description` | suite 説明 |
| `Order` | 並び順 |
| `Category` | suite 分類 |

`FeatureScope` と `AssemblyName` は全 suite で重複禁止です。

## Test Case

NUnit test method に `[FeatureTestCase(...)]` を付けると `Test List` で case 情報として表示されます。

指定できるもの:

- `title`
- `description`
- `order`
- `category`

付けなくても実行自体はされますが、`Test List` 上の説明が弱くなるので原則付けます。

## `InternalsVisibleTo`

test のために `internal` 実装へ触る場合は `Editor/AssemblyInfo.cs` に test asmdef 名を追加します。

```csharp
[assembly: InternalsVisibleTo("Ee4v.Sample.Tests.Editor")]
```

これで済む場合は API を `public` に上げません。

## Core 側テスト

Core 全体に効く監査や基盤テストは `Editor/Core/Test/Editor` に置きます。

現在の `Core` suite には以下が含まれます。

- I18N scope 解決テスト
- localization static audit
- AssetManager の asmdef 依存方向、内側レイヤーの技術非依存、composition root 一意性、SQLite / filesystem / Unity adapter の配置を確認する architecture static audit
- UI の direct `Label` 使用監査
- setting、bootstrap、Injector registry、Testing境界の基盤契約テスト

package 全体監査なのに feature 専用 suite を新設すると、scope や assembly 管理が散ります。まず Core へ寄せられないか確認します。

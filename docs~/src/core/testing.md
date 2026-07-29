# Test 登録

Testing は `Editor/Testing` に置く独立Moduleです。Coreの内部機能ではありません。
Test List window は Unity メニューの `ee4v/Debug/Test List` から開きます。
window の先頭には、登録された全 suite の状態と全 case の結果件数をまとめた
全体サマリを表示します。検索条件による絞り込みは全体サマリへ影響しません。
`すべて実行` は登録済みの全 suite を一括実行し、実行中または suite が未登録の
場合は操作できません。

| assembly | namespace | 役割 |
|---|---|---|
| `Ee4v.Testing.Contracts.Editor` | `Ee4v.Testing.Contracts` | suite / case metadataとassembly登録attribute |
| `Ee4v.Testing.Application.Editor` | `Ee4v.Testing.Application` | test case発見、descriptor構築規則、catalog / runner port |
| `Ee4v.Testing.Infrastructure.Unity.Editor` | `Ee4v.Testing.Infrastructure.Unity` | assembly登録発見、Unity Test Runner、SessionState adapter |
| `Ee4v.Testing.Infrastructure.StaticAnalysis.Editor` | `Ee4v.Testing.Infrastructure.StaticAnalysis` | source / localization監査 |
| `Ee4v.Testing.UI.Editor` | `Ee4v.Testing.UI` | Test List windowと結果presentation |
| `Ee4v.Testing.Composition.Editor` | `Ee4v.Testing.Composition` | Unity adapterをApplicationのportとしてUIへ注入 |

依存方向は `Composition -> UI / Infrastructure -> Application -> Contracts` です。
UIはInfrastructureの具象を参照せず、Applicationの
`IFeatureTestCatalog` / `IFeatureTestRunner` だけを利用します。
通常のfeature test assemblyは `Ee4v.Testing.Contracts.Editor` だけを参照します。

feature test は原則 `Editor/<Scope>/Test/Editor` に置きます。

必要になるもの:

- test asmdef
- assembly に付ける `[FeatureTestSuite(...)]`
- NUnit test class
- 必要なら `Editor/AssemblyInfo.cs` の `InternalsVisibleTo`

## Suite 登録

`Test List` に suite を出すには test assembly へ
`Ee4v.Testing.Contracts.FeatureTestSuiteAttribute` を付けます。
Unity adapter の `FeatureTestRegistry` が読み込み済み assembly から自動発見し、
Application の `FeatureTestDescriptorBuilder` へ渡します。

```csharp
[assembly: FeatureTestSuite(
    "Sample",
    "Sample",
    "Ee4v.Sample.Tests.Editor",
    "Sample module の判断を確認します。",
    order: 300)]
```

attribute では以下を決めます。

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

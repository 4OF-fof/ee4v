# Test 登録

feature test は原則 `Editor/<Scope>/Test/Editor` に置きます。

必要になるもの:

- test asmdef
- `<Scope>TestRegistrar.cs`
- NUnit test class
- 必要なら `Editor/AssemblyInfo.cs` の `InternalsVisibleTo`

## Registrar

`Test List` に suite を出すには `IFeatureTestRegistrar` を実装します。クラス名は `*TestRegistrar` で終わる必要があります。`FeatureTestRegistry` はこの命名で自動発見します。

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

package 全体監査なのに feature 専用 suite を新設すると、scope や assembly 管理が散ります。まず Core へ寄せられないか確認します。

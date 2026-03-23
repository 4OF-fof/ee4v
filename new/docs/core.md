# Core実装

## 1. Core の責務

Core は feature 横断で使う基盤を持つ。

- `Injector`
  - Hierarchy / Project への描画差し込み
- `I18n`
  - scope 解決、localization 読み込み、再読込
- `Settings`
  - 定義登録、保存、Preferences / Project Settings 表示
- `Testing`
  - `Test List` への suite 登録と実行状態管理
- `Internal`
  - package ルート解決や Unity 内部 API への薄いラッパー

feature 固有の仕様や状態は `Core` に置かず、`Editor/<Feature>` 側に置く。

## 2. 新規 feature の基本構成

Core 前提で新規 feature を作る場合、まず以下を揃える。

- `Editor/<Feature>/<Feature>Bootstrap.cs`
- `Editor/<Feature>/<Feature>Definitions.cs`
- `Editor/<Feature>/Localization/<locale>/*.jsonc`
- 必要なら `Editor/<Feature>/Test/Editor/*`

namespace は `Ee4v.<Feature>` を使う。

`<Feature>Bootstrap.cs` では `FeatureBootstrapContract.Initialize(...)` を使う。

- `featureScope` は `Feature` 名そのもの
- definitions 型名は必ず `<Feature>Definitions`
- definitions の namespace から解決される scope も `<Feature>` で一致している必要がある

実装例:

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
            typeof(SampleDefinitions),
            SampleDefinitions.RegisterAll,
            SampleFeatureBootstrap.RegisterAll);
    }
}
```

## 3. 可視性と `Internal`

### 3-1. 基本方針

- feature 内で閉じる型は `internal`
- `Core` から他 feature が使う共通 API だけを `public`
- test から触りたいだけの型を `public` にしない

test 用アクセスは `Editor/AssemblyInfo.cs` の `InternalsVisibleTo` で許可する。

### 3-2. `Core/Internal` に置くもの

`Editor/Core/Internal` は「公開したくないが複数箇所から必要な基盤」を置く場所。

置いてよいもの:

- package ルートや namespace 解決の補助
- Unity の internal / private API を叩く薄いラッパー
- bootstrap 制約の共通チェック

置かないもの:

- feature 固有ロジック
- domain 仕様
- 設定値や UI 状態

### 3-3. Unity 内部 API を使いたいとき

Unity の private / internal 実装に触る必要がある場合は、feature 側で直接 reflection しない。

- `Editor/Core/Internal/EditorAPI` に用途別 facade を作る
- reflection は `Backends` 側へ閉じる
- feature 側は facade だけを使う

## 4. Injector

`InjectorApi` は Unity Editor の以下 4 箇所へ差し込める。

- `HierarchyItem`
- `HierarchyHeader`
- `ProjectItem`
- `ProjectToolbar`

### 4-1. 登録方法

- IMGUI 描画なら `ItemInjectionRegistration`
- `VisualElement` を返すなら `VisualElementInjectionRegistration`

各 registration では以下を必ず決める。

- `id`
  - channel 内で一意な識別子
- `channel`
  - 差し込み先
- `priority`
  - 同一 channel 内の並び順
- `isEnabled`
  - Setting 連動などの有効条件

`InjectorApi.Register(...)` は同じ `id + channel` があれば上書きする。複数登録で押し込みたいわけではないので、`id` は安定させる。

### 4-2. `ItemInjectionContext` の使い方

`HierarchyItem` / `ProjectItem` では `ItemInjectionContext` を受け取る。

主に使う値:

- `SelectionRect`
  - 元の行全体
- `CurrentRect`
  - 他 registration と余白を分け合うための現在の描画可能領域
- `Target`
  - 対象 `Object`
- `HierarchyItemKind`
  - scene header / game object 判定
- `ProjectViewMode`
  - one column / two columns
- `ProjectOrientation`
  - horizontal / vertical

右側へ badge を足す場合などは、描画後に `CurrentRect` を狭めて次の registration と競合しないようにする。

### 4-3. `VisualHostContext` の使い方

`HierarchyHeader` / `ProjectToolbar` では `VisualHostContext` を受け取る。

- `Window` から host window を参照できる
- 返した `VisualElement` は host にそのまま追加される
- `null` を返すと何も追加しない

host 自体の生成や再構築は `InjectorApi` 側が管理するので、feature 側で host を探して差し込む実装はしない。

### 4-4. 再描画

Injector 表示に影響する setting を変えたら、該当 channel に対して `InjectorApi.Repaint(...)` を呼ぶ。

例:

- `HierarchyItem` が変わるなら `HierarchyItem`
- `HierarchyHeader` も変わるなら両方呼ぶ
- `Project` 側だけなら `ProjectItem` / `ProjectToolbar`

setting 変更監視は `SettingApi.Changed` に乗せる。

### 4-5. 実装ルール

- draw / create callback は idempotent に保つ
- callback 内で registration を再登録しない
- feature の有効 / 無効は `isEnabled` に寄せる
- channel ごとの描画差を `ItemInjectionContext` で吸収する

## 5. Settings

### 5-1. 追加場所

setting 定義は原則 `Editor/<Feature>/<Feature>Definitions.cs` に置く。

`SettingDefinition<T>` は source file の namespace から localization scope を解決するため、定義を別 namespace の util file へ逃がさない。

### 5-2. 定義項目

`SettingDefinition<T>` では以下を決める。

- `key`
  - 例: `phase1.injector.projectToolbar.enabled`
- `scope`
  - `User` か `Project`
- `sectionKey`
  - settings 画面のグループ見出し
- `displayNameKey`
  - 項目名
- `descriptionKey`
  - tooltip
- `defaultValue`
  - 既定値
- `order`
  - section 内並び順
- `validator`
  - 入力制約
- `customDrawer`
  - 標準 field で足りない場合だけ使う
- `keywords`
  - settings 検索補助

### 5-3. 登録と参照

setting は `RegisterAll()` を用意して 1 回だけ `SettingApi.Register(...)` する。

- `RegisterAll()` には `_registered` guard を入れる
- bootstrap から `RegisterAll()` を呼ぶ
- 値参照は `SettingApi.Get(...)`
- 更新は `SettingApi.Set(...)`

### 5-4. 保存先

- `SettingScope.User`
  - `EditorPrefs`
- `SettingScope.Project`
  - `ProjectSettings/ee4v.settings.json`

そのため、ユーザーごとの差分にしたいものだけ `User` に置き、repo 共有したい設定は `Project` に置く。

### 5-5. 設定画面

`Preferences/4OF/ee4v` と `Project/4OF/ee4v` は `RegisteredSettingsProviders` が既に提供している。

通常の feature 実装では provider を追加しない。`SettingApi.Register(...)` した定義は scope ごとに自動で一覧へ出る。

grouping は `localizationScope + sectionKey` 単位なので、section を増やす場合は localization key も揃える。

### 5-6. バリデーション

- invalid 値は `validator` で弾く
- エラーメッセージは `SettingValidationResult.Error(...)`
- validation 文言も `I18N.Get(...)` 経由にする

## 6. I18N / Localization

### 6-1. 使い方

永続表示文言は `I18N.Get("key")` を使う。

- caller namespace から scope を解決する
- `Ee4v.<Scope>` にいれば `<Scope>` の localization を引く
- format が必要なら `I18N.Get("key", args...)`

### 6-2. 配置ルール

localization は以下に置く。

- `Editor/<Scope>/Localization/ja-JP/*.jsonc`
- `Editor/<Scope>/Localization/en-US/*.jsonc`

scope は `Localization` フォルダの親 directory 名から決まる。`Editor/<Scope>` 以外へ置くと意図した scope とずれる。

### 6-3. key 管理

- jsonc は object を flatten して `a.b.c` 形式で読まれる
- 同一 locale / scope で duplicate key はエラー
- code 側で未定義 key を引くと key 文字列がそのまま返る

Core には localization 静的監査があり、duplicate / missing / unused が `Core` suite で監査される。

### 6-4. 再読込

`Localization` 配下の asset 変更時は `LocalizationAssetPostprocessor` が `I18N.Reload()` を呼ぶ。

feature 側で独自の localization reload 実装を持たない。

## 7. Test 登録

### 7-1. 基本構成

feature test は原則 `Editor/<Scope>/Test/Editor` に置く。

必要になるもの:

- test asmdef
- `<Scope>TestRegistrar.cs`
- NUnit test class
- 必要なら `Editor/AssemblyInfo.cs` の `InternalsVisibleTo`

### 7-2. registrar

`Test List` に suite を出すには `IFeatureTestRegistrar` を実装する。

クラス名は `*TestRegistrar` で終わる必要がある。`FeatureTestRegistry` はこの命名で自動発見している。

`FeatureTestDescriptor` では以下を決める。

- `FeatureScope`
  - suite 識別子
- `DisplayName`
  - `Test List` 表示名
- `AssemblyName`
  - 実行対象 asmdef 名
- `Description`
  - suite 説明
- `Order`
  - 並び順
- `Category`
  - suite 分類

`FeatureScope` と `AssemblyName` は全 suite で重複禁止。

### 7-3. test case

NUnit test method に `[FeatureTestCase(...)]` を付けると `Test List` で case 情報として表示される。

指定できるもの:

- `title`
- `description`
- `order`
- `category`

付けなくても実行自体はされるが、`Test List` 上の説明が弱くなるので原則付ける。

### 7-4. `InternalsVisibleTo`

test のために `internal` 実装へ触る場合は `Editor/AssemblyInfo.cs` に test asmdef 名を追加する。

- 例: `Ee4v.Sample.Tests.Editor`

これで済む場合は API を `public` に上げない。

### 7-5. Core 側テストの扱い

Core 全体に効く監査や基盤テストは `Editor/Core/Test/Editor` に置く。

現在の `Core` suite には以下が含まれる。

- I18N scope 解決テスト
- localization static audit

package 全体監査なのに feature 専用 suite を新設すると、scope や assembly 管理が散るのでまず Core へ寄せられないか確認する。

## 8. 実装チェックリスト

- namespace は `Ee4v.<Scope>` になっているか
- bootstrap は `FeatureBootstrapContract.Initialize(...)` を通しているか
- definitions 型名が `<Scope>Definitions` になっているか
- 共通 API でない型を `public` にしていないか
- Unity internal API を feature 側で直接 reflection していないか
- setting を `RegisterAll()` で 1 回だけ登録しているか
- `User` / `Project` の保存先選択は妥当か
- settings 文言と validation 文言を localization したか
- 文言を `I18N.Get(...)` 経由にしたか
- localization を `Editor/<Scope>/Localization/<locale>` に置いたか
- Injector の `id` と `channel` は安定しているか
- Injector 描画後に `CurrentRect` の競合を起こしていないか
- setting 変更時に必要な `InjectorApi.Repaint(...)` を呼んでいるか
- `Test List` に出す registrar を追加したか
- test asmdef が必要なら `InternalsVisibleTo` を追加したか

## 9. 補足

- settings provider は通常増やさない。定義追加で既存 provider に載せる
- localization scope と settings localization scope は namespace 依存なので、ファイル移動時は namespace の崩れに注意する
- feature 固有実装を `Core` へ入れると再利用ではなく依存逆転の崩れになる。迷ったらまず `Editor/<Feature>` に置く

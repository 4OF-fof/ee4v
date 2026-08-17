# Unity 6 移行メモ

現行 package の対象は Unity 2022.3 です。この文書は Unity 6 対応済みであることを示すものではなく、移行時に見直す実装の範囲をまとめたものです。

## Unity 6 で不要にするもの

このプロジェクトでは Unity 6 への移行後、`UiTextFactory` とその IMGUI fallback を不要とする方針です。現行実装では Unity 2022.3 向けの文字描画を統一するために使用しています。

対象は主に次のファイルです。

- `src/Editor/UI/Foundation/Typography/UiTextFactory.cs`
- `src/Editor/UI/Foundation/Typography/UiTextElement.cs`
- `src/Editor/UI/Foundation/Typography/TypographyStyleResolver.cs`
- `src/Editor/UI/Test/Editor/UiTextFactoryTests.cs`

移行時は `UiTextFactory.Create*` を Unity 6 の標準 UI Toolkit 要素へ置き換えます。`ImguiUiTextElement` と `UiTextButton` も削除対象です。文字の見た目は USS と既存の色・余白 token へ寄せます。

置換後は `AGENTS.md` にある `UiTextFactory` の使用規則も削除または更新します。Unity 2022.3 を対象としている間は現行規則を維持します。

## Unity に依存しない範囲

次の assembly は `noEngineReferences: true` です。Unity 6 移行で直接変更する対象ではありません。

- `Ee4v.Core.Contracts.Editor`
- `Ee4v.Core.Services.Editor`

ここには Settings、多言語化、Injector、Background の契約と Unity 非依存の実装があります。

## Unity に依存する主なファイル

### Settings 接続部

`src/Editor/Core/Unity/Settings` は `EditorPrefs`、`ProjectSettings`、Newtonsoft.Json に接続します。

- `EditorPrefsSettingStore.cs`
- `ProjectFileSettingStore.cs`
- `NewtonsoftSettingValueSerializer.cs`
- `CoreSettings.cs`

### Unity Editor の公開窓口

`src/Editor/Core/EditorIntegration` は `AssetDatabase`、`EditorWindow`、Inspector、Project Browser などに接続します。公開 API の呼び出し側より先に、このフォルダを確認します。

### Unity 内部 API

`src/Editor/Core/Internal/EditorAPI/Backends` は最も Unity version の影響を受ける範囲です。reflection、非公開型、serialized property 名を使用しています。

- `ProjectBrowserBackend.cs`
- `ProjectFavoritesBackend.cs`
- `InspectorHostBackend.cs`
- `EditorPopupWindowBackend.cs`
- `SceneHierarchyBackend.cs`
- `TextFieldMultilineScrollBackend.cs`

Unity 6 で API を利用できない場合は、`ProjectBrowserApi` などの公開 signature を変えずに backend 側で吸収します。

### Editor 表示と lifecycle

`src/Editor/Core/Presentation` は UI Toolkit と Editor lifecycle に依存します。

- `Presentation/Injector` は `EditorApplication` の callback と Project Browser の表示構造を使う
- `Presentation/Settings` は `SettingsProvider` と UI Toolkit field を使う
- `Presentation/Background` は EditorWindow に `StatusOverlay` を追加する
- `Presentation/Localization/I18N.cs` は Editor 全体の再描画を行う

### 翻訳と package path

次のファイルは AssetDatabase、AssetPostprocessor、package の配置に依存します。

- `Core/I18n/LocalizationAssetPostprocessor.cs`
- `Core/I18n/LocalizationCatalogLoader.cs`
- `Core/Internal/PackagePathUtility.cs`
- `Core/EditorIntegration/PackageAssetApi.cs`

## 移行時に行うこと

1. Unity 6 で各 assembly をコンパイルする。
2. `UiTextFactory` の使用箇所を標準 UI Toolkit 要素へ置き換える。
3. `Core/Internal/EditorAPI/Backends` の reflection と内部 member 名を確認する。
4. Settings、I18n、Injector、Background 表示を Editor 上で確認する。
5. Core と UI の EditMode テストを実行する。

VRChat SDK が対象とする Unity version は移行時点の公式要件を別途確認します。

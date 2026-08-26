# UI

`Ee4v.UI.Editor` は機能非依存の UI Toolkit 部品です。
部品、状態型、トークンは公開 API のため、新しい feature assembly から直接利用できます。

## 初期化

```csharp
UiComposition.Prepare(rootVisualElement);
rootVisualElement.Add(
    UiTextFactory.CreateButton("Run", Run));
```

`UiComposition.Prepare` は共通 class、共有コンポーネントの stylesheet、Unity Dark palette を適用します。
各EditorWindowが追加で登録するのは機能固有のstylesheetだけです。部品自体はUXMLの有無に依存しません。

## color palette

標準paletteは `UiColorPalettes.UnityDark` です。USS は `--ee4v-color-*` の役割トークンを参照します。
IMGUI fallback は `UiColorTokens` を参照し、同じ `UiColorPalette` から色を取得します。
再デザインでは `ui-color-tokens.uss` と `UiColorPalettes.UnityDark` を同時に更新します。
Catalog の `Foundation/Color Palette` で役割名と実際の色を確認できます。

## Story

Catalog 内の共通部品には対話可能な Story があります。
機能側は `IUiStoryProvider` を実装すると Catalog から自動発見されます。
Catalog assembly への参照や Catalog 本体の編集は不要です。

```csharp
public sealed class SampleStories : IUiStoryProvider
{
    public int Order => 100;

    public IReadOnlyList<UiStory> GetStories()
    {
        return new[]
        {
            new UiStory(
                "sample",
                "Feature/Sample",
                "Sample",
                "概要",
                "確認内容",
                parent => parent.Add(new SampleView()),
                usageLocations: new[]
                {
                    "Editor/Feature/SampleWindow.cs"
                })
        };
    }
}
```

本番 assembly に Story を含めたくない場合は `Feature.Stories.Editor` のような別 asmdef に置きます。

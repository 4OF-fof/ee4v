# UI

`Ee4v.UI.Editor`は機能非依存のUI Toolkit部品を所有します。開発時の正本は[`../../../docs~/core/ui.md`](../../../docs~/core/ui.md)です。componentの分類、`CustomPopup`、Storyの契約は同資料を参照してください。

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
Foundation の参照一覧を除く共通部品の Story は、表示状態を変える Controls と結果を確認する Preview を持ちます。
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

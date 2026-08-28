# UI

`Ee4v.UI.Editor`は機能非依存のUI Toolkit部品を所有します。開発時の正本は[`../../../docs~/core/ui.md`](../../../docs~/core/ui.md)です。componentの分類、`CustomPopup`、Storyの契約は同資料を参照してください。

## 初期化

```csharp
UiComposition.Prepare(rootVisualElement);
rootVisualElement.Add(
    new UiButton("Run", Run));
```

`UiComposition.Prepare` は共通 class、共有コンポーネントの stylesheet、現在のUnity Editorテーマに対応するpaletteを適用します。
各EditorWindowが追加で登録するのは機能固有のstylesheetだけです。部品自体はUXMLの有無に依存しません。

通常の操作、文字入力、検索入力にはそれぞれ`UiButton`、`InputField`、`SearchField`を使用します。`UiTextFactory`の低レベル文字入力は共有入力コンポーネント内部だけで使用します。

## color palette

`UiColorPalettes.UnityDark`と`UiColorPalettes.UnityLight`がテーマ別の標準色です。`UiColorTokens.Current`はUnity Editorのテーマに対応する`UiColorPalette`を返します。
USS は `--ee4v-color-*` の役割トークンを参照し、`UiComposition.Prepare`がrootへ付けるテーマclassでDark・Lightを切り替えます。IMGUI fallbackも`UiColorTokens`を参照します。
再デザインでは `ui-color-tokens.uss` と対応する`UiColorPalette`を同時に更新します。
Catalog の `Reference/Color Palette` で役割名と実際の色を確認できます。

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

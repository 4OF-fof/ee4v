using UnityEngine;
using UnityEngine.UIElements;

namespace Ee4v.UI
{
    internal sealed partial class CatalogWindow
    {
        private sealed class ImageTooltipCatalogRegistrar : ICatalogRegistrar
        {
            public int Order
            {
                get { return 64; }
            }

            public void Register(CatalogRegistry registry)
            {
                registry.RegisterStyleSheet("Editor/Core/UI/Components/Overlays/ImageTooltip/image-tooltip.uss");
                registry.RegisterStory(new StoryRegistration(
                    "image-tooltip",
                    "Overlays",
                    "ImageTooltip",
                    "画像とファイル名を縦に並べるプレビュー用 tooltip です。",
                    "画像を最大表示領域へ収め、その下にファイル名を中央揃えで表示します。hover 判定や画像取得は呼び出し側が担当します。",
                    new string[0],
                    ComponentImplementationKind.UiToolkit,
                    (window, parent) => window.BuildImageTooltipStory(parent)));
            }
        }

        private void BuildImageTooltipStory(VisualElement parent)
        {
            var preview = CreatePreviewSection(parent);
            var surface = CreatePreviewSurface();
            surface.style.alignItems = Align.Center;
            surface.style.paddingTop = 16f;
            surface.style.paddingBottom = 16f;

            var texture = CreateItemCardSampleThumbnail(240, 160);
            texture.hideFlags = HideFlags.HideAndDontSave;
            var tooltip = new ImageTooltip(new ImageTooltipState(texture, "sample-image.png"));
            tooltip.style.width = 256f;
            tooltip.RegisterCallback<DetachFromPanelEvent>(_ => Object.DestroyImmediate(texture));
            surface.Add(tooltip);
            preview.Body.Add(surface);
        }
    }
}

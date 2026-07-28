using UnityEngine;
using UnityEngine.UIElements;

namespace Ee4v.UI
{
    internal sealed partial class CatalogWindow
    {
        private sealed class ItemCardCatalogRegistrar : ICatalogRegistrar
        {
            public int Order
            {
                get { return 10; }
            }

            public void Register(CatalogRegistry registry)
            {
                registry.RegisterStyleSheet("Editor/UI/Components/Content/ItemImage/item-image.uss");
                registry.RegisterStyleSheet("Editor/UI/Components/Content/ImageStack/image-stack.uss");
                registry.RegisterStyleSheet("Editor/UI/Components/Content/ItemCard/item-card.uss");
                registry.RegisterStory(new StoryRegistration(
                    "item-card",
                    "Content",
                    "ItemCard",
                    "サムネイルと item 名だけの汎用カードコンポーネントです。",
                    "データ取得は外側の loader/service が担当し、ItemCard は byte[] と item 名を受け取って表示する薄い UI component として扱います。画像表示と Texture2D cache は ItemImage に委譲します。",
                    new[] { "ItemImage" },
                    ComponentImplementationKind.UiToolkit,
                    (window, parent) => window.BuildItemCardStory(parent)));
            }
        }

        private void BuildItemCardStory(VisualElement parent)
        {
            var preview = CreatePreviewSection(parent);
            var surface = CreatePreviewSurface();
            surface.style.flexDirection = FlexDirection.Row;
            surface.style.alignItems = Align.FlexStart;
            surface.style.paddingLeft = UiSpacingTokens.Xl;
            surface.style.paddingRight = UiSpacingTokens.Xl;
            surface.style.paddingTop = UiSpacingTokens.Xl;
            surface.style.paddingBottom = UiSpacingTokens.Xl;

            var thumbnail = CreateItemCardSampleThumbnail(132, 132);
            var thumbnailBytes = thumbnail.EncodeToPNG();
            UnityEngine.Object.DestroyImmediate(thumbnail);

            var itemCard = new ItemCard(new ItemCardState("Sample Avatar Asset", thumbnailBytes));
            itemCard.style.marginRight = UiSpacingTokens.Xxl;
            surface.Add(itemCard);
            surface.Add(new ItemCard(new ItemCardState("No Thumbnail Item")));

            preview.Body.Add(surface);
        }
    }
}

using UnityEngine;
using UnityEngine.UIElements;

namespace Ee4v.UI
{
    internal sealed partial class CatalogWindow
    {
        private sealed class ItemImageCatalogRegistrar : ICatalogRegistrar
        {
            public int Order
            {
                get { return 10; }
            }

            public void Register(CatalogRegistry registry)
            {
                registry.RegisterStyleSheet("Editor/UI/Components/Content/ItemImage/item-image.uss");
                registry.RegisterStory(new StoryRegistration(
                    "item-image",
                    "Content",
                    "ItemImage",
                    "item のサムネイル画像を正方形で表示する基本コンポーネントです。",
                    "byte[] からの Texture2D 解決、同一画像の自動 cache、ScaleAndCrop、未設定時 placeholder、正方形サイズ制御を担当します。ItemCard などの上位コンポーネントはこのコンポーネントに画像表示を委譲します。",
                    new string[0],
                    ComponentImplementationKind.UiToolkit,
                    (window, parent) => window.BuildItemImageStory(parent)));
            }
        }

        private void BuildItemImageStory(VisualElement parent)
        {
            var preview = CreatePreviewSection(parent);
            var surface = CreatePreviewSurface();
            surface.style.flexDirection = FlexDirection.Row;
            surface.style.alignItems = Align.FlexStart;
            surface.style.paddingLeft = 12f;
            surface.style.paddingRight = 12f;
            surface.style.paddingTop = 12f;
            surface.style.paddingBottom = 12f;

            var thumbnail = CreateItemCardSampleThumbnail(132, 132);
            var thumbnailBytes = thumbnail.EncodeToPNG();
            UnityEngine.Object.DestroyImmediate(thumbnail);

            var itemImage = new ItemImage(new ItemImageState(thumbnailBytes));
            itemImage.SetSize(132f);
            itemImage.style.marginRight = 16f;
            surface.Add(itemImage);

            var placeholder = new ItemImage();
            placeholder.SetSize(132f);
            surface.Add(placeholder);

            preview.Body.Add(surface);
        }
    }
}

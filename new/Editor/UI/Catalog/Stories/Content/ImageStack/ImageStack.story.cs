using UnityEngine;
using UnityEngine.UIElements;

namespace Ee4v.UI
{
    internal sealed partial class CatalogWindow
    {
        private sealed class ImageStackCatalogRegistrar : ICatalogRegistrar
        {
            public int Order
            {
                get { return 11; }
            }

            public void Register(CatalogRegistry registry)
            {
                registry.RegisterStyleSheet("Editor/UI/Components/Content/ItemImage/item-image.uss");
                registry.RegisterStyleSheet("Editor/UI/Components/Content/ImageStack/image-stack.uss");
                registry.RegisterStory(new StoryRegistration(
                    "image-stack",
                    "Content",
                    "ImageStack",
                    "複数 item の画像を重ねて表示するコンポーネントです。",
                    "最大 3 件の画像を中央基準で重ね、1 件目が最背面、2 件目、3 件目の順に上へ積み上がるように表示します。選択数や説明テキストは外側の panel が担当します。",
                    new[] { "ItemImage" },
                    ComponentImplementationKind.UiToolkit,
                    (window, parent) => window.BuildImageStackStory(parent)));
            }
        }

        private void BuildImageStackStory(VisualElement parent)
        {
            var preview = CreatePreviewSection(parent);
            var surface = CreatePreviewSurface();
            surface.style.flexDirection = FlexDirection.Row;
            surface.style.alignItems = Align.FlexStart;
            surface.style.paddingLeft = 12f;
            surface.style.paddingRight = 12f;
            surface.style.paddingTop = 12f;
            surface.style.paddingBottom = 12f;

            var states = CreateImageStackSampleStates();
            for (var count = 1; count <= 3; count++)
            {
                var stack = new ImageStack();
                stack.SetSize(150f);
                stack.SetStates(CreateImageStackStoryStates(states, count));
                stack.style.marginRight = 16f;
                surface.Add(stack);
            }

            preview.Body.Add(surface);
        }

        private static ItemImageState[] CreateImageStackSampleStates()
        {
            var states = new ItemImageState[3];
            for (var i = 0; i < states.Length; i++)
            {
                var texture = CreateItemCardSampleThumbnail(132 + (i * 8), 132 + (i * 8));
                var bytes = texture.EncodeToPNG();
                UnityEngine.Object.DestroyImmediate(texture);
                states[i] = new ItemImageState("image-stack-sample-" + i, bytes);
            }

            return states;
        }

        private static ItemImageState[] CreateImageStackStoryStates(ItemImageState[] states, int count)
        {
            var result = new ItemImageState[count];
            for (var i = 0; i < count; i++)
            {
                result[i] = states[i];
            }

            return result;
        }
    }
}

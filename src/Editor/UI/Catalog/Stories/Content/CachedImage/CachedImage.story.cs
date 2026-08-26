using System;
using Ee4v.Core.Images;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ee4v.UI
{
    internal sealed partial class CatalogWindow
    {
        private sealed class CachedImageCatalogRegistrar
            : ICatalogRegistrar
        {
            public int Order => 11;

            public void Register(CatalogRegistry registry)
            {
                registry.RegisterStory(new StoryRegistration(
                    "cached-image",
                    "Content",
                    "CachedImage",
                    "デコード済みTextureを共有する画像表示コンポーネントです。",
                    "同じcacheとkeyを使う複数要素が、1つのTextureを再利用する状態を確認します。",
                    new string[0],
                    ComponentImplementationKind.UiToolkit,
                    (window, parent) =>
                        window.BuildCachedImageStory(parent),
                    new[]
                    {
                        "Editor/AssetManager/UI/Components/AssetItemGridCard.cs",
                        "Editor/AssetManager/UI/Components/AssetThumbnailStack.cs"
                    }));
            }
        }

        private void BuildCachedImageStory(VisualElement parent)
        {
            var scaleMode = ScaleMode.ScaleAndCrop;
            Action refresh = null;
            var controls = CreatePlainControlsSection(
                parent,
                "表示方法を切り替え、同じcacheとkeyを使う2要素の描画を確認します。");
            var scaleModeField = AddEnumField(
                controls.Content,
                "表示方法",
                scaleMode,
                value =>
                {
                    scaleMode = value;
                    refresh();
                });

            var cache = new CachedImageCache();
            cache.SetSource("sample", CreateCachedImageSample());

            var preview = CreatePreviewSection(parent);
            var surface = CreatePreviewSurface(true);
            surface.style.flexDirection = FlexDirection.Row;
            var first = CreateCachedImagePreview(cache);
            var second = CreateCachedImagePreview(cache);
            surface.Add(first);
            surface.Add(second);
            surface.RegisterCallback<DetachFromPanelEvent>(_ =>
            {
                first.Dispose();
                second.Dispose();
                cache.Dispose();
            });
            preview.Body.Add(surface);

            refresh = () =>
            {
                scaleModeField.SetValueWithoutNotify(
                    (Enum)(object)scaleMode);
                first.scaleMode = scaleMode;
                second.scaleMode = scaleMode;
            };

            refresh();
            FinalizeControlsSection(parent, controls);
        }

        private static CachedImage CreateCachedImagePreview(
            CachedImageCache cache)
        {
            var image = new CachedImage(cache)
            {
                scaleMode = ScaleMode.ScaleAndCrop
            };
            image.style.width = 128f;
            image.style.height = 128f;
            image.style.marginRight = 8f;
            image.SetSource("sample");
            return image;
        }

        private static byte[] CreateCachedImageSample()
        {
            var texture = new Texture2D(2, 2);
            texture.SetPixels(new[]
            {
                new Color(0.16f, 0.38f, 0.62f),
                new Color(0.22f, 0.54f, 0.72f),
                new Color(0.42f, 0.26f, 0.58f),
                new Color(0.64f, 0.38f, 0.68f)
            });
            texture.Apply();
            var data = texture.EncodeToPNG();
            UnityEngine.Object.DestroyImmediate(texture);
            return data;
        }
    }
}

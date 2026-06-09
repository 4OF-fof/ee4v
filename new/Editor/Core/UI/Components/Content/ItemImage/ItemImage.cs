using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ee4v.UI
{
    internal sealed class ItemImageState
    {
        public ItemImageState(byte[] textureData = null)
            : this(CreateDataCacheKey(textureData), textureData)
        {
        }

        public ItemImageState(string cacheKey, byte[] textureData)
        {
            TextureData = textureData ?? Array.Empty<byte>();
            CacheKey = TextureData.Length == 0
                ? string.Empty
                : string.IsNullOrWhiteSpace(cacheKey)
                    ? CreateDataCacheKey(TextureData)
                    : cacheKey;
        }

        public byte[] TextureData { get; }

        public string CacheKey { get; }

        private static string CreateDataCacheKey(byte[] data)
        {
            if (data == null || data.Length == 0)
            {
                return string.Empty;
            }

            using (var sha256 = SHA256.Create())
            {
                var hash = sha256.ComputeHash(data);
                var builder = new StringBuilder("bytes:", 70);
                for (var i = 0; i < hash.Length; i++)
                {
                    builder.Append(hash[i].ToString("x2"));
                }

                return builder.ToString();
            }
        }
    }

    internal class ItemImage : VisualElement
    {
        private const string RootClassName = "ee4v-ui-item-image";
        private const string ImageClassName = "ee4v-ui-item-image__image";
        private const string PlaceholderClassName = "ee4v-ui-item-image__placeholder";
        private const float MinSize = 48f;
        private readonly Image _image;
        private readonly VisualElement _placeholder;

        public ItemImage(ItemImageState state = null)
        {
            AddToClassList(RootClassName);

            _placeholder = new VisualElement
            {
                pickingMode = PickingMode.Ignore
            };
            _placeholder.AddToClassList(PlaceholderClassName);

            _image = new Image
            {
                pickingMode = PickingMode.Ignore,
                scaleMode = ScaleMode.ScaleAndCrop
            };
            _image.AddToClassList(ImageClassName);

            Add(_placeholder);
            Add(_image);

            SetState(state ?? new ItemImageState());
        }

        public void SetState(ItemImageState state)
        {
            SetTexture(ItemImageCache.GetTexture(state));
        }

        private void SetTexture(Texture2D texture)
        {
            _image.image = texture;
            var hasTexture = texture != null;
            _image.style.display = hasTexture ? DisplayStyle.Flex : DisplayStyle.None;
            _placeholder.style.display = hasTexture ? DisplayStyle.None : DisplayStyle.Flex;
        }

        public void SetSize(float size)
        {
            var safeSize = Mathf.Max(MinSize, size);
            style.width = safeSize;
            style.height = safeSize;
            style.minWidth = safeSize;
            style.minHeight = safeSize;
            style.maxWidth = safeSize;
            style.maxHeight = safeSize;
        }
    }

    internal static class ItemImageCache
    {
        private static readonly Dictionary<string, Texture2D> Textures = new Dictionary<string, Texture2D>(StringComparer.Ordinal);

        public static Texture2D GetTexture(ItemImageState state)
        {
            if (state == null)
            {
                return null;
            }

            var data = state.TextureData;
            if (data == null || data.Length == 0)
            {
                return null;
            }

            Texture2D cached;
            if (Textures.TryGetValue(state.CacheKey, out cached) && cached != null)
            {
                return cached;
            }

            var texture = CreateTexture(data);
            if (texture == null)
            {
                return null;
            }

            Textures[state.CacheKey] = texture;
            return texture;
        }

        public static void Clear()
        {
            foreach (var texture in Textures.Values)
            {
                if (texture != null)
                {
                    UnityEngine.Object.DestroyImmediate(texture);
                }
            }

            Textures.Clear();
        }

        private static Texture2D CreateTexture(byte[] data)
        {
            var texture = new Texture2D(2, 2)
            {
                hideFlags = HideFlags.HideAndDontSave
            };

            if (texture.LoadImage(data))
            {
                return texture;
            }

            UnityEngine.Object.DestroyImmediate(texture);
            return null;
        }
    }
}

using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Ee4v.UI
{
    internal static class ItemImageTextureCache
    {
        private const int MaximumTextureCount = 512;
        private static readonly Dictionary<string, CacheEntry> Textures =
            new Dictionary<string, CacheEntry>(StringComparer.Ordinal);
        private static readonly LinkedList<string> Recency = new LinkedList<string>();

        static ItemImageTextureCache()
        {
            AssemblyReloadEvents.beforeAssemblyReload += Clear;
            EditorApplication.quitting += Clear;
        }

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

            CacheEntry cached;
            if (Textures.TryGetValue(state.CacheKey, out cached) && cached.Texture != null)
            {
                Recency.Remove(cached.Node);
                Recency.AddLast(cached.Node);
                return cached.Texture;
            }

            var texture = CreateTexture(data);
            if (texture == null)
            {
                return null;
            }

            var node = Recency.AddLast(state.CacheKey);
            Textures[state.CacheKey] = new CacheEntry(texture, node);
            Trim();
            return texture;
        }

        public static void Clear()
        {
            foreach (var entry in Textures.Values)
            {
                if (entry.Texture != null)
                {
                    UnityEngine.Object.DestroyImmediate(entry.Texture);
                }
            }

            Textures.Clear();
            Recency.Clear();
        }

        private static void Trim()
        {
            while (Textures.Count > MaximumTextureCount && Recency.First != null)
            {
                var key = Recency.First.Value;
                Recency.RemoveFirst();

                CacheEntry entry;
                if (!Textures.TryGetValue(key, out entry))
                {
                    continue;
                }

                Textures.Remove(key);
                if (entry.Texture != null)
                {
                    UnityEngine.Object.DestroyImmediate(entry.Texture);
                }
            }
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

        private sealed class CacheEntry
        {
            public CacheEntry(Texture2D texture, LinkedListNode<string> node)
            {
                Texture = texture;
                Node = node;
            }

            public Texture2D Texture { get; }

            public LinkedListNode<string> Node { get; }
        }
    }
}

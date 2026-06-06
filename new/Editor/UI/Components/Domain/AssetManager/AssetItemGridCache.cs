using System;
using System.Collections.Generic;
using UnityEngine;

namespace Ee4v.UI
{
    internal static class AssetItemGridCache
    {
        private static readonly Dictionary<string, CachedGridState> Cache = new Dictionary<string, CachedGridState>(StringComparer.Ordinal);
        private static bool _cacheInvalidationRegistered;

        public static void EnsureCacheInvalidationRegistered()
        {
            if (_cacheInvalidationRegistered)
            {
                return;
            }

            _cacheInvalidationRegistered = true;
            AssetManagerItemListProviderRegistry.SessionCacheCleared += Clear;
        }

        public static bool TryGet(AssetManagerItemListRequest request, out ItemGridState gridState, out string statusText)
        {
            CachedGridState cached;
            if (Cache.TryGetValue(CreateCacheKey(request), out cached))
            {
                gridState = cached.GridState;
                statusText = cached.StatusText;
                return true;
            }

            gridState = null;
            statusText = string.Empty;
            return false;
        }

        public static void Store(AssetManagerItemListRequest request, AssetManagerItemList itemList, out ItemGridState gridState, out string statusText)
        {
            var cacheKey = CreateCacheKey(request);
            CachedGridState existing;
            if (Cache.TryGetValue(cacheKey, out existing))
            {
                DestroyThumbnails(existing);
            }

            var list = itemList ?? new AssetManagerItemList(null);
            var itemCardStates = new List<ItemCardState>(list.Items.Count);
            for (var i = 0; i < list.Items.Count; i++)
            {
                var item = list.Items[i];
                if (item == null)
                {
                    continue;
                }

                itemCardStates.Add(new ItemCardState(item.ItemName, CreateThumbnail(item.ThumbnailData)));
            }

            gridState = new ItemGridState(itemCardStates, list.ItemsPerRow);
            statusText = itemCardStates.Count == 0 ? list.EmptyText : string.Empty;
            Cache[cacheKey] = new CachedGridState(gridState, statusText);
        }

        private static string CreateCacheKey(AssetManagerItemListRequest request)
        {
            var viewId = request != null ? request.ViewId : string.Empty;
            var limit = request != null ? request.Limit : 200;
            return AssetManagerItemListProviderRegistry.CacheVersion + "|" + viewId + "|" + limit;
        }

        private static Texture2D CreateThumbnail(byte[] data)
        {
            if (data == null || data.Length == 0)
            {
                return null;
            }

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

        private static void Clear()
        {
            foreach (var cached in Cache.Values)
            {
                DestroyThumbnails(cached);
            }

            Cache.Clear();
        }

        private static void DestroyThumbnails(CachedGridState cached)
        {
            if (cached == null || cached.GridState == null || cached.GridState.Items == null)
            {
                return;
            }

            for (var i = 0; i < cached.GridState.Items.Count; i++)
            {
                var item = cached.GridState.Items[i];
                if (item != null && item.Thumbnail != null)
                {
                    UnityEngine.Object.DestroyImmediate(item.Thumbnail);
                }
            }
        }

        private sealed class CachedGridState
        {
            public CachedGridState(ItemGridState gridState, string statusText)
            {
                GridState = gridState;
                StatusText = statusText ?? string.Empty;
            }

            public ItemGridState GridState { get; }

            public string StatusText { get; }
        }
    }
}

using System;
using System.Collections.Generic;
using Ee4v.UI;
using UnityEditor;

namespace Ee4v.AssetManager.Api
{
    [InitializeOnLoad]
    internal static class AssetManagerDatabaseItemListProviderRegistration
    {
        static AssetManagerDatabaseItemListProviderRegistration()
        {
            AssetManagerItemListProviderRegistry.SetProvider(new AssetManagerDatabaseItemListProvider());
        }
    }

    internal sealed class AssetManagerDatabaseItemListProvider : IAssetManagerItemListProvider, IAssetManagerItemListCacheControl
    {
        private readonly Dictionary<string, AssetManagerItemList> _cache = new Dictionary<string, AssetManagerItemList>(StringComparer.Ordinal);
        private readonly object _cacheLock = new object();

        public AssetManagerItemList GetItems(AssetManagerItemListRequest request)
        {
            var cacheKey = CreateCacheKey(request);
            lock (_cacheLock)
            {
                AssetManagerItemList cached;
                if (_cache.TryGetValue(cacheKey, out cached))
                {
                    return cached;
                }
            }

            var query = CreateQuery(request);
            var result = AssetManagerApi.SearchItems(query);
            var items = new List<AssetManagerItemListItem>();
            if (result == null || result.Items == null)
            {
                return Cache(cacheKey, new AssetManagerItemList(items, "No asset items."));
            }

            for (var i = 0; i < result.Items.Count; i++)
            {
                var item = result.Items[i];
                if (item == null)
                {
                    continue;
                }

                items.Add(new AssetManagerItemListItem(item.Name, LoadThumbnailData(item.Id)));
            }

            return Cache(cacheKey, new AssetManagerItemList(items, "No asset items."));
        }

        public void ClearCache()
        {
            lock (_cacheLock)
            {
                _cache.Clear();
            }
        }

        private static AssetItemQuery CreateQuery(AssetManagerItemListRequest request)
        {
            var viewId = request != null ? request.ViewId : string.Empty;
            var query = new AssetItemQuery
            {
                Limit = request != null ? request.Limit : 200
            };

            if (string.Equals(viewId, "booth-library", StringComparison.Ordinal))
            {
                query.SourceTypes = new[] { AssetSourceType.Blm, AssetSourceType.Eagle };
            }
            return query;
        }

        private AssetManagerItemList Cache(string cacheKey, AssetManagerItemList itemList)
        {
            lock (_cacheLock)
            {
                _cache[cacheKey] = itemList;
            }

            return itemList;
        }

        private static string CreateCacheKey(AssetManagerItemListRequest request)
        {
            var viewId = request != null ? request.ViewId : string.Empty;
            var limit = request != null ? request.Limit : 200;
            return viewId + "|" + limit;
        }

        private static byte[] LoadThumbnailData(string itemId)
        {
            var thumbnail = AssetManagerApi.GetThumbnail(itemId);
            return thumbnail != null && thumbnail.Found ? thumbnail.Data : Array.Empty<byte>();
        }
    }
}

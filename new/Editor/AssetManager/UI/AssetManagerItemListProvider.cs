using System;
using System.Collections.Generic;

namespace Ee4v.UI
{
    internal sealed class AssetManagerItemListRequest
    {
        public AssetManagerItemListRequest(string viewId, int limit = 200)
        {
            ViewId = viewId ?? string.Empty;
            Limit = limit <= 0 ? 200 : limit;
        }

        public string ViewId { get; }

        public int Limit { get; }
    }

    internal sealed class AssetManagerItemList
    {
        public AssetManagerItemList(IReadOnlyList<AssetManagerItemListItem> items, string emptyText = null, int itemsPerRow = 6)
        {
            Items = items ?? Array.Empty<AssetManagerItemListItem>();
            EmptyText = emptyText ?? string.Empty;
            ItemsPerRow = Math.Max(1, itemsPerRow);
        }

        public IReadOnlyList<AssetManagerItemListItem> Items { get; }

        public string EmptyText { get; }

        public int ItemsPerRow { get; }
    }

    internal sealed class AssetManagerItemListItem
    {
        public AssetManagerItemListItem(string itemName, byte[] thumbnailData = null)
        {
            ItemName = itemName ?? string.Empty;
            ThumbnailData = thumbnailData ?? Array.Empty<byte>();
        }

        public string ItemName { get; }

        public byte[] ThumbnailData { get; }
    }

    internal interface IAssetManagerItemListProvider
    {
        AssetManagerItemList GetItems(AssetManagerItemListRequest request);
    }

    internal interface IAssetManagerItemListCacheControl
    {
        void ClearCache();
    }

    internal static class AssetManagerItemListProviderRegistry
    {
        private static readonly IAssetManagerItemListProvider EmptyProvider = new EmptyAssetManagerItemListProvider();

        private static IAssetManagerItemListProvider _current = EmptyProvider;
        private static int _cacheVersion;

        public static event Action SessionCacheCleared;

        public static IAssetManagerItemListProvider Current
        {
            get { return _current ?? EmptyProvider; }
        }

        public static IAssetManagerItemListProvider GetCurrent()
        {
            return Current;
        }

        public static int CacheVersion
        {
            get { return _cacheVersion; }
        }

        public static void SetProvider(IAssetManagerItemListProvider provider)
        {
            _current = provider ?? EmptyProvider;
        }

        public static void ClearSessionCache()
        {
            var cacheControl = Current as IAssetManagerItemListCacheControl;
            if (cacheControl != null)
            {
                cacheControl.ClearCache();
            }

            _cacheVersion++;
            SessionCacheCleared?.Invoke();
        }

        private sealed class EmptyAssetManagerItemListProvider : IAssetManagerItemListProvider
        {
            public AssetManagerItemList GetItems(AssetManagerItemListRequest request)
            {
                return new AssetManagerItemList(Array.Empty<AssetManagerItemListItem>(), "No asset items.");
            }
        }
    }
}

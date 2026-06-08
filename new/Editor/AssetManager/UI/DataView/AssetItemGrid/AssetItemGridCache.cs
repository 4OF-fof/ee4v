using System;
using System.Collections.Generic;

namespace Ee4v.UI
{
    internal static class AssetItemGridCache
    {
        private static readonly Dictionary<string, CachedGridState> Cache = new Dictionary<string, CachedGridState>(StringComparer.Ordinal);

        public static bool TryGet(string cacheKey, out ItemGridState gridState, out string statusText)
        {
            CachedGridState cached;
            if (Cache.TryGetValue(cacheKey ?? string.Empty, out cached))
            {
                gridState = cached.GridState;
                statusText = cached.StatusText;
                return true;
            }

            gridState = null;
            statusText = string.Empty;
            return false;
        }

        public static void Store(string cacheKey, AssetItemGridList itemList, out ItemGridState gridState, out string statusText)
        {
            cacheKey = cacheKey ?? string.Empty;
            Cache.Remove(cacheKey);

            var list = itemList ?? new AssetItemGridList(null);
            var itemCardStates = new List<ItemCardState>(list.Items.Count);
            for (var i = 0; i < list.Items.Count; i++)
            {
                var item = list.Items[i];
                if (item == null)
                {
                    continue;
                }

                itemCardStates.Add(new ItemCardState(item.ItemName, item.ImageState));
            }

            gridState = new ItemGridState(itemCardStates, list.ItemsPerRow);
            statusText = itemCardStates.Count == 0 ? list.EmptyText : string.Empty;
            Cache[cacheKey] = new CachedGridState(gridState, statusText);
        }

        public static void Clear()
        {
            Cache.Clear();
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

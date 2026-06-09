using System;
using System.Collections.Generic;

namespace Ee4v.UI
{
    internal static class ItemGridStateCache
    {
        private static readonly Dictionary<string, CachedItemGridState> Cache = new Dictionary<string, CachedItemGridState>(StringComparer.Ordinal);

        public static bool TryGet(string cacheKey, out ItemGridState gridState, out string statusText)
        {
            CachedItemGridState cached;
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

        public static void Store(string cacheKey, ItemGridState gridState, string statusText)
        {
            Cache[cacheKey ?? string.Empty] = new CachedItemGridState(gridState, statusText);
        }

        public static void Clear()
        {
            Cache.Clear();
        }

        private sealed class CachedItemGridState
        {
            public CachedItemGridState(ItemGridState gridState, string statusText)
            {
                GridState = gridState ?? new ItemGridState(null);
                StatusText = statusText ?? string.Empty;
            }

            public ItemGridState GridState { get; }

            public string StatusText { get; }
        }
    }
}

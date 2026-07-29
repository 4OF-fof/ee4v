using System;
using System.Collections.Generic;
using System.Linq;
using Ee4v.AssetManager.Contracts;

namespace Ee4v.AssetManager.Application
{
    internal enum AssetManagerSnapshotInvalidation
    {
        None,
        Catalog,
        CatalogAndThumbnails
    }

    internal sealed class AssetCatalogSnapshotItem
    {
        internal AssetCatalogSnapshotItem(
            AssetItem item,
            bool hasBoothInformation,
            bool isUncategorized,
            IReadOnlyList<string> collectionIds,
            IReadOnlyList<string> keywordValues)
        {
            Item = item ?? throw new ArgumentNullException(nameof(item));
            HasBoothInformation = hasBoothInformation;
            IsUncategorized = isUncategorized;
            CollectionIds = (collectionIds ??
                             Array.Empty<string>())
                .ToArray();
            KeywordValues = (keywordValues ??
                             Array.Empty<string>())
                .ToArray();
        }

        internal AssetItem Item { get; }

        internal bool HasBoothInformation { get; }

        internal bool IsUncategorized { get; }

        internal IReadOnlyList<string> CollectionIds { get; }

        internal IReadOnlyList<string> KeywordValues { get; }
    }

    internal sealed class AssetCatalogSnapshot
    {
        internal AssetCatalogSnapshot(
            IReadOnlyList<AssetCatalogSnapshotItem> items,
            IReadOnlyList<string> collectionIds)
        {
            Items = (items ??
                     Array.Empty<AssetCatalogSnapshotItem>())
                .ToArray();
            CollectionIds = (collectionIds ??
                             Array.Empty<string>())
                .ToArray();
        }

        internal IReadOnlyList<AssetCatalogSnapshotItem> Items { get; }

        internal IReadOnlyList<string> CollectionIds { get; }
    }

    internal sealed class AssetCatalogSnapshotCache
    {
        private readonly Func<AssetCatalogSnapshot> _load;
        private readonly object _gate = new object();
        private volatile AssetCatalogSnapshot _snapshot;

        internal AssetCatalogSnapshotCache(
            Func<AssetCatalogSnapshot> load)
        {
            _load = load ?? throw new ArgumentNullException(nameof(load));
        }

        internal static bool CanSearch(AssetItemQuery query)
        {
            return query == null ||
                   (!query.Lifecycle.HasValue &&
                    (query.TagIds == null || query.TagIds.Count == 0) &&
                    (query.SourceTypes == null ||
                     query.SourceTypes.Count == 0));
        }

        internal AssetSearchResult Search(AssetItemQuery query)
        {
            return Search(GetSnapshot(), query);
        }

        internal bool TrySearch(
            AssetItemQuery query,
            out AssetSearchResult result)
        {
            var snapshot = _snapshot;
            if (snapshot == null)
            {
                result = null;
                return false;
            }

            result = Search(snapshot, query);
            return true;
        }

        private static AssetSearchResult Search(
            AssetCatalogSnapshot snapshot,
            AssetItemQuery query)
        {
            var collectionId =
                query != null ? query.CollectionId : null;
            if (collectionId != null &&
                !Contains(
                    snapshot.CollectionIds,
                    collectionId))
            {
                throw new AssetManagerException(
                    AssetManagerErrorCode.NotFound,
                    "Collection was not found: " + collectionId);
            }

            var offset =
                query != null && query.Offset > 0
                    ? query.Offset
                    : 0;
            var limit =
                query != null && query.Limit > 0
                    ? query.Limit
                    : 100;
            var items = new List<AssetItem>(
                Math.Min(limit, snapshot.Items.Count));
            var totalCount = 0;
            for (var i = 0; i < snapshot.Items.Count; i++)
            {
                var entry = snapshot.Items[i];
                if (!Matches(entry, query, collectionId))
                {
                    continue;
                }

                if (totalCount >= offset &&
                    items.Count < limit)
                {
                    items.Add(CloneSummary(entry.Item));
                }

                totalCount++;
            }

            return new AssetSearchResult
            {
                Items = items,
                TotalCount = totalCount
            };
        }

        private static bool Matches(
            AssetCatalogSnapshotItem entry,
            AssetItemQuery query,
            string collectionId)
        {
            if ((query == null ||
                 !query.IncludeUnavailable) &&
                !entry.Item.IsAvailable)
            {
                return false;
            }

            if (query != null &&
                !string.IsNullOrWhiteSpace(query.Keyword) &&
                !ContainsKeyword(
                    entry.KeywordValues,
                    query.Keyword))
            {
                return false;
            }

            if (collectionId != null &&
                !Contains(entry.CollectionIds, collectionId))
            {
                return false;
            }

            if (query != null &&
                query.HasBoothInformation &&
                !entry.HasBoothInformation)
            {
                return false;
            }

            return query == null ||
                   !query.UncategorizedOnly ||
                   entry.IsUncategorized;
        }

        internal void Invalidate()
        {
            lock (_gate)
            {
                _snapshot = null;
            }
        }

        private AssetCatalogSnapshot GetSnapshot()
        {
            lock (_gate)
            {
                if (_snapshot == null)
                {
                    _snapshot = _load() ??
                                new AssetCatalogSnapshot(
                                    null,
                                    null);
                }

                return _snapshot;
            }
        }

        private static bool Contains(
            IReadOnlyList<string> values,
            string expected)
        {
            for (var i = 0; i < values.Count; i++)
            {
                if (string.Equals(
                        values[i],
                        expected,
                        StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool ContainsKeyword(
            IReadOnlyList<string> values,
            string keyword)
        {
            for (var i = 0; i < values.Count; i++)
            {
                if ((values[i] ?? string.Empty).IndexOf(
                        keyword,
                        StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }

            return false;
        }

        private static AssetItem CloneSummary(AssetItem item)
        {
            return new AssetItem
            {
                Id = item.Id,
                Name = item.Name,
                Description = item.Description,
                IsAvailable = item.IsAvailable,
                Booth = null,
                Tags = Array.Empty<AssetTag>(),
                Files = Array.Empty<AssetFileSummary>(),
                CreatedAt = item.CreatedAt,
                UpdatedAt = item.UpdatedAt
            };
        }
    }

    internal sealed class AssetThumbnailSnapshotCache
    {
        private readonly Func<
            IReadOnlyList<string>,
            IReadOnlyDictionary<string, AssetThumbnail>> _loadMany;
        private readonly Func<string, AssetThumbnail> _loadOne;
        private readonly object _gate = new object();
        private readonly Dictionary<string, AssetThumbnail> _thumbnails =
            new Dictionary<string, AssetThumbnail>(
                StringComparer.Ordinal);
        private readonly HashSet<string> _loadedIds =
            new HashSet<string>(StringComparer.Ordinal);
        private int _generation;

        internal AssetThumbnailSnapshotCache(
            Func<
                IReadOnlyList<string>,
                IReadOnlyDictionary<string, AssetThumbnail>> loadMany,
            Func<string, AssetThumbnail> loadOne)
        {
            _loadMany = loadMany ??
                        throw new ArgumentNullException(nameof(loadMany));
            _loadOne = loadOne ??
                       throw new ArgumentNullException(nameof(loadOne));
        }

        internal AssetThumbnail Get(string itemId)
        {
            var normalizedId = itemId ?? string.Empty;
            while (true)
            {
                int generation;
                lock (_gate)
                {
                    if (_loadedIds.Contains(normalizedId))
                    {
                        AssetThumbnail cached;
                        _thumbnails.TryGetValue(
                            normalizedId,
                            out cached);
                        return cached;
                    }

                    generation = _generation;
                }

                var loaded = _loadOne(itemId);
                lock (_gate)
                {
                    if (generation != _generation)
                    {
                        continue;
                    }

                    _loadedIds.Add(normalizedId);
                    if (loaded != null)
                    {
                        _thumbnails[normalizedId] = loaded;
                    }

                    return loaded;
                }
            }
        }

        internal IReadOnlyDictionary<string, AssetThumbnail> GetMany(
            IReadOnlyList<string> itemIds)
        {
            var requestedIds = NormalizeItemIds(itemIds);
            while (true)
            {
                string[] missingIds;
                int generation;
                lock (_gate)
                {
                    missingIds = requestedIds
                        .Where(itemId =>
                            !_loadedIds.Contains(itemId))
                        .ToArray();
                    if (missingIds.Length == 0)
                    {
                        return CreateResult(requestedIds);
                    }

                    generation = _generation;
                }

                var loaded =
                    _loadMany(missingIds) ??
                    new Dictionary<string, AssetThumbnail>(
                        StringComparer.Ordinal);
                lock (_gate)
                {
                    if (generation != _generation)
                    {
                        continue;
                    }

                    foreach (var pair in loaded)
                    {
                        if (!string.IsNullOrWhiteSpace(
                                pair.Key) &&
                            pair.Value != null)
                        {
                            _thumbnails[pair.Key] =
                                pair.Value;
                        }
                    }

                    for (var i = 0; i < missingIds.Length; i++)
                    {
                        _loadedIds.Add(missingIds[i]);
                    }

                    return CreateResult(requestedIds);
                }
            }
        }

        internal bool TryGetMany(
            IReadOnlyList<string> itemIds,
            out IReadOnlyDictionary<string, AssetThumbnail> thumbnails)
        {
            var requestedIds = NormalizeItemIds(itemIds);
            if (!System.Threading.Monitor.TryEnter(_gate))
            {
                thumbnails =
                    new Dictionary<string, AssetThumbnail>(
                        StringComparer.Ordinal);
                return false;
            }

            try
            {
                var allLoaded = true;
                for (var i = 0; i < requestedIds.Length; i++)
                {
                    if (!_loadedIds.Contains(requestedIds[i]))
                    {
                        allLoaded = false;
                        break;
                    }
                }

                thumbnails = CreateResult(requestedIds);
                return allLoaded;
            }
            finally
            {
                System.Threading.Monitor.Exit(_gate);
            }
        }

        internal void Invalidate()
        {
            lock (_gate)
            {
                _generation++;
                _thumbnails.Clear();
                _loadedIds.Clear();
            }
        }

        private static string[] NormalizeItemIds(
            IReadOnlyList<string> itemIds)
        {
            return (itemIds ?? Array.Empty<string>())
                .Where(itemId =>
                    !string.IsNullOrWhiteSpace(itemId))
                .Distinct(StringComparer.Ordinal)
                .ToArray();
        }

        private IReadOnlyDictionary<string, AssetThumbnail>
            CreateResult(IReadOnlyList<string> requestedIds)
        {
            var result =
                new Dictionary<string, AssetThumbnail>(
                    requestedIds.Count,
                    StringComparer.Ordinal);
            for (var i = 0; i < requestedIds.Count; i++)
            {
                AssetThumbnail thumbnail;
                if (_thumbnails.TryGetValue(
                        requestedIds[i],
                        out thumbnail))
                {
                    result[requestedIds[i]] = thumbnail;
                }
            }

            return result;
        }
    }

    internal sealed class AssetManagerReadSnapshot
    {
        private readonly AssetCatalogSnapshotCache _catalog;
        private readonly AssetThumbnailSnapshotCache _thumbnails;

        internal AssetManagerReadSnapshot(
            Func<AssetCatalogSnapshot> loadCatalog,
            Func<
                IReadOnlyList<string>,
                IReadOnlyDictionary<string, AssetThumbnail>>
                loadThumbnails,
            Func<string, AssetThumbnail> loadThumbnail)
        {
            _catalog =
                new AssetCatalogSnapshotCache(loadCatalog);
            _thumbnails =
                new AssetThumbnailSnapshotCache(
                    loadThumbnails,
                    loadThumbnail);
        }

        internal bool CanSearch(AssetItemQuery query) =>
            AssetCatalogSnapshotCache.CanSearch(query);

        internal AssetSearchResult Search(
            AssetItemQuery query) =>
            _catalog.Search(query);

        internal bool TrySearch(
            AssetItemQuery query,
            out AssetSearchResult result)
        {
            if (!AssetCatalogSnapshotCache.CanSearch(query))
            {
                result = null;
                return false;
            }

            return _catalog.TrySearch(query, out result);
        }

        internal AssetThumbnail GetThumbnail(string itemId) =>
            _thumbnails.Get(itemId);

        internal IReadOnlyDictionary<string, AssetThumbnail>
            GetThumbnails(IReadOnlyList<string> itemIds) =>
            _thumbnails.GetMany(itemIds);

        internal bool TryGetThumbnails(
            IReadOnlyList<string> itemIds,
            out IReadOnlyDictionary<string, AssetThumbnail>
                thumbnails) =>
            _thumbnails.TryGetMany(itemIds, out thumbnails);

        internal void Invalidate(
            AssetManagerSnapshotInvalidation invalidation)
        {
            if (invalidation ==
                AssetManagerSnapshotInvalidation.None)
            {
                return;
            }

            _catalog.Invalidate();
            if (invalidation ==
                AssetManagerSnapshotInvalidation
                    .CatalogAndThumbnails)
            {
                _thumbnails.Invalidate();
            }
        }
    }
}

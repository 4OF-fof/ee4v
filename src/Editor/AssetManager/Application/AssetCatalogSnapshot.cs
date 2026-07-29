using System;
using System.Collections.Generic;
using System.Linq;
using Ee4v.AssetManager.Contracts;

namespace Ee4v.AssetManager.Application
{
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
            CollectionIds = collectionIds ?? Array.Empty<string>();
            KeywordValues = keywordValues ?? Array.Empty<string>();
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
            Items = items ?? Array.Empty<AssetCatalogSnapshotItem>();
            CollectionIds = collectionIds ?? Array.Empty<string>();
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

            IEnumerable<AssetCatalogSnapshotItem> filtered =
                snapshot.Items;
            if (query == null || !query.IncludeUnavailable)
            {
                filtered = filtered.Where(entry =>
                    entry.Item.IsAvailable);
            }

            if (query != null &&
                !string.IsNullOrWhiteSpace(query.Keyword))
            {
                filtered = filtered.Where(entry =>
                    ContainsKeyword(
                        entry.KeywordValues,
                        query.Keyword));
            }

            if (collectionId != null)
            {
                filtered = filtered.Where(entry =>
                    Contains(entry.CollectionIds, collectionId));
            }

            if (query != null && query.HasBoothInformation)
            {
                filtered = filtered.Where(entry =>
                    entry.HasBoothInformation);
            }

            if (query != null && query.UncategorizedOnly)
            {
                filtered = filtered.Where(entry =>
                    entry.IsUncategorized);
            }

            var matches = filtered.ToArray();
            var offset =
                query != null && query.Offset > 0
                    ? query.Offset
                    : 0;
            var limit =
                query != null && query.Limit > 0
                    ? query.Limit
                    : 100;
            return new AssetSearchResult
            {
                Items = matches
                    .Skip(offset)
                    .Take(limit)
                    .Select(entry => CloneSummary(entry.Item))
                    .ToArray(),
                TotalCount = matches.Length
            };
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
            lock (_gate)
            {
                AssetThumbnail cached;
                if (_thumbnails.TryGetValue(
                        itemId ?? string.Empty,
                        out cached))
                {
                    return cached;
                }

                var loaded = _loadOne(itemId);
                _thumbnails[itemId ?? string.Empty] = loaded;
                _loadedIds.Add(itemId ?? string.Empty);
                return loaded;
            }
        }

        internal IReadOnlyDictionary<string, AssetThumbnail> GetMany(
            IReadOnlyList<string> itemIds)
        {
            var requestedIds = (itemIds ?? Array.Empty<string>())
                .Where(itemId =>
                    !string.IsNullOrWhiteSpace(itemId))
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            lock (_gate)
            {
                var missingIds = new List<string>();
                for (var i = 0; i < requestedIds.Length; i++)
                {
                    if (!_loadedIds.Contains(
                            requestedIds[i]))
                    {
                        missingIds.Add(requestedIds[i]);
                    }
                }

                if (missingIds.Count > 0)
                {
                    var loaded = _loadMany(missingIds);
                    foreach (var pair in loaded)
                    {
                        _thumbnails[pair.Key] = pair.Value;
                    }

                    for (var i = 0; i < missingIds.Count; i++)
                    {
                        _loadedIds.Add(missingIds[i]);
                    }
                }

                return CreateResult(requestedIds);
            }
        }

        internal bool TryGetMany(
            IReadOnlyList<string> itemIds,
            out IReadOnlyDictionary<string, AssetThumbnail> thumbnails)
        {
            var requestedIds = (itemIds ?? Array.Empty<string>())
                .Where(itemId =>
                    !string.IsNullOrWhiteSpace(itemId))
                .Distinct(StringComparer.Ordinal)
                .ToArray();
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
                _thumbnails.Clear();
                _loadedIds.Clear();
            }
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
}

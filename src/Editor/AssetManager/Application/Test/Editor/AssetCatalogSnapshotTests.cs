using System;
using System.Collections.Generic;
using System.Linq;
using Ee4v.AssetManager.Contracts;
using Ee4v.Testing.Contracts;
using NUnit.Framework;

namespace Ee4v.AssetManager.Application.Tests
{
    public sealed class AssetCatalogSnapshotTests
    {
        [Test]
        [FeatureTestCase(
            "一覧切替を同じ catalog snapshot から絞り込む",
            "All、BOOTH Items、Collection、検索語の切替で永続 store を再読込しないことを確認します。",
            order: 6)]
        public void Search_ViewSwitches_LoadSnapshotOnlyOnce()
        {
            var loadCount = 0;
            var cache = new AssetCatalogSnapshotCache(
                () =>
                {
                    loadCount++;
                    return CreateSnapshot();
                });

            Assert.That(
                SearchIds(
                    cache,
                    new AssetItemQuery
                    {
                        Limit = 200
                    }),
                Is.EqualTo(new[]
                {
                    "booth",
                    "plain",
                    "collection"
                }));
            Assert.That(
                SearchIds(
                    cache,
                    new AssetItemQuery
                    {
                        HasBoothInformation = true,
                        Limit = 200
                    }),
                Is.EqualTo(new[] { "booth" }));
            Assert.That(
                SearchIds(
                    cache,
                    new AssetItemQuery
                    {
                        CollectionId = "favorites",
                        Limit = 200
                    }),
                Is.EqualTo(new[] { "collection" }));
            Assert.That(
                SearchIds(
                    cache,
                    new AssetItemQuery
                    {
                        Keyword = "shader",
                        Limit = 200
                    }),
                Is.EqualTo(new[] { "plain" }));
            Assert.That(loadCount, Is.EqualTo(1));
        }

        [Test]
        public void Search_PagingKeepsFullMatchCount()
        {
            var cache = new AssetCatalogSnapshotCache(
                CreateSnapshot);

            var result = cache.Search(
                new AssetItemQuery
                {
                    Offset = 1,
                    Limit = 1
                });

            Assert.That(result.TotalCount, Is.EqualTo(3));
            Assert.That(
                result.Items.Select(item => item.Id),
                Is.EqualTo(new[] { "plain" }));
        }

        [Test]
        [FeatureTestCase(
            "catalog 変更後だけ snapshot を再構築する",
            "通常の表示切替では保持し、明示的な invalidation 後の最初の検索でだけ再読込することを確認します。",
            order: 7)]
        public void Invalidate_NextSearchReloadsSnapshot()
        {
            var loadCount = 0;
            var cache = new AssetCatalogSnapshotCache(
                () =>
                {
                    loadCount++;
                    return CreateSnapshot();
                });

            cache.Search(new AssetItemQuery());
            cache.Search(new AssetItemQuery
            {
                HasBoothInformation = true
            });
            cache.Invalidate();
            cache.Search(new AssetItemQuery());

            Assert.That(loadCount, Is.EqualTo(2));
        }

        [Test]
        public void TrySearch_DoesNotLoadMissingSnapshot()
        {
            var loadCount = 0;
            var cache = new AssetCatalogSnapshotCache(
                () =>
                {
                    loadCount++;
                    return CreateSnapshot();
                });

            AssetSearchResult result;
            Assert.That(
                cache.TrySearch(
                    new AssetItemQuery(),
                    out result),
                Is.False);
            Assert.That(result, Is.Null);
            Assert.That(loadCount, Is.Zero);

            cache.Search(new AssetItemQuery());

            Assert.That(
                cache.TrySearch(
                    new AssetItemQuery
                    {
                        HasBoothInformation = true
                    },
                    out result),
                Is.True);
            Assert.That(
                result.Items.Select(item => item.Id),
                Is.EqualTo(new[] { "booth" }));
            Assert.That(loadCount, Is.EqualTo(1));
        }

        [Test]
        public void CanSearch_LeavesUnsupportedDetailFiltersToStore()
        {
            Assert.That(
                AssetCatalogSnapshotCache.CanSearch(
                    new AssetItemQuery
                    {
                        Lifecycle =
                            AssetFileLifecycle.Active
                    }),
                Is.False);
            Assert.That(
                AssetCatalogSnapshotCache.CanSearch(
                    new AssetItemQuery
                    {
                        TagIds = new[] { "tag" }
                    }),
                Is.False);
            Assert.That(
                AssetCatalogSnapshotCache.CanSearch(
                    new AssetItemQuery
                    {
                        SourceTypes = new[]
                        {
                            AssetSourceType.Eagle
                        }
                    }),
                Is.False);
        }

        [Test]
        public void ThumbnailCache_LoadsOnlyPreviouslyUnseenItems()
        {
            var loadedBatches =
                new List<IReadOnlyList<string>>();
            var cache = new AssetThumbnailSnapshotCache(
                itemIds =>
                {
                    loadedBatches.Add(itemIds.ToArray());
                    return itemIds.ToDictionary(
                        itemId => itemId,
                        itemId => new AssetThumbnail
                        {
                            Found = true,
                            Path = itemId + ".png"
                        },
                        StringComparer.Ordinal);
                },
                itemId => new AssetThumbnail
                {
                    Found = true,
                    Path = itemId + ".png"
                });

            cache.GetMany(new[] { "a", "b" });
            cache.GetMany(new[] { "b", "c" });

            Assert.That(loadedBatches.Count, Is.EqualTo(2));
            Assert.That(
                loadedBatches[0],
                Is.EqualTo(new[] { "a", "b" }));
            Assert.That(
                loadedBatches[1],
                Is.EqualTo(new[] { "c" }));
        }

        [Test]
        public void TryGetMany_ReturnsCachedThumbnailsWithoutLoading()
        {
            var loadCount = 0;
            var cache = new AssetThumbnailSnapshotCache(
                itemIds =>
                {
                    loadCount++;
                    return itemIds
                        .Where(itemId => itemId != "missing")
                        .ToDictionary(
                            itemId => itemId,
                            itemId => new AssetThumbnail
                            {
                                Found = true,
                                Path = itemId + ".png"
                            },
                            StringComparer.Ordinal);
                },
                itemId =>
                {
                    throw new InvalidOperationException();
                });

            IReadOnlyDictionary<string, AssetThumbnail>
                thumbnails;
            Assert.That(
                cache.TryGetMany(
                    new[] { "a" },
                    out thumbnails),
                Is.False);
            Assert.That(thumbnails, Is.Empty);
            Assert.That(loadCount, Is.Zero);

            cache.GetMany(new[] { "a", "missing" });

            Assert.That(
                cache.TryGetMany(
                    new[] { "a", "missing" },
                    out thumbnails),
                Is.True);
            Assert.That(
                thumbnails.Keys,
                Is.EqualTo(new[] { "a" }));
            Assert.That(loadCount, Is.EqualTo(1));
        }

        [Test]
        public void ReadSnapshot_InvalidatesCatalogAndThumbnailsByScope()
        {
            var snapshot = new AssetManagerReadSnapshot(
                CreateSnapshot,
                itemIds => itemIds.ToDictionary(
                    itemId => itemId,
                    itemId => new AssetThumbnail
                    {
                        Found = true,
                        Path = itemId + ".png"
                    },
                    StringComparer.Ordinal),
                itemId => new AssetThumbnail
                {
                    Found = true,
                    Path = itemId + ".png"
                });

            snapshot.Search(new AssetItemQuery());
            snapshot.GetThumbnails(new[] { "booth" });

            snapshot.Invalidate(
                AssetManagerSnapshotInvalidation.Catalog);

            AssetSearchResult searchResult;
            IReadOnlyDictionary<string, AssetThumbnail>
                thumbnails;
            Assert.That(
                snapshot.TrySearch(
                    new AssetItemQuery(),
                    out searchResult),
                Is.False);
            Assert.That(
                snapshot.TryGetThumbnails(
                    new[] { "booth" },
                    out thumbnails),
                Is.True);

            snapshot.Invalidate(
                AssetManagerSnapshotInvalidation
                    .CatalogAndThumbnails);

            Assert.That(
                snapshot.TryGetThumbnails(
                    new[] { "booth" },
                    out thumbnails),
                Is.False);
        }

        private static string[] SearchIds(
            AssetCatalogSnapshotCache cache,
            AssetItemQuery query)
        {
            return cache.Search(query).Items
                .Select(item => item.Id)
                .ToArray();
        }

        private static AssetCatalogSnapshot CreateSnapshot()
        {
            return new AssetCatalogSnapshot(
                new[]
                {
                    CreateEntry(
                        "booth",
                        "Booth Avatar",
                        hasBoothInformation: true),
                    CreateEntry(
                        "plain",
                        "Plain Asset",
                        keywordValues:
                        new[] { "Plain Asset", "toon shader" }),
                    CreateEntry(
                        "collection",
                        "Collected Asset",
                        collectionIds:
                        new[] { "favorites" })
                },
                new[] { "favorites" });
        }

        private static AssetCatalogSnapshotItem CreateEntry(
            string id,
            string name,
            bool hasBoothInformation = false,
            IReadOnlyList<string> collectionIds = null,
            IReadOnlyList<string> keywordValues = null)
        {
            var memberships =
                collectionIds ?? Array.Empty<string>();
            return new AssetCatalogSnapshotItem(
                new AssetItem
                {
                    Id = id,
                    Name = name,
                    Description = string.Empty,
                    IsAvailable = true,
                    Tags = Array.Empty<AssetTag>(),
                    Files =
                        Array.Empty<AssetFileSummary>()
                },
                hasBoothInformation,
                memberships,
                keywordValues ?? new[] { name });
        }
    }
}

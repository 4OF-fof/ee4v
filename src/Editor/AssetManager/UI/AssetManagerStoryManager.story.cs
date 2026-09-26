using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Ee4v.AssetManager.Contracts;

namespace Ee4v.AssetManager.UI
{
    internal sealed class AssetManagerStoryManager : IAssetManager
    {
        private const string SampleGuid =
            "0123456789abcdef0123456789abcdef";

        private readonly IReadOnlyList<AssetFile> _files;
        private readonly IReadOnlyList<AssetItem> _items;
        private IReadOnlyList<AssetCollection> _collections;
        private readonly Dictionary<string, IReadOnlyList<AssetFileTarget>>
            _itemTargets;

        public AssetManagerStoryManager()
        {
            _files = CreateFiles();
            _items = CreateItems(_files);
            _itemTargets = _items.ToDictionary(
                item => item.Id,
                item => (IReadOnlyList<AssetFileTarget>)_files
                    .Where(file => file.ItemId == item.Id)
                    .Select(file => new AssetFileTarget
                    {
                        FileId = file.Id,
                        TargetPath = string.Equals(
                            Path.GetExtension(file.FileName),
                            ".zip",
                            StringComparison.OrdinalIgnoreCase)
                            ? "Packages/Sample.unitypackage"
                            : string.Empty
                    })
                    .ToArray());
            _collections = new[]
            {
                new AssetCollection
                {
                    Id = "collection-avatar",
                    Name = "Avatars",
                    Root = AssetFilterNode.Condition(
                        AssetFilterConditionType.HasTag,
                        "Avatar")
                },
                new AssetCollection
                {
                    Id = "collection-world",
                    Name = "Worlds",
                    Root = AssetFilterNode.Condition(
                        AssetFilterConditionType.HasTag,
                        "World")
                },
                new AssetCollection
                {
                    Id = "collection-packages",
                    Name = "Unity Packages",
                    Root = AssetFilterNode.Condition(
                        AssetFilterConditionType.HasFileExtension,
                        ".unitypackage")
                }
            };
        }

        public event Action<AssetManagerChange> Changed;

        public AssetSearchResult SearchItems(AssetItemQuery query = null)
        {
            query = query ?? new AssetItemQuery();
            var search = GetFirstConditionValue(query.Filter);
            var matches = _items
                .Where(item => query.IncludeArchived || !item.IsArchived)
                .Where(item => string.IsNullOrEmpty(search) ||
                    Contains(item.Name, search) ||
                    Contains(item.Description, search))
                .ToArray();
            return Page(matches, query.Offset, query.Limit);
        }

        public AssetSearchResult SearchCollection(
            string collectionId,
            int offset = 0,
            int limit = 0)
        {
            var condition = GetCollection(collectionId).Root;
            var matches = _items
                .Where(item => !item.IsArchived)
                .Where(item => condition.ConditionType ==
                    AssetFilterConditionType.HasFileExtension
                    ? item.Files.Any(file => string.Equals(
                        file.Extension, condition.Value,
                        StringComparison.OrdinalIgnoreCase))
                    : item.Tags.Any(tag => string.Equals(
                        tag.Path, condition.Value,
                        StringComparison.OrdinalIgnoreCase)))
                .ToArray();
            return Page(matches, offset, limit);
        }

        public bool MatchesCollection(string collectionId, string itemId)
        {
            return SearchCollection(collectionId)
                .Items.Any(item => item.Id == itemId);
        }

        public AssetItem GetItem(string itemId)
        {
            return _items.First(item => item.Id == itemId);
        }

        public Task<AssetThumbnail> GetThumbnail(
            string itemId,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(new AssetThumbnail
            {
                Found = false,
                MissingReason = "Story preview"
            });
        }

        public async Task<IReadOnlyDictionary<string, AssetThumbnail>>
            GetThumbnails(
                IReadOnlyList<string> itemIds,
                CancellationToken cancellationToken = default)
        {
            var result = new Dictionary<string, AssetThumbnail>();
            foreach (var itemId in itemIds ?? Array.Empty<string>())
            {
                result[itemId] = await GetThumbnail(itemId, cancellationToken);
            }

            return result;
        }

        public AssetItem CreateItem(CreateAssetItemRequest request)
        {
            return _items[0];
        }

        public AssetItem UpdateItem(
            string itemId,
            UpdateAssetItemRequest request)
        {
            return GetItem(itemId);
        }

        public IReadOnlyList<AssetItem> SetItemArchived(
            IReadOnlyList<string> itemIds,
            bool archived)
        {
            return SelectItems(itemIds);
        }

        public void DeleteItem(IReadOnlyList<string> itemIds)
        {
        }

        public AssetFile GetFile(string fileId)
        {
            return _files.First(file => file.Id == fileId);
        }

        public IReadOnlyList<AssetFile> GetFiles(
            string itemId,
            bool includeArchived = false)
        {
            return _files
                .Where(file => file.ItemId == itemId)
                .Where(file => includeArchived || !file.IsArchived)
                .ToArray();
        }

        public IReadOnlyList<AssetFile> GetUnassignedFiles(
            bool includeArchived = false)
        {
            return _files
                .Where(file => string.IsNullOrEmpty(file.ItemId))
                .Where(file => includeArchived || !file.IsArchived)
                .ToArray();
        }

        public AssetFile RegisterFile(
            string itemId,
            RegisterFileRequest request)
        {
            return _files[0];
        }

        public IReadOnlyList<AssetFile> SetFileItem(
            IReadOnlyList<string> fileIds,
            string itemId)
        {
            return SelectFiles(fileIds);
        }

        public IReadOnlyList<AssetFile> SetFileArchived(
            IReadOnlyList<string> fileIds,
            bool archived)
        {
            return SelectFiles(fileIds);
        }

        public void DeleteFile(IReadOnlyList<string> fileIds)
        {
        }

        public IReadOnlyList<AssetFileTarget> GetItemTargets(string itemId)
        {
            return CopyTargets(_itemTargets.TryGetValue(
                    itemId,
                    out var targets)
                ? targets
                : Array.Empty<AssetFileTarget>());
        }

        private static IReadOnlyList<AssetFileTarget> CopyTargets(
            IEnumerable<AssetFileTarget> targets)
        {
            return targets.Select(target => new AssetFileTarget
                {
                    FileId = target.FileId,
                    TargetPath = target.TargetPath,
                    GroupName = target.GroupName
                })
                .ToArray();
        }

        public IReadOnlyList<AssetFileTarget> SetItemTargets(
            string itemId,
            IReadOnlyList<AssetFileTarget> targets)
        {
            var existing = GetItemTargets(itemId);
            _itemTargets[itemId] = (targets ??
                                    Array.Empty<AssetFileTarget>())
                .GroupBy(
                    target => target.FileId + "\n" + target.TargetPath,
                    StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First())
                .Select(target => new AssetFileTarget
                {
                    FileId = target.FileId,
                    TargetPath = target.TargetPath,
                    GroupName = existing.FirstOrDefault(current =>
                        string.Equals(
                            current.FileId,
                            target.FileId,
                            StringComparison.Ordinal) &&
                        string.Equals(
                            current.TargetPath,
                            target.TargetPath,
                            StringComparison.OrdinalIgnoreCase))?.GroupName
                })
                .ToArray();
            return GetItemTargets(itemId);
        }

        public AssetFileTarget SetItemTargetGroup(
            string itemId,
            string fileId,
            string targetPath,
            string groupName)
        {
            var targets = GetItemTargets(itemId).ToArray();
            var target = targets.First(entry =>
                string.Equals(
                    entry.FileId,
                    fileId,
                    StringComparison.Ordinal) &&
                string.Equals(
                    entry.TargetPath,
                    targetPath,
                    StringComparison.OrdinalIgnoreCase));
            target.GroupName = string.IsNullOrWhiteSpace(groupName)
                ? null
                : groupName.Trim();
            _itemTargets[itemId] = targets;
            return target;
        }

        public Task<AssetImportResult> ImportFileEntries(
            string fileId,
            IReadOnlyList<string> paths,
            CancellationToken cancellationToken = default)
        {
            return Import(fileId, cancellationToken);
        }

        public Task<AssetImportResult> ImportItemTargets(
            string itemId,
            IReadOnlyList<AssetFileTarget> selectedTargets,
            CancellationToken cancellationToken = default)
        {
            var fileId = GetFiles(itemId).FirstOrDefault()?.Id;
            return Import(fileId, cancellationToken);
        }

        public IReadOnlyList<AssetFileDependency> GetFileDependencies(
            string fileId)
        {
            return Array.Empty<AssetFileDependency>();
        }

        public IReadOnlyList<AssetFileDependency> SetFileDependencies(
            IReadOnlyList<string> dependentFileIds,
            IReadOnlyList<AssetFileTarget> dependencyTargets)
        {
            return Array.Empty<AssetFileDependency>();
        }

        public IReadOnlyList<AssetFileDependency> SetFileDependencies(
            IReadOnlyList<AssetFileTarget> dependentTargets,
            IReadOnlyList<AssetFileTarget> dependencyTargets)
        {
            return Array.Empty<AssetFileDependency>();
        }

        public AssetFileAnalysis AnalyzeFile(string fileId)
        {
            var file = GetFile(fileId);
            var isZip = string.Equals(
                Path.GetExtension(file.FileName),
                ".zip",
                StringComparison.OrdinalIgnoreCase);
            return new AssetFileAnalysis
            {
                FileId = fileId,
                Kind = isZip
                    ? AssetFileAnalysisKind.Zip
                    : AssetFileAnalysisKind.UnityPackage,
                Entries = new[]
                {
                    new AssetFileContentEntry
                    {
                        Path = isZip
                            ? "Packages/Sample.unitypackage"
                            : "Assets/Sample.prefab",
                        Kind = AssetFileContentEntryKind.File,
                        AssetGuid = SampleGuid
                    }
                }
            };
        }

        public Task<AssetFileAnalysis> AnalyzeFileAsync(
            string fileId,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(AnalyzeFile(fileId));
        }

        public IReadOnlyList<string> GetFileImportedAssetGuids(string fileId)
        {
            return new[] { SampleGuid };
        }

        public IReadOnlyList<string> GetItemImportedAssetGuids(string itemId)
        {
            return new[] { SampleGuid };
        }

        public IReadOnlyList<AssetImportedAssetAssociation>
            GetImportedAssetAssociations(IReadOnlyList<string> assetGuids = null)
        {
            return assetGuids != null &&
                assetGuids.Count > 0 &&
                !assetGuids.Contains(SampleGuid)
                    ? Array.Empty<AssetImportedAssetAssociation>()
                    : new[]
                    {
                        new AssetImportedAssetAssociation
                        {
                            ItemId = "item-avatar",
                            FileId = "file-avatar",
                            AssetGuid = SampleGuid
                        }
                    };
        }

        public IReadOnlyList<AssetTag> GetTags()
        {
            return _items.SelectMany(item => item.Tags).ToArray();
        }

        public IReadOnlyList<AssetItem> SetItemTags(
            IReadOnlyList<string> itemIds,
            IReadOnlyList<string> tagPaths)
        {
            return SelectItems(itemIds);
        }

        public IReadOnlyList<AssetCollection> GetCollections()
        {
            return _collections;
        }

        public AssetCollection GetCollection(string collectionId)
        {
            return _collections.First(item => item.Id == collectionId);
        }

        public AssetCollection CreateCollection(
            CreateAssetCollectionRequest request)
        {
            var now = DateTime.UtcNow;
            var collection = new AssetCollection
            {
                Id = "collection-" + Guid.NewGuid().ToString("N"),
                Name = request.Name,
                Icon = request.Icon,
                Root = request.Root,
                CreatedAt = now,
                UpdatedAt = now
            };
            _collections = _collections.Concat(new[] { collection }).ToArray();
            Changed?.Invoke(new AssetManagerChange(
                AssetManagerChangeKind.CollectionCreated,
                new[] { collection.Id }));
            return collection;
        }

        public AssetCollection UpdateCollection(
            string collectionId,
            UpdateAssetCollectionRequest request)
        {
            var collection = GetCollection(collectionId);
            collection.Name = request.Name;
            collection.Root = request.Root;
            collection.Icon = request.Icon ?? collection.Icon;
            Changed?.Invoke(new AssetManagerChange(
                AssetManagerChangeKind.CollectionUpdated,
                new[] { collectionId }));
            return collection;
        }

        public void DeleteCollection(string collectionId)
        {
        }

        public void ReorderCollections(IReadOnlyList<string> collectionIds)
        {
            _collections = collectionIds.Select(GetCollection).ToArray();
            Changed?.Invoke(new AssetManagerChange(
                AssetManagerChangeKind.CollectionsReordered,
                collectionIds));
        }

        public AssetSyncResult SyncEagle(EagleSyncRequest request)
        {
            return EmptySyncResult();
        }

        public AssetSyncResult SyncEe4v(Ee4vSyncRequest request)
        {
            return EmptySyncResult();
        }

        public AssetItem ImportEe4vFile(ImportEe4vFileRequest request)
        {
            return _items[0];
        }

        private IReadOnlyList<AssetItem> SelectItems(
            IReadOnlyList<string> itemIds)
        {
            return _items.Where(item => itemIds.Contains(item.Id)).ToArray();
        }

        private IReadOnlyList<AssetFile> SelectFiles(
            IReadOnlyList<string> fileIds)
        {
            return _files.Where(file => fileIds.Contains(file.Id)).ToArray();
        }

        private static Task<AssetImportResult> Import(
            string fileId,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(new AssetImportResult(
                AssetImportState.Success,
                new[] { fileId },
                new[] { SampleGuid }));
        }

        private static AssetSearchResult Page(
            IReadOnlyList<AssetItem> items,
            int offset,
            int limit)
        {
            var page = items.Skip(Math.Max(0, offset));
            if (limit > 0)
            {
                page = page.Take(limit);
            }

            return new AssetSearchResult
            {
                Items = page.ToArray(),
                TotalCount = items.Count
            };
        }

        private static string GetFirstConditionValue(AssetFilterNode node)
        {
            if (node == null)
            {
                return string.Empty;
            }

            if (node.Type == AssetFilterNodeType.Condition)
            {
                return node.Value ?? string.Empty;
            }

            return (node.Children ?? Array.Empty<AssetFilterNode>())
                .Select(GetFirstConditionValue)
                .FirstOrDefault(value => !string.IsNullOrEmpty(value)) ??
                string.Empty;
        }

        private static bool Contains(string source, string value)
        {
            return !string.IsNullOrEmpty(source) &&
                source.IndexOf(value, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static AssetSyncResult EmptySyncResult()
        {
            var empty = Array.Empty<string>();
            return new AssetSyncResult(
                empty,
                empty,
                empty,
                empty,
                empty,
                empty,
                empty,
                empty,
                3,
                empty);
        }

        private static IReadOnlyList<AssetFile> CreateFiles()
        {
            return new[]
            {
                new AssetFile
                {
                    Id = "file-avatar",
                    ItemId = "item-avatar",
                    FileName = "astral-avatar.unitypackage",
                    Extension = ".unitypackage",
                    SourceType = AssetSourceType.Eagle,
                    SourcePath = "items/AVATAR/package.unitypackage"
                },
                new AssetFile
                {
                    Id = "file-world",
                    ItemId = "item-world",
                    FileName = "night-pool.zip",
                    Extension = ".zip",
                    SourceType = AssetSourceType.Ee4v,
                    SourcePath = "Worlds/night-pool.zip"
                },
                new AssetFile
                {
                    Id = "file-unassigned",
                    FileName = "unassigned-tools.zip",
                    Extension = ".zip",
                    SourceType = AssetSourceType.Ee4v,
                    SourcePath = "Inbox/unassigned-tools.zip"
                }
            };
        }

        private static IReadOnlyList<AssetItem> CreateItems(
            IReadOnlyList<AssetFile> files)
        {
            return new[]
            {
                CreateItem(
                    "item-avatar",
                    "Astral Avatar",
                    "Avatar package with gestures and materials.",
                    "Avatar",
                    files,
                    new DateTime(2026, 1, 12),
                    new DateTime(2026, 5, 8),
                    new AssetBoothMetadata
                    {
                        ItemUrl = "https://booth.pm/ja/items/1234567",
                        ShopName = "Astral Workshop",
                        ShopUrl = "https://example.booth.pm"
                    }),
                CreateItem(
                    "item-world",
                    "Night Pool",
                    "A compact world environment.",
                    "World",
                    files,
                    new DateTime(2025, 9, 20),
                    new DateTime(2026, 7, 3)),
                new AssetItem
                {
                    Id = "item-archived",
                    Name = "Legacy Shader",
                    Description = "Archived compatibility shader.",
                    Tags = Array.Empty<AssetTag>(),
                    Files = Array.Empty<AssetFile>(),
                    IsArchived = true,
                    CreatedAt = new DateTime(2024, 4, 2),
                    UpdatedAt = new DateTime(2025, 2, 14)
                }
            };
        }

        private static AssetItem CreateItem(
            string id,
            string name,
            string description,
            string tag,
            IReadOnlyList<AssetFile> files,
            DateTime createdAt,
            DateTime updatedAt,
            AssetBoothMetadata booth = null)
        {
            var itemFiles = files
                .Where(file => file.ItemId == id)
                .ToArray();
            return new AssetItem
            {
                Id = id,
                Name = name,
                Description = description,
                Booth = booth,
                Tags = new[]
                {
                    new AssetTag { Id = "tag-" + tag, Path = tag }
                },
                Files = itemFiles,
                CreatedAt = createdAt,
                UpdatedAt = updatedAt
            };
        }
    }
}

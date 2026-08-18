using System;
using System.Collections.Generic;
using System.Linq;
using Ee4v.AssetManager.Application.Ports;
using Ee4v.AssetManager.Contracts;
using Ee4v.AssetManager.Domain;

namespace Ee4v.AssetManager.Application
{
    internal sealed class AssetManagerService : IAssetManager
    {
        private sealed class FileImportPlan
        {
            internal AssetItem Item { get; set; }
            internal AssetFile File { get; set; }
            internal IReadOnlyList<string> TargetPaths { get; set; }
        }

        private readonly IAssetManagerStore _store;
        private readonly IEagleAssetSource _eagle;
        private readonly IEe4vAssetSource _ee4v;
        private readonly IAssetTargetImporter _targetImporter;
        private readonly AssetManagerChangePublisher _changes;

        internal AssetManagerService(
            IAssetManagerStore store,
            IEagleAssetSource eagle,
            IEe4vAssetSource ee4v,
            IAssetTargetImporter targetImporter)
        {
            _store = store ??
                     throw new ArgumentNullException(nameof(store));
            _eagle = eagle ??
                     throw new ArgumentNullException(nameof(eagle));
            _ee4v = ee4v ??
                    throw new ArgumentNullException(nameof(ee4v));
            _targetImporter = targetImporter ??
                throw new ArgumentNullException(nameof(targetImporter));
            _changes = new AssetManagerChangePublisher();
        }

        public event Action<AssetManagerChange> Changed
        {
            add { _changes.Changed += value; }
            remove { _changes.Changed -= value; }
        }

        public AssetSearchResult SearchItems(AssetItemQuery query = null)
        {
            query = query ?? new AssetItemQuery();
            if (query.Offset < 0 || query.Limit < 0)
            {
                throw new AssetManagerException(
                    AssetManagerErrorCode.InvalidRequest,
                    "Offset and limit cannot be negative.");
            }

            if (query.Filter != null)
            {
                AssetManagerRequestValidator.ValidateFilter(query.Filter);
            }

            var matches = _store.GetItems()
                .Where(item => query.IncludeArchived || !item.IsArchived)
                .Where(item =>
                    AssetManagerRules.Matches(item, query.Filter))
                .OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
                .ThenBy(item => item.Id, StringComparer.Ordinal)
                .ToArray();
            var page = matches
                .Skip(query.Offset)
                .Take(query.Limit == 0
                    ? matches.Length
                    : query.Limit)
                .ToArray();
            return new AssetSearchResult
            {
                Items = page,
                TotalCount = matches.Length
            };
        }

        public AssetSearchResult SearchCollection(
            string collectionId,
            int offset = 0,
            int limit = 0)
        {
            AssetManagerRequestValidator.Require(
                collectionId,
                "collection id");
            return SearchItems(new AssetItemQuery
            {
                Filter = _store.GetCollection(collectionId).Root,
                Offset = offset,
                Limit = limit
            });
        }

        public AssetItem GetItem(string itemId)
        {
            AssetManagerRequestValidator.Require(itemId, "item id");
            return _store.GetItem(itemId);
        }

        public AssetItem CreateItem(CreateAssetItemRequest request)
        {
            AssetManagerRequestValidator.RequireRequest(
                request,
                "create item request");
            AssetManagerRequestValidator.Require(
                request.Name,
                "item name");
            var item = _store.CreateItem(request);
            Publish(AssetManagerChangeKind.Catalog, item.Id);
            return item;
        }

        public IReadOnlyList<AssetItem> SetItemArchived(
            IReadOnlyList<string> itemIds,
            bool archived)
        {
            var ids = AssetManagerRequestValidator.NormalizeIds(
                itemIds,
                "item ids");
            var items = _store.SetItemArchived(ids, archived);
            PublishCatalog(ids);
            return items;
        }

        public void DeleteItem(IReadOnlyList<string> itemIds)
        {
            var ids = AssetManagerRequestValidator.NormalizeIds(
                itemIds,
                "item ids");
            var items = ids.Select(_store.GetItem).ToArray();
            var files = items
                .SelectMany(item => item.Files ?? Array.Empty<AssetFile>())
                .GroupBy(file => file.Id, StringComparer.Ordinal)
                .Select(group => group.First())
                .ToArray();
            for (var i = 0; i < items.Length; i++)
            {
                if (items[i].SourceType != AssetSourceType.Ee4v)
                {
                    continue;
                }

                var sourceFile = _store.GetFileBySource(
                    AssetSourceType.Ee4v,
                    items[i].SourceId);
                if (!string.Equals(
                        sourceFile.ItemId,
                        items[i].Id,
                        StringComparison.Ordinal))
                {
                    throw new AssetManagerException(
                        AssetManagerErrorCode.InvalidRequest,
                        "ee4v source item and file are inconsistent.");
                }
            }

            if (items.Any(item =>
                    item.SourceType == AssetSourceType.Eagle) ||
                files.Any(file =>
                    file.SourceType != AssetSourceType.Ee4v))
            {
                throw new AssetManagerException(
                    AssetManagerErrorCode.InvalidRequest,
                    "Items containing non-ee4v sources cannot be deleted.");
            }

            _ee4v.Delete(files);
            _store.DeleteItem(ids);
            PublishCatalog(ids);
        }

        public AssetItem UpdateItem(
            string itemId,
            UpdateAssetItemRequest request)
        {
            AssetManagerRequestValidator.Require(itemId, "item id");
            AssetManagerRequestValidator.RequireRequest(
                request,
                "update item request");
            AssetManagerRequestValidator.Require(
                request.Name,
                "item name");
            var original = _store.GetItem(itemId);
            UpdateEe4vSource(
                original,
                request.Name.Trim(),
                request.Description ?? string.Empty,
                TagPaths(original));
            AssetItem item;
            try
            {
                item = _store.UpdateItem(itemId, request);
            }
            catch
            {
                RestoreEe4vSource(original);
                throw;
            }

            Publish(AssetManagerChangeKind.Catalog, item.Id);
            return item;
        }

        public AssetFile GetFile(string fileId)
        {
            AssetManagerRequestValidator.Require(fileId, "file id");
            return _store.GetFile(fileId);
        }

        public IReadOnlyList<AssetFile> GetFiles(
            string itemId,
            bool includeArchived = false)
        {
            AssetManagerRequestValidator.Require(itemId, "item id");
            return _store.GetFiles(itemId, includeArchived);
        }

        public IReadOnlyList<AssetFile> GetUnassignedFiles(
            bool includeArchived = false)
        {
            return _store.GetUnassignedFiles(includeArchived);
        }

        public AssetFile RegisterFile(
            string itemId,
            RegisterFileRequest request)
        {
            AssetManagerRequestValidator.RequireRequest(
                request,
                "register file request");
            AssetManagerRequestValidator.Require(
                request.LibraryPath,
                "ee4v library path");
            AssetManagerRequestValidator.Require(
                request.FilePath,
                "file path");
            var normalizedItemId = string.IsNullOrWhiteSpace(itemId)
                ? null
                : itemId;
            if (normalizedItemId != null)
            {
                _store.GetItem(normalizedItemId);
            }

            var sourceFile = _ee4v.Register(request);
            _store.ApplySourceSnapshot(
                AssetSourceType.Ee4v,
                new AssetSourceSnapshot(
                    Array.Empty<AssetSourceSnapshotItem>(),
                    new[] { sourceFile }),
                false);
            var file = _store.GetFileBySource(
                AssetSourceType.Ee4v,
                sourceFile.SourceId);
            if (normalizedItemId != null)
            {
                file = _store.SetFileItem(
                        new[] { file.Id },
                        normalizedItemId)
                    .Single();
            }

            Publish(AssetManagerChangeKind.Catalog, file.Id);
            return file;
        }

        public IReadOnlyList<AssetFile> SetFileItem(
            IReadOnlyList<string> fileIds,
            string itemId)
        {
            var ids = AssetManagerRequestValidator.NormalizeIds(
                fileIds,
                "file ids");
            var files = _store.SetFileItem(
                ids,
                string.IsNullOrWhiteSpace(itemId) ? null : itemId);
            PublishCatalog(ids);
            return files;
        }

        public IReadOnlyList<AssetFile> SetFileArchived(
            IReadOnlyList<string> fileIds,
            bool archived)
        {
            var ids = AssetManagerRequestValidator.NormalizeIds(
                fileIds,
                "file ids");
            var files = _store.SetFileArchived(ids, archived);
            PublishCatalog(ids);
            return files;
        }

        public void DeleteFile(IReadOnlyList<string> fileIds)
        {
            var ids = AssetManagerRequestValidator.NormalizeIds(
                fileIds,
                "file ids");
            var files = ids.Select(_store.GetFile).ToArray();
            if (files.Any(file =>
                    file.SourceType != AssetSourceType.Ee4v))
            {
                throw new AssetManagerException(
                    AssetManagerErrorCode.InvalidRequest,
                    "Only ee4v source files can be deleted.");
            }

            _ee4v.Delete(files);
            _store.DeleteFile(ids);
            PublishCatalog(ids);
        }

        public IReadOnlyList<AssetFileTarget> GetFileTargets(
            string fileId)
        {
            AssetManagerRequestValidator.Require(fileId, "file id");
            return _store.GetFileTargets(fileId);
        }

        public IReadOnlyList<AssetFileTarget> SetFileTargets(
            string fileId,
            IReadOnlyList<string> targetPaths)
        {
            AssetManagerRequestValidator.Require(fileId, "file id");
            var targets = _store.ReplaceFileTargets(
                fileId.Trim(),
                AssetManagerRequestValidator.NormalizeTargetPaths(
                    targetPaths));
            Publish(AssetManagerChangeKind.Catalog, fileId.Trim());
            return targets;
        }

        public void ImportFileTargets(string fileId)
        {
            AssetManagerRequestValidator.Require(fileId, "file id");
            var normalizedFileId = fileId.Trim();
            if (_store.GetFileTargets(normalizedFileId).Count == 0)
            {
                return;
            }

            var order = AssetManagerRequestValidator.ResolveDependencyOrder(
                normalizedFileId,
                GetDependencyIds);
            var plans = order
                .Select(CreateImportPlan)
                .ToArray();
            ImportNext(plans, 0);
        }

        public IReadOnlyList<AssetFileDependency> GetFileDependencies(
            string fileId)
        {
            AssetManagerRequestValidator.Require(fileId, "file id");
            return _store.GetFileDependencies(fileId.Trim());
        }

        public IReadOnlyList<AssetFileDependency> SetFileDependencies(
            string dependentFileId,
            IReadOnlyList<string> dependencyFileIds)
        {
            AssetManagerRequestValidator.Require(
                dependentFileId,
                "dependent file id");
            var normalizedFileId = dependentFileId.Trim();
            _store.GetFile(normalizedFileId);
            var dependencyIds =
                AssetManagerRequestValidator.NormalizeOptionalIds(
                    dependencyFileIds,
                    "dependency file ids");
            AssetManagerRequestValidator.ValidateDependencyReplacement(
                normalizedFileId,
                dependencyIds,
                GetDependencyIds);
            var dependencies = _store.ReplaceFileDependencies(
                normalizedFileId,
                dependencyIds);
            Publish(AssetManagerChangeKind.Catalog, normalizedFileId);
            return dependencies;
        }

        private FileImportPlan CreateImportPlan(string fileId)
        {
            var file = _store.GetFile(fileId);
            var targets = _store.GetFileTargets(file.Id)
                .Select(target => target.TargetPath)
                .ToArray();
            if (targets.Length == 0)
            {
                return new FileImportPlan
                {
                    File = file,
                    TargetPaths = targets
                };
            }

            if (!file.IsAvailable || file.IsArchived)
            {
                throw new AssetManagerException(
                    AssetManagerErrorCode.InvalidRequest,
                    "Unavailable or archived files cannot be imported.");
            }

            if (string.IsNullOrWhiteSpace(file.ItemId))
            {
                throw new AssetManagerException(
                    AssetManagerErrorCode.InvalidRequest,
                    "The file must belong to an item before import.");
            }

            return new FileImportPlan
            {
                Item = _store.GetItem(file.ItemId),
                File = file,
                TargetPaths = targets
            };
        }

        private void ImportNext(
            IReadOnlyList<FileImportPlan> plans,
            int index)
        {
            while (index < plans.Count &&
                   plans[index].TargetPaths.Count == 0)
            {
                index++;
            }

            if (index >= plans.Count)
            {
                return;
            }

            var nextIndex = index + 1;
            var plan = plans[index];
            _targetImporter.Import(
                plan.Item,
                plan.File,
                plan.TargetPaths,
                succeeded =>
                {
                    if (succeeded)
                    {
                        ImportNext(plans, nextIndex);
                    }
                });
        }

        private IReadOnlyList<string> GetDependencyIds(string fileId)
        {
            return _store.GetFileDependencies(fileId)
                .Select(dependency => dependency.DependencyFileId)
                .ToArray();
        }

        public IReadOnlyList<AssetTag> GetTags()
        {
            return _store.GetTags();
        }

        public IReadOnlyList<AssetItem> SetItemTags(
            IReadOnlyList<string> itemIds,
            IReadOnlyList<string> tagPaths)
        {
            var ids = AssetManagerRequestValidator.NormalizeIds(
                itemIds,
                "item ids");
            var normalized = AssetManagerRequestValidator.NormalizeTags(
                tagPaths);
            var originals = ids.Select(_store.GetItem).ToArray();
            IReadOnlyList<AssetItem> items;
            try
            {
                for (var i = 0; i < originals.Length; i++)
                {
                    UpdateEe4vSource(
                        originals[i],
                        originals[i].Name,
                        originals[i].Description,
                        normalized);
                }

                items = _store.SetItemTags(ids, normalized);
            }
            catch
            {
                for (var i = 0; i < originals.Length; i++)
                {
                    RestoreEe4vSource(originals[i]);
                }

                throw;
            }

            PublishCatalog(ids);
            return items;
        }

        public IReadOnlyList<AssetCollection> GetCollections()
        {
            return _store.GetCollections();
        }

        public AssetCollection GetCollection(string collectionId)
        {
            AssetManagerRequestValidator.Require(
                collectionId,
                "collection id");
            return _store.GetCollection(collectionId);
        }

        public AssetCollection CreateCollection(
            CreateAssetCollectionRequest request)
        {
            AssetManagerRequestValidator.RequireRequest(
                request,
                "create collection request");
            AssetManagerRequestValidator.Require(
                request.Name,
                "collection name");
            AssetManagerRequestValidator.ValidateFilter(request.Root);
            var collection = _store.CreateCollection(request);
            Publish(AssetManagerChangeKind.Collections, collection.Id);
            return collection;
        }

        public AssetCollection UpdateCollection(
            string collectionId,
            UpdateAssetCollectionRequest request)
        {
            AssetManagerRequestValidator.Require(
                collectionId,
                "collection id");
            AssetManagerRequestValidator.RequireRequest(
                request,
                "update collection request");
            AssetManagerRequestValidator.Require(
                request.Name,
                "collection name");
            AssetManagerRequestValidator.ValidateFilter(request.Root);
            var collection = _store.UpdateCollection(
                collectionId,
                request);
            Publish(AssetManagerChangeKind.Collections, collection.Id);
            return collection;
        }

        public void DeleteCollection(string collectionId)
        {
            AssetManagerRequestValidator.Require(
                collectionId,
                "collection id");
            _store.DeleteCollection(collectionId);
            Publish(AssetManagerChangeKind.Collections, collectionId);
        }

        public AssetSyncResult SyncEagle(EagleSyncRequest request)
        {
            AssetManagerRequestValidator.RequireRequest(
                request,
                "Eagle sync request");
            AssetSourceSnapshot snapshot;
            try
            {
                snapshot = _eagle.Read(request);
            }
            catch (AssetManagerException)
            {
                return new AssetSyncResult(
                    0,
                    0,
                    0,
                    1,
                    AssetSyncState.Failed);
            }

            var result = _store.ApplySourceSnapshot(
                AssetSourceType.Eagle,
                snapshot,
                true);
            Publish(AssetManagerChangeKind.Catalog);
            return result;
        }

        public AssetSyncResult SyncEe4v(Ee4vSyncRequest request)
        {
            AssetManagerRequestValidator.RequireRequest(
                request,
                "ee4v sync request");
            AssetManagerRequestValidator.Require(
                request.LibraryPath,
                "ee4v library path");
            AssetSourceSnapshot snapshot;
            try
            {
                snapshot = _ee4v.Read(request);
                NormalizeSnapshotTags(snapshot);
            }
            catch (AssetManagerException)
            {
                return new AssetSyncResult(
                    0,
                    0,
                    0,
                    1,
                    AssetSyncState.Failed);
            }

            var result = _store.ApplySourceSnapshot(
                AssetSourceType.Ee4v,
                snapshot,
                true);
            Publish(AssetManagerChangeKind.Catalog);
            return result;
        }

        public AssetItem ImportEe4vFile(
            ImportEe4vFileRequest request)
        {
            AssetManagerRequestValidator.RequireRequest(
                request,
                "ee4v import request");
            AssetManagerRequestValidator.Require(
                request.LibraryPath,
                "ee4v library path");
            AssetManagerRequestValidator.Require(
                request.FilePath,
                "import file path");
            var sourceItem = _ee4v.Import(
                request,
                AssetManagerRequestValidator.NormalizeTags(request.Tags));
            _store.ApplySourceSnapshot(
                AssetSourceType.Ee4v,
                new AssetSourceSnapshot(new[] { sourceItem }),
                false);
            var item = _store.GetItemBySource(
                AssetSourceType.Ee4v,
                sourceItem.SourceId);
            Publish(AssetManagerChangeKind.Catalog, item.Id);
            return item;
        }

        private void UpdateEe4vSource(
            AssetItem item,
            string name,
            string description,
            IReadOnlyList<string> tags)
        {
            if (item.SourceType != AssetSourceType.Ee4v)
            {
                return;
            }

            _ee4v.Update(
                _store.GetFileBySource(
                    AssetSourceType.Ee4v,
                    item.SourceId),
                name,
                description,
                tags);
        }

        private void RestoreEe4vSource(AssetItem item)
        {
            try
            {
                UpdateEe4vSource(
                    item,
                    item.Name,
                    item.Description,
                    TagPaths(item));
            }
            catch
            {
            }
        }

        private static IReadOnlyList<string> TagPaths(AssetItem item)
        {
            return (item.Tags ?? Array.Empty<AssetTag>())
                .Select(tag => tag.Path)
                .ToArray();
        }

        private static void NormalizeSnapshotTags(
            AssetSourceSnapshot snapshot)
        {
            var items = snapshot == null
                ? Array.Empty<AssetSourceSnapshotItem>()
                : snapshot.Items;
            for (var i = 0; i < items.Count; i++)
            {
                if (items[i].Tags != null)
                {
                    items[i].Tags =
                        AssetManagerRequestValidator.NormalizeTags(
                            items[i].Tags);
                }
            }
        }

        private void PublishCatalog(IReadOnlyList<string> ids)
        {
            Publish(
                AssetManagerChangeKind.Catalog,
                ids.Count == 1 ? ids[0] : null);
        }

        private void Publish(
            AssetManagerChangeKind kind,
            string subjectId = null)
        {
            _changes.Publish(new AssetManagerChange(kind, subjectId));
        }
    }
}

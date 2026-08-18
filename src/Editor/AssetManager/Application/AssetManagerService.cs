using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Ee4v.AssetManager.Application.Ports;
using Ee4v.AssetManager.Contracts;

namespace Ee4v.AssetManager.Application
{
    internal sealed class AssetManagerService : IAssetManager
    {
        private sealed class FileImportPlan
        {
            internal string FileId { get; set; }
            internal IReadOnlyList<string> TargetPaths { get; set; }
        }

        private readonly IAssetManagerStore _store;
        private readonly IEagleAssetSource _eagle;
        private readonly IEe4vAssetSource _ee4v;
        private readonly IAssetTargetImporter _targetImporter;
        private readonly IAssetFileAnalyzer _fileAnalyzer;
        private readonly IAssetThumbnailProvider _thumbnailProvider;

        internal AssetManagerService(
            IAssetManagerStore store,
            IEagleAssetSource eagle,
            IEe4vAssetSource ee4v,
            IAssetTargetImporter targetImporter,
            IAssetFileAnalyzer fileAnalyzer,
            IAssetThumbnailProvider thumbnailProvider)
        {
            _store = store ??
                     throw new ArgumentNullException(nameof(store));
            _eagle = eagle ??
                     throw new ArgumentNullException(nameof(eagle));
            _ee4v = ee4v ??
                    throw new ArgumentNullException(nameof(ee4v));
            _targetImporter = targetImporter ??
                throw new ArgumentNullException(nameof(targetImporter));
            _fileAnalyzer = fileAnalyzer ??
                throw new ArgumentNullException(nameof(fileAnalyzer));
            _thumbnailProvider = thumbnailProvider ??
                throw new ArgumentNullException(nameof(thumbnailProvider));
        }

        public event Action<AssetManagerChange> Changed;

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

            return _store.SearchItems(query);
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

        public bool MatchesCollection(string collectionId, string itemId)
        {
            AssetManagerRequestValidator.Require(
                collectionId,
                "collection id");
            AssetManagerRequestValidator.Require(itemId, "item id");
            var collection = _store.GetCollection(collectionId.Trim());
            return _store.MatchesItem(itemId.Trim(), collection.Root);
        }

        public AssetItem GetItem(string itemId)
        {
            AssetManagerRequestValidator.Require(itemId, "item id");
            return _store.GetItem(itemId);
        }

        public Task<AssetThumbnail> GetThumbnail(
            string itemId,
            CancellationToken cancellationToken = default)
        {
            AssetManagerRequestValidator.Require(itemId, "item id");
            return _thumbnailProvider.Get(
                _store.GetItem(itemId.Trim()),
                cancellationToken);
        }

        public Task<IReadOnlyDictionary<string, AssetThumbnail>> GetThumbnails(
            IReadOnlyList<string> itemIds,
            CancellationToken cancellationToken = default)
        {
            var ids = AssetManagerRequestValidator.NormalizeOptionalIds(
                itemIds,
                "item ids");
            return _thumbnailProvider.GetMany(
                ids.Select(_store.GetItem).ToArray(),
                cancellationToken);
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
            Publish(
                AssetManagerChangeKind.ItemCreated,
                new[] { item.Id });
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
            Publish(AssetManagerChangeKind.ItemArchiveChanged, ids);
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
            var dependentFileIds = _store.GetDependentFileIds(
                    files.Select(file => file.Id).ToArray())
                .Except(
                    files.Select(file => file.Id),
                    StringComparer.Ordinal)
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

            using (var deletion = _ee4v.BeginDelete(files))
            {
                _store.DeleteItem(ids);
                deletion.Commit();
            }
            Publish(
                AssetManagerChangeKind.FileDeleted,
                files.Select(file => file.Id).ToArray(),
                ItemIds(files));
            Publish(AssetManagerChangeKind.ItemDeleted, ids);
            Publish(
                AssetManagerChangeKind.FileDependenciesChanged,
                dependentFileIds);
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
            EnsureItemEditable(original);
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

            Publish(
                AssetManagerChangeKind.ItemUpdated,
                new[] { item.Id });
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
            AssetFile file;
            try
            {
                file = _store.ApplySourceFile(
                    AssetSourceType.Ee4v,
                    sourceFile,
                    normalizedItemId);
            }
            catch (Exception operationException)
            {
                DeleteNewSource(
                    new[] { sourceFile },
                    operationException);
                throw;
            }

            Publish(
                AssetManagerChangeKind.FileCreated,
                new[] { file.Id },
                ItemIds(new[] { file }));
            return file;
        }

        public IReadOnlyList<AssetFile> SetFileItem(
            IReadOnlyList<string> fileIds,
            string itemId)
        {
            var ids = AssetManagerRequestValidator.NormalizeIds(
                fileIds,
                "file ids");
            var existingFiles = ids.Select(_store.GetFile).ToArray();
            if (existingFiles.Any(file =>
                    file.SourceType == AssetSourceType.Eagle))
            {
                throw new AssetManagerException(
                    AssetManagerErrorCode.InvalidRequest,
                    "Eagle file placement is controlled by Eagle.");
            }

            var files = _store.SetFileItem(
                ids,
                string.IsNullOrWhiteSpace(itemId) ? null : itemId);
            Publish(
                AssetManagerChangeKind.FilePlacementChanged,
                ids,
                MergeIds(ItemIds(existingFiles), ItemIds(files)));
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
            Publish(
                AssetManagerChangeKind.FileArchiveChanged,
                ids,
                ItemIds(files));
            return files;
        }

        public void DeleteFile(IReadOnlyList<string> fileIds)
        {
            var ids = AssetManagerRequestValidator.NormalizeIds(
                fileIds,
                "file ids");
            var files = ids.Select(_store.GetFile).ToArray();
            var dependentFileIds = _store.GetDependentFileIds(ids)
                .Except(ids, StringComparer.Ordinal)
                .ToArray();
            if (files.Any(file =>
                    file.SourceType != AssetSourceType.Ee4v))
            {
                throw new AssetManagerException(
                    AssetManagerErrorCode.InvalidRequest,
                    "Only ee4v source files can be deleted.");
            }

            using (var deletion = _ee4v.BeginDelete(files))
            {
                _store.DeleteFile(ids);
                deletion.Commit();
            }
            Publish(
                AssetManagerChangeKind.FileDeleted,
                ids,
                ItemIds(files));
            Publish(
                AssetManagerChangeKind.FileDependenciesChanged,
                dependentFileIds);
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
            Publish(
                AssetManagerChangeKind.FileTargetsChanged,
                new[] { fileId.Trim() });
            return targets;
        }

        public async Task<AssetImportResult> ImportFileTargets(
            string fileId,
            CancellationToken cancellationToken = default)
        {
            AssetManagerRequestValidator.Require(fileId, "file id");
            var normalizedFileId = fileId.Trim();
            if (_store.GetFileTargets(normalizedFileId).Count == 0)
            {
                return new AssetImportResult(
                    AssetImportState.Success,
                    Array.Empty<string>(),
                    Array.Empty<string>());
            }

            FileImportPlan[] plans;
            try
            {
                var order =
                    AssetManagerRequestValidator.ResolveDependencyOrder(
                        normalizedFileId,
                        GetDependencyIds);
                plans = order
                    .Select(CreateImportPlan)
                    .ToArray();
            }
            catch (Exception exception)
            {
                return new AssetImportResult(
                    AssetImportState.Failed,
                    new[] { normalizedFileId },
                    Array.Empty<string>(),
                    exception.Message);
            }
            var importedFileIds = new List<string>();
            var importedGuids = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < plans.Length; i++)
            {
                if (plans[i].TargetPaths.Count == 0)
                {
                    continue;
                }

                if (cancellationToken.IsCancellationRequested)
                {
                    return new AssetImportResult(
                        AssetImportState.Canceled,
                        importedFileIds,
                        importedGuids.ToArray(),
                        "Asset import was canceled.");
                }

                var result = await ImportFileEntries(
                    plans[i].FileId,
                    plans[i].TargetPaths,
                    cancellationToken);
                if (!result.Succeeded)
                {
                    return new AssetImportResult(
                        result.State,
                        importedFileIds
                            .Concat(result.FileIds)
                            .Distinct(StringComparer.Ordinal)
                            .ToArray(),
                        importedGuids.ToArray(),
                        result.ErrorMessage);
                }

                importedFileIds.Add(plans[i].FileId);
                for (var guidIndex = 0;
                     guidIndex < result.AssetGuids.Count;
                     guidIndex++)
                {
                    importedGuids.Add(result.AssetGuids[guidIndex]);
                }
            }

            return new AssetImportResult(
                AssetImportState.Success,
                importedFileIds,
                importedGuids.ToArray());
        }

        public async Task<AssetImportResult> ImportFileEntries(
            string fileId,
            IReadOnlyList<string> paths,
            CancellationToken cancellationToken = default)
        {
            AssetManagerRequestValidator.Require(fileId, "file id");
            var normalizedFileId = fileId.Trim();
            try
            {
                var normalizedPaths =
                    AssetManagerRequestValidator.NormalizeTargetPaths(paths);
                if (normalizedPaths.Count == 0)
                {
                    return new AssetImportResult(
                        AssetImportState.Success,
                        Array.Empty<string>(),
                        Array.Empty<string>());
                }

                var file = _store.GetFile(normalizedFileId);
                if (file.IsArchived)
                {
                    throw new AssetManagerException(
                        AssetManagerErrorCode.InvalidRequest,
                        "Archived files cannot be imported.");
                }

                if (string.IsNullOrWhiteSpace(file.ItemId))
                {
                    throw new AssetManagerException(
                        AssetManagerErrorCode.InvalidRequest,
                        "The file must belong to an item before import.");
                }

                var result = await _targetImporter.Import(
                    _store.GetItem(file.ItemId),
                    file,
                    normalizedPaths,
                    cancellationToken);
                if (!result.Succeeded)
                {
                    return result;
                }

                var normalizedGuids =
                    AssetManagerRequestValidator.NormalizeAssetGuids(
                        result.AssetGuids);
                _store.ReplaceFileImportedAssetGuids(
                    file.Id,
                    normalizedGuids);
                Publish(
                    AssetManagerChangeKind.FileImportedAssetGuidsChanged,
                    new[] { file.Id },
                    new[] { file.ItemId });
                return new AssetImportResult(
                    AssetImportState.Success,
                    new[] { file.Id },
                    normalizedGuids);
            }
            catch (OperationCanceledException)
            {
                return new AssetImportResult(
                    AssetImportState.Canceled,
                    new[] { normalizedFileId },
                    Array.Empty<string>(),
                    "Asset import was canceled.");
            }
            catch (Exception exception)
            {
                return new AssetImportResult(
                    AssetImportState.Failed,
                    new[] { normalizedFileId },
                    Array.Empty<string>(),
                    exception.Message);
            }
        }

        public IReadOnlyList<AssetFileDependency> GetFileDependencies(
            string fileId)
        {
            AssetManagerRequestValidator.Require(fileId, "file id");
            return _store.GetFileDependencies(fileId.Trim());
        }

        public IReadOnlyList<AssetFileDependency> SetFileDependencies(
            IReadOnlyList<string> dependentFileIds,
            IReadOnlyList<string> dependencyFileIds)
        {
            var normalizedFileIds =
                AssetManagerRequestValidator.NormalizeIds(
                    dependentFileIds,
                    "dependent file ids");
            for (var i = 0; i < normalizedFileIds.Count; i++)
            {
                _store.GetFile(normalizedFileIds[i]);
            }

            var dependencyIds =
                AssetManagerRequestValidator.NormalizeOptionalIds(
                    dependencyFileIds,
                    "dependency file ids");
            AssetManagerRequestValidator.ValidateDependencyReplacement(
                normalizedFileIds,
                dependencyIds,
                GetDependencyIds);
            var dependencies = _store.ReplaceFileDependencies(
                normalizedFileIds,
                dependencyIds);
            Publish(
                AssetManagerChangeKind.FileDependenciesChanged,
                normalizedFileIds);
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
                    FileId = file.Id,
                    TargetPaths = targets
                };
            }

            if (file.IsArchived)
            {
                throw new AssetManagerException(
                    AssetManagerErrorCode.InvalidRequest,
                    "Archived files cannot be imported.");
            }

            if (string.IsNullOrWhiteSpace(file.ItemId))
            {
                throw new AssetManagerException(
                    AssetManagerErrorCode.InvalidRequest,
                    "The file must belong to an item before import.");
            }

            _store.GetItem(file.ItemId);
            return new FileImportPlan
            {
                FileId = file.Id,
                TargetPaths = targets
            };
        }

        private IReadOnlyList<string> GetDependencyIds(string fileId)
        {
            return _store.GetFileDependencies(fileId)
                .Select(dependency => dependency.DependencyFileId)
                .ToArray();
        }

        public AssetFileAnalysis AnalyzeFile(string fileId)
        {
            AssetManagerRequestValidator.Require(fileId, "file id");
            return _fileAnalyzer.Analyze(_store.GetFile(fileId.Trim()));
        }

        public IReadOnlyList<string> GetFileImportedAssetGuids(
            string fileId)
        {
            AssetManagerRequestValidator.Require(fileId, "file id");
            return _store.GetFileImportedAssetGuids(fileId.Trim());
        }

        public IReadOnlyList<string> GetItemImportedAssetGuids(
            string itemId)
        {
            AssetManagerRequestValidator.Require(itemId, "item id");
            return _store.GetItemImportedAssetGuids(itemId.Trim());
        }

        public IReadOnlyList<AssetImportedAssetAssociation>
            GetImportedAssetAssociations(
                IReadOnlyList<string> assetGuids = null)
        {
            return _store.GetImportedAssetAssociations(
                assetGuids == null
                    ? null
                    : AssetManagerRequestValidator.NormalizeAssetGuids(
                        assetGuids));
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
            for (var i = 0; i < originals.Length; i++)
            {
                EnsureItemEditable(originals[i]);
            }

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

            Publish(AssetManagerChangeKind.ItemTagsChanged, ids);
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
            Publish(
                AssetManagerChangeKind.CollectionCreated,
                new[] { collection.Id });
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
            Publish(
                AssetManagerChangeKind.CollectionUpdated,
                new[] { collection.Id });
            return collection;
        }

        public void DeleteCollection(string collectionId)
        {
            AssetManagerRequestValidator.Require(
                collectionId,
                "collection id");
            _store.DeleteCollection(collectionId);
            Publish(
                AssetManagerChangeKind.CollectionDeleted,
                new[] { collectionId });
        }

        public AssetSyncResult SyncEagle(EagleSyncRequest request)
        {
            AssetManagerRequestValidator.RequireRequest(
                request,
                "Eagle sync request");
            return SyncSource(
                AssetSourceType.Eagle,
                () => _eagle.Read(request));
        }

        public AssetSyncResult SyncEe4v(Ee4vSyncRequest request)
        {
            AssetManagerRequestValidator.RequireRequest(
                request,
                "ee4v sync request");
            AssetManagerRequestValidator.Require(
                request.LibraryPath,
                "ee4v library path");
            return SyncSource(
                AssetSourceType.Ee4v,
                () => _ee4v.Read(request));
        }

        private AssetSyncResult SyncSource(
            AssetSourceType sourceType,
            Func<AssetSourceSnapshot> read)
        {
            AssetSourceSnapshot snapshot;
            try
            {
                snapshot = read();
                NormalizeSnapshotTags(snapshot);
            }
            catch (AssetManagerException exception)
            {
                return new AssetSyncResult(
                    Array.Empty<string>(),
                    Array.Empty<string>(),
                    Array.Empty<string>(),
                    Array.Empty<string>(),
                    Array.Empty<string>(),
                    Array.Empty<string>(),
                    Array.Empty<string>(),
                    Array.Empty<string>(),
                    0,
                    new[] { exception.Message });
            }

            var result = _store.ApplySourceSnapshot(
                sourceType,
                snapshot);
            Publish(
                AssetManagerChangeKind.SourceSynchronized,
                result.AffectedItemIds,
                result.AffectedFileIds,
                sourceType: sourceType);
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
            AssetItem item;
            try
            {
                item = _store.ApplySourceItem(
                    AssetSourceType.Ee4v,
                    sourceItem);
            }
            catch (Exception operationException)
            {
                DeleteNewSource(
                    sourceItem.Files ??
                    Array.Empty<AssetSourceSnapshotFile>(),
                    operationException);
                throw;
            }
            Publish(
                AssetManagerChangeKind.ItemCreated,
                new[] { item.Id });
            Publish(
                AssetManagerChangeKind.FileCreated,
                (item.Files ?? Array.Empty<AssetFile>())
                    .Select(file => file.Id)
                    .ToArray(),
                new[] { item.Id });
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

        private static void EnsureItemEditable(AssetItem item)
        {
            if (item.SourceType == AssetSourceType.Eagle)
            {
                throw new AssetManagerException(
                    AssetManagerErrorCode.InvalidRequest,
                    "Eagle item metadata is controlled by Eagle.");
            }
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

        private static IReadOnlyList<string> ItemIds(
            IEnumerable<AssetFile> files)
        {
            return files
                .Select(file => file.ItemId)
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Distinct(StringComparer.Ordinal)
                .ToArray();
        }

        private static IReadOnlyList<string> MergeIds(
            params IEnumerable<string>[] groups)
        {
            return groups
                .SelectMany(group => group)
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Distinct(StringComparer.Ordinal)
                .ToArray();
        }

        private void DeleteNewSource(
            IReadOnlyList<AssetSourceSnapshotFile> sourceFiles,
            Exception operationException)
        {
            try
            {
                using (var deletion = _ee4v.BeginDelete(
                           (sourceFiles ??
                            Array.Empty<AssetSourceSnapshotFile>())
                           .Select(file => new AssetFile
                           {
                               SourceType = AssetSourceType.Ee4v,
                               SourceId = file.SourceId,
                               SourcePath = file.SourcePath
                           })
                           .ToArray()))
                {
                    deletion.Commit();
                }
            }
            catch (Exception cleanupException)
            {
                throw new AssetManagerException(
                    AssetManagerErrorCode.DatasourceError,
                    "The ee4v source rollback failed.",
                    new AggregateException(
                        operationException,
                        cleanupException));
            }
        }

        private void Publish(
            AssetManagerChangeKind kind,
            IReadOnlyList<string> subjectIds = null,
            IReadOnlyList<string> relatedIds = null,
            AssetSourceType? sourceType = null)
        {
            var change = new AssetManagerChange(
                kind,
                subjectIds,
                relatedIds,
                sourceType);
            if (change.SubjectIds.Count == 0 &&
                change.RelatedIds.Count == 0 &&
                !change.SourceType.HasValue)
            {
                return;
            }

            var handlers = Changed;
            if (handlers == null)
            {
                return;
            }

            foreach (Action<AssetManagerChange> handler in
                     handlers.GetInvocationList())
            {
                try
                {
                    handler(change);
                }
                catch
                {
                }
            }
        }
    }
}

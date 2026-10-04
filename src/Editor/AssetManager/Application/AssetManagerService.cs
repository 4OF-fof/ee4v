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
                _store.GetThumbnailItem(itemId.Trim()),
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
                ids.Select(_store.GetThumbnailItem).ToArray(),
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
            if (items.Any(item => !item.IsArchived))
            {
                throw new AssetManagerException(
                    AssetManagerErrorCode.InvalidRequest,
                    "Only archived items can be deleted.");
            }

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
            EnsureItemMetadataEditable(original);
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

            var sourceFile = NormalizeSourceFile(_ee4v.Register(
                new RegisterFileRequest
                {
                    LibraryPath = request.LibraryPath,
                    FilePath = request.FilePath,
                    FileName = AssetSourceText.Normalize(request.FileName)
                }));
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
            if (files.Any(file => !file.IsArchived))
            {
                throw new AssetManagerException(
                    AssetManagerErrorCode.InvalidRequest,
                    "Only archived files can be deleted.");
            }

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

        public IReadOnlyList<AssetFileTarget> GetItemTargets(
            string itemId)
        {
            AssetManagerRequestValidator.Require(itemId, "item id");
            return _store.GetItemTargets(itemId.Trim());
        }

        public IReadOnlyList<AssetFileTarget> SetItemTargets(
            string itemId,
            IReadOnlyList<AssetFileTarget> targets)
        {
            AssetManagerRequestValidator.Require(itemId, "item id");
            var normalizedItemId = itemId.Trim();
            _store.GetItem(normalizedItemId);
            var normalized = NormalizeItemTargets(
                normalizedItemId,
                targets);
            var result = _store.ReplaceItemTargets(
                normalizedItemId,
                normalized);
            Publish(
                AssetManagerChangeKind.ItemTargetsChanged,
                new[] { normalizedItemId },
                result.Select(target => target.FileId).Distinct().ToArray());
            return result;
        }

        public AssetFileTarget SetItemTargetGroup(
            string itemId,
            string fileId,
            string targetPath,
            string groupName)
        {
            AssetManagerRequestValidator.Require(itemId, "item id");
            AssetManagerRequestValidator.Require(fileId, "file id");
            var normalizedItemId = itemId.Trim();
            var normalizedFileId = fileId.Trim();
            var normalizedTargetPath =
                AssetManagerRequestValidator.NormalizeTargetPaths(
                    new[] { targetPath }).Single();
            var normalizedGroupName =
                AssetManagerRequestValidator.NormalizeTargetGroupName(
                    groupName);
            var target = _store.SetItemTargetGroup(
                normalizedItemId,
                normalizedFileId,
                normalizedTargetPath,
                normalizedGroupName);
            Publish(
                AssetManagerChangeKind.ItemTargetsChanged,
                new[] { normalizedItemId },
                new[] { normalizedFileId });
            return target;
        }

        public async Task<AssetImportResult> ImportItemTargets(
            string itemId,
            IReadOnlyList<AssetFileTarget> selectedTargets,
            CancellationToken cancellationToken = default)
        {
            AssetManagerRequestValidator.Require(itemId, "item id");
            var normalizedItemId = itemId.Trim();
            try
            {
                _store.GetItem(normalizedItemId);
                var targets = _store.GetItemTargets(normalizedItemId);
                if (targets.Count == 0)
                {
                    return new AssetImportResult(
                        AssetImportState.Success,
                        Array.Empty<string>(),
                        Array.Empty<string>());
                }

                var groups = targets
                    .Where(target => !string.IsNullOrWhiteSpace(
                        target.GroupName))
                    .GroupBy(
                        target => target.GroupName,
                        StringComparer.OrdinalIgnoreCase)
                    .ToArray();
                var choices = NormalizeTargetChoices(
                    selectedTargets,
                    targets);
                if (choices.Count != groups.Length)
                {
                    throw new AssetManagerException(
                        AssetManagerErrorCode.InvalidRequest,
                        "Select exactly one target from every target group.");
                }

                for (var i = 0; i < groups.Length; i++)
                {
                    if (choices.Count(choice => groups[i].Any(target =>
                            AssetFileTarget.HasSameIdentity(
                                target,
                                choice))) != 1)
                    {
                        throw new AssetManagerException(
                            AssetManagerErrorCode.InvalidRequest,
                            "Select exactly one target from group: " +
                            groups[i].Key);
                    }
                }

                var selected = targets
                    .Where(target => string.IsNullOrWhiteSpace(
                        target.GroupName))
                    .Concat(choices)
                    .ToArray();
                var plans = AssetFileImportPlanner.Resolve(selected, _store.GetFileDependencies)
                    .Select(plan => CreateImportPlan(plan.FileId, plan.TargetPaths))
                    .ToArray();
                return await ImportPlans(plans, cancellationToken);
            }
            catch (Exception exception)
            {
                return new AssetImportResult(
                    AssetImportState.Failed,
                    Array.Empty<string>(),
                    Array.Empty<string>(),
                    exception.Message);
            }
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
                AssetManagerRequestValidator
                    .EnsureImportTargetsDoNotContainZip(
                        file,
                        normalizedPaths);
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
            IReadOnlyList<AssetFileTarget> dependencyTargets)
        {
            var normalizedFileIds =
                AssetManagerRequestValidator.NormalizeIds(
                    dependentFileIds,
                    "dependent file ids");
            return SetFileDependencies(
                normalizedFileIds.Select(fileId => new AssetFileTarget
                {
                    FileId = fileId,
                    TargetPath = string.Empty
                }).ToArray(),
                dependencyTargets);
        }

        public IReadOnlyList<AssetFileDependency> SetFileDependencies(
            IReadOnlyList<AssetFileTarget> dependentTargets,
            IReadOnlyList<AssetFileTarget> dependencyTargets)
        {
            var normalizedSources = NormalizeDependencyTargets(
                dependentTargets);
            if (normalizedSources.Count == 0)
            {
                throw new AssetManagerException(
                    AssetManagerErrorCode.InvalidRequest,
                    "Dependent targets are required.");
            }
            var normalizedTargets = NormalizeDependencyTargets(
                dependencyTargets);
            ValidateDependencyReplacement(
                normalizedSources,
                normalizedTargets);
            var dependencies = _store.ReplaceFileDependencies(
                normalizedSources,
                normalizedTargets);
            Publish(
                AssetManagerChangeKind.FileDependenciesChanged,
                normalizedSources.Select(source => source.FileId)
                    .Distinct(StringComparer.Ordinal).ToArray());
            return dependencies;
        }

        private void ValidateDependencyReplacement(
            IReadOnlyList<AssetFileTarget> sources,
            IReadOnlyList<AssetFileTarget> targets)
        {
            foreach (var source in sources)
            {
                if (targets.Any(target =>
                        AssetFileTarget.HasSameIdentity(source, target)))
                {
                    throw new AssetManagerException(
                        AssetManagerErrorCode.InvalidRequest,
                        "A target cannot depend on itself.");
                }
            }

            var sourcesByFile = sources.GroupBy(
                    source => source.FileId,
                    StringComparer.Ordinal)
                .ToDictionary(group => group.Key,
                    group => group.ToArray(),
                    StringComparer.Ordinal);
            IReadOnlyList<string> GetDependencies(string fileId)
            {
                var existing = _store.GetFileDependencies(fileId);
                if (!sourcesByFile.TryGetValue(
                        fileId,
                        out var replacedSources))
                {
                    return existing.Select(dependency =>
                            dependency.DependencyFileId)
                        .Where(id => !string.Equals(
                            id, fileId, StringComparison.Ordinal))
                        .Distinct(StringComparer.Ordinal)
                        .ToArray();
                }

                return existing
                    .Where(dependency => !replacedSources.Any(source =>
                        string.Equals(
                            source.TargetPath,
                            dependency.DependentTargetPath,
                            StringComparison.OrdinalIgnoreCase)))
                    .Select(dependency => dependency.DependencyFileId)
                    .Concat(targets.Select(target => target.FileId))
                    .Where(id => !string.Equals(
                        id, fileId, StringComparison.Ordinal))
                    .Distinct(StringComparer.Ordinal)
                    .ToArray();
            }

            foreach (var fileId in sourcesByFile.Keys)
            {
                AssetManagerRequestValidator.ResolveDependencyOrder(
                    fileId,
                    GetDependencies);
            }
        }

        private IReadOnlyList<AssetFileTarget> NormalizeDependencyTargets(
            IReadOnlyList<AssetFileTarget> dependencyTargets)
        {
            var result = new List<AssetFileTarget>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var target in dependencyTargets ??
                     Array.Empty<AssetFileTarget>())
            {
                if (target == null)
                {
                    throw new AssetManagerException(
                        AssetManagerErrorCode.InvalidRequest,
                        "Dependency target is required.");
                }

                AssetManagerRequestValidator.Require(
                    target.FileId,
                    "dependency target file id");
                var fileId = target.FileId.Trim();
                var path = AssetManagerRequestValidator
                    .NormalizeTargetPaths(new[] { target.TargetPath })
                    .Single();
                var file = _store.GetFile(fileId);
                if (string.IsNullOrWhiteSpace(file.ItemId))
                {
                    throw new AssetManagerException(
                        AssetManagerErrorCode.InvalidRequest,
                        "Dependency targets must belong to an item.");
                }

                AssetManagerRequestValidator
                    .EnsureImportTargetsDoNotContainZip(
                        file,
                        new[] { path });
                if (!seen.Add(fileId + "\n" + path))
                {
                    continue;
                }

                result.Add(new AssetFileTarget
                {
                    FileId = fileId,
                    TargetPath = path
                });
            }
            return result;
        }

        private static IReadOnlyList<AssetFileTarget> NormalizeTargetChoices(
            IReadOnlyList<AssetFileTarget> selectedTargets,
            IReadOnlyList<AssetFileTarget> storedTargets)
        {
            var result = new List<AssetFileTarget>();
            foreach (var selected in selectedTargets ??
                     Array.Empty<AssetFileTarget>())
            {
                if (selected == null)
                {
                    throw new AssetManagerException(
                        AssetManagerErrorCode.InvalidRequest,
                        "Selected target is required.");
                }

                AssetManagerRequestValidator.Require(
                    selected.FileId,
                    "selected target file id");
                var normalizedPath =
                    AssetManagerRequestValidator.NormalizeTargetPaths(
                        new[] { selected.TargetPath }).Single();
                var stored = storedTargets.SingleOrDefault(target =>
                    string.Equals(
                        target.FileId,
                        selected.FileId.Trim(),
                        StringComparison.Ordinal) &&
                    string.Equals(
                        target.TargetPath,
                        normalizedPath,
                        StringComparison.OrdinalIgnoreCase));
                if (stored == null ||
                    string.IsNullOrWhiteSpace(stored.GroupName))
                {
                    throw new AssetManagerException(
                        AssetManagerErrorCode.InvalidRequest,
                        "Selected target must belong to a target group.");
                }

                if (result.Any(choice =>
                        AssetFileTarget.HasSameIdentity(
                            choice,
                            stored)))
                {
                    throw new AssetManagerException(
                        AssetManagerErrorCode.InvalidRequest,
                        "Selected targets cannot contain duplicates.");
                }

                result.Add(stored);
            }

            return result;
        }

        private IReadOnlyList<AssetFileTarget> NormalizeItemTargets(
            string itemId,
            IReadOnlyList<AssetFileTarget> targets)
        {
            var result = new List<AssetFileTarget>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var target in targets ??
                     Array.Empty<AssetFileTarget>())
            {
                if (target == null)
                {
                    throw new AssetManagerException(
                        AssetManagerErrorCode.InvalidRequest,
                        "Item target is required.");
                }

                AssetManagerRequestValidator.Require(
                    target.FileId,
                    "item target file id");
                var fileId = target.FileId.Trim();
                var path = AssetManagerRequestValidator
                    .NormalizeTargetPaths(new[] { target.TargetPath })
                    .Single();
                var file = _store.GetFile(fileId);
                if (!string.Equals(
                        file.ItemId,
                        itemId,
                        StringComparison.Ordinal))
                {
                    throw new AssetManagerException(
                        AssetManagerErrorCode.InvalidRequest,
                        "Item targets must belong to the item.");
                }

                AssetManagerRequestValidator
                    .EnsureImportTargetsDoNotContainZip(
                        file,
                        new[] { path });
                if (!seen.Add(fileId + "\n" + path))
                {
                    continue;
                }

                result.Add(new AssetFileTarget
                {
                    FileId = fileId,
                    TargetPath = path
                });
            }
            return result;
        }

        private async Task<AssetImportResult> ImportPlans(
            IReadOnlyList<FileImportPlan> plans,
            CancellationToken cancellationToken)
        {
            var importedFileIds = new List<string>();
            var importedGuids = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < plans.Count; i++)
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

        private FileImportPlan CreateImportPlan(
            string fileId,
            IReadOnlyList<string> targetPaths)
        {
            var file = _store.GetFile(fileId);
            var targets = targetPaths ?? Array.Empty<string>();
            if (targets.Count == 0)
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

        public AssetFileAnalysis AnalyzeFile(string fileId)
        {
            return _fileAnalyzer.Analyze(GetFileForAnalysis(fileId));
        }

        public Task<AssetFileAnalysis> AnalyzeFileAsync(
            string fileId,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var file = GetFileForAnalysis(fileId);
            return Task.Run(
                () => _fileAnalyzer.Analyze(file, cancellationToken),
                cancellationToken);
        }

        private AssetFile GetFileForAnalysis(string fileId)
        {
            AssetManagerRequestValidator.Require(fileId, "file id");
            return _store.GetFile(fileId.Trim());
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
            AssetManagerRequestValidator.ValidateCollectionIcon(request.Icon);
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
            if (request.Icon.HasValue)
            {
                AssetManagerRequestValidator.ValidateCollectionIcon(request.Icon.Value);
            }
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

        public void ReorderCollections(IReadOnlyList<string> collectionIds)
        {
            AssetManagerRequestValidator.RequireRequest(
                collectionIds,
                "collection order");
            var ids = collectionIds.Count == 0
                ? Array.Empty<string>()
                : AssetManagerRequestValidator.NormalizeIds(
                    collectionIds,
                    "collection id").ToArray();
            if (ids.Length != collectionIds.Count)
            {
                throw new AssetManagerException(
                    AssetManagerErrorCode.InvalidRequest,
                    "Collection order must not contain duplicate ids.");
            }

            _store.ReorderCollections(ids);
            Publish(AssetManagerChangeKind.CollectionsReordered, ids);
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
            AssetSyncResult result;
            try
            {
                snapshot = read();
                NormalizeSourceSnapshot(snapshot);
                result = _store.ApplySourceSnapshot(
                    sourceType,
                    snapshot);
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
            var normalizedTags = NormalizeSourceTags(request.Tags);
            var sourceItem = NormalizeSourceItem(_ee4v.Import(
                new ImportEe4vFileRequest(
                    request.LibraryPath,
                    request.FilePath,
                    AssetSourceText.Normalize(request.Name),
                    AssetSourceText.Normalize(request.Description),
                    normalizedTags),
                normalizedTags));
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

        private static void EnsureItemMetadataEditable(AssetItem item)
        {
            if (item.SourceType == AssetSourceType.Eagle)
            {
                throw new AssetManagerException(
                    AssetManagerErrorCode.InvalidRequest,
                    "Eagle item metadata is controlled by Eagle.");
            }
        }

        private static void NormalizeSourceSnapshot(
            AssetSourceSnapshot snapshot)
        {
            var items = snapshot == null
                ? Array.Empty<AssetSourceSnapshotItem>()
                : snapshot.Items;
            for (var i = 0; i < items.Count; i++)
            {
                NormalizeSourceItem(items[i]);
            }

            var files = snapshot == null
                ? Array.Empty<AssetSourceSnapshotFile>()
                : snapshot.Files;
            for (var i = 0; i < files.Count; i++)
            {
                NormalizeSourceFile(files[i]);
            }
        }

        private static AssetSourceSnapshotItem NormalizeSourceItem(
            AssetSourceSnapshotItem item)
        {
            if (item == null)
            {
                return null;
            }

            item.Name = AssetSourceText.Normalize(item.Name);
            item.Description = AssetSourceText.Normalize(item.Description);
            if (item.Booth != null)
            {
                item.Booth.ShopName = AssetSourceText.Normalize(
                    item.Booth.ShopName);
            }
            if (item.Tags != null)
            {
                item.Tags = NormalizeSourceTags(item.Tags);
            }
            var files = item.Files ??
                        Array.Empty<AssetSourceSnapshotFile>();
            for (var i = 0; i < files.Count; i++)
            {
                NormalizeSourceFile(files[i]);
            }

            return item;
        }

        private static AssetSourceSnapshotFile NormalizeSourceFile(
            AssetSourceSnapshotFile file)
        {
            if (file != null)
            {
                file.FileName = AssetSourceText.Normalize(file.FileName);
                file.Extension = AssetSourceText.Normalize(file.Extension);
            }

            return file;
        }

        private static IReadOnlyList<string> NormalizeSourceTags(
            IReadOnlyList<string> tags)
        {
            return AssetManagerRequestValidator.NormalizeTags(
                (tags ?? Array.Empty<string>())
                .Select(AssetSourceText.Normalize)
                .Where(tag => !string.IsNullOrWhiteSpace(tag))
                .ToArray());
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

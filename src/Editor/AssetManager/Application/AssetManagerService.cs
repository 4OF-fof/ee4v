using System;
using System.Collections.Generic;
using System.Linq;
using Ee4v.AssetManager.Application.Ports;
using Ee4v.AssetManager.Contracts;

namespace Ee4v.AssetManager.Application
{
    internal sealed class AssetManagerService :
        IAssetManager,
        IAssetManagerSnapshotReader
    {
        private readonly IAssetCatalogReadStore _catalogReader;
        private readonly IAssetCatalogCommandStore _catalogWriter;
        private readonly IAssetFileReadStore _fileReader;
        private readonly IAssetFileCommandStore _fileWriter;
        private readonly IAssetCollectionReadStore _collectionReader;
        private readonly IAssetCollectionCommandStore _collectionWriter;
        private readonly IAssetDependencyReadStore _dependencyReader;
        private readonly IAssetDependencyCommandStore _dependencyWriter;
        private readonly IAssetImportTargetReadStore _importTargetReader;
        private readonly IAssetImportTargetCommandStore _importTargetWriter;
        private readonly IImportedAssetGuidReadStore _importedAssetGuidReader;
        private readonly IImportedAssetGuidCommandStore _importedAssetGuidWriter;
        private readonly IAssetSyncStore _sync;
        private readonly IAssetImportGateway _importGateway;
        private readonly AssetManagerChangePublisher _changePublisher;
        private readonly SetFileImportTargetsUseCase _setFileImportTargets;
        private readonly ImportFileUseCase _importFile;
        private readonly AssetManagerReadSnapshot _readSnapshot;

        internal AssetManagerService(
            IAssetManagerStore store,
            IAssetImportGateway importGateway,
            IAssetManagerDiagnostics diagnostics)
        {
            store = store ?? throw new ArgumentNullException(nameof(store));
            _catalogReader = store;
            _catalogWriter = store;
            _fileReader = store;
            _fileWriter = store;
            _collectionReader = store;
            _collectionWriter = store;
            _dependencyReader = store;
            _dependencyWriter = store;
            _importTargetReader = store;
            _importTargetWriter = store;
            _importedAssetGuidReader = store;
            _importedAssetGuidWriter = store;
            _sync = store;
            _importGateway = importGateway ?? throw new ArgumentNullException(nameof(importGateway));
            _changePublisher = new AssetManagerChangePublisher(diagnostics);
            _readSnapshot =
                new AssetManagerReadSnapshot(
                    _catalogReader.LoadCatalogSnapshot,
                    _catalogReader.GetThumbnails,
                    _catalogReader.GetThumbnail);
            _setFileImportTargets = new SetFileImportTargetsUseCase(
                _importTargetReader,
                _importTargetWriter,
                change => Publish(change));
            _importFile = new ImportFileUseCase(
                _catalogReader,
                _fileReader,
                _importTargetReader,
                _importedAssetGuidWriter,
                _importGateway,
                change => Publish(change));
        }

        public event Action<AssetManagerChange> Changed
        {
            add { _changePublisher.Changed += value; }
            remove { _changePublisher.Changed -= value; }
        }

        public AssetSearchResult SearchItems(AssetItemQuery query) => _catalogReader.SearchItems(query);
        public AssetSearchResult SearchItemSummaries(
            AssetItemQuery query) =>
            _readSnapshot.CanSearch(query)
                ? _readSnapshot.Search(query)
                : _catalogReader.SearchItemSummaries(query);
        public AssetItem GetItem(string itemId) => _catalogReader.GetItem(itemId);
        public AssetThumbnail GetThumbnail(string itemId) =>
            _readSnapshot.GetThumbnail(itemId);
        public IReadOnlyDictionary<string, AssetThumbnail> GetThumbnails(IReadOnlyList<string> itemIds) =>
            _readSnapshot.GetThumbnails(itemIds);
        public bool TrySearchItemSummaries(
            AssetItemQuery query,
            out AssetSearchResult result) =>
            _readSnapshot.TrySearch(query, out result);

        public bool TryGetThumbnails(
            IReadOnlyList<string> itemIds,
            out IReadOnlyDictionary<string, AssetThumbnail> thumbnails) =>
            _readSnapshot.TryGetThumbnails(
                itemIds,
                out thumbnails);

        public AssetItem CreateItem(CreateAssetItemRequest request)
        {
            AssetManagerRequestValidator.RequireRequest(request, "Create item request");
            AssetManagerRequestValidator.Require(request.Name, "item name");
            return PublishCatalog(_catalogWriter.CreateItem(request));
        }

        public AssetItem UpdateItem(string itemId, UpdateAssetItemRequest request)
        {
            AssetManagerRequestValidator.Require(itemId, "item id");
            AssetManagerRequestValidator.RequireRequest(request, "Update item request");
            AssetManagerRequestValidator.Require(request.Name, "item name");
            return PublishCatalog(_catalogWriter.UpdateItem(itemId, request));
        }

        public IReadOnlyList<AssetFile> GetFiles(string itemId, AssetFileQuery query = null) =>
            _fileReader.GetFiles(itemId, query);

        public AssetFile RegisterFile(string itemId, RegisterFileRequest request)
        {
            AssetManagerRequestValidator.Require(itemId, "item id");
            AssetManagerRequestValidator.RequireRequest(request, "Register file request");
            AssetManagerRequestValidator.Require(request.FilePath, "file path");
            return PublishCatalog(_fileWriter.RegisterFile(itemId, request));
        }

        public IReadOnlyList<AssetVariantGroup> GetVariantGroups(string itemId) =>
            _fileReader.GetVariantGroups(itemId);

        public AssetVariantGroup CreateVariantGroup(
            string itemId,
            CreateVariantGroupRequest request)
        {
            AssetManagerRequestValidator.Require(itemId, "item id");
            AssetManagerRequestValidator.RequireRequest(request, "Create variant group request");
            AssetManagerRequestValidator.Require(request.Name, "variant group name");
            return PublishCatalog(_fileWriter.CreateVariantGroup(itemId, request));
        }

        public IReadOnlyList<AssetVersionGroup> GetVersionGroups(string itemId) =>
            _fileReader.GetVersionGroups(itemId);

        public AssetVersionGroup CreateVersionGroup(
            string itemId,
            CreateVersionGroupRequest request)
        {
            AssetManagerRequestValidator.Require(itemId, "item id");
            AssetManagerRequestValidator.RequireRequest(request, "Create version group request");
            AssetManagerRequestValidator.Require(request.Name, "version group name");
            return PublishCatalog(_fileWriter.CreateVersionGroup(itemId, request));
        }

        public void SetVersionGroupPrimaryFile(string versionGroupId, string fileId)
        {
            var primaryFileId = _fileWriter.SetVersionGroupPrimaryFile(versionGroupId, fileId);
            Publish(new AssetManagerChange(
                AssetManagerChangeKind.VersionGroupPrimaryFile,
                versionGroupId,
                primaryFileId));
            Publish(new AssetManagerChange(AssetManagerChangeKind.FileTree));
        }

        public void ArchiveFile(string fileId)
        {
            _fileWriter.ArchiveFile(fileId);
            PublishCatalog();
        }

        public AssetFilePathResolution ResolveFilePath(string fileId) => _fileReader.ResolveFilePath(fileId);
        public IReadOnlyList<AssetTag> GetTags(string keyword = null) => _catalogReader.GetTags(keyword);

        public AssetTag CreateTag(string name)
        {
            AssetManagerRequestValidator.Require(name, "tag name");
            return PublishCatalog(_catalogWriter.CreateTag(name));
        }

        public void SetItemTags(string itemId, IReadOnlyList<string> tagIds)
        {
            _catalogWriter.SetItemTags(itemId, tagIds);
            PublishCatalog();
        }

        public IReadOnlyList<AssetCollection> GetCollections() => _collectionReader.GetCollections();

        public AssetCollection CreateCollection(CreateCollectionRequest request)
        {
            AssetManagerRequestValidator.ValidateCollection(request);
            return PublishCollections(_collectionWriter.CreateCollection(request));
        }

        public AssetCollection CreateSmartCollection(CreateSmartCollectionRequest request)
        {
            AssetManagerRequestValidator.ValidateSmartCollection(request);
            var collection =
                _collectionWriter.CreateSmartCollection(request);
            PublishCollections();
            PublishSmartCollectionRule(collection.Id);
            return collection;
        }

        public AssetCollection UpdateCollection(
            string collectionId,
            UpdateCollectionRequest request)
        {
            AssetManagerRequestValidator.Require(
                collectionId,
                "collection id");
            AssetManagerRequestValidator.ValidateCollection(
                request);
            return PublishCollections(
                _collectionWriter.UpdateCollection(
                    collectionId,
                    request));
        }

        public AssetCollection UpdateSmartCollection(
            string collectionId,
            UpdateSmartCollectionRequest request)
        {
            AssetManagerRequestValidator.Require(
                collectionId,
                "collection id");
            AssetManagerRequestValidator.ValidateSmartCollection(
                request);
            var collection =
                _collectionWriter.UpdateSmartCollection(
                    collectionId,
                    request);
            PublishCollections();
            PublishSmartCollectionRule(collectionId);
            return collection;
        }

        public void DeleteCollection(string collectionId)
        {
            AssetManagerRequestValidator.Require(
                collectionId,
                "collection id");
            DeleteCollections(new[] { collectionId });
        }

        public void DeleteCollections(
            IReadOnlyList<string> collectionIds)
        {
            var affectedSmartCollection =
                _collectionWriter.DeleteCollections(collectionIds);
            PublishCollections();
            if (affectedSmartCollection)
            {
                PublishSmartCollectionRule(
                    collectionIds[0]);
            }
        }

        public void MoveCollection(
            string collectionId,
            string parentCollectionId,
            int siblingIndex = -1)
        {
            MoveCollections(
                new[] { collectionId },
                parentCollectionId,
                siblingIndex);
        }

        public void MoveCollections(
            IReadOnlyList<string> collectionIds,
            string parentCollectionId,
            int siblingIndex = -1)
        {
            _collectionWriter.MoveCollections(
                collectionIds,
                parentCollectionId,
                siblingIndex);
            PublishCollections();
        }

        public void SetItemCollections(string itemId, IReadOnlyList<string> collectionIds)
        {
            _collectionWriter.SetItemCollections(itemId, collectionIds);
            PublishCatalog();
        }

        public void AddItemsToCollection(
            IReadOnlyList<string> itemIds,
            string collectionId)
        {
            AssetManagerRequestValidator.Require(
                collectionId,
                "collection id");
            if (itemIds == null || itemIds.Count == 0)
            {
                throw new AssetManagerException(
                    AssetManagerErrorCode.InvalidRequest,
                    "At least one item id is required.");
            }

            for (var i = 0; i < itemIds.Count; i++)
            {
                AssetManagerRequestValidator.Require(
                    itemIds[i],
                    "item id");
            }

            if (_collectionWriter.AddItemsToCollection(
                    itemIds,
                    collectionId))
            {
                Publish(new AssetManagerChange(
                    AssetManagerChangeKind.ItemCollections,
                    relatedId: collectionId),
                    AssetManagerSnapshotInvalidation.Catalog);
            }
        }

        public IReadOnlyList<AssetFileDependency> GetFileDependencies(string fileId) =>
            _dependencyReader.GetFileDependencies(fileId);

        public void SetFileDependencies(string dependentFileId, IReadOnlyList<string> dependencyFileIds)
        {
            AssetManagerRequestValidator.ValidateFileDependencies(
                dependentFileId,
                dependencyFileIds);
            _dependencyWriter.SetFileDependencies(dependentFileId, dependencyFileIds);
            PublishCatalog();
        }

        public IReadOnlyList<AssetDependency> GetDependencies(DependencyEndpointRequest source) =>
            _dependencyReader.GetDependencies(source);

        public void SetDependencies(
            DependencyEndpointRequest source,
            IReadOnlyList<DependencyEndpointRequest> targets)
        {
            AssetManagerRequestValidator.ValidateDependencies(source, targets);
            _dependencyWriter.SetDependencies(source, targets);
            PublishCatalog();
        }

        public IReadOnlyList<AssetFileImportTarget> GetFileImportTargets(string fileId) =>
            _importTargetReader.GetFileImportTargets(fileId);

        public void SetFileImportTargets(
            string fileId,
            IReadOnlyList<AssetFileImportTargetRequest> targets)
        {
            _setFileImportTargets.Execute(fileId, targets);
        }

        public IReadOnlyList<string> GetFileImportedAssetGuids(string fileId) =>
            _importedAssetGuidReader.GetFileImportedAssetGuids(fileId);

        public IReadOnlyList<string> GetItemImportedAssetGuids(string itemId) =>
            _importedAssetGuidReader.GetItemImportedAssetGuids(itemId);

        public IReadOnlyList<AssetImportedAssetAssociation>
            GetImportedAssetAssociations() =>
            _importedAssetGuidReader.GetImportedAssetAssociations();

        public void ImportFileTargets(string itemId, string fileId)
        {
            _importFile.ImportConfiguredTargets(itemId, fileId);
        }

        public void ImportFileEntry(string itemId, string fileId, string relativePath)
        {
            _importFile.ImportEntry(itemId, fileId, relativePath);
        }

        public AssetSyncResult SyncBlm(BlmSyncRequest request) =>
            PublishCatalog(
                _sync.SyncBlm(request),
                invalidateThumbnails: true);
        public AssetSyncResult SyncEagle(EagleSyncRequest request) =>
            PublishCatalog(
                _sync.SyncEagle(request),
                invalidateThumbnails: true);
        public IReadOnlyList<AssetSyncInfo> GetSyncInfo() => _sync.GetSyncInfo();

        internal PreparedAssetSync PrepareBlmSync(BlmSyncRequest request) =>
            _sync.PrepareBlmSync(request);

        internal PreparedAssetSync PrepareEagleSync(EagleSyncRequest request) =>
            _sync.PrepareEagleSync(request);

        internal AssetSyncResult ApplyPreparedSync(
            PreparedAssetSync prepared,
            bool overwriteItemText) =>
            _sync.ApplyPreparedSync(prepared, overwriteItemText);

        internal void NotifyCatalogChanged()
        {
            PublishCatalog(invalidateThumbnails: true);
        }

        private T PublishCatalog<T>(
            T result,
            bool invalidateThumbnails = false)
        {
            PublishCatalog(invalidateThumbnails);
            return result;
        }

        private void PublishCatalog(
            bool invalidateThumbnails = false)
        {
            Publish(
                new AssetManagerChange(
                    AssetManagerChangeKind.Catalog),
                invalidateThumbnails
                    ? AssetManagerSnapshotInvalidation
                        .CatalogAndThumbnails
                    : AssetManagerSnapshotInvalidation.Catalog);
        }

        private T PublishCollections<T>(T result)
        {
            PublishCollections();
            return result;
        }

        private void PublishCollections()
        {
            Publish(new AssetManagerChange(
                    AssetManagerChangeKind.Collections),
                AssetManagerSnapshotInvalidation.Catalog);
        }

        private void PublishSmartCollectionRule(
            string collectionId)
        {
            Publish(new AssetManagerChange(
                AssetManagerChangeKind.SmartCollectionRule,
                collectionId));
        }

        private void Publish(
            AssetManagerChange change,
            AssetManagerSnapshotInvalidation invalidation =
                AssetManagerSnapshotInvalidation.None)
        {
            _readSnapshot.Invalidate(invalidation);
            _changePublisher.Publish(change);
        }
    }
}

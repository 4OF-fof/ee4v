using System;
using System.Collections.Generic;
using Ee4v.AssetManager.Contracts;

namespace Ee4v.AssetManager.Application.Ports
{
    internal interface IAssetManagerStore :
        IAssetCatalogReadStore,
        IAssetCatalogCommandStore,
        IAssetFileReadStore,
        IAssetFileCommandStore,
        IAssetCollectionReadStore,
        IAssetCollectionCommandStore,
        IAssetDependencyReadStore,
        IAssetDependencyCommandStore,
        IAssetImportTargetReadStore,
        IAssetImportTargetCommandStore,
        IImportedAssetGuidReadStore,
        IImportedAssetGuidCommandStore,
        IAssetSyncStore
    {
    }

    internal interface IAssetCatalogReadStore
    {
        AssetCatalogSnapshot LoadCatalogSnapshot();
        AssetSearchResult SearchItems(AssetItemQuery query);
        AssetSearchResult SearchItemSummaries(AssetItemQuery query);
        AssetItem GetItem(string itemId);
        AssetThumbnail GetThumbnail(string itemId);
        IReadOnlyDictionary<string, AssetThumbnail> GetThumbnails(IReadOnlyList<string> itemIds);
        IReadOnlyList<AssetTag> GetTags(string keyword);
    }

    internal interface IAssetCatalogCommandStore
    {
        AssetItem CreateItem(CreateAssetItemRequest request);
        AssetItem UpdateItem(string itemId, UpdateAssetItemRequest request);
        AssetTag CreateTag(string name);
        void SetItemTags(string itemId, IReadOnlyList<string> tagIds);
    }

    internal interface IAssetFileReadStore
    {
        AssetFile GetFile(string fileId);
        string GetFileOwnerItemId(string fileId);
        IReadOnlyList<AssetFile> GetFiles(string itemId, AssetFileQuery query);
        IReadOnlyList<AssetVariantGroup> GetVariantGroups(string itemId);
        IReadOnlyList<AssetVersionGroup> GetVersionGroups(string itemId);
        AssetFilePathResolution ResolveFilePath(string fileId);
    }

    internal interface IAssetFileCommandStore
    {
        AssetFile RegisterFile(string itemId, RegisterFileRequest request);
        AssetVariantGroup CreateVariantGroup(string itemId, CreateVariantGroupRequest request);
        AssetVersionGroup CreateVersionGroup(string itemId, CreateVersionGroupRequest request);
        string SetVersionGroupPrimaryFile(string versionGroupId, string fileId);
        void ArchiveFile(string fileId);
    }

    internal interface IAssetCollectionReadStore
    {
        IReadOnlyList<AssetCollection> GetCollections();
    }

    internal interface IAssetCollectionCommandStore
    {
        AssetCollection CreateCollection(CreateCollectionRequest request);
        AssetCollection CreateSmartCollection(CreateSmartCollectionRequest request);
        AssetCollection UpdateCollection(
            string collectionId,
            UpdateCollectionRequest request);
        AssetCollection UpdateSmartCollection(
            string collectionId,
            UpdateSmartCollectionRequest request);
        bool DeleteCollections(IReadOnlyList<string> collectionIds);
        void MoveCollections(
            IReadOnlyList<string> collectionIds,
            string parentCollectionId,
            int siblingIndex);
        void SetItemCollections(string itemId, IReadOnlyList<string> collectionIds);
        bool AddItemsToCollection(
            IReadOnlyList<string> itemIds,
            string collectionId);
    }

    internal interface IAssetDependencyReadStore
    {
        IReadOnlyList<AssetFileDependency> GetFileDependencies(string fileId);
        IReadOnlyList<AssetDependency> GetDependencies(DependencyEndpointRequest source);
    }

    internal interface IAssetDependencyCommandStore
    {
        void SetFileDependencies(string dependentFileId, IReadOnlyList<string> dependencyFileIds);
        void SetDependencies(DependencyEndpointRequest source, IReadOnlyList<DependencyEndpointRequest> targets);
    }

    internal interface IAssetImportTargetReadStore
    {
        IReadOnlyList<AssetFileImportTarget> GetFileImportTargets(string fileId);
    }

    internal interface IAssetImportTargetCommandStore
    {
        void ReplaceFileImportTargets(string fileId, IReadOnlyList<string> normalizedRelativePaths);
    }

    internal interface IImportedAssetGuidReadStore
    {
        IReadOnlyList<string> GetFileImportedAssetGuids(string fileId);
        IReadOnlyList<string> GetItemImportedAssetGuids(string itemId);
        IReadOnlyList<AssetImportedAssetAssociation> GetImportedAssetAssociations();
    }

    internal interface IImportedAssetGuidCommandStore
    {
        void ReplaceFileImportedAssetGuids(
            string fileId,
            IReadOnlyList<string> assetGuids);

        void SetImportedAssetProtection(
            string assetGuid,
            bool isProtected);
    }

    internal interface IAssetSyncStore
    {
        AssetSyncResult SyncBlm(BlmSyncRequest request);
        AssetSyncResult SyncEagle(EagleSyncRequest request);
        PreparedAssetSync PrepareBlmSync(BlmSyncRequest request);
        PreparedAssetSync PrepareEagleSync(EagleSyncRequest request);
        AssetSyncResult ApplyPreparedSync(
            PreparedAssetSync prepared,
            bool overwriteItemText);
        IReadOnlyList<AssetSyncInfo> GetSyncInfo();
    }

    internal interface IAssetImportGateway
    {
        void Import(
            AssetImportPlan plan,
            Action<AssetImportResult> completed);
    }

    internal interface IAssetManagerDiagnostics
    {
        void ReportChangeSubscriberFailure(Exception exception);
    }
}

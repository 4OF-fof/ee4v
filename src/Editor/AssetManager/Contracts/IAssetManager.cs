using System;
using System.Collections.Generic;

namespace Ee4v.AssetManager.Contracts
{
    public interface IAssetManager
    {
        event Action<AssetManagerChange> Changed;

        AssetSearchResult SearchItems(AssetItemQuery query);
        AssetSearchResult SearchItemSummaries(AssetItemQuery query);
        AssetItem GetItem(string itemId);
        AssetThumbnail GetThumbnail(string itemId);
        IReadOnlyDictionary<string, AssetThumbnail> GetThumbnails(IReadOnlyList<string> itemIds);
        AssetItem CreateItem(CreateAssetItemRequest request);
        AssetItem UpdateItem(string itemId, UpdateAssetItemRequest request);
        IReadOnlyList<AssetFile> GetFiles(string itemId, AssetFileQuery query = null);
        AssetFile RegisterFile(string itemId, RegisterFileRequest request);
        IReadOnlyList<AssetVariantGroup> GetVariantGroups(string itemId);
        AssetVariantGroup CreateVariantGroup(string itemId, CreateVariantGroupRequest request);
        IReadOnlyList<AssetVersionGroup> GetVersionGroups(string itemId);
        AssetVersionGroup CreateVersionGroup(string itemId, CreateVersionGroupRequest request);
        void SetVersionGroupPrimaryFile(string versionGroupId, string fileId);
        void ArchiveFile(string fileId);
        AssetFilePathResolution ResolveFilePath(string fileId);
        IReadOnlyList<AssetTag> GetTags(string keyword = null);
        AssetTag CreateTag(string name);
        void SetItemTags(string itemId, IReadOnlyList<string> tagIds);
        IReadOnlyList<AssetCollection> GetCollections();
        AssetCollection CreateCollection(CreateCollectionRequest request);
        AssetCollection CreateSmartCollection(CreateSmartCollectionRequest request);
        AssetCollection UpdateCollection(
            string collectionId,
            UpdateCollectionRequest request);
        AssetCollection UpdateSmartCollection(
            string collectionId,
            UpdateSmartCollectionRequest request);
        void DeleteCollection(string collectionId);
        void MoveCollection(
            string collectionId,
            string parentCollectionId,
            int siblingIndex = -1);
        void MoveCollections(
            IReadOnlyList<string> collectionIds,
            string parentCollectionId,
            int siblingIndex = -1);
        void SetItemCollections(string itemId, IReadOnlyList<string> collectionIds);
        IReadOnlyList<AssetFileDependency> GetFileDependencies(string fileId);
        void SetFileDependencies(string dependentFileId, IReadOnlyList<string> dependencyFileIds);
        IReadOnlyList<AssetDependency> GetDependencies(DependencyEndpointRequest source);
        void SetDependencies(DependencyEndpointRequest source, IReadOnlyList<DependencyEndpointRequest> targets);
        IReadOnlyList<AssetFileImportTarget> GetFileImportTargets(string fileId);
        void SetFileImportTargets(string fileId, IReadOnlyList<AssetFileImportTargetRequest> targets);
        void ImportFileTargets(string itemId, string fileId);
        void ImportFileEntry(string itemId, string fileId, string relativePath);
        AssetSyncResult SyncBlm(BlmSyncRequest request);
        AssetSyncResult SyncEagle(EagleSyncRequest request);
        IReadOnlyList<AssetSyncInfo> GetSyncInfo();
    }

    public enum AssetManagerChangeKind
    {
        Catalog,
        Collections,
        SmartCollectionRule,
        FileTree,
        FileImportTargets,
        VersionGroupPrimaryFile
    }

    public sealed class AssetManagerChange
    {
        public AssetManagerChange(
            AssetManagerChangeKind kind,
            string subjectId = null,
            string relatedId = null,
            IReadOnlyList<AssetFileImportTarget> importTargets = null)
        {
            Kind = kind;
            SubjectId = subjectId ?? string.Empty;
            RelatedId = relatedId ?? string.Empty;
            ImportTargets = importTargets ?? Array.Empty<AssetFileImportTarget>();
        }

        public AssetManagerChangeKind Kind { get; }
        public string SubjectId { get; }
        public string RelatedId { get; }
        public IReadOnlyList<AssetFileImportTarget> ImportTargets { get; }
    }
}

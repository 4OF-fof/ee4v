using System;
using System.Collections.Generic;

namespace Ee4v.AssetManager.Contracts
{
    public interface IAssetManager
    {
        event Action<AssetManagerChange> Changed;

        AssetSearchResult SearchItems(AssetItemQuery query = null);
        AssetSearchResult SearchCollection(
            string collectionId,
            int offset = 0,
            int limit = 0);
        AssetItem GetItem(string itemId);
        AssetItem CreateItem(CreateAssetItemRequest request);
        AssetItem UpdateItem(string itemId, UpdateAssetItemRequest request);
        IReadOnlyList<AssetItem> SetItemArchived(
            IReadOnlyList<string> itemIds,
            bool archived);
        void DeleteItem(IReadOnlyList<string> itemIds);

        AssetFile GetFile(string fileId);
        IReadOnlyList<AssetFile> GetFiles(
            string itemId,
            bool includeArchived = false);
        IReadOnlyList<AssetFile> GetUnassignedFiles(
            bool includeArchived = false);
        AssetFile RegisterFile(
            string itemId,
            RegisterFileRequest request);
        IReadOnlyList<AssetFile> SetFileItem(
            IReadOnlyList<string> fileIds,
            string itemId);
        IReadOnlyList<AssetFile> SetFileArchived(
            IReadOnlyList<string> fileIds,
            bool archived);
        void DeleteFile(IReadOnlyList<string> fileIds);
        IReadOnlyList<AssetFileTarget> GetFileTargets(string fileId);
        IReadOnlyList<AssetFileTarget> SetFileTargets(
            string fileId,
            IReadOnlyList<string> targetPaths);
        void ImportFileTargets(string fileId);
        IReadOnlyList<AssetFileDependency> GetFileDependencies(
            string fileId);
        IReadOnlyList<AssetFileDependency> SetFileDependencies(
            string dependentFileId,
            IReadOnlyList<string> dependencyFileIds);

        IReadOnlyList<AssetTag> GetTags();
        IReadOnlyList<AssetItem> SetItemTags(
            IReadOnlyList<string> itemIds,
            IReadOnlyList<string> tagPaths);

        IReadOnlyList<AssetCollection> GetCollections();
        AssetCollection GetCollection(string collectionId);
        AssetCollection CreateCollection(
            CreateAssetCollectionRequest request);
        AssetCollection UpdateCollection(
            string collectionId,
            UpdateAssetCollectionRequest request);
        void DeleteCollection(string collectionId);

        AssetSyncResult SyncEagle(EagleSyncRequest request);
        AssetSyncResult SyncEe4v(Ee4vSyncRequest request);
        AssetItem ImportEe4vFile(ImportEe4vFileRequest request);
    }

    public enum AssetManagerChangeKind
    {
        Catalog,
        Collections
    }

    public sealed class AssetManagerChange
    {
        public AssetManagerChange(
            AssetManagerChangeKind kind,
            string subjectId = null)
        {
            Kind = kind;
            SubjectId = subjectId ?? string.Empty;
        }

        public AssetManagerChangeKind Kind { get; }
        public string SubjectId { get; }
    }
}

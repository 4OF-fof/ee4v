using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

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
        bool MatchesCollection(string collectionId, string itemId);
        AssetItem GetItem(string itemId);
        Task<AssetThumbnail> GetThumbnail(
            string itemId,
            CancellationToken cancellationToken = default);
        Task<IReadOnlyDictionary<string, AssetThumbnail>> GetThumbnails(
            IReadOnlyList<string> itemIds,
            CancellationToken cancellationToken = default);
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
        IReadOnlyList<AssetFileTarget> GetItemTargets(string itemId);
        IReadOnlyList<AssetFileTarget> SetItemTargets(
            string itemId,
            IReadOnlyList<AssetFileTarget> targets);
        AssetFileTarget SetItemTargetGroup(
            string itemId,
            string fileId,
            string targetPath,
            string groupName);
        Task<AssetImportResult> ImportFileEntries(
            string fileId,
            IReadOnlyList<string> paths,
            CancellationToken cancellationToken = default);
        Task<AssetImportResult> ImportFileTargets(
            string fileId,
            CancellationToken cancellationToken = default);
        Task<AssetImportResult> ImportItemTargets(
            string itemId,
            IReadOnlyList<AssetFileTarget> selectedTargets,
            CancellationToken cancellationToken = default);
        IReadOnlyList<AssetFileDependency> GetFileDependencies(
            string fileId);
        IReadOnlyList<AssetFileDependency> SetFileDependencies(
            IReadOnlyList<string> dependentFileIds,
            IReadOnlyList<string> dependencyFileIds);
        AssetFileAnalysis AnalyzeFile(string fileId);
        Task<AssetFileAnalysis> AnalyzeFileAsync(
            string fileId,
            CancellationToken cancellationToken = default);
        IReadOnlyList<string> GetFileImportedAssetGuids(string fileId);
        IReadOnlyList<string> GetItemImportedAssetGuids(string itemId);
        IReadOnlyList<AssetImportedAssetAssociation>
            GetImportedAssetAssociations(
                IReadOnlyList<string> assetGuids = null);

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
        ItemCreated,
        ItemUpdated,
        ItemArchiveChanged,
        ItemDeleted,
        ItemTagsChanged,
        FileCreated,
        FilePlacementChanged,
        FileArchiveChanged,
        FileDeleted,
        FileTargetsChanged,
        ItemTargetsChanged,
        FileDependenciesChanged,
        FileImportedAssetGuidsChanged,
        CollectionCreated,
        CollectionUpdated,
        CollectionDeleted,
        SourceSynchronized
    }

    public sealed class AssetManagerChange
    {
        public AssetManagerChange(
            AssetManagerChangeKind kind,
            IReadOnlyList<string> subjectIds = null,
            IReadOnlyList<string> relatedIds = null,
            AssetSourceType? sourceType = null)
        {
            Kind = kind;
            SubjectIds = NormalizeIds(subjectIds);
            RelatedIds = NormalizeIds(relatedIds);
            SourceType = sourceType;
        }

        public AssetManagerChangeKind Kind { get; }
        public IReadOnlyList<string> SubjectIds { get; }
        public IReadOnlyList<string> RelatedIds { get; }
        public AssetSourceType? SourceType { get; }

        private static IReadOnlyList<string> NormalizeIds(
            IReadOnlyList<string> ids)
        {
            if (ids == null || ids.Count == 0)
            {
                return Array.Empty<string>();
            }

            var result = new List<string>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < ids.Count; i++)
            {
                if (!string.IsNullOrWhiteSpace(ids[i]) &&
                    seen.Add(ids[i]))
                {
                    result.Add(ids[i]);
                }
            }

            return result.ToArray();
        }
    }
}

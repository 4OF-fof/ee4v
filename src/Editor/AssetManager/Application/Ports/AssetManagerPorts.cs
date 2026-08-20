using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Ee4v.AssetManager.Contracts;

namespace Ee4v.AssetManager.Application.Ports
{
    internal interface IAssetManagerStore
    {
        AssetSearchResult SearchItems(AssetItemQuery query);
        bool MatchesItem(string itemId, AssetFilterNode filter);
        AssetItem GetItem(string itemId);
        AssetItem CreateItem(CreateAssetItemRequest request);
        AssetItem UpdateItem(
            string itemId,
            UpdateAssetItemRequest request);
        IReadOnlyList<AssetItem> SetItemArchived(
            IReadOnlyList<string> itemIds,
            bool archived);
        void DeleteItem(IReadOnlyList<string> itemIds);

        AssetFile GetFile(string fileId);
        IReadOnlyList<AssetFile> GetFiles(
            string itemId,
            bool includeArchived);
        IReadOnlyList<AssetFile> GetUnassignedFiles(bool includeArchived);
        IReadOnlyList<AssetFile> SetFileItem(
            IReadOnlyList<string> fileIds,
            string itemId);
        IReadOnlyList<AssetFile> SetFileArchived(
            IReadOnlyList<string> fileIds,
            bool archived);
        void DeleteFile(IReadOnlyList<string> fileIds);
        IReadOnlyList<AssetFileTarget> GetItemTargets(string itemId);
        IReadOnlyList<AssetFileTarget> ReplaceItemTargets(
            string itemId,
            IReadOnlyList<AssetFileTarget> normalizedTargets);
        AssetFileTarget SetItemTargetGroup(
            string itemId,
            string fileId,
            string normalizedTargetPath,
            string normalizedGroupName);
        IReadOnlyList<AssetFileDependency> GetFileDependencies(
            string fileId);
        IReadOnlyList<AssetFileDependency> ReplaceFileDependencies(
            IReadOnlyList<string> dependentFileIds,
            IReadOnlyList<AssetFileTarget> dependencyTargets);
        IReadOnlyList<string> GetDependentFileIds(
            IReadOnlyList<string> dependencyFileIds);
        IReadOnlyList<string> GetFileImportedAssetGuids(string fileId);
        IReadOnlyList<string> GetItemImportedAssetGuids(string itemId);
        IReadOnlyList<AssetImportedAssetAssociation>
            GetImportedAssetAssociations(
                IReadOnlyList<string> assetGuids);
        void ReplaceFileImportedAssetGuids(
            string fileId,
            IReadOnlyList<string> assetGuids);
        AssetFile ApplySourceFile(
            AssetSourceType sourceType,
            AssetSourceSnapshotFile file,
            string itemId);
        AssetItem ApplySourceItem(
            AssetSourceType sourceType,
            AssetSourceSnapshotItem item);

        IReadOnlyList<AssetTag> GetTags();
        IReadOnlyList<AssetItem> SetItemTags(
            IReadOnlyList<string> itemIds,
            IReadOnlyList<string> normalizedPaths);

        IReadOnlyList<AssetCollection> GetCollections();
        AssetCollection GetCollection(string collectionId);
        AssetCollection CreateCollection(
            CreateAssetCollectionRequest request);
        AssetCollection UpdateCollection(
            string collectionId,
            UpdateAssetCollectionRequest request);
        void DeleteCollection(string collectionId);

        AssetSyncResult ApplySourceSnapshot(
            AssetSourceType sourceType,
            AssetSourceSnapshot snapshot);
        AssetItem GetItemBySource(
            AssetSourceType sourceType,
            string sourceId);
        AssetFile GetFileBySource(
            AssetSourceType sourceType,
            string sourceId);
    }

    internal interface IEagleAssetSource
    {
        AssetSourceSnapshot Read(EagleSyncRequest request);
    }

    internal interface IEe4vAssetSource
    {
        AssetSourceSnapshot Read(Ee4vSyncRequest request);
        AssetSourceSnapshotItem Import(
            ImportEe4vFileRequest request,
            IReadOnlyList<string> normalizedTags);
        AssetSourceSnapshotFile Register(RegisterFileRequest request);
        IEe4vDeleteOperation BeginDelete(
            IReadOnlyList<AssetFile> files);
        void Update(
            AssetFile sourceFile,
            string name,
            string description,
            IReadOnlyList<string> normalizedTags);
    }

    internal interface IEe4vDeleteOperation : IDisposable
    {
        void Commit();
    }

    internal interface IAssetTargetImporter
    {
        Task<AssetImportResult> Import(
            AssetItem item,
            AssetFile file,
            IReadOnlyList<string> targetPaths,
            CancellationToken cancellationToken);
    }

    internal interface IAssetFileAnalyzer
    {
        AssetFileAnalysis Analyze(
            AssetFile file,
            CancellationToken cancellationToken = default);
    }

    internal interface IAssetThumbnailProvider
    {
        Task<AssetThumbnail> Get(
            AssetItem item,
            CancellationToken cancellationToken);
        Task<IReadOnlyDictionary<string, AssetThumbnail>> GetMany(
            IReadOnlyList<AssetItem> items,
            CancellationToken cancellationToken);
    }

    internal sealed class AssetSourceSnapshot
    {
        internal AssetSourceSnapshot(
            IReadOnlyList<AssetSourceSnapshotItem> items,
            IReadOnlyList<AssetSourceSnapshotFile> files = null)
        {
            Items = items ?? Array.Empty<AssetSourceSnapshotItem>();
            Files = files ?? Array.Empty<AssetSourceSnapshotFile>();
        }

        internal IReadOnlyList<AssetSourceSnapshotItem> Items { get; }
        internal IReadOnlyList<AssetSourceSnapshotFile> Files { get; }
    }

    internal sealed class AssetSourceSnapshotItem
    {
        internal string SourceId { get; set; }
        internal string Name { get; set; }
        internal string Description { get; set; }
        internal AssetBoothMetadata Booth { get; set; }
        internal string ThumbnailUrl { get; set; }
        internal IReadOnlyList<string> Tags { get; set; }
        internal IReadOnlyList<AssetSourceSnapshotFile> Files { get; set; }
    }

    internal sealed class AssetSourceSnapshotFile
    {
        internal string SourceId { get; set; }
        internal string FileName { get; set; }
        internal string Extension { get; set; }
        internal string SourcePath { get; set; }
    }
}

using System;
using System.Collections.Generic;
using Ee4v.AssetManager.Contracts;

namespace Ee4v.AssetManager.Application.Ports
{
    internal interface IAssetManagerStore
    {
        IReadOnlyList<AssetItem> GetItems();
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
        IReadOnlyList<AssetFileTarget> GetFileTargets(string fileId);
        IReadOnlyList<AssetFileTarget> ReplaceFileTargets(
            string fileId,
            IReadOnlyList<string> normalizedTargetPaths);
        IReadOnlyList<AssetFileDependency> GetFileDependencies(
            string fileId);
        IReadOnlyList<AssetFileDependency> ReplaceFileDependencies(
            string dependentFileId,
            IReadOnlyList<string> dependencyFileIds);

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
            AssetSourceSnapshot snapshot,
            bool markMissingFiles);
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
        void Delete(IReadOnlyList<AssetFile> files);
        void Update(
            AssetFile sourceFile,
            string name,
            string description,
            IReadOnlyList<string> normalizedTags);
    }

    internal interface IAssetTargetImporter
    {
        void Import(
            AssetItem item,
            AssetFile file,
            IReadOnlyList<string> targetPaths,
            Action<bool> completed);
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
        internal IReadOnlyList<string> Tags { get; set; }
        internal IReadOnlyList<AssetSourceSnapshotFile> Files { get; set; }
    }

    internal sealed class AssetSourceSnapshotFile
    {
        internal string SourceId { get; set; }
        internal string FileName { get; set; }
        internal string Extension { get; set; }
        internal string SourcePath { get; set; }
        internal bool IsAvailable { get; set; }
    }
}

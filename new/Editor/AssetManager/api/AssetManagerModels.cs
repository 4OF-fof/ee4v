using System;
using System.Collections.Generic;

namespace Ee4v.AssetManager.Api
{
    public sealed class AssetItem
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public BoothSnapshot Booth { get; set; }
        public IReadOnlyList<AssetTag> Tags { get; set; }
        public IReadOnlyList<AssetFileSummary> Files { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    public sealed class BoothSnapshot
    {
        public string Id { get; set; }
        public long BoothItemId { get; set; }
        public string ItemUrl { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public string ThumbnailUrl { get; set; }
        public string ShopName { get; set; }
        public string ShopUrl { get; set; }
        public string ShopThumbnailUrl { get; set; }
        public DateTime? LastUpdatedAt { get; set; }
    }

    public sealed class AssetThumbnail
    {
        public bool Found { get; set; }
        public byte[] Data { get; set; }
        public string Path { get; set; }
        public string SourceUrl { get; set; }
        public string MissingReason { get; set; }
    }

    public sealed class AssetFile
    {
        public string Id { get; set; }
        public string ItemId { get; set; }
        public string FileName { get; set; }
        public string Extension { get; set; }
        public long? SizeBytes { get; set; }
        public long? DownloadId { get; set; }
        public AssetFileLifecycle Lifecycle { get; set; }
        public IReadOnlyList<AssetFileOrigin> Origins { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    public sealed class AssetFileImportTarget
    {
        public string Id { get; set; }
        public string FileId { get; set; }
        public string RelativePath { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    public sealed class AssetFileSummary
    {
        public string Id { get; set; }
        public string FileName { get; set; }
        public string Extension { get; set; }
        public long? SizeBytes { get; set; }
        public long? DownloadId { get; set; }
        public AssetFileLifecycle Lifecycle { get; set; }
    }

    public sealed class AssetFileOrigin
    {
        public AssetSourceType SourceType { get; set; }
        public string SourceId { get; set; }
        public string FilePathCache { get; set; }
        public DateTime? ImportedAt { get; set; }
    }

    public sealed class AssetFilePathResolution
    {
        public bool Found { get; set; }
        public string Path { get; set; }
        public AssetSourceType? SourceType { get; set; }
        public string MissingReason { get; set; }
    }

    public sealed class AssetTag
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    public sealed class AssetCollection
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public bool IsSmartCollection { get; set; }
        public string ParentCollectionId { get; set; }
        public SmartCollectionRule SmartRule { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    public sealed class SmartCollectionRule
    {
        public SmartCollectionMatchMode MatchMode { get; set; }
        public IReadOnlyList<SmartCollectionCondition> Conditions { get; set; }
    }

    public sealed class SmartCollectionCondition
    {
        public string Id { get; set; }
        public SmartCollectionConditionField Field { get; set; }
        public SmartCollectionConditionOperator Operator { get; set; }
        public string QueryText { get; set; }
    }

    public sealed class AssetFileDependency
    {
        public string DependentFileId { get; set; }
        public string DependencyFileId { get; set; }
    }

    public sealed class AssetSyncInfo
    {
        public AssetSourceType SourceType { get; set; }
        public DateTime? LastSyncAt { get; set; }
        public AssetSyncState LastSyncState { get; set; }
    }
}

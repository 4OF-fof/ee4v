using System;
using System.Collections.Generic;

namespace Ee4v.AssetManager.Contracts
{
    public sealed class AssetItem
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public AssetBoothMetadata Booth { get; set; }
        public string ThumbnailUrl { get; set; }
        public AssetSourceType? SourceType { get; set; }
        public string SourceId { get; set; }
        public IReadOnlyList<AssetTag> Tags { get; set; }
        public IReadOnlyList<AssetFile> Files { get; set; }
        public bool IsArchived { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    public sealed class AssetBoothMetadata
    {
        public string ItemUrl { get; set; }
        public string ShopName { get; set; }
        public string ShopUrl { get; set; }
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
        public AssetSourceType SourceType { get; set; }
        public string SourceId { get; set; }
        public string SourcePath { get; set; }
        public bool IsArchived { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    public sealed class AssetFileTarget
    {
        public string FileId { get; set; }
        public string TargetPath { get; set; }
        public string GroupName { get; set; }

        public static bool HasSameIdentity(
            AssetFileTarget first,
            AssetFileTarget second)
        {
            return string.Equals(
                       first?.FileId,
                       second?.FileId,
                       StringComparison.Ordinal) &&
                   string.Equals(
                       first?.TargetPath,
                       second?.TargetPath,
                       StringComparison.OrdinalIgnoreCase);
        }
    }

    public sealed class AssetFileDependency
    {
        public string DependentFileId { get; set; }
        public string DependentTargetPath { get; set; }
        public string DependencyFileId { get; set; }
        public string TargetPath { get; set; }
    }

    public sealed class AssetImportedAssetAssociation
    {
        public string ItemId { get; set; }
        public string FileId { get; set; }
        public string AssetGuid { get; set; }
        public DateTime ImportedAt { get; set; }
    }

    public sealed class AssetFileAnalysis
    {
        public string FileId { get; set; }
        public AssetFileAnalysisKind Kind { get; set; }
        public IReadOnlyList<AssetFileContentEntry> Entries { get; set; }
    }

    public sealed class AssetFileContentEntry
    {
        public string Path { get; set; }
        public AssetFileContentEntryKind Kind { get; set; }
        public long SizeBytes { get; set; }
        public string AssetGuid { get; set; }
    }

    public sealed class AssetImportResult
    {
        public AssetImportResult(
            AssetImportState state,
            IReadOnlyList<string> fileIds,
            IReadOnlyList<string> assetGuids,
            string errorMessage = null)
        {
            State = state;
            FileIds = fileIds ?? Array.Empty<string>();
            AssetGuids = assetGuids ?? Array.Empty<string>();
            ErrorMessage = errorMessage ?? string.Empty;
        }

        public AssetImportState State { get; }
        public bool Succeeded => State == AssetImportState.Success;
        public bool Canceled => State == AssetImportState.Canceled;
        public IReadOnlyList<string> FileIds { get; }
        public IReadOnlyList<string> AssetGuids { get; }
        public string ErrorMessage { get; }
    }

    public sealed class AssetTag
    {
        public string Id { get; set; }
        public string Path { get; set; }
        public bool IsSourceOwned { get; set; }
    }

    public sealed class AssetCollection
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public AssetFilterNode Root { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    public sealed class AssetFilterNode
    {
        public AssetFilterNodeType Type { get; set; }
        public AssetFilterConditionType? ConditionType { get; set; }
        public string Value { get; set; }
        public IReadOnlyList<AssetFilterNode> Children { get; set; }

        public static AssetFilterNode And(params AssetFilterNode[] children)
        {
            return Group(AssetFilterNodeType.And, children);
        }

        public static AssetFilterNode Or(params AssetFilterNode[] children)
        {
            return Group(AssetFilterNodeType.Or, children);
        }

        public static AssetFilterNode Not(AssetFilterNode child)
        {
            return Group(AssetFilterNodeType.Not, child);
        }

        public static AssetFilterNode Condition(
            AssetFilterConditionType type,
            string value)
        {
            return new AssetFilterNode
            {
                Type = AssetFilterNodeType.Condition,
                ConditionType = type,
                Value = value,
                Children = Array.Empty<AssetFilterNode>()
            };
        }

        private static AssetFilterNode Group(
            AssetFilterNodeType type,
            params AssetFilterNode[] children)
        {
            return new AssetFilterNode
            {
                Type = type,
                Children = children ?? Array.Empty<AssetFilterNode>()
            };
        }
    }

    public sealed class AssetSearchResult
    {
        public IReadOnlyList<AssetItem> Items { get; set; }
        public int TotalCount { get; set; }
    }
}

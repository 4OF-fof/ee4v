using System;
using System.Collections.Generic;

namespace Ee4v.AssetManager.Contracts
{
    public sealed class AssetItem
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public AssetSourceType? SourceType { get; set; }
        public string SourceId { get; set; }
        public IReadOnlyList<AssetTag> Tags { get; set; }
        public IReadOnlyList<AssetFile> Files { get; set; }
        public bool IsArchived { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
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
        public bool IsAvailable { get; set; }
        public bool IsArchived { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    public sealed class AssetFileTarget
    {
        public string FileId { get; set; }
        public string TargetPath { get; set; }
    }

    public sealed class AssetFileDependency
    {
        public string DependentFileId { get; set; }
        public string DependencyFileId { get; set; }
    }

    public sealed class AssetTag
    {
        public string Id { get; set; }
        public string Path { get; set; }
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

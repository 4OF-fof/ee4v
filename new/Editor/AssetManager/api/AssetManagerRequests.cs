using System.Collections.Generic;

namespace Ee4v.AssetManager.Api
{
    public sealed class AssetItemQuery
    {
        public string Keyword { get; set; }
        public string CollectionId { get; set; }
        public IReadOnlyList<string> TagIds { get; set; }
        public IReadOnlyList<AssetSourceType> SourceTypes { get; set; }
        public AssetFileLifecycle? Lifecycle { get; set; }
        public int Offset { get; set; }
        public int Limit { get; set; }
    }

    public sealed class AssetSearchResult
    {
        public IReadOnlyList<AssetItem> Items { get; set; }
        public int TotalCount { get; set; }
    }

    public sealed class CreateAssetItemRequest
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public IReadOnlyList<string> TagIds { get; set; }
        public IReadOnlyList<string> CollectionIds { get; set; }
    }

    public sealed class UpdateAssetItemRequest
    {
        public string Name { get; set; }
        public string Description { get; set; }
    }

    public sealed class AssetFileQuery
    {
        public AssetSourceType? SourceType { get; set; }
        public AssetFileLifecycle? Lifecycle { get; set; }
        public string Extension { get; set; }
    }

    public sealed class RegisterFileRequest
    {
        public string FileName { get; set; }
        public string FilePath { get; set; }
        public long? SizeBytes { get; set; }
        public string VersionGroupId { get; set; }
        public string VariantGroupId { get; set; }
    }

    public sealed class CreateVariantGroupRequest
    {
        public string Name { get; set; }
    }

    public sealed class CreateVersionGroupRequest
    {
        public string Name { get; set; }
        public string VariantGroupId { get; set; }
    }

    public sealed class DependencyEndpointRequest
    {
        public AssetDependencyEndpointType Type { get; set; }
        public string Id { get; set; }
    }

    public sealed class AssetFileImportTargetRequest
    {
        public string RelativePath { get; set; }
    }

    public sealed class CreateCollectionRequest
    {
        public string Name { get; set; }
        public string ParentCollectionId { get; set; }
    }

    public sealed class CreateSmartCollectionRequest
    {
        public string Name { get; set; }
        public string ParentCollectionId { get; set; }
        public SmartCollectionMatchMode MatchMode { get; set; }
        public IReadOnlyList<SmartCollectionCondition> Conditions { get; set; }
    }
}

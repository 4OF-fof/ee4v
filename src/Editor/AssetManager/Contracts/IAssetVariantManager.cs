using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Ee4v.AssetManager.Contracts
{
    public interface IAssetVariantManager
    {
        event Action Changed;
        IReadOnlyList<AssetVariant> GetVariants();
        IReadOnlyList<AssetVariantRevision> GetRevisions(string variantId);
        AssetVariantRevisionDetails GetRevisionDetails(string variantId, string revisionId);
        Task<AssetThumbnail> GetRevisionThumbnail(string variantId, string revisionId,
            CancellationToken cancellationToken = default);
        Task<AssetVariantRevision> Save(AssetVariantSaveRequest request);
        Task<string> Restore(string variantId, string revisionId);
        void RebuildIndex();
    }

    public sealed class AssetVariant
    {
        public string Id { get; set; }
        /// <summary>The original Prefab, retained as provenance independently of the owning asset.</summary>
        public string SourcePrefabGuid { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        /// <summary>The AssetManager Item that owns this Variant and its Git repository.</summary>
        public string ParentItemId { get; set; }
        public string RootAssetPath { get; set; }
        public string HeadRevisionId { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    public sealed class AssetVariantRevision
    {
        public string Id { get; set; }
        public string VariantId { get; set; }
        public string ParentRevisionId { get; set; }
        public int Number { get; set; }
        public string CommitId { get; set; }
        public string Memo { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public sealed class AssetVariantSaveRequest
    {
        public string RootAssetPath { get; set; }
        public string Memo { get; set; }
    }

    public sealed class AssetVariantRevisionDetails
    {
        public string UnityVersion { get; set; }
        public IReadOnlyList<AssetVariantFileDependency> Dependencies { get; set; }
    }

    public sealed class AssetVariantFileDependency
    {
        public string FileId { get; set; }
        public AssetSourceType SourceType { get; set; }
        public string SourceId { get; set; }
        public IReadOnlyList<string> TargetPaths { get; set; }
    }
}

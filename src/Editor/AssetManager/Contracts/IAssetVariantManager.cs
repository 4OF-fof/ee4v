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
        /// <summary>The saved version currently used as the project's editing base, or null if unknown.</summary>
        string GetCurrentRevisionId(string variantId);
        AssetVariantRevisionDetails GetRevisionDetails(string variantId, string revisionId);
        bool HasChanges(string rootAssetPath);
        bool HasChangesFromCurrentRevision(string rootAssetPath);
        AssetVariantChangeStatus GetChangeStatus(string rootAssetPath);
        AssetVariantChangeDetails GetChangeDetails(string rootAssetPath);
        Task<AssetThumbnail> GetRevisionThumbnail(string variantId, string revisionId,
            CancellationToken cancellationToken = default);
        IReadOnlyList<AssetVariantGalleryImage> GetGalleryImages(string variantId);
        Task<AssetThumbnail> GetGalleryImage(string variantId, string imageId,
            CancellationToken cancellationToken = default);
        Task AddGalleryImages(string variantId, string parentItemId,
            IReadOnlyList<AssetVariantGalleryUpload> images);
        Task RemoveGalleryImage(string variantId, string imageId);
        Task MoveGalleryImageToFront(string variantId, string imageId);
        Task<AssetVariantRevision> Save(AssetVariantSaveRequest request);
        Task UpdateMetadata(string variantId, UpdateAssetVariantRequest request);
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

    public sealed class UpdateAssetVariantRequest
    {
        public string Name { get; set; }
        public string Description { get; set; }
    }

    public sealed class AssetVariantChangeStatus
    {
        public bool HasChanges { get; set; }
        public bool HasChangesFromCurrentRevision { get; set; }
    }

    public sealed class AssetVariantChangeDetails
    {
        public int? LatestRevisionNumber { get; set; }
        public int? CurrentRevisionNumber { get; set; }
        public IReadOnlyList<AssetVariantChange> SaveChanges { get; set; }
        public IReadOnlyList<AssetVariantChange> DiscardChanges { get; set; }
    }

    public enum AssetVariantChangeKind { Added, Modified, Removed }
    public enum AssetVariantChangeSubject { Asset, Dependency, Metadata }

    public sealed class AssetVariantChange
    {
        public AssetVariantChangeKind Kind { get; set; }
        public AssetVariantChangeSubject Subject { get; set; }
        public string Path { get; set; }
    }

    public sealed class AssetVariantGalleryImage
    {
        public string Id { get; set; }
        public string FileName { get; set; }
    }

    public sealed class AssetVariantGalleryUpload
    {
        public string FileName { get; set; }
        public byte[] Data { get; set; }
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

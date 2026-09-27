using System;
using System.Collections.Generic;
using Ee4v.AssetManager.Contracts;

namespace Ee4v.AssetManager.Application.Ports
{
    internal interface IAssetVariantRepository
    {
        IReadOnlyList<AssetVariantSnapshot> ReadAll();
        AssetVariantSnapshot Read(string variantId, string revisionId);
        AssetVariantSnapshot Save(AssetVariantSnapshot snapshot, string stagingPath);
        void UpdateMetadata(AssetVariant variant);
        byte[] ReadPreview(AssetVariantSnapshot snapshot);
        IReadOnlyList<AssetVariantGalleryImage> ReadGallery(string variantId);
        byte[] ReadGalleryImage(string variantId, string imageId);
        void AddGalleryImages(string variantId, string parentItemId,
            IReadOnlyList<AssetVariantGalleryUpload> images);
        void RemoveGalleryImage(string variantId, string imageId);
        void MoveGalleryImageToFront(string variantId, string imageId);
        string Extract(AssetVariantSnapshot snapshot);
        void DeleteStaging(string path);
    }

    internal interface IAssetVariantIndex
    {
        void ReplaceVariantIndex(IReadOnlyList<AssetVariantSnapshot> snapshots);
        IReadOnlyList<AssetVariant> GetVariants();
        IReadOnlyList<AssetVariantRevision> GetVariantRevisions(string variantId);
    }

    internal interface IAssetVariantWorkspace
    {
        AssetVariantCapture Capture(AssetVariantSaveRequest request);
        AssetVariantSnapshot Inspect(string rootAssetPath);
        string Restore(AssetVariantSnapshot snapshot, string stagingPath, string rootAssetPath);
        string GetRootAssetPath(string variantId, string name);
        string GetBaseRevision(string variantId);
        void SetBaseRevision(string variantId, string revisionId);
        // Returns an undo action, or null when the Prefab is absent from this Project.
        Action UpdateMetadata(string variantId, string name, string description);
        bool HasAssets(AssetVariantDependency dependency);
        void ValidateRestore(AssetVariantSnapshot snapshot, string rootAssetPath);
    }

    internal sealed class AssetVariantCapture
    {
        public AssetVariantSnapshot Snapshot { get; set; }
        public string StagingPath { get; set; }
    }

    internal sealed class AssetVariantSnapshot
    {
        public int SchemaVersion { get; set; } = 1;
        public AssetVariant Variant { get; set; }
        public AssetVariantRevision Revision { get; set; }
        public string ContentHash { get; set; }
        public string PreviewHash { get; set; }
        public AssetVariantOwnedAsset[] Assets { get; set; }
        public AssetVariantDependency[] Dependencies { get; set; }
        public string UnityVersion { get; set; }
        public string PackagesManifest { get; set; }
        public string PackagesLock { get; set; }
    }

    internal sealed class AssetVariantOwnedAsset
    {
        public string Path { get; set; }
        public string Guid { get; set; }
        public string Hash { get; set; }
        public string MetaHash { get; set; }
        public bool IsFolder { get; set; }
    }

    internal sealed class AssetVariantDependency
    {
        public string FileId { get; set; }
        public AssetSourceType SourceType { get; set; }
        public string SourceId { get; set; }
        public string[] AssetGuids { get; set; }
        public string[] TargetPaths { get; set; }
    }
}

using System;
using System.Collections.Generic;
using _4OF.ee4v.Core.Utility;

namespace _4OF.ee4v.AssetManager.Data {
    public sealed class AssetLibrary {
        private static readonly Lazy<AssetLibrary> Singleton = new(() => new AssetLibrary());
        public static AssetLibrary Instance => Singleton.Value;
        private AssetLibrary() { }
        
        private readonly List<AssetMetadata> _assetMetadataList = new();

        public IReadOnlyList<AssetMetadata> Assets => _assetMetadataList;
        public IReadOnlyList<LibraryMetadata> Libraries { get; } = new List<LibraryMetadata>();

        public void AddAsset(AssetMetadata assetMetadata) {
            if (assetMetadata == null) return;
            _assetMetadataList.Add(assetMetadata);
        }
        
        public void RemoveAsset(Ulid assetId) {
            _assetMetadataList.RemoveAll(a => a.ID == assetId);
        }
        
        public void UpdateAsset(AssetMetadata assetMetadata) {
            var index = _assetMetadataList.FindIndex(a => a.ID == assetMetadata.ID);
            if (index != -1) {
                _assetMetadataList[index] = assetMetadata;
            }
        }
        
        public AssetMetadata GetAsset(Ulid assetId) {
            return _assetMetadataList.Find(a => a.ID == assetId);
        }
    }
}
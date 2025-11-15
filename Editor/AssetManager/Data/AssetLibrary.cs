using System.Collections.Generic;
using _4OF.ee4v.Core.Utility;

namespace _4OF.ee4v.AssetManager.Data {
    public sealed class AssetLibrary {
        public static readonly AssetLibrary Instance = new();
        private AssetLibrary() { }
        
        private readonly List<AssetMetadata> _assetMetadataList = new();
        private LibraryMetadata _libraryMetadata;

        public IReadOnlyList<AssetMetadata> Assets => _assetMetadataList;
        public LibraryMetadata Libraries => _libraryMetadata;

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

        public void LoadAsset(AssetMetadata assetMetadata) {
            if (assetMetadata == null) return;
            var existingAsset = GetAsset(assetMetadata.ID);
            if (existingAsset != null) {
                UpdateAsset(assetMetadata);
            } else {
                AddAsset(assetMetadata);
            }
        }
        
        public void LoadLibrary(LibraryMetadata libraryMetadata) {
            _libraryMetadata = libraryMetadata;
        }
        
        public void UnloadLibrary() {
            _assetMetadataList.Clear();
            _libraryMetadata = null;
        }
    }
}
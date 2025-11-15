using System.Collections.Generic;
using System.Linq;
using _4OF.ee4v.Core.Utility;

namespace _4OF.ee4v.AssetManager.Data {
    public class AssetLibrary {
        public static readonly AssetLibrary Instance = new();

        private readonly List<AssetMetadata> _assetMetadataList = new();

        private AssetLibrary() {
        }

        public IReadOnlyList<AssetMetadata> Assets => _assetMetadataList;
        public LibraryMetadata Libraries { get; private set; }

        public void AddAsset(AssetMetadata assetMetadata) {
            if (assetMetadata == null) return;
            _assetMetadataList.Add(assetMetadata);
        }

        public void RemoveAsset(Ulid assetId) {
            var asset = _assetMetadataList.Find(a => a.ID == assetId);
            asset?.SetDeleted(true);
        }

        public void UpdateAsset(AssetMetadata assetMetadata) {
            var index = _assetMetadataList.FindIndex(a => a.ID == assetMetadata.ID);
            if (index != -1) _assetMetadataList[index] = assetMetadata;
        }

        public AssetMetadata GetAsset(Ulid assetId) {
            return _assetMetadataList.Find(a => a.ID == assetId);
        }

        public void LoadAsset(AssetMetadata assetMetadata) {
            if (assetMetadata == null) return;
            var existingAsset = GetAsset(assetMetadata.ID);
            if (existingAsset != null)
                UpdateAsset(assetMetadata);
            else
                AddAsset(assetMetadata);
        }

        public List<string> GetAllTags() {
            var tags = new HashSet<string>();
            foreach (var tag in _assetMetadataList.SelectMany(asset => asset.Tags)) tags.Add(tag);

            return tags.ToList();
        }

        public void RenameTag(string tag, string newTag) {
            if (string.IsNullOrEmpty(tag) || string.IsNullOrEmpty(newTag) || tag == newTag) return;
            var allTags = GetAllTags();
            if (!allTags.Contains(tag) || allTags.Contains(newTag)) return;
            foreach (var asset in _assetMetadataList.Where(asset => asset.Tags.Contains(tag))) {
                asset.AddTag(newTag);
                asset.RemoveTag(tag);
            }
        }

        public void LoadLibrary(LibraryMetadata libraryMetadata) {
            Libraries = libraryMetadata;
        }

        public void UnloadLibrary() {
            _assetMetadataList.Clear();
            Libraries = null;
        }
    }
}